using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;
using DeepSeekBrowser.Services.DeepSeekTui;

namespace DeepSeek.Desktop.Services;

public static class DesktopEmbeddedStack
{
    public static async Task EnsureLinkedAsync(
        AppConfig config,
        DeepSeekTuiHost? tuiHost,
        WinUiWebInjectService web,
        CancellationToken ct = default)
    {
        Chat2ApiCompat.EnsureDefaultMappings(config);

        await EmbeddedStackBridgeLinker.LinkWebBridgeAsync(config, web, ct)
            .ConfigureAwait(false);

        if (tuiHost is null) return;
        try
        {
            await DeepSeekTuiBundle.EnsureBinariesAsync(config, ct).ConfigureAwait(false);
            await tuiHost.EnsureRunningAsync(config, ct).ConfigureAwait(false);
        }
        catch
        {
            // retry on first agent message
        }
    }
}
