using System.Text.Json.Serialization;

namespace DeepSeekBrowser.Models;

public sealed class AppConfig
{
    public string DeepSeekApiKey { get; set; } = "";
    public string WebUserToken { get; set; } = "";
    public string Model { get; set; } = "deepseek-v4-pro";
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
    /// <summary>chat = 网页对话；agent / plan = 进程内 Harness + Chat2API</summary>
    public string DefaultWorkMode { get; set; } = "chat";
    /// <summary>blueprint = Explore→Blueprint；execute = Execute 单阶段。plan/react 为兼容别名。</summary>
    public string DefaultAgentStrategy { get; set; } = "execute";

    /// <summary>true：首次 run_shell 时再初始化本地沙盒；false：任务开始前初始化。</summary>
    public bool AgentSandboxLazyInit { get; set; } = true;

    /// <summary>Agent：Chat2API 深度思考；默认关，由 UI 或用户显式开启，不由客户端预判消息类型。</summary>
    public bool AgentDeepThinking { get; set; }

    /// <summary>Agent：Chat2API 联网搜索；默认关，由 UI 或用户显式开启。</summary>
    public bool AgentWebSearch { get; set; }

    /// <summary>Agent 运行时写入调试日志（%LocalAppData%\deepseek_desktop\logs）。</summary>
    public bool AgentDebugLogEnabled { get; set; } = true;

    /// <summary>Agent 调试日志是否弹出 CMD 窗口实时 tail。</summary>
    public bool AgentDebugLogConsole { get; set; } = true;
    public bool EnableSubAgents { get; set; } = true;

    /// <summary>启用 MetaGPT 式 Team SOP（/team）。</summary>
    public bool EnableTeamWorkflow { get; set; } = true;

    /// <summary>启用并行 Explore 扇出（/parallel-explore、parallel_explore 工具）。</summary>
    public bool EnableParallelExplore { get; set; } = true;

    /// <summary>启用 CAMEL 式双 Agent 辩论（/debate）。</summary>
    public bool EnableDebateWorkflow { get; set; } = true;

    /// <summary>并行 Explore 汇总后由模型选择下一子 Agent 角色（AutoGen 式）。</summary>
    public bool EnableDynamicGroupChat { get; set; }

    public int MaxSubAgentSteps { get; set; } = 10;

    /// <summary>并行子 Agent 上限（AutoGen-style 并发委派）。</summary>
    public int MaxConcurrentSubAgents { get; set; } = 3;

    /// <summary>并行 Explore 扇出数量（≤ MaxConcurrentSubAgents）。</summary>
    public int ParallelExploreFanOut { get; set; } = 3;

    /// <summary>辩论模式 Advocate↔Critic 往返轮数。</summary>
    public int DebateMaxRounds { get; set; } = 3;

    /// <summary>Run 前自动分析需求、匹配 Skill、规划并校验工具（无需用户指定）。</summary>
    public bool AgentAutoIntentRouting { get; set; } = true;

    /// <summary>意图分析使用模型 JSON 规划（关闭则仅启发式，省 1 次 LLM 调用）。</summary>
    public bool AgentIntentUseLlmPlanner { get; set; }

    /// <summary>同会话复用已缓存的意图规划（后续轮次跳过 LLM 规划调用）。</summary>
    public bool AgentIntentCacheEnabled { get; set; } = true;

    /// <summary>精简系统提示：有意图规划时跳过 MCP/内置工具长目录（OpenAI schema 或意图段已足够）。</summary>
    public bool AgentPromptMinimalMode { get; set; } = true;

    /// <summary>OpenAI 请求中 MCP 工具 definition 数量上限（意图裁剪后）。</summary>
    public int AgentMcpToolsMaxInRequest { get; set; } = 8;

    /// <summary>MCP 工具目录注入系统提示的最大行数。</summary>
    public int AgentMcpCatalogMaxLines { get; set; } = 16;

    /// <summary>Skill 正文注入字符上限。</summary>
    public int AgentSkillMaxChars { get; set; } = 3000;

