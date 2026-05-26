using System.Text.Json;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;
using DeepSeekBrowser.Services.Harness.Observability;
using DeepSeekBrowser.Services.Harness.Sandbox;

namespace DeepSeekBrowser.Services.Harness.Graph;

public sealed class HarnessGraphRunner
{
    private readonly IAgentWebChat _chat;
    private readonly McpHub _mcp;
    private readonly PermissionGate _permission;
    private readonly HarnessSubAgentService _subAgents;
    private readonly IUserQuestionHandler? _userQuestions;
    private readonly string _workspace;

    public HarnessGraphRunner(
        IAgentWebChat chat,
        McpHub mcp,
        PermissionGate permission,
        HarnessSubAgentService subAgents,
        IUserQuestionHandler? userQuestions,
        string workspace)
    {
        _chat = chat;
        _mcp = mcp;
        _permission = permission;
        _subAgents = subAgents;
        _userQuestions = userQuestions;
        _workspace = workspace;
    }

    public Task<HarnessRunResult> RunAsync(
        string graphId,
        HarnessRunRequest request,
        HarnessRunCallbacks callbacks,
        CancellationToken ct,
        string? resumeThreadId = null) =>
        RunInternalAsync(graphId, request, callbacks, ct, resumeThreadId);

    private async Task<HarnessRunResult> RunInternalAsync(
        string graphId,
        HarnessRunRequest request,
        HarnessRunCallbacks callbacks,
        CancellationToken ct,
        string? resumeThreadId)
    {
        if (!HarnessGraphRegistry.TryGet(graphId, _workspace, out var graph) || graph is null)
            return new HarnessRunResult { Answer = $"ERROR: 未找到 graph「{graphId}」" };

        var isResume = !string.IsNullOrWhiteSpace(resumeThreadId);
        var threadId = resumeThreadId
                       ?? request.AgentSessionId
                       ?? ("thread-" + Guid.NewGuid().ToString("N")[..12]);

        var checkpoint = isResume ? HarnessGraphCheckpoint.Load(threadId) : null;
        checkpoint ??= new HarnessGraphCheckpointState
        {
            ThreadId = threadId,
            GraphId = graphId,
            Status = "running",
            UserPrompt = request.Prompt,
            SessionId = request.AgentSessionId,
            RunId = "run-" + Guid.NewGuid().ToString("N")[..12]
        };

        if (checkpoint.IsPaused && isResume)
            checkpoint.Status = "running";

        var runId = checkpoint.RunId ?? ("run-" + Guid.NewGuid().ToString("N")[..12]);
        checkpoint.RunId = runId;

        using var runTracer = HarnessRunTracer.TryBegin(
            _workspace,
            runId,
            request.Config,
            new HarnessRunTracerContext
            {
                Strategy = HarnessGraphStrategy.Format(graphId),
                SessionId = request.AgentSessionId,
                PromptPreview = request.Prompt
            });

        var trace = new HarnessTrace();
        trace.BindTracer(runTracer);
        var toolExecutor = new HarnessToolExecutor(_mcp, _permission, trace, _userQuestions, _workspace, _subAgents);

        var state = new HarnessRunState { RunId = runId, Phase = HarnessPhase.Execute };
        await using var sandboxCoord = await HarnessSandboxCoordinator.BeginRunAsync(
            state, request.Config, _workspace, trace, callbacks.OnLog, ct);

        var startNodeId = ResolveStartNode(graph, checkpoint, isResume);
        if (string.IsNullOrWhiteSpace(startNodeId))
            return new HarnessRunResult { Answer = "ERROR: Graph 无起始节点" };

        var currentId = startNodeId;
        var lastOutput = checkpoint.LastAnswer ?? "";
        var variables = new Dictionary<string, object?>(checkpoint.Variables, StringComparer.OrdinalIgnoreCase);

        while (!string.IsNullOrWhiteSpace(currentId))
        {
            ct.ThrowIfCancellationRequested();
            var node = graph.Nodes.FirstOrDefault(n =>
                n.Id.Equals(currentId, StringComparison.OrdinalIgnoreCase));
            if (node is null)
                break;

            callbacks.OnLog?.Invoke($"Graph 节点: {node.Id} ({node.Type})");
            using var nodeSpan = runTracer?.StartSpan("graph.node", null, new Dictionary<string, object?>
            {
                ["nodeId"] = node.Id,
                ["nodeType"] = node.Type
            });

            var nodeResult = await ExecuteNodeAsync(
                node, request, isResume, variables, lastOutput,
                toolExecutor, sandboxCoord, callbacks, ct);

            lastOutput = nodeResult.Output;
            checkpoint.CurrentNodeId = node.Id;
            checkpoint.LastAnswer = lastOutput;
            checkpoint.Variables = variables;
            foreach (var kv in nodeResult.Variables)
                variables[kv.Key] = kv.Value;

            if (nodeResult.IsPaused)
            {
                checkpoint.Status = "interrupted";
                HarnessGraphCheckpoint.Save(checkpoint);
                runTracer?.FinalizeRun(new HarnessRunMetaFinalizeArgs
                {
                    WorkspaceRoot = _workspace,
                    Strategy = HarnessGraphStrategy.Format(graphId),
                    SessionId = request.AgentSessionId,
                    PromptPreview = request.Prompt,
                    AnswerPreview = lastOutput,
                    Phase = "graph:paused",
                    RetentionDays = request.Config.AgentTraceRetentionDays
                });

                return new HarnessRunResult
                {
                    Answer = lastOutput + "\n\n（Graph 已暂停；使用 /resume thread " + threadId + " 继续）",
                    RunId = runId,
                    GraphThreadId = threadId,
                    GraphId = graphId,
                    GraphPaused = true,
                    HarnessState = SerializeGraphHarnessState(checkpoint)
                };
            }

            if (ShouldCheckpoint(graph))
                HarnessGraphCheckpoint.Save(checkpoint);

            currentId = ResolveNextNode(graph, node.Id, variables);
            isResume = false;
        }

        checkpoint.Status = "completed";
        checkpoint.CurrentNodeId = null;
        HarnessGraphCheckpoint.Save(checkpoint);

        runTracer?.FinalizeRun(new HarnessRunMetaFinalizeArgs
        {
            WorkspaceRoot = _workspace,
            Strategy = HarnessGraphStrategy.Format(graphId),
            SessionId = request.AgentSessionId,
            PromptPreview = request.Prompt,
            AnswerPreview = lastOutput,
            Phase = "graph:completed",
            RetentionDays = request.Config.AgentTraceRetentionDays
        });

        return new HarnessRunResult
        {
            Answer = string.IsNullOrWhiteSpace(lastOutput) ? "（Graph 执行完成，无输出）" : lastOutput,
            RunId = runId,
            GraphThreadId = threadId,
            GraphId = graphId,
            HarnessState = SerializeGraphHarnessState(checkpoint)
        };
    }

