namespace DeepSeekBrowser.Models;

public sealed class AgentPlan
{
    public List<AgentPlanStep> Steps { get; init; } = [];
}

public sealed class AgentPlanStep
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>建议使用的 MCP 工具名（serverId__toolName），可为空表示由子 Agent 自选。</summary>
    public List<string> ToolHints { get; set; } = [];

    /// <summary>可选：委派给 .qwen/agents 中的命名 Subagent。</summary>
    public string? SubAgentName { get; set; }
}
