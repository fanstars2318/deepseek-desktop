using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.ApiManagement;

/// <summary>与 DSD API-main src/main/providers/builtin 对齐的内置供应商元数据（桌面端展示与添加向导）。</summary>
public static class BuiltinProviderCatalog
{
    public sealed record BuiltinMeta(
        string Id,
        string Name,
        string DescriptionZh,
        string AuthType,
        string ApiEndpoint,
        string[] Models,
        object[] CredentialFields);

    public static IReadOnlyList<BuiltinMeta> All { get; } =
    [
        new("deepseek", "DeepSeek",
            "DeepSeek 智能对话助手，支持深度思考和联网搜索",
            "userToken", "https://chat.deepseek.com/api",
            ["deepseek-v4-pro", "deepseek-v4-pro-think", "deepseek-v4-pro-search",
                "deepseek-chat", "deepseek-reasoner", "DeepSeek-V3.2", "DeepSeek-R1", "DeepSeek-R1-Search"],
            [Cred("token", "用户 Token", "从 DeepSeek 网页版获取", "浏览器开发者工具 Application → Local Storage → userToken")]),
        new("glm", "GLM",
            "智谱清言 AI 助手，支持 GLM-5 旗舰模型、深度思考和联网搜索",
            "refresh_token", "https://chatglm.cn/api",
            ["GLM-5", "GLM-5-Flash", "GLM-4-Plus", "GLM-4-Flash", "GLM-Zero-Preview", "GLM-DeepResearch"],
            [Cred("refresh_token", "Refresh Token", "从浏览器 Local Storage 获取 chatglm_refresh_token", null)]),
        new("kimi", "Kimi",
            "Kimi AI 助手，支持长文本处理和联网搜索",
            "jwt", "https://www.kimi.com",
            ["Kimi-K2.6", "Kimi-K2.5"],
            [Cred("token", "Access Token", "从 Kimi 网页版获取", null)]),
        new("minimax", "MiniMax",
            "MiniMax 智能体，支持多模态与工具调用",
            "jwt", "https://agent.minimaxi.com",
            ["MiniMax-M2.5"],
            [Cred("token", "Token", "从 MiniMax Agent 获取", null),
                Cred("realUserID", "Real User ID", "可选", null, required: false)]),
        new("qwen", "通义千问",
            "通义千问网页版，支持多模型",
            "tongyi_sso_ticket", "https://qianwen.biz.aliyun.com/api",
            ["qwen-turbo", "qwen-plus", "qwen-max"],
            [Cred("ticket", "SSO Ticket", "从通义网页获取", null)]),
        new("qwen-ai", "Qwen AI",
            "Qwen AI 国际版",
            "cookie", "https://chat.qwen.ai",
            ["qwen-max", "qwen-plus"],
            [Cred("cookie", "Cookie", "从 chat.qwen.ai 获取", null)]),
        new("perplexity", "Perplexity",
            "Perplexity AI 搜索助手，支持多模型和联网搜索增强",
            "cookie", "https://www.perplexity.ai",
            ["Auto", "Turbo", "PPLX-Pro", "GPT-5", "Gemini-2.5-Pro", "Claude-Sonnet-4", "Claude-Opus-4", "Nemotron"],
            [Cred("sessionToken", "Session Token", "从 Perplexity Cookie 获取", null)]),
        new("mimo", "MiMo",
            "小米 MiMo 大模型",
            "cookie", "https://aistudio.xiaomimimo.com",
            ["MiMo-V2.5-Pro", "MiMo-V2.5", "MiMo-V2-Flash"],
            [Cred("service_token", "Service Token", "从 Cookie serviceToken 获取", null)]),
        new("zai", "Z.ai",
            "Z.ai — GLM 系列免费对话",
            "jwt", "https://chat.z.ai/api",
            ["GLM-5-Turbo", "glm-5", "glm-4.7", "glm-4.6", "glm-4.5-air"],
            [Cred("token", "JWT Token", "从 Z.ai 网页获取", null)])
    ];

    public static BuiltinMeta? Find(string id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    public static object[] ToUiBuiltinList(AppConfig config)
    {
        var deepseekModels = DsdOpenAiCompat.ListModelIds(config).ToArray();
        return All.Select(p =>
        {
            var models = p.Id == "deepseek" && deepseekModels.Length > 0 ? deepseekModels : p.Models;
            return new
            {
                id = p.Id,
                name = p.Name,
                type = "builtin",
                authType = p.AuthType,
                apiEndpoint = p.ApiEndpoint,
                chatPath = "",
                headers = new Dictionary<string, string>(),
                enabled = true,
                description = p.DescriptionZh,
                supportedModels = models,
                credentialFields = p.CredentialFields,
                tokenCheckEndpoint = "",
                tokenCheckMethod = "GET"
            };
        }).Cast<object>().ToArray();
    }

    private static object Cred(string name, string label, string placeholder, string? help, bool required = true) =>
        new
        {
            name,
            label,
            type = "password",
            required,
            placeholder,
            helpText = help ?? ""
        };
}