    private static bool ShouldCheckpoint(HarnessGraphDefinition graph) =>
        !string.Equals(graph.Checkpoint, "never", StringComparison.OrdinalIgnoreCase);

    private static string? ResolveStartNode(
        HarnessGraphDefinition graph,
        HarnessGraphCheckpointState checkpoint,
        bool isResume)
    {
        if (isResume && !string.IsNullOrWhiteSpace(checkpoint.CurrentNodeId))
            return checkpoint.CurrentNodeId;

        if (!isResume && !string.IsNullOrWhiteSpace(checkpoint.CurrentNodeId)
            && !checkpoint.IsPaused)
            return checkpoint.CurrentNodeId;

        var targeted = new HashSet<string>(graph.Edges.Select(e => e.To), StringComparer.OrdinalIgnoreCase);
        return graph.Nodes.FirstOrDefault(n => !targeted.Contains(n.Id))?.Id
               ?? graph.Nodes.FirstOrDefault()?.Id;
    }

    private static string? ResolveNextNode(
        HarnessGraphDefinition graph,
        string fromNodeId,
        IReadOnlyDictionary<string, object?> variables)
    {
        foreach (var edge in graph.Edges.Where(e =>
                     e.From.Equals(fromNodeId, StringComparison.OrdinalIgnoreCase)))
        {
            if (HarnessGraphCondition.Evaluate(edge.Condition, variables))
                return edge.To;
        }

        return null;
    }

