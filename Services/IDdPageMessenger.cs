using System.Text.Json;

namespace DeepSeekBrowser.Services;

/// <summary>向嵌入页推送 JSON 消息（WebView2 或 DD 管道）。</summary>
public interface IDdPageMessenger
{
    event EventHandler<JsonElement>? MessageReceived;

    IReadOnlyList<string> AgentRefFileIds { get; set; }

    string? Source { get; }

    Task PostToPageAsync(object message);

    Task PostWebMessageAsync(object message);

    Task PushWorkModeStateAsync(WorkModeStatePayload state);

    Task PushAgentAuthHintAsync(bool loggedIn);
}
