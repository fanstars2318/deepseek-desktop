using DeepSeekBrowser.Services.Dd;

namespace DeepSeekBrowser.DdBridge;

internal static class BridgeStartupContext
{
    public static Task<DdDesktopIpc>? PendingIpc { get; private set; }

    public static void BeginAcceptPipe() =>
        PendingIpc ??= DdDesktopIpc.AcceptServerAsync();

    public static Task<DdDesktopIpc> WaitForIpcAsync() =>
        PendingIpc ?? DdDesktopIpc.AcceptServerAsync();
}
