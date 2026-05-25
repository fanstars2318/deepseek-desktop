using DeepSeekBrowser.Services;
using DeepSeekBrowser.Services.Dd;

namespace DeepSeekBrowser.DdBridge;

/// <summary>无 WebView2 的管道回声自检（build / CI 用）。</summary>
internal static class BridgeIpcSmoke
{
    public static async Task<int> RunAsync(CancellationToken ct = default)
    {
        var ipc = await BridgeStartupContext.WaitForIpcAsync();
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        ipc.LineReceived += envelope =>
        {
            if (!envelope.TryGetProperty("channel", out var chEl) ||
                !envelope.TryGetProperty("payload", out var payload))
                return;

            if (chEl.GetString() != "agent" || !payload.TryGetProperty("type", out var typeEl))
                return;

            var type = typeEl.GetString();
            if (type is not ("nativeReady" or "refreshLoginState"))
                return;

            _ = RespondAsync(ipc, done, ct);
        };

        ipc.StartReading();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(40));
        try
        {
            await done.Task.WaitAsync(timeout.Token);
            return 0;
        }
        catch
        {
            return 1;
        }
        finally
        {
            await ipc.DisposeAsync();
        }
    }

    private static async Task RespondAsync(DdDesktopIpc ipc, TaskCompletionSource<bool> done, CancellationToken ct)
    {
        try
        {
            var state = WorkModeStatePayload.For("chat", agentSurfaceVisible: false);
            await ipc.SendEnvelopeAsync("agent", new { type = "workModeState", state }, ct);
            await ipc.SendEnvelopeAsync("agent", new { type = "loginState", loggedIn = false }, ct);
            done.TrySetResult(true);
        }
        catch
        {
            // client disconnected
        }
    }
}
