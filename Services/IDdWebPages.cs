using System.Text.Json;

namespace DeepSeekBrowser.Services;

/// <summary>桌面双 Web 面（Chat + Agent）宿主抽象，供 WPF 与 DD Bridge 复用。</summary>
public interface IDdWebPages
{
    event EventHandler<JsonElement>? MessageReceived;

    IDdPageMessenger Chat { get; }

    IDdPageMessenger Agent { get; }

    WorkModeCoordinator WorkMode { get; }

    bool IsAgentVisible { get; }

    bool AgentPageReady { get; }

    bool IsAgentHostPage { get; }

    string? AgentSource { get; }

    IReadOnlyList<string> AgentRefFileIds { get; set; }

    Task PostToPageAsync(object message);

    Task PushWorkModeStateNowAsync(WorkModeStatePayload state, CancellationToken ct = default);

    Task BroadcastWorkModeStateAsync(CancellationToken ct = default);

    Task BroadcastWorkModeStateAsync(bool includeImmediate, CancellationToken ct = default);

    void ScheduleWorkModeBroadcastRetries(CancellationToken ct = default);

    Task AfterShowChatAsync(string workMode, CancellationToken ct = default);

    Task PushAgentAuthHintAsync(bool loggedIn);

    Task SyncApiBridgeTokenAsync(string? token);

    Task EnsureApiBridgeReadyAsync(CancellationToken ct = default);

    Task<Chat2ApiHealth?> ProbeChat2ApiHealthAsync(string? configWebUserToken, string baseUrl,
        CancellationToken ct = default);

    Task<string?> TryReadUserTokenAsync();

    Task<string?> GetUserTokenAsync(bool waitForBridge = true);

    Task SyncChatSessionAsync(string? sessionId);

    void ShowChat();

    void ShowAgent();

    Task<WebChatResult> WebChatAsync(
        IReadOnlyList<ChatMessage> messages,
        string model,
        bool thinking,
        bool search,
        IReadOnlyList<string> refFileIds,
        bool allowToolCalls,
        CancellationToken ct,
        string? webUserToken = null,
        string? webChatSessionId = null);

    IAsyncEnumerable<WebChatStreamEvent> WebChatStreamAsync(
        IReadOnlyList<ChatMessage> messages,
        string model,
        bool thinking,
        bool search,
        IReadOnlyList<string> refFileIds,
        bool allowToolCalls,
        CancellationToken ct,
        string? webUserToken = null,
        string? webChatSessionId = null);
}
