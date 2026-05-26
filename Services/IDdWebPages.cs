using System.Text.Json;

namespace DeepSeekBrowser.Services;

/// <summary>桌面双 Web 面（Chat + Agent）宿主抽象，供 WPF 复用。</summary>
[Obsolete("Prefer IDesktopWebHost and focused interfaces (IDesktopWebSurfaces, IWebChatBridge, …).")]
public interface IDdWebPages :
    IDesktopWebSurfaces,
    IWorkModeBroadcast,
    IWebAuthBridge,
    IWebChatBridge,
    IEmbeddedPageMessenger
{
    event EventHandler<JsonElement>? MessageReceived;

    IDdPageMessenger Chat { get; }

    IDdPageMessenger Agent { get; }

    WorkModeCoordinator WorkMode { get; }
}

/// <summary>Preferred name for <see cref="IDdWebPages"/>.</summary>
public interface IDesktopWebHost : IDdWebPages;