    private async Task<HarnessGraphNodeResult> ExecuteNodeAsync(
        HarnessGraphNode node,
        HarnessRunRequest request,
        bool isResume,
        Dictionary<string, object?> variables,
        string priorOutput,
        HarnessToolExecutor toolExecutor,
        HarnessSandboxCoordinator sandboxCoord,
        HarnessRunCallbacks callbacks,
        CancellationToken ct)
    {
        switch (node.Type.ToLowerInvariant())
        {
            case "subagent":
            {
                var sub = await _subAgents.RunAsync(
                    new HarnessSubAgentRequest
                    {
                        Config = request.Config,
                        Role = node.Role ?? "general",
                        Task = string.IsNullOrWhiteSpace(node.Prompt)
                            ? request.Prompt + "\n\nPrior:\n" + priorOutput
                            : node.Prompt + "\n\n" + priorOutput,
                        ParentSessionId = request.AgentSessionId,
                        RefFileIds = request.RefFileIds
                    },
                    callbacks,
                    ct);
                return HarnessGraphNodeResult.FromOutput(sub.Ok ? sub.Answer : "ERROR: " + sub.Error);
            }
            case "llm":
            {
                var orchestrator = new HarnessOrchestrator(_chat, _mcp, _permission, _userQuestions, _workspace, _subAgents);
                var prompt = string.IsNullOrWhiteSpace(node.Prompt)
                    ? request.Prompt
                    : node.Prompt + "\n\nContext:\n" + priorOutput;
                var result = await orchestrator.RunAsync(
                    new HarnessRunRequest
                    {
                        Config = request.Config,
                        Prompt = prompt,
                        Strategy = AgentStrategies.Execute,
                        AgentSessionId = request.AgentSessionId,
                        MaxStepsOverride = 3,
                        RefFileIds = request.RefFileIds,
                        SkillId = request.SkillId,
                        PlaybookId = request.PlaybookId
                    },
                    callbacks,
                    ct);
                return HarnessGraphNodeResult.FromOutput(result.Answer);
            }
            case "tool":
            {
                var toolName = MapGraphTool(node.Tool ?? "bash");
                var argsJson = node.Args.Count > 0 ? JsonSerializer.Serialize(node.Args) : "{}";
                toolExecutor.SetRunCallbacks(callbacks);
                var exec = await toolExecutor.ExecuteDetailedAsync(
                    toolName, argsJson, request.Config, _workspace,
                    HarnessPhase.Execute, ct, sandboxCoord);
                var exitCode = exec.Output.StartsWith("ERROR:", StringComparison.Ordinal) ? 1 : 0;
                return HarnessGraphNodeResult.FromOutput(exec.Output, new Dictionary<string, object?>
                {
                    ["last_exit_code"] = exitCode,
                    ["last_tool_ok"] = exitCode == 0
                });
            }
            case "parallel":
            {
                var roles = ParseCsv(node.Args.GetValueOrDefault("roles", "explore,plan"));
                var maxConc = 2;
                if (node.Args.TryGetValue("maxConcurrency", out var mc) && int.TryParse(mc, out var n))
                    maxConc = Math.Clamp(n, 1, 10);
                var taskBase = string.IsNullOrWhiteSpace(node.Prompt) ? request.Prompt : node.Prompt;
                var reqs = roles.Select(role => new HarnessSubAgentRequest
                {
                    Config = request.Config,
                    Role = role.Trim(),
                    Task = taskBase + "\n\nPrior:\n" + priorOutput,
                    ParentSessionId = request.AgentSessionId,
                    RefFileIds = request.RefFileIds
                }).ToList();
                var limited = reqs.Take(maxConc).ToList();
                var parallel = await RunParallelLimitedAsync(limited, callbacks, ct);
                var merged = string.Join("\n\n", parallel.Select((r, i) =>
                    $"### {roles.ElementAtOrDefault(i) ?? "agent"}\n" + (r.Ok ? r.Answer : "ERROR: " + r.Error)));
                return HarnessGraphNodeResult.FromOutput(merged);
            }
            case "map":
            {
                var items = ParseJsonStringArray(node.Args.GetValueOrDefault("items", "[]"));
                var role = node.Role ?? "explore";
                var prompt = node.Prompt ?? "Process this item:";
                var parts = new List<string>();
                foreach (var item in items)
                {
                    var sub = await _subAgents.RunAsync(
                        new HarnessSubAgentRequest
                        {
                            Config = request.Config,
                            Role = role,
                            Task = prompt + "\n\nItem:\n" + item + "\n\nContext:\n" + priorOutput,
                            ParentSessionId = request.AgentSessionId,
                            RefFileIds = request.RefFileIds
                        },
                        callbacks,
                        ct);
                    parts.Add("### " + item + "\n" + (sub.Ok ? sub.Answer : "ERROR: " + sub.Error));
                }

                return HarnessGraphNodeResult.FromOutput(string.Join("\n\n", parts));
            }
            case "interrupt":
            {
                if (!isResume)
                {
                    return HarnessGraphNodeResult.CreatePaused(
                        "Graph 在 interrupt 节点「" + node.Id + "」暂停。\n"
                        + (node.Prompt ?? "使用 /resume thread 继续。"));
                }

                if (_userQuestions is null)
                {
                    return HarnessGraphNodeResult.FromOutput(
                        "（interrupt 已恢复，无 UI；自动继续）\n" + (node.Prompt ?? ""));
                }

                var answer = await _userQuestions.AskAsync(new UserQuestionRequest
                {
                    Question = node.Prompt ?? "Continue with graph?",
                    Options =
                    [
                        new UserQuestionOption { Label = "继续", Description = "Resume graph" },
                        new UserQuestionOption { Label = "停止", Description = "Stop graph" }
                    ]
                }, ct);

                if (answer.Contains("停止", StringComparison.OrdinalIgnoreCase))
                    return HarnessGraphNodeResult.FromOutput("用户选择停止 Graph。");

                return HarnessGraphNodeResult.FromOutput(
                    "用户确认: " + answer,
                    new Dictionary<string, object?> { ["interrupt_answer"] = answer });
            }
            default:
                return HarnessGraphNodeResult.FromOutput("ERROR: 未知节点类型 " + node.Type);
        }
    }

