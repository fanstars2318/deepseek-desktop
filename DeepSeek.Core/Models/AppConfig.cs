using System.Text.Json.Serialization;

namespace DeepSeekBrowser.Models;

public sealed class AppConfig
{
    public string DeepSeekApiKey { get; set; } = "";
    public string WebUserToken { get; set; } = "";
    public string Model { get; set; } = "DeepSeek-V3.2";
    public string ApiBaseUrl { get; set; } = "https://api.deepseek.com";
    public string WebApiBaseUrl { get; set; } = "https://chat.deepseek.com/api";
    /// <summary>外部 OpenAI 兼容 API 端口；0 表示使用内置默认（17425）。桌面模块间通信不经此端口。</summary>
    public int LocalApiPort { get; set; }

    /// <summary>允许本机第三方应用调用 OpenAI 兼容 HTTP API（可选，默认关闭）。</summary>
    public bool EnableExternalOpenAiApi { get; set; }

    /// <summary>启用后，外部调用本地 OpenAI 兼容 API 须携带 Bearer / X-API-Key。</summary>
    public bool EnableLocalApiKeyAuth { get; set; }

    /// <summary>本地 OpenAI 兼容 API 的访问密钥列表。</summary>
    public List<LocalApiKey> LocalApiKeys { get; set; } = new();

    /// <summary>Chat2API 会话模式：single（单轮，默认）| multi（多轮，需 session_id）。</summary>
    public string Chat2ApiSessionMode { get; set; } = "single";

    public int Chat2ApiSessionTimeoutMinutes { get; set; } = 30;

    public int Chat2ApiMaxMessagesPerSession { get; set; } = 100;

    /// <summary>模型映射（对齐 chat2api-doc 模型映射）。</summary>
    public List<ModelMappingEntry> ModelMappings { get; set; } = new();

    public bool PreferWebSessionForApi { get; set; } = true;
    public int MaxAgentSteps { get; set; } = 25;
    /// <summary>chat = 网页对话；agent / plan = DeepSeek-TUI + Chat2API</summary>
    public string DefaultWorkMode { get; set; } = "chat";
    /// <summary>react = Agent；plan = Plan（只读调研）</summary>
    public string DefaultAgentStrategy { get; set; } = "react";

    /// <summary>Agent：Chat2API <c>reasoning_effort</c> / 深度思考（默认开启）。</summary>
    public bool AgentDeepThinking { get; set; } = true;

    /// <summary>Agent：Chat2API <c>web_search</c> + TUI <c>[features].web_search</c>（默认关闭）。</summary>
    public bool AgentWebSearch { get; set; }

    /// <summary>Agent 运行时写入调试日志（%LocalAppData%\deepseek_desktop\logs）。</summary>
    public bool AgentDebugLogEnabled { get; set; } = true;

    /// <summary>Agent 调试日志是否弹出 CMD 窗口实时 tail。</summary>
    public bool AgentDebugLogConsole { get; set; } = true;
    public bool EnableSubAgents { get; set; } = true;
    public int MaxSubAgentSteps { get; set; } = 10;

    /// <summary>DeepSeek-TUI 运行时 HTTP 端口（<c>deepseek serve --http</c>）。</summary>
    public int DeepSeekTuiRuntimePort { get; set; } = 7878;

    /// <summary>可选：<c>deepseek.exe</c> 或 <c>deepseek.cmd</c> 完整路径；留空则自动探测。</summary>
    public string DeepSeekTuiExecutablePath { get; set; } = "";

    /// <summary>
    /// 本地 DeepSeek-TUI 源码根目录（含 Cargo.toml / crates/tui）。
    /// 留空则尝试 Desktop\DSD\DeepSeek-TUI-main；build.ps1 -UseLocalTui 会从该目录 cargo build。
    /// </summary>
    public string DeepSeekTuiSourcePath { get; set; } =
        @"C:\Users\xiaow\Desktop\DSD\DeepSeek-TUI-main\DeepSeek-TUI-main";

    /// <summary>DeepSeek-TUI <c>serve --http</c> 的 Bearer Token；留空则自动生成并持久化。</summary>
    public string DeepSeekTuiRuntimeToken { get; set; } = "";

    /// <summary>工作区根目录；DeepSeek-TUI 工具限制在此目录下。</summary>
    [JsonPropertyName("qwenCodeWorkspaceRoot")]
    public string AgentWorkspaceRoot { get; set; } = "";

    /// <summary>审批模式：smart | readonly | always | never（同步到 ~/.deepseek/config.toml）。</summary>
    [JsonPropertyName("qwenCodeApprovalMode")]
    public string AgentApprovalMode { get; set; } = "smart";

    [JsonPropertyName("qwenCodeAutoApproveReadOnly")]
    public bool AgentAutoApproveReadOnly { get; set; } = true;

    [JsonPropertyName("qwenCodeAllowShell")]
    public bool AgentAllowShell { get; set; } = true;

    /// <summary>网页对话输出截断时自动续写。</summary>
    public bool EnableAdaptiveOutputEscalation { get; set; } = true;

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
