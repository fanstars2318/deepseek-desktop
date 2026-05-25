using System.IO;
using System.Text.Json;
using System.Windows;
using DeepSeekBrowser.Services;
using DeepSeekBrowser.Services.Dd;
using Microsoft.Web.WebView2.Core;

namespace DeepSeekBrowser.DdBridge;

public partial class BridgeHostWindow : Window
{
    private static readonly string UserDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeepSeek",
        "WebView2",
        "DdBridge");

    private DdBridgeWebHost? _webHost;
    private WebChatBridgeHost? _apiBridgeHost;
    private LocalOpenAiServer? _localApi;
    private DesktopAgentHost? _agentHost;
    private DdDesktopIpc? _ipc;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(UserDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: UserDataFolder);

            await ChatWebView.EnsureCoreWebView2Async(env);
            _ipc = await BridgeStartupContext.WaitForIpcAsync();
            _ipc.StartReading();
            _apiBridgeHost = new WebChatBridgeHost(BridgeWebView);
            await _apiBridgeHost.AttachAndNavigateAsync(env);

            _webHost = new DdBridgeWebHost(ChatWebView, _ipc);
            _webHost.AttachApiBridge(_apiBridgeHost);
            _localApi = new LocalOpenAiServer(_webHost.Chat);
            _agentHost = new DesktopAgentHost(_webHost, _localApi);
            _agentHost.SetOwner(this);
            _agentHost.Start();
            ShutdownCoordinator.Register(_agentHost, _localApi);

            var savedConfig = ConfigStore.Load();
            if (!string.IsNullOrWhiteSpace(savedConfig.WebUserToken))
                await _apiBridgeHost.SyncWebUserTokenAsync(savedConfig.WebUserToken);

            var config = ConfigStore.Load();
            await _webHost.InitializeAsync(env, config.DefaultWorkMode);

            _ipc.LineReceived += OnIpcEnvelope;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "DeepSeek Bridge 启动失败：\n" + ex.Message,
                "DeepSeek",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Application.Current.Shutdown(1);
        }
    }

    private void OnIpcEnvelope(JsonElement envelope)
    {
        if (envelope.TryGetProperty("channel", out var ch) &&
            ch.GetString() == "control" &&
            envelope.TryGetProperty("payload", out var payload) &&
            payload.TryGetProperty("type", out var typeEl) &&
            typeEl.GetString() == "ddReady")
        {
            _webHost?.MarkDdReady();
            _ = OnDdClientReadyAsync();
        }
    }

    /// <summary>DD Qt 壳连接后预推送 workMode / 登录态（等价于 Agent 页 nativeReady 回声）。</summary>
    private async Task OnDdClientReadyAsync()
    {
        if (_webHost is null || _agentHost is null) return;
        try
        {
            await _webHost.WorkMode.BroadcastImmediateAsync();
            await _agentHost.RefreshLoginStateAsync();
        }
        catch
        {
            // page may not be ready yet; agent-app.js will send nativeReady again
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        ShutdownCoordinator.RunExitCleanup();
        if (_ipc is not null)
            _ = _ipc.DisposeAsync();
    }
}
