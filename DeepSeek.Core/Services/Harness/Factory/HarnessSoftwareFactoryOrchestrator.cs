using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.Harness.Observability;

namespace DeepSeekBrowser.Services.Harness.Factory;

/// <summary>MetaGPT-style software factory: PM → Architect → Engineer → QA with artifacts and git init.</summary>
public sealed class HarnessSoftwareFactoryOrchestrator
{
    private readonly Func<IAgentWebChat> _chatFactory;
    private readonly McpHub _mcp;
    private readonly HarnessSubAgentService _subAgents;
    private readonly Func<string, string, Task<bool>> _requestApproval;
    private readonly Func<string, string, IReadOnlyList<string>, Task<bool>>? _scopeApproval;
    private readonly IUserQuestionHandler? _userQuestions;
    private readonly string _workspace;

    public HarnessSoftwareFactoryOrchestrator(
        Func<IAgentWebChat> chatFactory,
        McpHub mcp,
        HarnessSubAgentService subAgents,
        Func<string, string, Task<bool>> requestApproval,
        string workspace,
        IUserQuestionHandler? userQuestions = null,
        Func<string, string, IReadOnlyList<string>, Task<bool>>? scopeApproval = null)
    {
        _chatFactory = chatFactory;
        _mcp = mcp;
        _subAgents = subAgents;
        _requestApproval = requestApproval;
        _workspace = workspace;
        _userQuestions = userQuestions;
        _scopeApproval = scopeApproval;
    }

    public async Task<HarnessRunResult> RunAsync(
        HarnessRunRequest request,
        HarnessRunCallbacks callbacks,
        CancellationToken ct)
    {
        if (!request.Config.EnableSoftwareFactoryWorkflow)
        {
            return new HarnessRunResult
            {
                Answer = "软件工厂工作流已在设置中关闭（EnableSoftwareFactoryWorkflow）。"
            };
        }

        if (!request.Config.EnableSubAgents)
        {
            return new HarnessRunResult
            {
                Answer = "子 Agent 已关闭（EnableSubAgents），无法运行软件工厂流水线。"
            };
        }

        var runId = "run-" + Guid.NewGuid().ToString("N")[..12];
        using var tracer = HarnessRunTracer.TryBegin(
            _workspace,
            runId,
            request.Config,
            new HarnessRunTracerContext
            {
                Strategy = AgentStrategies.SoftwareFactory,
                SessionId = request.AgentSessionId,
                Model = request.Config.Model,
                PromptPreview = request.Prompt
            });

        callbacks.OnLog?.Invoke("Software factory: PM → Architect → Engineer → QA");
        var phaseOutputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var artifacts = new List<string>();
        var handoff = new System.Text.StringBuilder();
        handoff.AppendLine("# User request").AppendLine(request.Prompt);

        async Task<string> RunRoleAsync(string phaseName, string roleId, string task)
        {
            callbacks.OnLog?.Invoke($"Factory phase: {phaseName} ({roleId})");
            var sub = await _subAgents.RunAsync(
                new HarnessSubAgentRequest
                {
                    Config = request.Config,
                    Role = roleId,
                    Task = task,
                    Context = handoff.ToString(),
                    ParentSessionId = request.AgentSessionId,
                    RefFileIds = request.RefFileIds
                },
                callbacks,
                ct);
            if (!sub.Ok)
                throw new InvalidOperationException($"{phaseName} failed: {sub.Error}");
            phaseOutputs[phaseName] = sub.Answer;
            handoff.AppendLine().AppendLine($"## {phaseName}").AppendLine(sub.Answer);
            var file = phaseName.ToLowerInvariant().Replace(' ', '-') + ".md";
            var path = HarnessArtifactWriter.Write(_workspace, runId, file, sub.Answer);
            artifacts.Add(HarnessArtifactWriter.RelativePath(_workspace, path));
            return sub.Answer;
        }

        try
        {
            await RunRoleAsync("PM", "product-manager",
                "Write a concise PRD (goals, user stories, acceptance criteria, out-of-scope).");
            await RunRoleAsync("Architect", "architect",
                "Produce technical design from the PRD: components, files, APIs, risks.");

            callbacks.OnLog?.Invoke("Factory phase: Engineer (Harness execute loop)…");
            var chat = _chatFactory();
            var approval = new ApprovalGate(request.Config, _requestApproval);
            var permission = new PermissionGate(request.Config, approval, _scopeApproval);
            var orchestrator = new HarnessOrchestrator(chat, _mcp, permission, _userQuestions, _workspace, _subAgents);
            var engineerPrompt =
                "Implement the software factory design in the workspace. Use tools; keep changes focused and verifiable.\n\n"
                + handoff;
            var engineerResult = await orchestrator.RunAsync(
                new HarnessRunRequest
                {
                    Config = request.Config,
                    Prompt = engineerPrompt,
                    Strategy = AgentStrategies.Execute,
                    AgentSessionId = request.AgentSessionId,
                    RefFileIds = request.RefFileIds,
                    MaxStepsOverride = request.MaxStepsOverride
                },
                callbacks,
                ct);
            phaseOutputs["Engineer"] = engineerResult.Answer;
            handoff.AppendLine().AppendLine("## Engineer").AppendLine(engineerResult.Answer);
            var engPath = HarnessArtifactWriter.Write(_workspace, runId, "engineer.md", engineerResult.Answer);
            artifacts.Add(HarnessArtifactWriter.RelativePath(_workspace, engPath));

            await RunRoleAsync("QA", "reviewer",
                "Review implementation against PRD. List findings by severity; note test gaps and acceptance status.");

            var gitSummary = await HarnessRepoBootstrapper.EnsureGitRepoAsync(_workspace, ct);
            string? verifySummary = null;
            if (request.Config.AgentVerifyAfterExecute && !string.IsNullOrWhiteSpace(request.Config.AgentVerifyCommand))
            {
                var verify = await HarnessVerifyChain.RunAsync(
                    [new HarnessVerifyStep { Command = request.Config.AgentVerifyCommand, Name = "factory-verify" }],
                    _workspace,
                    ct);
                verifySummary = verify.CombinedOutput;
            }

            var delivery = HarnessDeliveryReport.Build(
                request.Prompt, phaseOutputs, artifacts, gitSummary, verifySummary);
            var deliveryPath = HarnessDeliveryReport.WriteFile(_workspace, runId, delivery);
            artifacts.Add(HarnessArtifactWriter.RelativePath(_workspace, deliveryPath));

            tracer?.FinalizeRun(new HarnessRunMetaFinalizeArgs
            {
                WorkspaceRoot = _workspace,
                Strategy = AgentStrategies.SoftwareFactory,
                SessionId = request.AgentSessionId,
                PromptPreview = request.Prompt,
                AnswerPreview = delivery,
                RetentionDays = request.Config.AgentTraceRetentionDays
            });

            return new HarnessRunResult
            {
                Answer = delivery,
                RunId = runId,
                HarnessState = request.ExistingHarnessState,
                WebChatSessionId = request.WebChatSessionId
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            callbacks.OnLog?.Invoke("Software factory error: " + ex.Message);
            return new HarnessRunResult { Answer = "Software factory failed: " + ex.Message, RunId = runId };
        }
    }
}
