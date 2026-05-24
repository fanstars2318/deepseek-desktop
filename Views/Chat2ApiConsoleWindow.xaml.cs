using System.IO;
using System.Text.Json;
using System.Windows;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;
using Microsoft.Web.WebView2.Core;

namespace DeepSeekBrowser.Views;

public partial class Chat2ApiConsoleWindow : Window
{
    private readonly LocalOpenAiServer _localApi;
    private readonly WebInjectService _web;
    private Chat2ApiIpcBridge? _ipc;
    private bool _initialized;
    private bool _pageReady;
    private TaskCompletionSource<bool>? _pageReadyTcs;

    public Chat2ApiConsoleWindow(LocalOpenAiServer localApi, WebInjectService web)
    {
        InitializeComponent();
        _localApi = localApi;
        _web = web;
        _ipc = new Chat2ApiIpcBridge(localApi, web, () => this);
        SetLoadingVisible(true);
        SetContentVisible(false);
    }

    public bool IsWebViewReady => _initialized;

    public bool IsPageReady => _pageReady;

    public void SetLoadingVisible(bool visible) =>
        LoadingOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    public void SetContentVisible(bool visible) =>
        WebViewHost.Opacity = visible ? 1 : 0;

    public Task WaitForPageReadyAsync(TimeSpan timeout)
    {
        if (_pageReady) return Task.CompletedTask;
        _pageReadyTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _pageReadyTcs.Task.WaitAsync(timeout);
    }

    public async Task EnsureInitializedAsync(CoreWebView2Environment env)
    {
        if (_initialized) return;

        await ConsoleWebView.EnsureCoreWebView2Async(env);
        var core = ConsoleWebView.CoreWebView2!;
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.AreDevToolsEnabled = true;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = true;
        ConsoleWebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 249, 250, 251);

        var dir = Path.Combine(AppContext.BaseDirectory, "Assets", "chat2api");
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"Chat2API UI 资源缺失: {dir}，请先运行 scripts/build-chat2api-ui.ps1");

        core.SetVirtualHostNameToFolderMapping(
            "ds-chat2api.local",
            dir,
            CoreWebView2HostResourceAccessKind.Allow);

        core.WebMessageReceived += OnWebMessageReceived;
        core.NavigationCompleted += OnNavigationCompleted;
        core.Navigate(AppNavigation.Chat2ApiConsoleUrl);
        _initialized = true;
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess) return;
        var src = ConsoleWebView.CoreWebView2?.Source ?? "";
        if (!src.Contains("ds-chat2api.local", StringComparison.OrdinalIgnoreCase)) return;
        // React 首屏未就绪前保持遮罩，由 ds-ready.js 发送 consoleUiReady
        _ = FallbackRevealAsync();
    }

    private async Task FallbackRevealAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(12));
            MarkPageReady();
        }
        catch
        {
            // ignore
        }
    }

    private void MarkPageReady()
    {
        if (_pageReady) return;
        _pageReady = true;
        Dispatcher.Invoke(() =>
        {
            SetContentVisible(true);
            SetLoadingVisible(false);
        });
        _pageReadyTcs?.TrySetResult(true);
    }

    public void RefreshConfig(AppConfig config) => _ipc?.RefreshConfig(config);

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!TryParseWebMessage(e, out var msg))
            return;

        if (!msg.TryGetProperty("type", out var typeEl)) return;
        var type = typeEl.GetString();

        if (type == "consoleUiReady")
        {
            MarkPageReady();
            return;
        }

        if (type == "ipcInvoke" && _ipc is not null)
        {
            var id = msg.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var parsed) ? parsed : 0;
            var channel = msg.TryGetProperty("channel", out var chEl) ? chEl.GetString() ?? "" : "";
            var args = msg.TryGetProperty("args", out var argsEl) && argsEl.ValueKind == JsonValueKind.Array
                ? argsEl.EnumerateArray().Select(x => x.Clone()).ToArray()
                : Array.Empty<JsonElement>();

            object? result = null;
            string? error = null;
            try
            {
                result = await _ipc.InvokeAsync(channel, args);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            var payload = JsonSerializer.Serialize(new
            {
                type = "ipcResult",
                id,
                result,
                error
            });
            ConsoleWebView.CoreWebView2?.PostWebMessageAsJson(payload);
            return;
        }

        if (type == "trayOpenDashboard")
        {
            Activate();
            return;
        }

        if (type == "trayQuitApp")
            Application.Current.Shutdown();
    }

    private static bool TryParseWebMessage(CoreWebView2WebMessageReceivedEventArgs e, out JsonElement msg)
    {
        msg = default;
        try
        {
            var json = e.TryGetWebMessageAsString();
            if (string.IsNullOrWhiteSpace(json))
                json = e.WebMessageAsJson;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                var inner = root.GetString();
                if (string.IsNullOrWhiteSpace(inner))
                    return false;
                using var innerDoc = JsonDocument.Parse(inner);
                root = innerDoc.RootElement;
            }

            if (root.ValueKind != JsonValueKind.Object)
                return false;

            msg = root.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (ConsoleWebView.CoreWebView2 is not null)
        {
            ConsoleWebView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            ConsoleWebView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
        }
        base.OnClosed(e);
    }
}