    /// <summary>工作区顶层快照最大条目数。</summary>
    public int AgentWorkspaceSnapshotMaxEntries { get; set; } = 30;

    /// <summary>工作区根目录；Harness 内置工具限制在此目录下。</summary>
    [JsonPropertyName("qwenCodeWorkspaceRoot")]
    public string AgentWorkspaceRoot { get; set; } = "";

    /// <summary>审批模式：smart | readonly | always | never（同步到 ~/.deepseek/config.toml）。</summary>
    [JsonPropertyName("qwenCodeApprovalMode")]
    public string AgentApprovalMode { get; set; } = "smart";

    [JsonPropertyName("qwenCodeAutoApproveReadOnly")]
    public bool AgentAutoApproveReadOnly { get; set; } = true;

    [JsonPropertyName("qwenCodeAllowShell")]
    public bool AgentAllowShell { get; set; } = true;

    /// <summary>Execute 完成后自动运行 Verify 验收命令（可被 Playbook 覆盖）。</summary>
    public bool AgentVerifyAfterExecute { get; set; }

    /// <summary>默认 Verify 命令，如 dotnet test。</summary>
    public string AgentVerifyCommand { get; set; } = "";

    public int AgentVerifyTimeoutSeconds { get; set; } = 120;

    /// <summary>Verify 失败时不阻断任务（仅附加警告）。</summary>
    public bool AgentVerifyOptional { get; set; }

    /// <summary>连接 MCP 时合并 ~/.cursor/mcp.json、Claude Desktop 等市场配置（默认开启）。</summary>
    public bool AgentImportMarketMcp { get; set; } = true;

    /// <summary>工具输出超过阈值时落盘到 .deepseek/runs/ 并注入摘要。</summary>
    public bool AgentToolOutputSpill { get; set; } = true;

    public int AgentToolOutputInlineMaxChars { get; set; } = 3000;

    /// <summary>任务完成后写入 .deepseek/runs/&lt;runId&gt;/postmortem.md</summary>
    public bool AgentWritePostMortem { get; set; } = true;

    /// <summary>多步 Verify（Execute 完成后按序执行；非空时优先于 AgentVerifyCommand）。</summary>
    public List<string> AgentVerifyCommands { get; set; } = new();

    /// <summary>网页对话输出截断时自动续写。</summary>
    public bool EnableAdaptiveOutputEscalation { get; set; } = true;

    /// <summary>Agent 对话保留天数，0 = 不按时间自动删除。</summary>
    public int AgentSessionRetentionDays { get; set; } = 30;

    /// <summary>Agent 对话本地占用上限（GB），超出则删除最久未更新的对话；0 = 不限制。</summary>
    public double AgentSessionMaxStorageGb { get; set; } = 2;

    /// <summary>启动与保存后是否按上述规则自动清理。</summary>
    public bool AgentSessionAutoCleanup { get; set; } = true;

    /// <summary>Agent 自动化 Webhook 监听端口（仅 127.0.0.1）。</summary>
    public int AgentAutomationsWebhookPort { get; set; } = 17426;

    /// <summary>Agent 推理通道：web（网页桥接，默认）| api（直连 OpenAI 兼容 API）。</summary>
    public string AgentInferenceMode { get; set; } = "web";

    /// <summary>工具调用协议：xml（Harness XML）| openai（function calling）。API 模式默认 openai。</summary>
    public string AgentToolCallingProtocol { get; set; } = "xml";

    /// <summary>直连 API 专用 Key；为空时使用 DeepSeekApiKey。</summary>
    public string AgentApiKey { get; set; } = "";

    /// <summary>直连 API Base URL；为空时使用 ApiBaseUrl。</summary>
    public string AgentApiBaseUrl { get; set; } = "";

    /// <summary>推理强度：high | max（直连 API / Chat2API）。</summary>
    public string AgentReasoningEffort { get; set; } = "max";

    /// <summary>思考过程展示：normal | lite | raw。</summary>
    public string AgentThinkingDisplayMode { get; set; } = "normal";

    /// <summary>上下文压缩 token 阈值；0 = 禁用。</summary>
    public int AgentContextCompactTokenThreshold { get; set; } = 40 * 1024;

