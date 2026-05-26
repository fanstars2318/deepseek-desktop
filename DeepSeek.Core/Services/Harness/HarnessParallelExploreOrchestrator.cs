using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;
using DeepSeekBrowser.Services.Harness.Observability;

namespace DeepSeekBrowser.Services.Harness;

/// <summary>
/// AutoGen-style parallel explore fan-out: N read-only explorers with different lenses, then a synthesizer.
/// </summary>
public sealed class HarnessParallelExploreOrchestrator
{
    private static readonly (string Id, string TaskSuffix)[] DefaultLenses =
    [
        ("structure", "Focus on repository layout, modules, entry points, and key files. Map paths only."),
        ("dependencies", "Focus on call chains, imports, APIs, and cross-module dependencies."),
        ("risks", "Focus on tests, error handling, security surfaces, and technical debt hotspots.")
    ];

    private readonly HarnessSubAgentService _subAgents;

    public HarnessParallelExploreOrchestrator(HarnessSubAgentService subAgents) => _subAgents = subAgents;

    public async Task<HarnessRunResult> RunAsync(
        HarnessRunRequest request,
        HarnessRunCallbacks callbacks,
        CancellationToken ct)
    {
        if (!request.Config.EnableParallelExplore)
        {
            return new HarnessRunResult
            {
                Answer = "并行 Explore 已在设置中关闭（EnableParallelExplore）。"
            };
        }

        if (!request.Config.EnableSubAgents)
        {
            return new HarnessRunResult
            {
                Answer = "子 Agent 已关闭（EnableSubAgents），无法并行扇出。"
            };
        }

        var workspace = AgentWorkspace.ResolveRoot(request.Config);
        var runId = "run-" + Guid.NewGuid().ToString("N")[..12];
        using var tracer = HarnessRunTracer.TryBegin(
            workspace,
            runId,
            request.Config,
            new HarnessRunTracerContext
            {
                Strategy = AgentStrategies.ParallelExplore,
                SessionId = request.AgentSessionId,
                Model = request.Config.Model,
                PromptPreview = request.Prompt
            });

        var fanOut = Math.Clamp(
            request.ParallelExploreFanOutOverride ?? request.Config.ParallelExploreFanOut,
            1,
            Math.Clamp(request.Config.MaxConcurrentSubAgents, 1, 10));
        var lenses = DefaultLenses.Take(fanOut).ToList();
        while (lenses.Count < fanOut)
            lenses.Add(DefaultLenses[lenses.Count % DefaultLenses.Length]);

        callbacks.OnLog?.Invoke($"Parallel explore: {fanOut} explorers (AutoGen fan-out)…");

        var exploreRequests = lenses.Select((lens, i) => new HarnessSubAgentRequest
        {
            Config = request.Config,
            Role = "explore",
            Task = $"Lens [{lens.Id}]: {lens.TaskSuffix}\n\nUser question:\n{request.Prompt}",
            Context = $"You are explorer #{i + 1} of {fanOut}. Do not duplicate other explorers' focus.",
            ParentSessionId = request.AgentSessionId,
            RefFileIds = request.RefFileIds
        }).ToList();

        var results = await _subAgents.RunParallelAsync(exploreRequests, callbacks, ct);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Parallel explore findings");
        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var lensId = lenses[i].Id;
            sb.AppendLine();
            sb.AppendLine($"### Explorer: {lensId} ({r.Role})");
            sb.AppendLine(r.Ok ? r.Answer : "FAILED: " + (r.Error ?? "unknown"));
        }

        callbacks.OnLog?.Invoke("Parallel explore: synthesizing…");
        var synthRole = "plan";
        if (request.Config.EnableDynamicGroupChat && AgentChatClientFactory.UsesDirectApi(request.Config))
        {
            try
            {
                using var chat = new OpenAiAgentChatClient(request.Config);
                synthRole = await HarnessGroupChatPlanner.PickNextRoleAsync(
                    chat, request.Config, request.Prompt, sb.ToString(), ct);
                callbacks.OnLog?.Invoke("Dynamic group chat picked synthesizer role: " + synthRole);
            }
            catch
            {
                // keep plan
            }
        }

        var synth = await _subAgents.RunAsync(
            new HarnessSubAgentRequest
            {
                Config = request.Config,
                Role = synthRole,
                Task =
                    "Merge the parallel explorer reports into one actionable brief for the lead agent: " +
                    "summary, key paths, open questions, suggested next steps.",
                Context = sb.ToString(),
                ParentSessionId = request.AgentSessionId
            },
            callbacks,
            ct);

        var answer = synth.Ok
            ? "## Parallel explore complete\n\n" + synth.Answer + "\n\n---\n\n" + sb
            : "Parallel explore synthesis failed: " + (synth.Error ?? "") + "\n\n" + sb;

        tracer?.FinalizeRun(new HarnessRunMetaFinalizeArgs
        {
            WorkspaceRoot = workspace,
            Strategy = AgentStrategies.ParallelExplore,
            SessionId = request.AgentSessionId,
            PromptPreview = request.Prompt,
            AnswerPreview = answer,
            RetentionDays = request.Config.AgentTraceRetentionDays
        });

        return new HarnessRunResult
        {
            Answer = answer,
            HarnessState = request.ExistingHarnessState,
            WebChatSessionId = request.WebChatSessionId,
            RunId = runId
        };
    }
}
