namespace DeepSeekBrowser.Models;

public sealed class AppConfig
{
    public string DeepSeekApiKey { get; set; } = "";
    public string WebUserToken { get; set; } = "";
    public string Model { get; set; } = "deepseek-chat";
    public string ApiBaseUrl { get; set; } = "https://api.deepseek.com";
    public string WebApiBaseUrl { get; set; } = "https://chat.deepseek.com/api";
    public int LocalApiPort { get; set; } = 5111;
    public bool PreferWebSessionForApi { get; set; } = true;
    public int MaxAgentSteps { get; set; } = 25;
    /// <summary>chat = 网页对话；agent / plan = 本地 Chat2API + MCP 智能体</summary>
    public string DefaultWorkMode { get; set; } = "chat";
    /// <summary>react = 单 Agent ReAct；plan = 规划 + 子 Agent（Qwen react_demo 风格）</summary>
    public string DefaultAgentStrategy { get; set; } = "react";
    public bool EnableSubAgents { get; set; } = true;
    public int MaxSubAgentSteps { get; set; } = 10;

    /// <summary>发现并使用 .qwen/skills 与 bundled Skills（官方 Skills）。</summary>
    public bool EnableQwenSkills { get; set; } = true;

    /// <summary>包含 npm @qwen-code/qwen-code/bundled 内置 Skills（review、qc-helper 等）。</summary>
    public bool EnableQwenBundledSkills { get; set; } = true;

    /// <summary>发现 .qwen/agents 命名 Subagent 配置。</summary>
    public bool EnableQwenSubAgentConfigs { get; set; } = true;

    /// <summary>启用 Qwen Code Core 内置工具（C# 移植，见 Services/QwenCode）。</summary>
    public bool EnableQwenCodeBuiltinTools { get; set; } = true;

    /// <summary>工作区根目录；Agent 内置文件/Shell 工具限制在此目录下。</summary>
    public string QwenCodeWorkspaceRoot { get; set; } = "";

    /// <summary>审批模式：smart=只读自动、写/Shell 需确认；readonly=仅只读自动；always=全部确认；never=全部自动（不安全）。</summary>
    public string QwenCodeApprovalMode { get; set; } = "smart";

    public bool QwenCodeAutoApproveReadOnly { get; set; } = true;

    public bool QwenCodeAllowShell { get; set; } = true;

    /// <summary>允许内置 web_fetch（只读拉取 URL 文本）。</summary>
    public bool EnableQwenCodeWebFetch { get; set; } = true;

    public int QwenCodeMaxFileReadChars { get; set; } = 120_000;

    public int QwenCodeMaxShellOutputChars { get; set; } = 32_000;

    /// <summary>启用 Qwen Code 自适应输出续写（截断时多轮恢复，见官方设计文档）。</summary>
    public bool EnableAdaptiveOutputEscalation { get; set; } = true;

    /// <summary>0 = 使用默认 8K 上限；&gt;0 时作为 max_tokens 且不参与自动扩容（预留）。</summary>
    public int QwenCodeMaxOutputTokens { get; set; }

    /// <summary>Agent 对话保留天数，0 = 不按时间自动删除。</summary>
    public int AgentSessionRetentionDays { get; set; } = 30;

    /// <summary>Agent 对话本地占用上限（GB），超出则删除最久未更新的对话；0 = 不限制。</summary>
    public double AgentSessionMaxStorageGb { get; set; } = 2;

    /// <summary>启动与保存后是否按上述规则自动清理。</summary>
    public bool AgentSessionAutoCleanup { get; set; } = true;

    public List<McpServerConfig> McpServers { get; set; } = new()
    {
        new()
        {
            Name = "本地文件系统",
            Arguments = ["-y", "@modelcontextprotocol/server-filesystem", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)]
        }
    };
}
