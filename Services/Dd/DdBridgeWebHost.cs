using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DeepSeekBrowser.Services.Dd;

/// <summary>
/// DD Qt 主进程 + 命名管道：Agent/Chat 页在 Qt QWebEngine 渲染，Bridge 进程内隐藏 WebView2 仅服务 Chat API 桥。
/// </summary>
public sealed class DdBridgeWebHost : IDdWebPages
{
    private readonly WebView2 _chatView;
    private readonly DdDesktopIpc _ipc;
    private readonly DdPipePageMessenger _agentPipe;
    private readonly DdPipePageMessenger _chatPipe;

    public DdBridgeWebHost(WebView2 chatView, DdDesktopIpc ipc)
    {
        _chatView = chatView;
        _ipc = ipc;
        _agentPipe = new DdPipePageMessenger(ipc, "agent");
        _chatPipe = new DdPipePageMessenger(ipc, "chat");
        Chat = new WebInjectService(chatView, WebViewPageKind.Chat);
        WorkMode = new WorkModeCoordinator(this);
        Chat.MessageReceived += ForwardMessage;
        _agentPipe.MessageReceived += ForwardMessage;
        _chatPipe.MessageReceived += ForwardMessage;
        _ipc.LineReceived += OnIpcLine;
    }

    public WebInjectService Chat { get; }

    IDdPageMessenger IDdWebPages.Chat => Chat;

    public DdPipePageMessenger AgentPipe => _agentPipe;

    IDdPageMessenger IDdWebPages.Agent => _agentPipe;

    public WorkModeCoordinator WorkMode { get; }

    public bool IsAgentVisible { get; private set; }

    public bool AgentPageReady { get; private set; }

    public bool IsAgentHostPage => IsAgentVisible;

    public string? AgentSource => _agentPipe.Source;

    public event EventHandler<JsonElement>? MessageReceived;

    public IReadOnlyList<string> AgentRefFileIds
    {
        get => Chat.AgentRefFileIds;
        set => Chat.AgentRefFileIds = value;
    }

    public void AttachApiBridge(WebChatBridgeHost bridge) => Chat.AttachApiBridge(bridge);

    public async Task InitializeAsync(CoreWebView2Environment env, string? startWorkMode)
    {
        await _chatView.EnsureCoreWebView2Async(env);
        var chatCore = _chatView.CoreWebView2!;
        await Chat.AttachAsync(chatCore);
        chatCore.Navigate(AppNavigation.DeepSeekUrl);

        WorkMode.SetModeFromConfig(startWorkMode);
        if (WorkMode.IsAgentLike)
            await WorkMode.ShowAgentSurfaceAsync();
        else
            await WorkMode.ShowChatSurfaceAsync();
        await WorkMode.BroadcastAsync();
    }

    public void MarkDdReady()
    {
        AgentPageReady = true;
    }

    public Task PostToPageAsync(object message) =>
        (IsAgentVisible ? (IDdPageMessenger)_agentPipe : Chat).PostToPageAsync(message);

    public Task PushWorkModeStateNowAsync(WorkModeStatePayload state, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (IsAgentVisible)
        {
            var agentTask = _agentPipe.PushWorkModeStateAsync(state);
            _ = Chat.PushWorkModeStateAsync(state);
            return agentTask;
        }

        var chatTask = Chat.PushWorkModeStateAsync(state);
        _ = _agentPipe.PushWorkModeStateAsync(state);
        return chatTask;
    }

    public Task BroadcastWorkModeStateAsync(CancellationToken ct = default) =>
        BroadcastWorkModeStateAsync(includeImmediate: true, ct);

    public async Task BroadcastWorkModeStateAsync(bool includeImmediate, CancellationToken ct = default)
    {
        var delays = includeImmediate
            ? new[] { 0, 50, 150, 350, 700, 1200 }
            : new[] { 150, 500 };
        foreach (var delay in delays)
        {
            ct.ThrowIfCancellationRequested();
            if (delay > 0)
                await Task.Delay(delay, ct);
            var state = WorkMode.BuildState();
            await Chat.PushWorkModeStateAsync(state);
            await _agentPipe.PushWorkModeStateAsync(state);
        }
    }

    public void ScheduleWorkModeBroadcastRetries(CancellationToken ct = default) =>
        _ = BroadcastWorkModeStateAsync(includeImmediate: false, ct);

    public Task AfterShowChatAsync(string workMode, CancellationToken ct = default)
    {
        WorkMode.SetModeFromConfig(workMode);
        return WorkMode.ShowChatSurfaceAsync(ct);
    }

