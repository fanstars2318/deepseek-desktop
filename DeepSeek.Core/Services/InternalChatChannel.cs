using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services;

/// <summary>
/// 桌面端模块间 LLM 通道标识（进程内直连网页桥，不经 HTTP 端口）。
/// DeepSeek-TUI 子进程的进程内 IPC 为后续工作；当前仅在有官方 API Key 时写入 HTTP base_url。
/// </summary>
public static class InternalChatChannel
{
    public const string DesktopV1 = "internal://desktop/v1";

    /// <summary>可选外部 OpenAI 兼容 API 的默认端口（仅在 <see cref="AppConfig.EnableExternalOpenAiApi"/> 且未配置端口时使用）。</summary>
    public const int ExternalApiDefaultPort = 17425;

    public static bool IsInternal(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        url.StartsWith("internal://", StringComparison.OrdinalIgnoreCase);

    public static int ResolveExternalApiPort(AppConfig config)
    {
        if (config.LocalApiPort > 0)
            return config.LocalApiPort;
        return ExternalApiDefaultPort;
    }

    public static string GetExternalApiBaseUrl(AppConfig config) =>
        $"http://127.0.0.1:{ResolveExternalApiPort(config)}/v1";

    public static string GetAgentScopedLlmBaseUrl(AppConfig config) =>
        GetExternalApiBaseUrl(config);

    /// <summary>写入 ~/.deepseek/config.toml 的 base_url；网页会话仅进程内可用时不写 HTTP 地址。</summary>
    public static string? ResolveTuiLlmBaseUrl(AppConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.DeepSeekApiKey))
        {
            var root = (config.ApiBaseUrl ?? "https://api.deepseek.com").Trim().TrimEnd('/');
            return root.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? root : root + "/v1";
        }

        if (DsdAgentApiScope.HasActiveAgentRun)
            return GetAgentScopedLlmBaseUrl(config);

        return null;
    }
}
