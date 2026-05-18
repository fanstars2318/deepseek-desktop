using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services;

/// <summary>
/// Qwen ReAct 单 Agent 模式：编排逻辑在 C#，推理经 127.0.0.1 Chat2API → 网页会话。
/// </summary>
public sealed class AgentSessionRunner
{
    private readonly LocalChat2ApiClient _chat2Api;
    private readonly QwenCode.QwenCodeCore _qwenCode;
    private readonly AgentReactExecutor _react;

    public AgentSessionRunner(LocalChat2ApiClient chat2Api, QwenCode.QwenCodeCore qwenCode)
    {
        _chat2Api = chat2Api;
        _qwenCode = qwenCode;
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
        if (string.IsNullOrWhiteSpace(config.WebUserToken))
            throw new InvalidOperationException("请先在网页登录 DeepSeek，本地 Chat2API 将自动使用网页会话。");

        if (useMcpTools)
        {
            if (!_qwenCode.Tools.HasAnyTools(config))
                throw new InvalidOperationException("请启用内置工具或连接 MCP。");

            var tools = await _qwenCode.Tools.ListAllToolsAsync(config, ct);
            onLog(tools.Count > 0
                ? $"已注册 {tools.Count} 个工具（ReAct）。"
                : "警告: 未发现可用工具。");
        }

        onLog($"推理: {_chat2Api.BaseUrl}/chat/completions → 网页 Token（Qwen Code Core）");

        var systemPrompt = await AgentPromptBuilder.BuildSystemPromptAsync(_qwenCode.Tools, config, useMcpTools, ct);
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userTask }
        };

        var answer = await _react.RunUntilFinalAnswerAsync(
            messages, config, useMcpTools, thinking, search, config.MaxAgentSteps, onLog, ct);

        if (string.IsNullOrWhiteSpace(answer))
            onLog("已达到最大步数。可缩小任务、改用计划模式，或在设置中提高步数。");
        else
            onLog("任务完成。");

        return answer;
    }
}
