namespace DeepSeekBrowser.Services;

/// <summary>Agent 内嵌页消息与附件上下文。</summary>
public interface IEmbeddedPageMessenger
{
    IReadOnlyList<string> AgentRefFileIds { get; set; }

    Task PostToPageAsync(object message);

    Task PushAgentAuthHintAsync(bool loggedIn);
}