    public Task PushAgentAuthHintAsync(bool loggedIn) => _agentPipe.PushAgentAuthHintAsync(loggedIn);

    public Task SyncApiBridgeTokenAsync(string? token) => Chat.SyncApiBridgeTokenAsync(token);

    public Task EnsureApiBridgeReadyAsync(CancellationToken ct = default) =>
        Chat.EnsureApiBridgeReadyAsync(ct);

    public Task<Chat2ApiHealth?> ProbeChat2ApiHealthAsync(string? configWebUserToken, string baseUrl,
        CancellationToken ct = default) =>
        Chat.ProbeChat2ApiHealthAsync(configWebUserToken, baseUrl, ct);

    public Task<string?> TryReadUserTokenAsync() => Chat.TryReadUserTokenAsync();

    public Task<string?> GetUserTokenAsync(bool waitForBridge = true) =>
        Chat.GetUserTokenAsync(waitForBridge);

    public Task SyncChatSessionAsync(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return Task.CompletedTask;
        var url = AppNavigation.ChatSessionUrl(sessionId);
        var core = _chatView.CoreWebView2;
        if (core is null) return Task.CompletedTask;

        var current = core.Source ?? "";
        if (SameChatLocation(current, url)) return Task.CompletedTask;

        core.Navigate(url);
        return Task.CompletedTask;
    }

    public void ShowChat()
    {
        IsAgentVisible = false;
        _ = _ipc.SendEnvelopeAsync("control", new { type = "ddSurface", surface = "chat" });
        WorkModeTrace.Write("DdBridge ShowChat");
    }

    public void ShowAgent()
    {
        IsAgentVisible = true;
        _ = _ipc.SendEnvelopeAsync("control", new { type = "ddSurface", surface = "agent" });
        WorkModeTrace.Write("DdBridge ShowAgent");
    }

    public Task<WebChatResult> WebChatAsync(
        IReadOnlyList<ChatMessage> messages,
        string model,
        bool thinking,
        bool search,
        IReadOnlyList<string> refFileIds,
        bool allowToolCalls,
        CancellationToken ct,
        string? webUserToken = null,
        string? webChatSessionId = null)
    {
        var prev = Chat.AgentRefFileIds;
        Chat.AgentRefFileIds = refFileIds;
        try
        {
            return Chat.WebChatAsync(
                messages, model, thinking, search, ct, webUserToken, webChatSessionId, allowToolCalls);
        }
        finally
        {
            Chat.AgentRefFileIds = prev;
        }
    }

    public IAsyncEnumerable<WebChatStreamEvent> WebChatStreamAsync(
        IReadOnlyList<ChatMessage> messages,
        string model,
        bool thinking,
        bool search,
        IReadOnlyList<string> refFileIds,
        bool allowToolCalls,
        CancellationToken ct,
        string? webUserToken = null,
        string? webChatSessionId = null)
    {
        var prev = Chat.AgentRefFileIds;
        Chat.AgentRefFileIds = refFileIds;
        try
        {
            return Chat.WebChatStreamAsync(
                messages, model, thinking, search, ct, webUserToken, webChatSessionId, allowToolCalls);
        }
        finally
        {
            Chat.AgentRefFileIds = prev;
        }
    }

    private void OnIpcLine(JsonElement line)
    {
        if (!line.TryGetProperty("channel", out var chEl) ||
            !line.TryGetProperty("payload", out var payload))
            return;

        var channel = chEl.GetString();
        switch (channel)
        {
            case "agent":
                _agentPipe.RaiseMessage(payload);
                break;
            case "chat":
                _chatPipe.RaiseMessage(payload);
                break;
            case "control":
                if (payload.TryGetProperty("type", out var typeEl) &&
                    typeEl.GetString() == "ddReady")
                    AgentPageReady = true;
                break;
        }
    }

    private void ForwardMessage(object? sender, JsonElement e) =>
        MessageReceived?.Invoke(this, e);

    private static bool SameChatLocation(string current, string target)
    {
        if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(target))
            return false;

        try
        {
            var c = new Uri(current);
            var t = new Uri(target);
            if (!c.Host.Equals(t.Host, StringComparison.OrdinalIgnoreCase))
                return false;

            var cp = c.AbsolutePath.TrimEnd('/');
            var tp = t.AbsolutePath.TrimEnd('/');
            return string.Equals(cp, tp, StringComparison.OrdinalIgnoreCase);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }
}
