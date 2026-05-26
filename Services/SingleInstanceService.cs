using System.IO;
using System.IO.Pipes;
using System.Windows;
using System.Windows.Threading;

namespace DeepSeekBrowser.Services;

/// <summary>
/// 单实例互斥 + 二次启动时通知已运行进程显示主窗口（含托盘隐藏场景）。
/// </summary>
public static class SingleInstanceService
{
    private static readonly string MutexName = DeepSeekDesktopApp.SingleInstanceMutexName;
    private static readonly string PipeName = DeepSeekDesktopApp.SingleInstancePipeName;

    private static Mutex? _mutex;
    private static CancellationTokenSource? _pipeCts;

    public static bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        return createdNew;
    }

    public static void Release()
    {
        _pipeCts?.Cancel();
        _pipeCts = null;

        if (_mutex is null)
            return;

        try
        {
            _mutex.ReleaseMutex();
        }
        catch
        {
            // ignore
        }

        _mutex.Dispose();
        _mutex = null;
    }

    public static bool TryNotifyRunningInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.None);
            client.Connect(500);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine("activate");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void StartActivationServer(Action activateMainWindow)
    {
        _pipeCts?.Cancel();
        _pipeCts = new CancellationTokenSource();
        var token = _pipeCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    using var reader = new StreamReader(server);
                    _ = await reader.ReadLineAsync(token).ConfigureAwait(false);

                    var app = Application.Current;
                    if (app is null)
                        continue;

                    await app.Dispatcher.InvokeAsync(
                        activateMainWindow,
                        DispatcherPriority.Normal);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    if (token.IsCancellationRequested)
                        break;
                    await Task.Delay(200, token).ConfigureAwait(false);
                }
            }
        }, token);
    }
}
