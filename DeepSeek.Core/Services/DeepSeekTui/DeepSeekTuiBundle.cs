using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.DeepSeekTui;

/// <summary>
/// 捆绑并维护 DeepSeek-TUI 官方二进制（dispatcher + TUI runtime，须同目录）。
/// 见 <see href="https://deepseek-tui.com/zh/install">官方安装文档</see>。
/// </summary>
public static class DeepSeekTuiBundle
{
    /// <summary>与 npm 包 / GitHub Release 对齐（<see href="https://deepseek-tui.com/zh"/>）。</summary>
    public const string BundledVersion = "0.8.39";

    private const string ReleaseTag = "v0.8.39";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public static string ToolsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "tools");

    public static string DispatcherPath => Path.Combine(ToolsDirectory, "deepseek.exe");

    public static string RuntimePath => Path.Combine(ToolsDirectory, "deepseek-tui.exe");

    private const long MinDispatcherBytes = 10_000_000;
    private const long MinRuntimeBytes = 30_000_000;

    public static bool IsBundledComplete =>
        IsValidPeExecutable(DispatcherPath, MinDispatcherBytes) &&
        IsValidPeExecutable(RuntimePath, MinRuntimeBytes);

    public static bool IsValidPeExecutable(string path, long minBytes)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            var info = new FileInfo(path);
            if (info.Length < minBytes)
                return false;

            using var fs = File.OpenRead(path);
            Span<byte> header = stackalloc byte[2];
            if (fs.Read(header) != 2)
                return false;
            return header[0] == (byte)'M' && header[1] == (byte)'Z';
        }
        catch
        {
            return false;
        }
    }

    private static void RemoveIfInvalid(string path, long minBytes)
    {
        if (!File.Exists(path))
            return;
        if (!IsValidPeExecutable(path, minBytes))
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }

    public static string? ResolveDispatcher(string? configuredPath, AppConfig? config = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            return configuredPath;

        config ??= ConfigStore.Load();
        var fromSource = DeepSeekTuiSourceBuild.TryResolveReleaseBinaries(config);
        if (fromSource is not null &&
            DeepSeekTuiBundle.IsValidPeExecutable(fromSource.Value.Dispatcher, MinDispatcherBytes) &&
            DeepSeekTuiBundle.IsValidPeExecutable(fromSource.Value.Runtime, MinRuntimeBytes))
            return fromSource.Value.Dispatcher;

        if (IsBundledComplete)
            return DispatcherPath;

        var npmDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "npm", "node_modules", "deepseek-tui", "bin", "downloads");
        var npmDispatcher = Path.Combine(npmDir, "deepseek.exe");
        if (File.Exists(npmDispatcher))
            return npmDispatcher;

        var npmCmd = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "npm", "deepseek.cmd");
        if (File.Exists(npmCmd))
            return npmCmd;

        return FindOnPath("deepseek.exe") ?? FindOnPath("deepseek.cmd");
    }

    public static string? ResolveCompanionDirectory(string dispatcherPath, AppConfig? config = null)
    {
        var dir = Path.GetDirectoryName(dispatcherPath);
        if (string.IsNullOrEmpty(dir))
            return ToolsDirectory;

        if (File.Exists(Path.Combine(dir, "deepseek-tui.exe")))
            return dir;

        config ??= ConfigStore.Load();
        var fromSource = DeepSeekTuiSourceBuild.TryResolveReleaseBinaries(config);
        if (fromSource is not null &&
            string.Equals(dispatcherPath, fromSource.Value.Dispatcher, StringComparison.OrdinalIgnoreCase) &&
            DeepSeekTuiBundle.IsValidPeExecutable(fromSource.Value.Runtime, MinRuntimeBytes))
            return Path.GetDirectoryName(fromSource.Value.Runtime);

        if (IsBundledComplete)
            return ToolsDirectory;

        var npmDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "npm", "node_modules", "deepseek-tui", "bin", "downloads");
        if (File.Exists(Path.Combine(npmDir, "deepseek-tui.exe")))
            return npmDir;

        return dir;
    }

    public static async Task EnsureBinariesAsync(AppConfig? config = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(ToolsDirectory);
        config ??= ConfigStore.Load();

        // 已捆绑且可运行则跳过复制（避免覆盖正在被 TUI 子进程占用的 exe）
        if (IsBundledComplete &&
            await TryValidateRunnablePairAsync(DispatcherPath, RuntimePath, ct).ConfigureAwait(false))
            return;

        RemoveIfInvalid(DispatcherPath, MinDispatcherBytes);
        RemoveIfInvalid(RuntimePath, MinRuntimeBytes);

        if (await TryCopyReleaseToToolsAsync(config, ct).ConfigureAwait(false) &&
            await TryValidateRunnablePairAsync(DispatcherPath, RuntimePath, ct).ConfigureAwait(false))
            return;

        if (TryCopyFromNpmInstall() &&
            await TryValidateRunnablePairAsync(DispatcherPath, RuntimePath, ct).ConfigureAwait(false))
            return;

        if (IsBundledComplete &&
            await TryValidateRunnablePairAsync(DispatcherPath, RuntimePath, ct).ConfigureAwait(false))
            return;

        var repoRoot = DeepSeekTuiSourceBuild.ResolveRepositoryRoot(config);
        if (repoRoot is not null && DeepSeekTuiSourceBuild.FindCargo() is not null)
        {
            try
            {
                await DeepSeekTuiSourceBuild.BuildReleaseAsync(repoRoot, ct).ConfigureAwait(false);
                if (await DeepSeekTuiSourceBuild.TryCopyReleaseToToolsAsync(config, ct).ConfigureAwait(false) &&
                    await TryValidateRunnablePairAsync(DispatcherPath, RuntimePath, ct).ConfigureAwait(false))
                    return;
            }
            catch
            {
                // 无 Rust 或编译失败时继续尝试下载
            }
        }

        var baseUrl = $"https://github.com/Hmbown/DeepSeek-TUI/releases/download/{ReleaseTag}";
        await DownloadAsync($"{baseUrl}/deepseek-windows-x64.exe", DispatcherPath, MinDispatcherBytes, ct)
            .ConfigureAwait(false);
        await DownloadAsync($"{baseUrl}/deepseek-tui-windows-x64.exe", RuntimePath, MinRuntimeBytes, ct)
            .ConfigureAwait(false);

        if (!IsBundledComplete ||
            !await TryValidateRunnablePairAsync(DispatcherPath, RuntimePath, ct).ConfigureAwait(false))
        {
            if (TryCopyFromNpmInstall() &&
                await TryValidateRunnablePairAsync(DispatcherPath, RuntimePath, ct).ConfigureAwait(false))
            {
                await WriteVersionFileAsync(ct).ConfigureAwait(false);
                return;
            }

            throw new InvalidOperationException(
                "DeepSeek-TUI 二进制不可用或无法运行。\n" +
                "请先执行: npm install -g deepseek-tui\n" +
                "或在已安装 Rust 后运行: build.ps1 -UseLocalTui\n" +
                $"源码目录: {config.DeepSeekTuiSourcePath}");
        }

        await WriteVersionFileAsync(ct).ConfigureAwait(false);
    }

    public static async Task<bool> TryValidateRunnablePairAsync(
        string dispatcherPath,
        string runtimePath,
        CancellationToken ct = default)
    {
        if (!IsValidPeExecutable(dispatcherPath, MinDispatcherBytes) ||
            !IsValidPeExecutable(runtimePath, MinRuntimeBytes))
            return false;

        var toolsDir = Path.GetDirectoryName(dispatcherPath) ?? ToolsDirectory;
        if (!await TryRunVersionAsync(runtimePath, toolsDir, ct).ConfigureAwait(false))
            return false;

        return await TryRunVersionAsync(dispatcherPath, toolsDir, ct).ConfigureAwait(false);
    }

    private static async Task WriteVersionFileAsync(CancellationToken ct)
    {
        var version = await TryGetVersionAsync(DispatcherPath, ct).ConfigureAwait(false) ?? BundledVersion;
        await File.WriteAllTextAsync(
            Path.Combine(ToolsDirectory, "version.txt"),
            version + Environment.NewLine,
            ct).ConfigureAwait(false);
    }

    private static async Task<bool> TryRunVersionAsync(string exe, string workingDir, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "--version",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return false;
            var output = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            return proc.ExitCode == 0 &&
                   output.Contains("deepseek", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TryCopyReleaseToToolsAsync(AppConfig config, CancellationToken ct) =>
        await DeepSeekTuiSourceBuild.TryCopyReleaseToToolsAsync(config, ct).ConfigureAwait(false);

    private static bool TryCopyFromNpmInstall()
    {
        var npmDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "npm", "node_modules", "deepseek-tui", "bin", "downloads");
        var npmDispatcher = Path.Combine(npmDir, "deepseek.exe");
        var npmRuntime = Path.Combine(npmDir, "deepseek-tui.exe");
        if (!IsValidPeExecutable(npmDispatcher, MinDispatcherBytes) ||
            !IsValidPeExecutable(npmRuntime, MinRuntimeBytes))
            return false;

        try
        {
            File.Copy(npmDispatcher, DispatcherPath, overwrite: true);
            File.Copy(npmRuntime, RuntimePath, overwrite: true);
        }
        catch (IOException) when (IsBundledComplete)
        {
            // TUI 子进程可能正占用 tools 目录下的 exe
        }

        return IsBundledComplete;
    }

    public static async Task<string?> TryGetVersionAsync(string? dispatcherPath, CancellationToken ct = default)
    {
        var exe = ResolveDispatcher(dispatcherPath);
        if (string.IsNullOrEmpty(exe))
            return null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "--version",
                WorkingDirectory = ResolveCompanionDirectory(exe) ?? ToolsDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return null;
            var output = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            var line = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            return string.IsNullOrWhiteSpace(line) ? null : line;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<JsonDocument?> TryDoctorJsonAsync(string? dispatcherPath, CancellationToken ct = default)
    {
        var exe = ResolveDispatcher(dispatcherPath);
        if (string.IsNullOrEmpty(exe))
            return null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "doctor --json",
                WorkingDirectory = ResolveCompanionDirectory(exe) ?? ToolsDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return null;
            var output = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return null;
            return JsonDocument.Parse(output);
        }
        catch
        {
            return null;
        }
    }

    private static async Task DownloadAsync(string url, string targetPath, long minBytes, CancellationToken ct)
    {
        var tmp = targetPath + ".download";
        if (File.Exists(tmp))
        {
            try { File.Delete(tmp); } catch { /* ignore */ }
        }

        await using (var stream = await Http.GetStreamAsync(url, ct).ConfigureAwait(false))
        await using (var file = File.Create(tmp))
            await stream.CopyToAsync(file, ct).ConfigureAwait(false);

        if (!IsValidPeExecutable(tmp, minBytes))
            throw new InvalidOperationException($"下载的文件无效或不完整: {Path.GetFileName(targetPath)}");

        if (File.Exists(targetPath))
            File.Delete(targetPath);
        File.Move(tmp, targetPath);
    }

    private static string? FindOnPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var full = Path.Combine(dir.Trim(), fileName);
                if (File.Exists(full))
                    return full;
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }
}
