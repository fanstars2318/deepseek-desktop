using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services;

/// <summary>
/// 子 Agent：在计划模式下执行单步任务，工具集可限定为 planner 建议的 MCP 工具。
/// </summary>
internal sealed class SubAgentRunner
{
    private readonly AgentReactExecutor _react;

    public SubAgentRunner(LocalChat2ApiClient chat2Api, QwenCode.QwenCodeCore qwenCode) =>
        _react = new AgentReactExecutor(chat2Api, qwenCode.Tools);

    public async Task<string> RunStepAsync(
        AppConfig config,
        string parentTask,
        AgentPlanStep step,
        IReadOnlyList<string> priorSummaries,
        McpToolRegistry registry,
        bool useMcpTools,
        bool thinking,
        bool search,
        Action<string> onLog,
        CancellationToken ct)
    {
        var system = AgentPromptBuilder.BuildSubAgentPrompt(parentTask, step, registry, priorSummaries);
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = system },
            new() { Role = "user", Content = step.Description }
        };

        var maxSteps = Math.Max(3, config.MaxSubAgentSteps);
        var answer = await _react.RunUntilFinalAnswerAsync(
            messages, config, useMcpTools, thinking, search, maxSteps, onLog, ct);

        return string.IsNullOrWhiteSpace(answer)
            ? "（本子任务未得到明确结论，请结合 Observation 继续）"
            : answer;
    }
}
