using System.Text;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.QwenCode;

namespace DeepSeekBrowser.Services;

internal static class AgentPromptBuilder
{
    private const string ReActFormatBlock =
        """
        Use the following format:

        Question: the input question you must answer
        Thought: you should always think about what to do
        Action: the action to take, should be one of [{tool_names}]
        Action Input: the input to the action
        Observation: the result of the action
        ... (this Thought/Action/Action Input/Observation can be repeated zero or more times)
        Thought: I now know the final answer
        Final Answer: the final answer to the original input question
        """;

    public static async Task<string> BuildSystemPromptAsync(
        UnifiedToolHub tools,
        AppConfig config,
        bool useTools,
        CancellationToken ct)
    {
        if (!useTools)
        {
            return """
                   你是 DeepSeek 桌面 Agent。当前未启用工具，请直接推理并输出 Final Answer。
                   """.Trim();
        }

        var registry = McpToolRegistry.FromDescriptors(await tools.ListAllToolsAsync(config, ct));
        return BuildReactAgentPrompt(registry, isCoordinator: true, config);
    }

    public static string BuildExtensionsBlock(AppConfig config)
    {
        var sb = new StringBuilder();
        if (config.EnableQwenSkills)
        {
            var skills = QwenSkillRegistry.Discover(config);
            if (skills.Count > 0)
            {
                sb.AppendLine("可用 Skills（用户可用 /skills <name> 加载，或你判断相关时遵循其说明）：");
                sb.AppendLine(QwenSkillRegistry.FormatCatalog(skills));
                sb.AppendLine();
            }
        }

        if (config.EnableQwenSubAgentConfigs)
        {
            var agents = QwenSubAgentRegistry.Discover(config);
            if (agents.Count > 0)
            {
                sb.AppendLine("可用命名 Subagents（用户可用 /agents <name> 委派，计划步骤可填 subagent 字段）：");
                sb.AppendLine(QwenSubAgentRegistry.FormatCatalog(agents));
                sb.AppendLine();
            }
        }

        return sb.Length > 0 ? sb.ToString().TrimEnd() : "";
    }

    public static string BuildReactAgentPrompt(
        McpToolRegistry registry,
        bool isCoordinator,
        AppConfig? config = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(isCoordinator
            ? "你是 DeepSeek 桌面 Agent（外壳）中的 Qwen Code Core（C# 移植 @qwen-code/qwen-code）。通过官方同名 Core 工具与 MCP 完成任务；推理仅使用已登录的 DeepSeek 网页会话（Chat2API），不得编造 API 结果。"
            : "你是 DeepSeek 子 Agent，负责完成计划中的单一步骤。");
        sb.AppendLine("工作方式遵循 Qwen Code / Qwen ReAct（Thought → Action → Observation → Final Answer）。");
        sb.AppendLine("Core 工具名（与官方 CLI 一致）：read_file, write_file, edit, list_directory, glob, grep_search, run_shell_command, web_fetch；MCP：serverId__toolName。");
        sb.AppendLine("用户可用 !<cmd> 直接执行 Shell；/skills、/agents 加载扩展能力。");
        if (config is not null)
        {
            var ext = BuildExtensionsBlock(config);
            if (!string.IsNullOrWhiteSpace(ext))
            {
                sb.AppendLine();
                sb.AppendLine(ext);
            }
        }

        sb.AppendLine();
        sb.AppendLine("规则：");
        sb.AppendLine("1. 不要编造 Observation；工具返回由系统注入。");
        sb.AppendLine("2. 工具名必须与下方列表完全一致（serverId__toolName）。");
        sb.AppendLine("3. 完成时输出 Final Answer: + 中文总结。");
        sb.AppendLine();
        sb.AppendLine("可用 APIs（Qwen TOOL_DESC 格式）：");
        sb.AppendLine();
        sb.AppendLine(registry.BuildQwenToolDescBlock());
        sb.AppendLine();
        sb.AppendLine(ReActFormatBlock.Replace("{tool_names}", registry.BuildToolNamesCsv()));
        sb.AppendLine();
        sb.AppendLine("也可用 XML：");
        sb.AppendLine("<tool_calling><name>serverId__tool</name><arguments>{\"k\":\"v\"}</arguments></tool_calling>");
        return sb.ToString().Trim();
    }

    public static string BuildPlannerPrompt(McpToolRegistry registry)
    {
        var sb = new StringBuilder(
            """
            你是任务规划器（Planner），只做分解、不执行工具。
            根据用户目标与下方 MCP 工具清单，输出 2–6 个可执行步骤。

            只输出一个 JSON 对象，不要 markdown 说明，格式：
            {"steps":[{"id":"1","title":"简短标题","description":"本子任务要做什么","subagent":"可选命名Subagent","tool_hints":["serverId__toolName"]}]}

            subagent 可选，须来自下方 Subagents 列表。tool_hints 可选。复杂任务把依赖少的步骤放前面。
            """);

        sb.AppendLine();
        sb.AppendLine("已注册 MCP 工具：");
        sb.AppendLine(registry.BuildCompactList());
        return sb.ToString().Trim();
    }

    public static string BuildPlannerPrompt(McpToolRegistry registry, AppConfig config) =>
        BuildPlannerPrompt(registry) + "\n\n" + BuildExtensionsBlock(config);

    public static string BuildSubAgentPrompt(
        string parentTask,
        AgentPlanStep step,
        McpToolRegistry registry,
        IReadOnlyList<string> priorSummaries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是子 Agent，只完成当前计划步骤，完成后给出 Final Answer。");
        sb.AppendLine();
        sb.AppendLine("总任务: " + parentTask);
        sb.AppendLine($"当前步骤 [{step.Id}] {step.Title}: {step.Description}");
        if (priorSummaries.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("已完成步骤摘要：");
            foreach (var s in priorSummaries)
                sb.AppendLine("- " + s);
        }

        sb.AppendLine();
        sb.Append(BuildReactAgentPrompt(registry, isCoordinator: false));
        return sb.ToString().Trim();
    }

    public static string BuildSynthesisPrompt(string parentTask, IReadOnlyList<string> stepSummaries)
    {
        var sb = new StringBuilder(
            """
            你是协调者，各子 Agent 已完成计划步骤。请根据步骤摘要回答用户总任务。
            不要调用工具。输出 Final Answer: 开头的中文总结。
            """);

        sb.AppendLine();
        sb.AppendLine("总任务: " + parentTask);
        sb.AppendLine();
        sb.AppendLine("步骤结果：");
        foreach (var s in stepSummaries)
            sb.AppendLine("- " + s);
        return sb.ToString().Trim();
    }
}

public sealed class AgentToolDescriptor
{
    public required string ExposedName { get; init; }
    public required string Description { get; init; }
    public required string ParametersJson { get; init; }
}
