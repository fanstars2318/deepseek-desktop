using System.Diagnostics;
using System.IO;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.DeepSeekTui;

/// <summary>退出时清理 DeepSeek-TUI 子进程（dispatcher + runtime）。</summary>
public static class DeepSeekTuiProcessCleanup
{
    public static void ShutdownAll(AppConfig? config = null, Process? managedProcess = null)
    {
        config ??= ConfigStore.Load();
        var port = config.DeepSeekTuiRuntimePort > 0
            ? config.DeepSeekTuiRuntimePort
            : DeepSeekTuiHost.DefaultPort;

        KillManagedProcess(managedProcess);
        KillToolsDirectoryProcesses(DeepSeekTuiBundle.ToolsDirectory);
        KillPortListeners(port);
    }

    public static void KillManagedProcess(Process? process)
    {
        if (process is null) return;
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // ignore
        }
        finally
        {
            try { process.Dispose(); }
            catch { /* ignore */ }
        }
    }

    /// <summary>仅结束位于本应用 <c>Assets/tools</c> 下的 deepseek.exe / deepseek-tui.exe。</summary>
    public static void KillToolsDirectoryProcesses(string toolsDir)
    {
        if (string.IsNullOrWhiteSpace(toolsDir) || !Directory.Exists(toolsDir))
            return;

        var prefix = Path.GetFullPath(toolsDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;

        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                string? path;
                try
                {
                    path = proc.MainModule?.FileName;
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var full = Path.GetFullPath(path);
                if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var name = Path.GetFileName(full);
                if (name is not ("deepseek.exe" or "deepseek-tui.exe"))
                    continue;

                if (!proc.HasExited)
                    proc.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignore per-process failures
            }
            finally
            {
                proc.Dispose();
            }
        }
    }

    public static void KillPortListeners(int port)
    {
        if (port <= 0 || port > 65535) return;
        try
        {
            var script =
                $"Get-NetTCPConnection -LocalPort {port} -State Listen -ErrorAction SilentlyContinue | " +
                "ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }";
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
        }
        catch
        {
            // ignore
        }
    }
}
