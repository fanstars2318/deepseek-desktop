using System.Diagnostics;

namespace DeepSeekBrowser.Services.Harness;

public static class HarnessProcessTree
{
    public static bool Kill(int pid)
    {
        if (pid <= 0)
            return false;

        if (OperatingSystem.IsWindows())
            return RunWindowsTaskKill(pid);

        try
        {
            Process.GetProcessById(pid).Kill(entireProcessTree: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool RunWindowsTaskKill(int pid)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/PID {pid} /T /F",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
