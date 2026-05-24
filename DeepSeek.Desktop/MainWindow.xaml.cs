using DeepSeek.Desktop.Pages;
using DeepSeek.Desktop.Services;
using DeepSeekBrowser.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeepSeek.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly TrayService _tray = new();
    private Task? _initTask;
    private WebView2? _chatWebView;
    private WebView2? _bridgeWebView;

    public bool IsAgentSelected { get; private set; }

    public MainWindow()
    {
        WorkModeTrace.Write("WinUI MainWindow ctor");
        InitializeComponent();
        WorkModeTrace.Write("WinUI MainWindow ready");
        try { AppWindow.SetIcon("Assets/AppIcon.ico"); } catch { /* ignore */ }
        ShowChatSurface();
        _tray.Initialize(this);
        RootGrid.Loaded += async (_, _) => await EnsureWebViewsCreatedAsync();
    }

    private async Task EnsureWebViewsCreatedAsync()
    {
        if (_chatWebView is not null) return;

        WorkModeTrace.Write("WinUI: creating WebViews");
        _chatWebView = new WebView2();
        _bridgeWebView = new WebView2 { Width = 1, Height = 1, Opacity = 0, IsHitTestVisible = false };

        ChatHost.Children.Add(_chatWebView);
        RootGrid.Children.Add(_bridgeWebView);

        await EnsureHostInitializedAsync();
    }

    private void ChatNavButton_Click(object sender, RoutedEventArgs e) => NavigateToChat();
    private void AgentNavButton_Click(object sender, RoutedEventArgs e) => NavigateToAgent();
    private void SettingsNavButton_Click(object sender, RoutedEventArgs e) => NavigateToSettings();

    public void NavigateToChat()
    {
        IsAgentSelected = false;
        ShowChatSurface();
    }

    public void NavigateToAgent()
    {
        IsAgentSelected = true;
        ShowOverlayPage(typeof(AgentPage));
    }

    public void NavigateToSettings() => ShowOverlayPage(typeof(SettingsPage));

    private void ShowChatSurface()
    {
        ContentFrame.Visibility = Visibility.Collapsed;
        ChatHost.Visibility = Visibility.Visible;
    }

    private void ShowOverlayPage(Type pageType)
    {
        ChatHost.Visibility = Visibility.Collapsed;
        ContentFrame.Visibility = Visibility.Visible;
        if (ContentFrame.CurrentSourcePageType != pageType)
            ContentFrame.Navigate(pageType);
    }

    public Task EnsureHostInitializedAsync() =>
        _initTask ??= InitializeHostCoreAsync();

    private async Task InitializeHostCoreAsync()
    {
        if (_chatWebView is null || _bridgeWebView is null) return;

        WorkModeTrace.Write("WinUI: initializing host");
        await AppHost.Instance.InitializeAsync(_chatWebView, _bridgeWebView);
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        _chatWebView.CoreWebView2?.Navigate(AppNavigation.DeepSeekUrl);
        _ = AppHost.Instance.ChatInject?.BurstInjectAsync();
        WorkModeTrace.Write("WinUI: host ready");
    }
}
