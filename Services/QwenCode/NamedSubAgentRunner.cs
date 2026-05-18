using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.QwenCode;

/// <summary>运行 <c>.qwen/agents</c> 中配置的命名 Subagent（对齐官方 sub-agents）。</summary>
public sealed class NamedSubAgentRunner
{
    private readonly LocalChat2ApiClient _chat2Api;
    private readonly QwenCodeCore _core;

    public NamedSubAgentRunner(LocalChat2ApiClient chat2Api, QwenCodeCore core)
    {
        _chat2Api = chat2Api;
        _core = core;
    }

    public async Task<string?> RunAsync(
        AppConfig config,
        QwenSubAgentDefinition agent,
        string userTask,
        bool useMcpTools,
        bool thinking,
        bool search,
        Action<string> onLog,
        CancellationToken ct)
    {
        onLog($"[Subagent] {agent.Name} ({agent.Scope})");
        if (!string.IsNullOrWhiteSpace(agent.Description))
            onLog(agent.Description);

        var descriptors = useMcpTools
            ? await _core.Tools.ListAllToolsAsync(config, ct)
            : [];
        var registry = FilterTools(McpToolRegistry.FromDescriptors(descriptors), agent);

        var system = BuildSystemPrompt(agent, userTask, registry);
        var react = new AgentReactExecutor(_chat2Api, _core.Tools);
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = system },
            new() { Role = "user", Content = userTask }
        };

        var maxSteps = Math.Max(5, config.MaxSubAgentSteps);
        var answer = await react.RunUntilFinalAnswerAsync(
            messages, config, useMcpTools, thinking, search, maxSteps, onLog, ct);

        return string.IsNullOrWhiteSpace(answer)
            ? "（Subagent 未返回明确结论）"
            : answer;
    }

    private static McpToolRegistry FilterTools(McpToolRegistry registry, QwenSubAgentDefinition agent)
    {
        var names = registry.All.Select(t => t.ExposedName).ToList();
        if (agent.AllowedTools.Count > 0)
        {
            var allow = new HashSet<string>(agent.AllowedTools, StringComparer.OrdinalIgnoreCase);
            names = names.Where(n => allow.Contains(n) || allow.Contains(QwenCodePort.NormalizeToolName(n) ?? ""))
                .ToList();
        }

        if (agent.DisallowedTools.Count > 0)
        {
            var deny = new HashSet<string>(agent.DisallowedTools, StringComparer.OrdinalIgnoreCase);
            names = names.Where(n =>
            {
                var norm = QwenCodePort.NormalizeToolName(n) ?? n;
                return !deny.Contains(n) && !deny.Contains(norm);
            }).ToList();
        }

        return registry.FilterByNames(names);
    }

    private static string BuildSystemPrompt(
        QwenSubAgentDefinition agent,
        string userTask,
        McpToolRegistry registry)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(agent.SystemPrompt.Trim());
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("你是 DeepSeek 桌面端中的 Qwen Code Subagent。完成用户任务后输出 Final Answer。");
        sb.AppendLine("用户任务: " + userTask);
        if (registry.Count > 0)
        {
            sb.AppendLine();
            sb.Append(AgentPromptBuilder.BuildReactAgentPrompt(registry, isCoordinator: false));
        }

        return sb.ToString().Trim();
    }
}
