namespace DeepSeekBrowser.Services;

/// <summary>统一 fire-and-forget 后台任务，避免裸 <c>_ =</c> 与未观察异常。</summary>
internal static class HostFireAndForget
{
    public static void Run(Func<Task> work, string name)
    {
        _ = RunCoreAsync(work, name);
    }

    private static async Task RunCoreAsync(Func<Task> work, string name)
    {
        try
        {
            await work().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DesktopUiTrace.Write($"background:{name} failed: {ex.Message}");
        }
    }
}
