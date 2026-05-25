using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace DeepSeekBrowser.Services.Runtime;

/// <summary>检测 publish 目录所需 .NET 共享框架与 WebView2 是否已安装。</summary>
public static class RuntimeDependencyChecker
{
    public const string WebView2DependencyId = "webview2";

    private static readonly RuntimeDependency WebView2Dependency = new()
    {
        Id = WebView2DependencyId,
        DisplayName = "Microsoft Edge WebView2 Runtime",
        WingetPackageId = "Microsoft.EdgeWebView2Runtime",
        DirectDownloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703",
        InstallerArgs = "/silent /install"
    };

    public static RuntimeDependencyReport Analyze(string publishDirectory)
    {
        var required = NormalizeRequirements(ParseRequiredFrameworks(publishDirectory).ToList());
        required.Add(WebView2Dependency);

        var missing = new List<RuntimeDependency>();
        var satisfied = new List<RuntimeDependency>();
        foreach (var dep in required)
        {
            if (IsInstalled(dep))
                satisfied.Add(dep);
            else
                missing.Add(dep);
        }

        return new RuntimeDependencyReport { Missing = missing, Satisfied = satisfied };
    }

    /// <summary>WPF 应用只需 Desktop 运行时；且以 shared 目录为准（比注册表可靠）。</summary>
    public static bool IsMainAppRunnable(string publishDirectory)
    {
        var frameworks = NormalizeRequirements(ParseRequiredFrameworks(publishDirectory).ToList());
        if (frameworks.Count == 0)
            return true;

        return frameworks.All(IsInstalled) && IsWebView2Installed();
    }

    private static List<RuntimeDependency> NormalizeRequirements(List<RuntimeDependency> required)
    {
        if (required.Any(d => d.FrameworkName == "Microsoft.WindowsDesktop.App"))
        {
            // Desktop 运行时已包含 NETCore，避免重复检测/重复安装。
            required.RemoveAll(d => d.FrameworkName == "Microsoft.NETCore.App");
        }

        return required;
    }

    public static bool IsInstalled(RuntimeDependency dep)
    {
        if (dep.Id == WebView2DependencyId)
            return IsWebView2Installed();

        if (string.IsNullOrWhiteSpace(dep.FrameworkName) || dep.MinVersion is null)
            return false;

        var installed = GetInstalledFrameworkVersions(dep.FrameworkName);
        return installed.Any(v => v >= dep.MinVersion);
    }

    public static IReadOnlyList<RuntimeDependency> ParseRequiredFrameworks(string publishDirectory)
    {
        var configPath = Path.Combine(publishDirectory, "DeepSeek.runtimeconfig.json");
        if (!File.Exists(configPath))
            return Array.Empty<RuntimeDependency>();

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            if (!doc.RootElement.TryGetProperty("runtimeOptions", out var opts)
                || !opts.TryGetProperty("frameworks", out var frameworks))
                return Array.Empty<RuntimeDependency>();

            var list = new List<RuntimeDependency>();
            foreach (var fw in frameworks.EnumerateArray())
            {
                var name = fw.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                var versionText = fw.TryGetProperty("version", out var verEl) ? verEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(versionText))
                    continue;
                if (!Version.TryParse(NormalizeVersion(versionText), out var minVersion))
                    continue;

                list.Add(new RuntimeDependency
                {
                    Id = "fx:" + name,
                    DisplayName = DescribeFramework(name, minVersion),
                    FrameworkName = name,
                    MinVersion = minVersion,
                    WingetPackageId = ResolveWingetId(name, minVersion),
                    DirectDownloadUrl = ResolveDirectDownloadUrl(name, minVersion),
                    InstallerArgs = "/install /quiet /norestart"
                });
            }

