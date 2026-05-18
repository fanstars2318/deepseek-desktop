using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.QwenCode;

namespace DeepSeekBrowser.Services;

/// <summary>
/// 计划模式：先规划（Qwen planning_prompt）→ 子 Agent 逐步执行 → 汇总（Chat2API）。
/// </summary>
internal sealed class PlanModeRunner
{
    private readonly LocalChat2ApiClient _chat2Api;
    private readonly QwenCode.QwenCodeCore _qwenCode;
    private readonly SubAgentRunner _subAgent;
    private readonly NamedSubAgentRunner _namedSubAgent;
    private readonly AgentReactExecutor _react;

    public PlanModeRunner(LocalChat2ApiClient chat2Api, QwenCode.QwenCodeCore qwenCode)
    {
        _chat2Api = chat2Api;
        _qwenCode = qwenCode;
        _subAgent = new SubAgentRunner(chat2Api, qwenCode);
        _namedSubAgent = new NamedSubAgentRunner(chat2Api, qwenCode);
        _react = new AgentReactExecutor(chat2Api, qwenCode.Tools);
    }

    public async Task<string?> RunAsync(
        AppConfig config,
        string userTask,
        bool useMcpTools,
        bool thinking,
        bool search,
        Action<string> onLog,
        CancellationToken ct)
    {
        var descriptors = useMcpTools ? await _qwenCode.Tools.ListAllToolsAsync(config, ct) : [];
        var registry = McpToolRegistry.FromDescriptors(descriptors);

        onLog("[计划模式] 正在生成任务分解…");
        var planMessages = new List<ChatMessage>
        {
            new() { Role = "system", Content = AgentPromptBuilder.BuildPlannerPrompt(registry, config) },
            new() { Role = "user", Content = userTask }
        };

        var planResult = await _chat2Api.ChatAsync(planMessages, config, thinking, search, ct, onLog);
        var plan = AgentPlanParser.Parse(planResult.Content);

        if (plan.Steps.Count == 0)
        {
            onLog("[计划模式] 未能解析 JSON 计划，退回单 Agent ReAct。");
            var fallback = new AgentSessionRunner(_chat2Api, _qwenCode);
            return await fallback.RunAsync(config, userTask, useMcpTools, thinking, search, onLog, ct);
        }

        onLog($"[计划模式] 共 {plan.Steps.Count} 步");
        foreach (var s in plan.Steps)
            onLog($"  · {s.Id}. {s.Title}");

        var priorSummaries = new List<string>();
        foreach (var step in plan.Steps)
        {
            ct.ThrowIfCancellationRequested();
            onLog($"══ 子 Agent · 步骤 {step.Id}: {step.Title} ══");

            string summary;
            if (!string.IsNullOrWhiteSpace(step.SubAgentName)
                && config.EnableQwenSubAgentConfigs
                && QwenSubAgentRegistry.Find(config, step.SubAgentName) is { } named)
            {
                summary = await _namedSubAgent.RunAsync(
                    config, named, step.Description, useMcpTools, thinking, search, onLog, ct)
                    ?? "（Subagent 无输出）";
            }
            else
            {
                var stepRegistry = registry.FilterByHints(step.ToolHints);
                if (step.ToolHints.Count > 0)
                    onLog($"工具范围: {stepRegistry.Count} 个（来自 planner 建议）");

                summary = await _subAgent.RunStepAsync(
                    config, userTask, step, priorSummaries, stepRegistry,
                    useMcpTools, thinking, search, onLog, ct);
            }

            priorSummaries.Add($"步骤 {step.Id}「{step.Title}」: {summary}");
        }

        onLog("[计划模式] 汇总各步结果…");
        var synthMessages = new List<ChatMessage>
        {
            new() { Role = "system", Content = AgentPromptBuilder.BuildSynthesisPrompt(userTask, priorSummaries) },
            new() { Role = "user", Content = "请给出 Final Answer。" }
        };

        var final = await _react.RunUntilFinalAnswerAsync(
            synthMessages, config, useMcpTools: false, thinking, search,
            maxSteps: Math.Min(6, config.MaxAgentSteps), onLog, ct);

        onLog(string.IsNullOrWhiteSpace(final) ? "任务完成。" : "Final Answer: " + final);
        return final;
    }
}
