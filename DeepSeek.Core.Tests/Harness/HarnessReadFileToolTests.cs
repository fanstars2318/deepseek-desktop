using System.Text.Json;
using DeepSeekBrowser.Services.Harness;
using DeepSeekBrowser.Services.Harness.Sandbox;

namespace DeepSeek.Core.Tests.Harness;

public sealed class HarnessReadFileToolTests
{
    [Fact]
    public void Read_honors_offset_and_limit_with_line_numbers()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "dsd-read-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        File.WriteAllLines(Path.Combine(workspace, "lines.txt"), ["a", "b", "c", "d", "e"]);

        try
        {
            var paths = new SandboxPathResolver(workspace);
            var args = JsonSerializer.SerializeToElement(new { file_path = "lines.txt", offset = 2, limit = 2 });
            var result = HarnessReadFileTool.Execute(args, paths);
            Assert.Contains("2|b", result.Output);
            Assert.Contains("3|c", result.Output);
            Assert.DoesNotContain("1|a", result.Output);
            Assert.Empty(result.FollowUpMessages);
        }
        finally
        {
            try { Directory.Delete(workspace, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Read_image_emits_follow_up_with_image_url()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "dsd-img-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        File.WriteAllBytes(Path.Combine(workspace, "pic.png"), [0x89, 0x50, 0x4E, 0x47]);

        try
        {
            var paths = new SandboxPathResolver(workspace);
            var args = JsonSerializer.SerializeToElement(new { file_path = "pic.png" });
            var result = HarnessReadFileTool.Execute(args, paths);
            Assert.Contains("File loaded", result.Output);
            Assert.Single(result.FollowUpMessages);
            var fu = result.FollowUpMessages[0];
            Assert.Equal("system", fu.Role);
            Assert.NotNull(fu.ContentParts);
            Assert.Contains(fu.ContentParts!, p => p.Type == "image_url" && (p.ImageUrl ?? "").StartsWith("data:image/png;base64,"));
        }
        finally
        {
            try { Directory.Delete(workspace, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void ParsePageRange_parses_single_and_range()
    {
        var single = HarnessReadFileTool.ParsePageRange("3");
        Assert.NotNull(single);
        Assert.Equal(3, single!.Start);
        Assert.Equal(3, single.End);

        var range = HarnessReadFileTool.ParsePageRange("2-5");
        Assert.NotNull(range);
        Assert.Equal(2, range!.Start);
        Assert.Equal(5, range.End);
        Assert.Equal(4, range.Count);
    }

    [Fact]
    public void Read_pdf_large_without_pages_returns_error()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "dsd-pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        var pdfPath = Path.Combine(workspace, "big.pdf");
        var sb = new byte[200];
        for (var i = 0; i < 15; i++)
            File.AppendAllText(pdfPath, "/Type /Page\n");

        try
        {
            var paths = new SandboxPathResolver(workspace);
            var args = JsonSerializer.SerializeToElement(new { file_path = "big.pdf" });
            var result = HarnessReadFileTool.Execute(args, paths);
            Assert.StartsWith("ERROR:", result.Output);
            Assert.Contains("pages", result.Output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(workspace, true); } catch { /* ignore */ }
        }
    }
}