    private async Task<IReadOnlyList<HarnessSubAgentResult>> RunParallelLimitedAsync(
        IReadOnlyList<HarnessSubAgentRequest> requests,
        HarnessRunCallbacks callbacks,
        CancellationToken ct)
    {
        if (requests.Count == 0)
            return Array.Empty<HarnessSubAgentResult>();
        return await _subAgents.RunParallelAsync(requests, callbacks, ct);
    }

    private static IReadOnlyList<string> ParseCsv(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

    private static IReadOnlyList<string> ParseJsonStringArray(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();
            return doc.RootElement.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : e.GetRawText())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string MapGraphTool(string tool) => tool.ToLowerInvariant() switch
    {
        "bash" or "shell" => "run_shell",
        "read" => "read_file",
        "write" => "write_file",
        _ => tool
    };

    private static string SerializeGraphHarnessState(HarnessGraphCheckpointState cp) =>
        JsonSerializer.Serialize(new
        {
            graphThreadId = cp.ThreadId,
            graphId = cp.GraphId,
            graphStatus = cp.Status,
            currentNodeId = cp.CurrentNodeId
        });

    private sealed class HarnessGraphNodeResult
    {
        public string Output { get; init; } = "";
        public Dictionary<string, object?> Variables { get; init; } = new();
        public bool IsPaused { get; init; }

        public static HarnessGraphNodeResult FromOutput(
            string output,
            Dictionary<string, object?>? variables = null) => new()
        {
            Output = output,
            Variables = variables ?? new Dictionary<string, object?>()
        };

        public static HarnessGraphNodeResult CreatePaused(string output) => new()
        {
            Output = output,
            IsPaused = true
        };
    }
}
