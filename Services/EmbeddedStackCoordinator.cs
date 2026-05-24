using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.DeepSeekTui;

namespace DeepSeekBrowser.Services;

/// <summary>
/// 桌面内嵌栈：本地对话服务 + DeepSeek-TUI 运行时，启动后自动联通。
/// </summary>
public static class EmbeddedStackCoordinator
{
    public static async Task EnsureLinkedAsync(
        AppConfig config,
        LocalOpenAiServer localApi,
        DeepSeekTuiHost? tuiHost,
        WebInjectService web,
        CancellationToken ct = default)
    {
        Chat2ApiCompat.EnsureDefaultMappings(config);
        localApi.UpdateConfig(config);

        if (!string.IsNullOrWhiteSpace(config.WebUserToken) && config.EnableExternalOpenAiApi)
            localApi.EnsureExternalApiListening();

        await EmbeddedStackBridgeLinker.LinkWebBridgeAsync(config, web, ct)
            .ConfigureAwait(false);

        if (tuiHost is null)
            return;

        try
        {
            await DeepSeekTuiBundle.EnsureBinariesAsync(config, ct).ConfigureAwait(false);
            await tuiHost.EnsureRunningAsync(config, ct).ConfigureAwait(false);
        }
        catch
        {
            // Agent 发消息时会再次尝试
        }
    }
}
