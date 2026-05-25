using System.Text.Json;
using DeepSeekBrowser.Services.Harness;
using DeepSeekBrowser.Services.Harness.Sandbox;

namespace DeepSeek.Core.Tests.Harness;

public sealed class HarnessEditFileToolTests
{
    [Fact]
    public void Edit_replaces_single_occurrence()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "dsd-edit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        var file = Path.Combine(workspace, "sample.txt");
        File.WriteAllText(file, "alpha beta gamma");

        try
        {
            var paths = new SandboxPathResolver(workspace);
            var args = JsonSerializer.SerializeToElement(new { path = "sample.txt", old_string = "beta", new_string = "BETA" });
            var result = HarnessEditFileTool.Execute(args, paths);
            Assert.Contains("已编辑", result);
            Assert.Equal("alpha BETA gamma", File.ReadAllText(file));
        }
        finally
        {
            try { Directory.Delete(workspace, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Edit_returns_error_when_old_string_missing()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "dsd-edit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "a.txt"), "hello");

        try
        {
            var paths = new SandboxPathResolver(workspace);
            var args = JsonSerializer.SerializeToElement(new { path = "a.txt", old_string = "missing", new_string = "x" });
            var result = HarnessEditFileTool.Execute(args, paths);
            Assert.StartsWith("ERROR:", result);
        }
        finally
        {
            try { Directory.Delete(workspace, true); } catch { /* ignore */ }
        }
    }
}
