using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services;

/// <summary>
/// Agent 编排：ReAct（默认）或计划+子 Agent；推理使用用户在 chat.deepseek.com 的登录会话（见 bridge.js / Chat2API-main 同款网页 API）。
/// </summary>
public sealed class AgentOrchestrator
{
    private readonly QwenCode.QwenCodeCore _qwenCode;
    private readonly LocalChat2ApiClient _chat2Api;

    public AgentOrchestrator(QwenCode.QwenCodeCore qwenCode, LocalChat2ApiClient chat2Api)
    {
        _qwenCode = qwenCode;
        _chat2Api = chat2Api;
    }

    public async Task<string?> RunAsync(
        AppConfig config,
        string userTask,
        string strategy,
        bool useMcpTools,
        bool thinking,
        bool search,
        Action<string> onLog,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.WebUserToken))
            throw new InvalidOperationException("请先在网页登录 DeepSeek，本地 Chat2API 将自动使用网页会话。");

        if (useMcpTools && !_qwenCode.Tools.HasAnyTools(config))
            throw new InvalidOperationException("请启用 Qwen Code 内置工具或在「MCP 设置」中连接 MCP 服务器。");

        var mode = NormalizeStrategy(strategy, config.DefaultAgentStrategy);
        onLog(mode == AgentStrategies.Plan
            ? "策略: 计划模式（Qwen Code 规划 → 子 Agent → 汇总）"
            : "策略: ReAct 模式（Qwen Thought/Action/Observation）");

        if (useMcpTools)
        {
            var tools = await _qwenCode.Tools.ListAllToolsAsync(config, ct);
            var builtin = tools.Count(t => QwenCode.QwenCodePort.IsOfficialBuiltin(t.ExposedName));
            var mcp = tools.Count - builtin;
            onLog($"工具: {builtin} 个内置 + {mcp} 个 MCP");
        }

        if (mode == AgentStrategies.Plan && config.EnableSubAgents)
        {
            var planner = new PlanModeRunner(_chat2Api, _qwenCode);
            return await planner.RunAsync(config, userTask, useMcpTools, thinking, search, onLog, ct);
        }

        var react = new AgentSessionRunner(_chat2Api, _qwenCode);
        return await react.RunAsync(config, userTask, useMcpTools, thinking, search, onLog, ct);
    }

    private static string NormalizeStrategy(string? fromUi, string? fromConfig)
    {
        if (AgentStrategies.Plan.Equals(fromUi, StringComparison.OrdinalIgnoreCase))
            return AgentStrategies.Plan;
        if (AgentStrategies.React.Equals(fromUi, StringComparison.OrdinalIgnoreCase))
            return AgentStrategies.React;
        return AgentStrategies.Plan.Equals(fromConfig, StringComparison.OrdinalIgnoreCase)
            ? AgentStrategies.Plan
            : AgentStrategies.React;
    }
}

public static class AgentStrategies
{
    public const string React = "react";
    public const string Plan = "plan";
}
