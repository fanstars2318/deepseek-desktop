using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;
using DeepSeekBrowser.Services.Harness.Observability;

namespace DeepSeekBrowser.Services.Harness;

/// <summary>
/// CAMEL-style dual-agent debate: Advocate proposes, Critic challenges, repeat, then moderator summary.
/// </summary>
public sealed class HarnessDebateOrchestrator
{
    private readonly HarnessSubAgentService _subAgents;

    public HarnessDebateOrchestrator(HarnessSubAgentService subAgents) => _subAgents = subAgents;

    public async Task<HarnessRunResult> RunAsync(
        HarnessRunRequest request,
        HarnessRunCallbacks callbacks,
        CancellationToken ct)
    {
        if (!request.Config.EnableDebateWorkflow)
        {
            return new HarnessRunResult
            {
                Answer = "辩论模式已在设置中关闭（EnableDebateWorkflow）。"
            };
        }

        if (!request.Config.EnableSubAgents)
        {
            return new HarnessRunResult
            {
                Answer = "子 Agent 已关闭（EnableSubAgents），无法运行辩论。"
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
                Strategy = AgentStrategies.Debate,
                SessionId = request.AgentSessionId,
                Model = request.Config.Model,
                PromptPreview = request.Prompt
            });

        var rounds = Math.Clamp(request.Config.DebateMaxRounds, 1, 8);
        var transcript = new System.Text.StringBuilder();
        transcript.AppendLine("# User task");
        transcript.AppendLine(request.Prompt);

        callbacks.OnLog?.Invoke($"Debate mode: Advocate ↔ Critic ×{rounds} rounds (CAMEL)");

        string? lastAdvocate = null;
        for (var round = 1; round <= rounds; round++)
        {
            callbacks.OnLog?.Invoke($"Debate round {round}/{rounds}: Advocate");
            var advocateTask = round == 1
                ? "Propose an initial solution or design for the user task."
                : "Revise your proposal addressing the Critic's latest feedback.";
            var advocate = await _subAgents.RunAsync(
                new HarnessSubAgentRequest
                {
                    Config = request.Config,
                    Role = "advocate",
                    Task = advocateTask,
                    Context = transcript.ToString(),
                    ParentSessionId = request.AgentSessionId,
                    RefFileIds = request.RefFileIds
                },
                callbacks,
                ct);
            if (!advocate.Ok)
                return Fail($"Advocate failed (round {round}): {advocate.Error}");

            lastAdvocate = advocate.Answer;
            transcript.AppendLine();
            transcript.AppendLine($"## Round {round} — Advocate");
            transcript.AppendLine(advocate.Answer);

            callbacks.OnLog?.Invoke($"Debate round {round}/{rounds}: Critic");
            var critic = await _subAgents.RunAsync(
                new HarnessSubAgentRequest
                {
                    Config = request.Config,
                    Role = "critic",
                    Task = "Critique the Advocate's latest proposal. List concrete issues and improvements.",
                    Context = transcript.ToString(),
                    ParentSessionId = request.AgentSessionId
                },
                callbacks,
                ct);
            if (!critic.Ok)
                return Fail($"Critic failed (round {round}): {critic.Error}");

            transcript.AppendLine();
            transcript.AppendLine($"## Round {round} — Critic");
            transcript.AppendLine(critic.Answer);
        }

        callbacks.OnLog?.Invoke("Debate: moderator summary");
        var moderator = await _subAgents.RunAsync(
            new HarnessSubAgentRequest
            {
                Config = request.Config,
                Role = "plan",
                Task =
                    "As neutral moderator, summarize the debate: final recommended approach, " +
                    "resolved disagreements, and remaining risks. Output markdown for the lead agent.",
                Context = transcript.ToString(),
                ParentSessionId = request.AgentSessionId
            },
            callbacks,
            ct);

        var summary = moderator.Ok
            ? moderator.Answer
            : "Moderator synthesis failed; see transcript below.\n\n" + transcript;

        var answer =
            "## Debate complete (CAMEL)\n\n" +
            "### Moderator summary\n" + summary +
            "\n\n### Last advocate position\n" + (lastAdvocate ?? "") +
            "\n\n---\n\n## Full transcript\n\n" + transcript;

        tracer?.FinalizeRun(new HarnessRunMetaFinalizeArgs
        {
            WorkspaceRoot = workspace,
            Strategy = AgentStrategies.Debate,
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

        HarnessRunResult Fail(string msg) => new() { Answer = "Debate workflow failed: " + msg };
    }
}
