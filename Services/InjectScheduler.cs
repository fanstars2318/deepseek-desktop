using System.Windows.Threading;

namespace DeepSeekBrowser.Services;

/// <summary>全应用唯一注入调度：debounce 合并 SPA/导航触发的重复 burst。</summary>
public sealed class InjectScheduler
{
    private readonly Func<CancellationToken, bool, Task> _runInject;
    private readonly Func<bool> _isSurfaceSwitching;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource? _cts;
    private readonly object _gate = new();

    public InjectScheduler(
        Dispatcher dispatcher,
        Func<CancellationToken, bool, Task> runInject,
        Func<bool> isSurfaceSwitching)
    {
        _dispatcher = dispatcher;
        _runInject = runInject;
        _isSurfaceSwitching = isSurfaceSwitching;
    }

    public void Request(string reason, bool forceReset = false)
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            var reset = forceReset;
            _ = RunDebouncedAsync(reason, reset, ct);
        }
    }

    private async Task RunDebouncedAsync(string reason, bool forceReset, CancellationToken ct)
    {
        try
        {
            await Task.Delay(300, ct);
            if (_isSurfaceSwitching())
                await Task.Delay(150, ct);

            await _dispatcher.InvokeAsync(async () =>
            {
                if (ct.IsCancellationRequested) return;
                DesktopUiTrace.InjectBurstScheduled(reason, forceReset);
                try
                {
                    await _runInject(ct, forceReset);
                }
                finally
                {
                    DesktopUiTrace.InjectBurstCompleted(reason);
                }
            }, DispatcherPriority.Background);
        }
        catch (OperationCanceledException)
        {
            // superseded
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
