namespace DeepSeekBrowser.Services;

/// <summary>双 WebView 表面显隐与注入调度。</summary>
public interface IDesktopWebSurfaces
{
    bool IsAgentVisible { get; }

    bool AgentPageReady { get; }

    bool IsSurfaceSwitching { get; }

    string? AgentSource { get; }

    void ShowChat();

    void ShowAgent();

    void RequestChatInject(string reason, bool forceReset = false);

    void CancelChatInject();
}
