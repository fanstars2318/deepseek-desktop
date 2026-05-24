using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.DeepSeekTui;

namespace DeepSeekBrowser.Services;

/// <summary>网页登录后：启动内嵌栈并预热 DeepSeek-TUI。</summary>
public static class Chat2ApiStackBootstrap
{
    public static Task OnWebLoginAsync(
        AppConfig config,
        LocalOpenAiServer localApi,
        DeepSeekTuiHost? tuiHost,
        WebInjectService web,
        CancellationToken ct = default) =>
        EmbeddedStackCoordinator.EnsureLinkedAsync(config, localApi, tuiHost, web, ct);
}
