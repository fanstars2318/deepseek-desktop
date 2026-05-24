using System.IO;
using System.Text;

namespace DeepSeekBrowser.Services;

internal static class WorkModeTrace
{
    private static readonly object Gate = new();
    private static readonly string LogPath = Path.Combine(DeepSeekDesktopApp.LogsDirectory, "work-mode-trace.log");

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
}
