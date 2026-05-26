using DeepSeekBrowser.Services.Runtime;
using Xunit;

namespace DeepSeek.Core.Tests.Runtime;

public sealed class RuntimeDependencyCheckerTests
{
    [Fact]
    public void ParseRequiredFrameworks_reads_runtimeconfig()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ds-runtime-test-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "DeepSeek.runtimeconfig.json"),
                """
                {
                  "runtimeOptions": {
                    "frameworks": [
                      { "name": "Microsoft.NETCore.App", "version": "10.0.0" },
                      { "name": "Microsoft.WindowsDesktop.App", "version": "10.0.0" }
                    ]
                  }
                }
                """);

            var frameworks = RuntimeDependencyChecker.ParseRequiredFrameworks(dir);
            Assert.Equal(2, frameworks.Count);
            Assert.Contains(frameworks, f => f.FrameworkName == "Microsoft.WindowsDesktop.App");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Analyze_includes_webview2_dependency()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ds-runtime-test-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "DeepSeek.runtimeconfig.json"),
                """{"runtimeOptions":{"frameworks":[{"name":"Microsoft.WindowsDesktop.App","version":"10.0.0"}]}}""");

            var report = RuntimeDependencyChecker.Analyze(dir);
            Assert.Contains(report.Missing.Concat(report.Satisfied),
                d => d.Id == RuntimeDependencyChecker.WebView2DependencyId);
            Assert.DoesNotContain(report.Missing.Concat(report.Satisfied),
                d => d.FrameworkName == "Microsoft.NETCore.App");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void IsInstalled_detects_desktop_runtime_from_shared_folder()
    {
        var shared = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "shared",
            "Microsoft.WindowsDesktop.App");
        if (!Directory.Exists(shared))
            return;

        var dep = new RuntimeDependency
        {
            Id = "fx:test",
            DisplayName = "desktop",
            FrameworkName = "Microsoft.WindowsDesktop.App",
            MinVersion = new Version(10, 0, 0)
        };
        Assert.True(RuntimeDependencyChecker.IsInstalled(dep));
    }
}
