using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DeepSeekBrowser.Services;
using DeepSeekBrowser.Views;
using Microsoft.Web.WebView2.Core;

namespace DeepSeekBrowser;

public partial class MainWindow : System.Windows.Window
{
    public const string DeepSeekUrl = AppNavigation.DeepSeekUrl;
    public const string AgentPageUrl = AppNavigation.AgentPageUrl;
    private static readonly string UserDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeepSeekEdge",
        "User Data");

    private bool _webViewReady;
    private bool _isExiting;
    private bool _exitCleanupDone;
    private WebInjectService? _inject;
    private LocalOpenAiServer? _localApi;
    private DesktopAgentHost? _agentHost;
    private TrayIconService? _tray;
    private CancellationTokenSource? _injectBurstCts;

    public MainWindow()
    {
        InitializeComponent();
        _tray = new TrayIconService(this, ExitApplication);
    }

    private void ApplyWindowIconInternal()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "deepseek.ico"),
            Path.Combine(AppContext.BaseDirectory, "deepseek.ico")
        };

        foreach (var iconPath in candidates)
        {
            if (!File.Exists(iconPath)) continue;
            try
            {
                using var stream = File.OpenRead(iconPath);
                Icon = BitmapFrame.Create(stream);
                return;
            }
            catch
            {
                // try next path
            }
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyWindowIconInternal();

        try
        {
            Directory.CreateDirectory(UserDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: UserDataFolder);

            await WebView.EnsureCoreWebView2Async(env);

            var core = WebView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = true;

            _inject = new WebInjectService(WebView);
            _localApi = new LocalOpenAiServer(_inject);
            _agentHost = new DesktopAgentHost(_inject, _localApi);
            _agentHost.SetOwner(this);
            _agentHost.NavigateToUrl = NavigateWebAsync;
            _agentHost.Start();

            await _inject.AttachAsync(core);

            core.NewWindowRequested += OnNewWindowRequested;
            core.NavigationStarting += OnNavigationStarting;
            core.NavigationCompleted += OnNavigationCompleted;
            core.HistoryChanged += OnSpaNavigation;
            core.SourceChanged += OnSpaNavigation;
            core.DocumentTitleChanged += OnDocumentTitleChanged;

            _webViewReady = true;
            LoadingOverlay.Visibility = Visibility.Collapsed;
            var config = ConfigStore.Load();
            var startUrl = config.DefaultWorkMode is "agent" or "plan"
                ? AgentPageUrl
                : DeepSeekUrl;
            core.Navigate(startUrl);
        }
        catch (Exception ex)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            System.Windows.MessageBox.Show(
                $"无法初始化 Edge 内核 (WebView2)。\n\n{ex.Message}\n\n请安装 Microsoft Edge WebView2 运行时：\nhttps://developer.microsoft.com/microsoft-edge/webview2/",
                "DeepSeek",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            _isExiting = true;
            Close();
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExiting)
        {
            e.Cancel = false;
            PerformExitCleanup();
            return;
        }

        e.Cancel = true;

        var dlg = new ClosePromptWindow { Owner = this };
        if (dlg.ShowDialog() != true)
            return;

        if (dlg.Choice == ClosePromptWindow.CloseChoice.MinimizeToTray)
        {
            Hide();
            _tray?.ShowInTray();
            return;
        }

        if (dlg.Choice == ClosePromptWindow.CloseChoice.ExitProcess)
        {
            _isExiting = true;
            e.Cancel = false;
            PerformExitCleanup();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_isExiting)
        {
            PerformExitCleanup();
            System.Windows.Application.Current.Shutdown();
        }

        base.OnClosed(e);
    }

    private void PerformExitCleanup()
    {
        if (_exitCleanupDone) return;
        _exitCleanupDone = true;

        _injectBurstCts?.Cancel();
        _injectBurstCts?.Dispose();
        _injectBurstCts = null;

        _tray?.Dispose();
        _tray = null;

        _localApi?.Dispose();
        _localApi = null;

        var host = _agentHost;
        _agentHost = null;
        if (host is not null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await host.DisposeAsync().AsTask().WaitAsync(cts.Token);
                }
                catch
                {
                    // MCP 断开超时不阻止退出
                }
            });
        }
    }

    private void ExitApplication()
    {
        if (_isExiting) return;
        _isExiting = true;
        PerformExitCleanup();
        Close();
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        OpenExternal(e.Uri);
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        _injectBurstCts?.Cancel();
        if (e.Uri.Contains("chat.deepseek.com", StringComparison.OrdinalIgnoreCase))
            LoadingOverlay.Visibility = Visibility.Visible;
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        LoadingOverlay.Visibility = Visibility.Collapsed;

        if (!e.IsSuccess && WebView.CoreWebView2 != null)
        {
            Title = "DeepSeek - 加载失败";
            return;
        }

        if (_inject is not null && !_inject.IsAgentHostPage)
        {
            if (_agentHost is not null)
                await _agentHost.OnChatNavigationCompletedAsync();
            ScheduleInjectBurst(forceReset: false);
        }
        else if (_agentHost is not null)
            await _agentHost.OnAgentNavigationCompletedAsync();
    }

    private async Task NavigateWebAsync(string url)
    {
        if (!_webViewReady || WebView.CoreWebView2 is null) return;
        var current = WebView.CoreWebView2.Source ?? "";
        if (AppNavigation.IsAgentPage(url) && AppNavigation.IsAgentPage(current))
            return;
        if (url.StartsWith("https://chat.deepseek.com", StringComparison.OrdinalIgnoreCase) &&
            current.StartsWith("https://chat.deepseek.com", StringComparison.OrdinalIgnoreCase))
            return;

        if (AppNavigation.IsAgentPage(url)
            && current.Contains("chat.deepseek.com", StringComparison.OrdinalIgnoreCase)
            && _agentHost is not null)
        {
            await NavigateToAgentAfterTokenSyncAsync(url);
            return;
        }

        WebView.CoreWebView2.Navigate(url);
    }

    private async Task NavigateToAgentAfterTokenSyncAsync(string url)
    {
        if (_agentHost is not null)
            await _agentHost.SyncTokenFromChatPageAsync();
        if (WebView.CoreWebView2 is null) return;
        WebView.CoreWebView2.Navigate(url);
    }

    private void OnSpaNavigation(object? sender, object e)
    {
        if (_inject is not null && _inject.IsAgentHostPage)
            return;
        ScheduleInjectBurst(forceReset: false);
    }

    private void ScheduleInjectBurst(bool forceReset)
    {
        if (_inject is null || !_webViewReady) return;

        _injectBurstCts?.Cancel();
        _injectBurstCts?.Dispose();
        _injectBurstCts = new CancellationTokenSource();
        var ct = _injectBurstCts.Token;
        var reset = forceReset;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(120, ct);
                await Dispatcher.InvokeAsync(async () =>
                {
                    if (_inject is not null && !ct.IsCancellationRequested)
                        await _inject.BurstInjectAsync(ct, reset);
                });
            }
            catch (OperationCanceledException)
            {
                // superseded by a newer navigation
            }
        }, ct);
    }

    private void OnDocumentTitleChanged(object? sender, object e)
    {
        var title = WebView.CoreWebView2?.DocumentTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            Title = "DeepSeek";
        else if (title.Contains("Agent", StringComparison.OrdinalIgnoreCase))
            Title = title;
        else if (title.StartsWith("DeepSeek", StringComparison.OrdinalIgnoreCase))
            Title = title;
        else
            Title = $"DeepSeek - {title}";
    }

    private static void OpenExternal(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // ignore
        }
    }

    private CoreWebView2? Core => _webViewReady ? WebView.CoreWebView2 : null;

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F5 || (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control))
        {
            Core?.Reload();
            e.Handled = true;
        }
        else if (e.Key == Key.F12)
        {
            Core?.OpenDevToolsWindow();
            e.Handled = true;
        }
        else if (e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            if (Core?.CanGoBack == true) Core.GoBack();
            e.Handled = true;
        }
        else if (e.Key == Key.Right && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            if (Core?.CanGoForward == true) Core.GoForward();
            e.Handled = true;
        }

        base.OnPreviewKeyDown(e);
    }
}
