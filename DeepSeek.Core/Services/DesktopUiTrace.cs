using System.IO;
using System.Text;

namespace DeepSeekBrowser.Services;

/// <summary>桌面 UI 流畅度与注入调度追踪（desktop-ui-trace.log）。</summary>
public static class DesktopUiTrace
{
    private static readonly object Gate = new();
    private static readonly string LogPath = Path.Combine(DeepSeekDesktopApp.LogsDirectory, "desktop-ui-trace.log");

    public static int InjectBurstCount { get; private set; }
    public static int LoadingOverlayShowCount { get; private set; }
    public static int SpaRouteCount { get; private set; }
    public static int CrossFadeCount { get; private set; }

    public static void ResetCounters()
    {
        lock (Gate)
        {
            InjectBurstCount = 0;
            LoadingOverlayShowCount = 0;
            SpaRouteCount = 0;
            CrossFadeCount = 0;
        }
    }

    public static void Write(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // ignore
            }
        }
    }

    public static void InjectBurstScheduled(string reason, bool forceReset)
    {
        lock (Gate)
        {
            InjectBurstCount++;
        }

        Write($"inject_burst_scheduled reason={reason} forceReset={forceReset} count={InjectBurstCount}");
    }

    public static void InjectBurstCompleted(string reason)
    {
        Write($"inject_burst_completed reason={reason}");
    }

    public static void LoadingOverlayShow(string trigger)
    {
        lock (Gate)
        {
            LoadingOverlayShowCount++;
        }

        Write($"loading_overlay_show trigger={trigger} count={LoadingOverlayShowCount}");
    }

    public static void LoadingOverlayHide(string trigger)
    {
        Write($"loading_overlay_hide trigger={trigger}");
    }

    public static void SpaRoute(string source)
    {
        lock (Gate)
        {
            SpaRouteCount++;
        }

        Write($"spa_route source={source} count={SpaRouteCount}");
    }

    public static void CrossFadeStart(string target)
    {
        lock (Gate)
        {
            CrossFadeCount++;
        }

        Write($"crossfade_start target={target} count={CrossFadeCount}");
    }
}