            return list;
        }
        catch
        {
            return Array.Empty<RuntimeDependency>();
        }
    }

    private static string DescribeFramework(string name, Version version) =>
        name switch
        {
            "Microsoft.WindowsDesktop.App" => $".NET Windows Desktop Runtime {version.Major}.0",
            "Microsoft.NETCore.App" => $".NET Runtime {version.Major}.0",
            _ => $"{name} {version}"
        };

    private static string? ResolveWingetId(string frameworkName, Version minVersion) =>
        frameworkName switch
        {
            "Microsoft.WindowsDesktop.App" => minVersion.Major switch
            {
                >= 11 => "Microsoft.DotNet.DesktopRuntime.Preview",
                10 => "Microsoft.DotNet.DesktopRuntime.10",
                9 => "Microsoft.DotNet.DesktopRuntime.9",
                8 => "Microsoft.DotNet.DesktopRuntime.8",
                _ => null
            },
            "Microsoft.NETCore.App" => minVersion.Major switch
            {
                >= 11 => "Microsoft.DotNet.Runtime.Preview",
                10 => "Microsoft.DotNet.Runtime.10",
                9 => "Microsoft.DotNet.Runtime.9",
                8 => "Microsoft.DotNet.Runtime.8",
                _ => null
            },
            _ => null
        };

    private static string? ResolveDirectDownloadUrl(string frameworkName, Version minVersion)
    {
        var major = minVersion.Major;
        return frameworkName switch
        {
            "Microsoft.WindowsDesktop.App" =>
                $"https://dotnet.microsoft.com/en-us/download/dotnet/{major}.0/runtime",
            "Microsoft.NETCore.App" =>
                $"https://dotnet.microsoft.com/en-us/download/dotnet/{major}.0/runtime",
            _ => null
        };
    }

    private static string NormalizeVersion(string raw)
    {
        var parts = raw.Split('.');
        return parts.Length switch
        {
            1 => parts[0] + ".0.0",
            2 => parts[0] + "." + parts[1] + ".0",
            _ => raw
        };
    }

    public static bool IsWebView2Installed()
    {
        try
        {
            const string clientKey =
                @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00F3BD02655}";
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                using var key = hive.OpenSubKey(clientKey);
                if (key?.GetValue("pv") is string pv
                    && !string.IsNullOrWhiteSpace(pv)
                    && pv != "0.0.0.0")
                    return true;
            }

            var fixedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft", "EdgeWebView", "Application");
            return Directory.Exists(fixedPath)
                   && Directory.EnumerateFileSystemEntries(fixedPath).Any();
        }
        catch
        {
            return false;
        }
    }

    public static IReadOnlyList<Version> GetInstalledFrameworkVersions(string frameworkName)
    {
        var versions = new HashSet<Version>();

        CollectVersionsFromRegistry(frameworkName, versions);
        CollectVersionsFromSharedFolder(frameworkName, versions);
        CollectVersionsFromDotNetCli(frameworkName, versions);

        return versions.OrderDescending().ToList();
    }

    private static void CollectVersionsFromRegistry(string frameworkName, HashSet<Version> versions)
    {
        try
        {
            var subKey = $@"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\{frameworkName}";
            using var key = Registry.LocalMachine.OpenSubKey(subKey);
            if (key is null) return;

            foreach (var name in key.GetValueNames())
            {
                if (Version.TryParse(name, out var v))
                    versions.Add(v);
            }
        }
        catch
        {
            // ignore
        }
    }

    private static void CollectVersionsFromSharedFolder(string frameworkName, HashSet<Version> versions)
    {
        try
        {
            var sharedRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "dotnet",
                "shared",
                frameworkName);
            if (!Directory.Exists(sharedRoot)) return;

            foreach (var dir in Directory.EnumerateDirectories(sharedRoot))
            {
                var folder = Path.GetFileName(dir);
                if (Version.TryParse(folder, out var v))
                    versions.Add(v);
            }
        }
        catch
        {
            // ignore
        }
    }

    private static void CollectVersionsFromDotNetCli(string frameworkName, HashSet<Version> versions)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--list-runtimes",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };
            if (!proc.Start()) return;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith(frameworkName + " ", StringComparison.Ordinal))
                    continue;
                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && Version.TryParse(parts[1], out var v))
                    versions.Add(v);
            }
        }
        catch
        {
            // ignore
        }
    }
}
