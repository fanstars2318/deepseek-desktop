using Microsoft.UI.Dispatching;

namespace DeepSeek.Desktop.Services;

internal static class DispatcherQueueExtensions
{
    public static Task InvokeAsync(this DispatcherQueue queue, Func<Task> action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!queue.TryEnqueue(async () =>
            {
                try
                {
                    await action().ConfigureAwait(false);
                    tcs.TrySetResult();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
        {
            tcs.TrySetException(new InvalidOperationException("Dispatcher queue rejected enqueue."));
        }

        return tcs.Task;
    }

    public static Task<T> InvokeAsync<T>(this DispatcherQueue queue, Func<Task<T>> action)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!queue.TryEnqueue(async () =>
            {
                try
                {
                    tcs.TrySetResult(await action().ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
        {
            tcs.TrySetException(new InvalidOperationException("Dispatcher queue rejected enqueue."));
        }

        return tcs.Task;
    }

    public static T Invoke<T>(this DispatcherQueue queue, Func<T> func)
    {
        if (queue.HasThreadAccess)
            return func();

        T? result = default;
        Exception? err = null;
        using var wait = new ManualResetEventSlim(false);
        if (!queue.TryEnqueue(() =>
            {
                try { result = func(); }
                catch (Exception ex) { err = ex; }
                finally { wait.Set(); }
            }))
            throw new InvalidOperationException("Dispatcher queue rejected enqueue.");

        wait.Wait();
        if (err is not null) throw err;
        return result!;
    }

    public static void Invoke(this DispatcherQueue queue, Action action)
    {
        if (queue.HasThreadAccess)
        {
            action();
            return;
        }

        using var wait = new ManualResetEventSlim(false);
        Exception? err = null;
        if (!queue.TryEnqueue(() =>
            {
                try { action(); }
                catch (Exception ex) { err = ex; }
                finally { wait.Set(); }
            }))
            throw new InvalidOperationException("Dispatcher queue rejected enqueue.");

        wait.Wait();
        if (err is not null) throw err;
    }
}
