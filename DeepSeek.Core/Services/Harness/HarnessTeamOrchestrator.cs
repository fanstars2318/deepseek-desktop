using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;
using DeepSeekBrowser.Services.Harness.Observability;

namespace DeepSeekBrowser.Services.Harness;

/// <summary>
/// MetaGPT-style SOP: PM → Architect → Engineer → Reviewer (sequential "dream team").
/// AutoGen-inspired: each role is a sub-run with handoff context.
/// </summary>
public sealed class HarnessTeamOrchestrator
{
    private readonly HarnessSubAgentService _subAgents;

    public HarnessTeamOrchestrator(HarnessSubAgentService subAgents) => _subAgents = subAgents;

    public async Task<HarnessRunResult> RunAsync(
        HarnessRunRequest request,
        HarnessRunCallbacks callbacks,
        CancellationToken ct)
    {
        var workspace = AgentWorkspace.ResolveRoot(request.Config);
        var runId = "run-" + Guid.NewGuid().ToString("N")[..12];
        using var tracer = HarnessRunTracer.TryBegin(
            workspace,
            runId,
            request.Config,
            new HarnessRunTracerContext
            {
                Strategy = AgentStrategies.Team,
                SessionId = request.AgentSessionId,
                Model = request.Config.Model,
                PromptPreview = request.Prompt
            });

        if (!request.Config.EnableTeamWorkflow)
        {
            return new HarnessRunResult
            {
                Answer = "Team 梦之队工作流已在设置中关闭（EnableTeamWorkflow）。可在 DeepSeek 设置 → 多智能体 中启用。"
            };
        }

        var audit = new HarnessTeamAuditLog(request.AgentSessionId);
        audit.Append("team_start", new { prompt = Truncate(request.Prompt, 300) });
        callbacks.OnLog?.Invoke("Team SOP: Product Manager → Architect → Engineer → Reviewer");

        var handoff = new System.Text.StringBuilder();
        handoff.AppendLine("# User request");
        handoff.AppendLine(request.Prompt);

        async Task<string> RunRoleAsync(string roleId, string task)
        {
            callbacks.OnLog?.Invoke($"Team phase: {roleId}");
            callbacks.OnActivity?.Invoke(new AgentUiActivity("Team", roleId, Truncate(task, 80)));
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
                throw new InvalidOperationException($"Team role {roleId} failed: {sub.Error}");
            handoff.AppendLine();
            handoff.AppendLine($"## Output from {roleId}");
            handoff.AppendLine(sub.Answer);
            audit.Append("team_role_done", new { role = roleId, len = sub.Answer.Length });
            return sub.Answer;
        }

        try
        {
            await RunRoleAsync("product-manager",
                "Write a concise PRD (goals, user stories, acceptance criteria, out-of-scope) for the user request.");
            await RunRoleAsync("architect",
                "Produce technical design from the PRD: components, files to touch, APIs, risks. Read workspace as needed.");
            await RunRoleAsync("engineer",
                "Implement the design in the workspace. Use tools; keep changes focused and verifiable.");
            var review = await RunRoleAsync("reviewer",
                "Review the implementation against the PRD. List findings by severity; note test gaps.");

            audit.Append("team_complete", new { ok = true });
            var summary =
                "## Team run complete\n\n" +
                "### Review summary\n" + review + "\n\n" +
                "Full handoff transcript is in the audit log: " + audit.FilePath;

            callbacks.OnLog?.Invoke("Team SOP finished. Audit: " + audit.FilePath);
            tracer?.FinalizeRun(new HarnessRunMetaFinalizeArgs
            {
                WorkspaceRoot = workspace,
                Strategy = AgentStrategies.Team,
                SessionId = request.AgentSessionId,
                PromptPreview = request.Prompt,
                AnswerPreview = summary,
                RetentionDays = request.Config.AgentTraceRetentionDays
            });
            return new HarnessRunResult
            {
                Answer = summary,
                HarnessState = request.ExistingHarnessState,
                WebChatSessionId = request.WebChatSessionId,
                RunId = runId
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            audit.Append("team_error", new { error = ex.Message });
            callbacks.OnLog?.Invoke("Team SOP error: " + ex.Message);
            return new HarnessRunResult { Answer = "Team workflow failed: " + ex.Message };
        }
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