    /// <summary>任务完成后调用的 notify 脚本路径。</summary>
    public string AgentNotifyScript { get; set; } = "";

    /// <summary>可选自定义 web search 脚本（API 模式 web_search 工具）。</summary>
    public string AgentWebSearchScript { get; set; } = "";

    /// <summary>Execute 模式会话内 markdown 计划（UpdatePlan 工具写入）。</summary>
    public string AgentSessionPlanMarkdown { get; set; } = "";

    /// <summary>image_analyze 使用的 vision 模型名。</summary>
    public string AgentVisionModel { get; set; } = "gpt-4o-mini";

    /// <summary>Vision API Base URL；空则使用 Agent API Base。</summary>
    public string AgentVisionApiBaseUrl { get; set; } = "";

    /// <summary>Vision API Key；空则使用 Agent API Key。</summary>
    public string AgentVisionApiKey { get; set; } = "";

    /// <summary>沙盒 bash 默认超时（毫秒）；默认 10 分钟。</summary>
    public int AgentBashTimeoutMs { get; set; } = 600_000;

    /// <summary>沙盒 bash 最小可配置超时（毫秒）；默认 1 分钟。</summary>
    public int AgentBashMinTimeoutMs { get; set; } = 60_000;

    /// <summary>按 scope 的权限策略 JSON（read/write/bash/mcp/network → allow|ask|deny）。</summary>
    public string AgentPermissionScopesJson { get; set; } = "";

    /// <summary>结构化 Run Trace（Langfuse 本地子集）；写入 .deepseek/runs/{runId}/trace.jsonl。</summary>
    public bool AgentStructuredTraceEnabled { get; set; } = true;

    /// <summary>Run 结束后导出 trace 到 Langfuse Cloud。</summary>
    public bool AgentLangfuseEnabled { get; set; }

    public string AgentLangfuseHost { get; set; } = "https://cloud.langfuse.com";

    public string AgentLangfusePublicKey { get; set; } = "";

    public string AgentLangfuseSecretKey { get; set; } = "";

    public string AgentLangfuseProject { get; set; } = "";

    /// <summary>Trace 保留天数；0 = 不自动清理。</summary>
    public int AgentTraceRetentionDays { get; set; } = 30;

    /// <summary>启用 SQLite 语义记忆检索（Mem0 本地子集）。</summary>
    public bool AgentSemanticMemoryEnabled { get; set; } = true;

    /// <summary>任务完成后 LLM 自动抽取记忆（默认关）。</summary>
    public bool AgentSemanticMemoryAutoExtract { get; set; }

    /// <summary>语义记忆 top-K 条数。</summary>
    public int AgentSemanticMemoryTopK { get; set; } = 4;

    /// <summary>session:* 记忆保留天数；0 = 不自动清理。</summary>
    public int AgentSemanticMemorySessionTtlDays { get; set; } = 7;

    /// <summary>语义记忆注入字符预算。</summary>
    public int AgentSemanticMemoryMaxChars { get; set; } = 1200;

    /// <summary>Embedding 模型名。</summary>
    public string AgentEmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>Embedding API Base URL；空则同 Agent API。</summary>
    public string AgentEmbeddingApiBaseUrl { get; set; } = "";

    /// <summary>Embedding API Key；空则同 Agent API Key。</summary>
    public string AgentEmbeddingApiKey { get; set; } = "";

    /// <summary>额外 Skill 扫描根（本地 *-main 解压目录，见 scripts/setup-skill-catalog.ps1）。</summary>
    public List<string> AgentSkillExtraRoots { get; set; } = new()
    {
        @"C:\Users\xiaow\Desktop\DSD\antigravity-awesome-skills-main",
        @"C:\Users\xiaow\Desktop\DSD\awesome-claude-skills-master"
    };

    public List<McpServerConfig> McpServers { get; set; } = new()
    {
        new()
        {
            Name = "本地文件系统",
            Arguments = ["-y", "@modelcontextprotocol/server-filesystem", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)]
        }
    };
}
