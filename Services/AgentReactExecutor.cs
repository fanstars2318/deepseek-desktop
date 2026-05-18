using System.Text.RegularExpressions;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.QwenCode;

namespace DeepSeekBrowser.Services;

/// <summary>
/// Qwen ReAct 执行循环（Thought → Action → Observation），经本地 Chat2API → 网页会话。
/// </summary>
internal sealed class AgentReactExecutor
{
    private readonly LocalChat2ApiClient _chat2Api;
    private readonly UnifiedToolHub _tools;

    public AgentReactExecutor(LocalChat2ApiClient chat2Api, UnifiedToolHub tools)
    {
        _chat2Api = chat2Api;
        _tools = tools;
    }

    public async Task<string?> RunUntilFinalAnswerAsync(
        List<ChatMessage> messages,
        AppConfig config,
        bool useMcpTools,
        bool thinking,
        bool search,
        int maxSteps,
        Action<string> onLog,
        CancellationToken ct)
    {
        for (var step = 0; step < maxSteps; step++)
        {
            ct.ThrowIfCancellationRequested();
            onLog($"--- ReAct 第 {step + 1} 步 ---");

            var result = await _chat2Api.ChatAsync(
                AgentMessageTrimmer.TrimForContext(messages), config, thinking, search, ct, onLog);
            var turn = AgentToolCallParser.Parse(result);

            if (!string.IsNullOrWhiteSpace(result.ReasoningContent))
                onLog("思考: " + Truncate(result.ReasoningContent, 400));

            var thought = AgentToolCallParser.ExtractThought(turn.Text ?? result.Content);
            if (!string.IsNullOrWhiteSpace(thought))
                onLog("Thought: " + Truncate(thought, 300));

            if (turn.ToolCalls is { Count: > 0 })
            {
                var assistantContent = turn.Text;
                if (!string.IsNullOrWhiteSpace(assistantContent))
                    messages.Add(new ChatMessage { Role = "assistant", Content = assistantContent });

                messages.Add(new ChatMessage { Role = "assistant", ToolCalls = turn.ToolCalls });
                onLog($"Action: 调用 {turn.ToolCalls.Count} 个工具");

                var observations = await Task.WhenAll(turn.ToolCalls.Select(async call =>
                {
                    var exposed = _tools.ResolveToolName(call.Name);
                    onLog($"Calling {exposed}");
                    try
                    {
                        if (!useMcpTools)
                            return (call, "ERROR: 未启用工具。");
                        var text = await _tools.CallToolAsync(exposed, call.Arguments, config, ct);
                        return (call, text);
                    }
                    catch (Exception ex)
                    {
                        return (call, "ERROR: " + ex.Message);
                    }
                }));

                foreach (var (call, observation) in observations)
                {
                    onLog("Observation: " + Truncate(observation, 500));
                    messages.Add(new ChatMessage
                    {
                        Role = "tool",
                        ToolCallId = call.Id,
                        Content = observation
                    });
                }

                continue;
            }

            var answer = ExtractFinalAnswer(turn.Text ?? result.Content);
            if (!string.IsNullOrWhiteSpace(answer))
            {
                onLog("Final Answer: " + answer);
                return answer;
            }

            if (!string.IsNullOrWhiteSpace(turn.Text ?? result.Content))
            {
                var text = turn.Text ?? result.Content!;
                onLog(text);
                return text;
            }

            return null;
        }

        onLog("已达到本阶段最大 ReAct 步数。");
        return null;
    }

    private static string? ExtractFinalAnswer(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        var m = Regex.Match(content, @"Final Answer:\s*(.+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
