using System.Diagnostics;
using System.Net.Http;

namespace DeepSeekBrowser.Services.Runtime;

/// <summary>通过 winget 或官方安装包静默安装缺失运行库。</summary>
public static class RuntimeDependencyInstaller
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public static async Task<IReadOnlyList<RuntimeInstallResult>> InstallMissingAsync(
        IReadOnlyList<RuntimeDependency> missing,
        string logDirectory,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, "runtime-install.log");
        void Write(string line)
        {
            log?.Invoke(line);
            try { File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}{Environment.NewLine}"); }
            catch { /* ignore */ }
        }

        var results = new List<RuntimeInstallResult>();
        var wingetInstalled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dep in missing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (RuntimeDependencyChecker.IsInstalled(dep))
            {
                results.Add(new RuntimeInstallResult
                {
                    DependencyId = dep.Id,
                    Success = true,
                    Message = "已满足"
                });
                continue;
            }

            Write($"Installing {dep.DisplayName}...");

            var ok = false;
            var message = "";

            if (!string.IsNullOrWhiteSpace(dep.WingetPackageId)
                && IsWingetAvailable()
                && wingetInstalled.Add(dep.WingetPackageId))
            {
                ok = await TryWingetInstallAsync(dep.WingetPackageId, Write, cancellationToken);
                message = ok ? "winget 安装成功" : "winget 安装失败";
            }
            else if (!string.IsNullOrWhiteSpace(dep.WingetPackageId) && wingetInstalled.Contains(dep.WingetPackageId))
            {
                ok = RuntimeDependencyChecker.IsInstalled(dep);
                message = ok ? "已由同批次 winget 包满足" : "仍缺失";
            }

            if (!ok && !string.IsNullOrWhiteSpace(dep.DirectDownloadUrl)
                     && dep.Id == RuntimeDependencyChecker.WebView2DependencyId)
            {
                ok = await TryDownloadInstallerAsync(
                    dep.DirectDownloadUrl!,
                    "MicrosoftEdgeWebview2Setup.exe",
                    dep.InstallerArgs ?? "/silent /install",
                    Write,
                    cancellationToken);
                message = ok ? "WebView2 安装包安装成功" : "WebView2 安装包安装失败";
            }

            if (!RuntimeDependencyChecker.IsInstalled(dep) && string.IsNullOrWhiteSpace(message))
                message = $"请手动安装：{dep.DirectDownloadUrl ?? dep.DisplayName}";

            var success = RuntimeDependencyChecker.IsInstalled(dep);
            Write(message);
            results.Add(new RuntimeInstallResult
            {
                DependencyId = dep.Id,
                Success = success,
                Message = message
            });
        }

        return results;
    }

    public static bool IsWingetAvailable()
    {
        try
        {
            return RunProcess("where.exe", "winget", TimeSpan.FromSeconds(5), _ => { }) == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TryWingetInstallAsync(
        string packageId,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var args =
            $"install --id {packageId} -e --accept-package-agreements --accept-source-agreements --disable-interactivity";
        var code = await Task.Run(
            () => RunProcess("winget", args, TimeSpan.FromMinutes(15), log),
            cancellationToken);
        return code == 0 || (uint)code == 2316632105u; // already installed
    }

    private static async Task<bool> TryDownloadInstallerAsync(
        string url,
        string fileName,
        string installerArgs,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "deepseek-runtime");
        Directory.CreateDirectory(tempDir);
        var installerPath = Path.Combine(tempDir, fileName);

        try
        {
            log($"Downloading {url}...");
            await using (var stream = await Http.GetStreamAsync(url, cancellationToken))
            await using (var file = File.Create(installerPath))
                await stream.CopyToAsync(file, cancellationToken);

            log($"Running {fileName} {installerArgs}");
            var code = RunProcess(installerPath, installerArgs, TimeSpan.FromMinutes(10), log);
            return code == 0;
        }
        catch (Exception ex)
        {
            log("Download/install error: " + ex.Message);
            return false;
        }
    }

    private static int RunProcess(string fileName, string arguments, TimeSpan timeout, Action<string> log)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        proc.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) log(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) log(e.Data);
        };

        if (!proc.Start())
            return -1;

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        if (!proc.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            log("Process timed out: " + fileName);
            return -2;
        }

        return proc.ExitCode;
    }
}
