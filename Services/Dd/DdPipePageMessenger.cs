using System.Text.Json;

namespace DeepSeekBrowser.Services.Dd;

/// <summary>经命名管道向 Qt QWebEngine 页推送消息（替代 Agent WebView2）。</summary>
public sealed class DdPipePageMessenger : IDdPageMessenger
{
    private readonly DdDesktopIpc _ipc;
    private readonly string _channel;

    public DdPipePageMessenger(DdDesktopIpc ipc, string channel)
    {
        _ipc = ipc;
        _channel = channel;
    }

    public event EventHandler<JsonElement>? MessageReceived;

    public IReadOnlyList<string> AgentRefFileIds { get; set; } = Array.Empty<string>();

    public string? Source { get; set; } = AppNavigation.AgentPageUrl;

    internal void RaiseMessage(JsonElement payload) =>
        MessageReceived?.Invoke(this, payload);

    public Task PostToPageAsync(object message) =>
        _ipc.SendEnvelopeAsync(_channel, message);

    public Task PostWebMessageAsync(object message) =>
        PostToPageAsync(message);

    public Task PushWorkModeStateAsync(WorkModeStatePayload state) =>
        PostToPageAsync(new { type = "workModeState", state });

    public Task PushAgentAuthHintAsync(bool loggedIn) =>
        PostToPageAsync(new { type = "agentAuthHint", loggedIn });
}
