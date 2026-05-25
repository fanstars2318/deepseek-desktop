using System.Text;
using System.Text.Json;
using DeepSeekBrowser.Services;
using DeepSeekBrowser.Services.Harness.Sandbox;

namespace DeepSeekBrowser.Services.Harness;

/// <summary>read / read_file — aligned with deepcode-cli read-handler (text, images, PDF).</summary>
public static class HarnessReadFileTool
{
    public const int DefaultLineLimit = 2000;
    public const int MaxLineLength = 2000;
    public const int MaxOutputChars = 120_000;
    public const int PdfLargePageThreshold = 10;
    public const int PdfMaxPageRange = 20;

    public static HarnessToolExecuteResult Execute(JsonElement args, SandboxPathResolver paths)
    {
        var path = GetString(args, "file_path") ?? GetString(args, "path")
                   ?? throw new ArgumentException("read 需要 file_path 或 path");

        string full;
        try
        {
            full = paths.ResolveRead(path);
        }
        catch (Exception ex)
        {
            return HarnessToolExecuteResult.FromOutput("ERROR: " + ex.Message);
        }

        if (!File.Exists(full))
            return HarnessToolExecuteResult.FromOutput("ERROR: 文件不存在: " + path);

        var ext = Path.GetExtension(full).ToLowerInvariant();
        if (ext == ".pdf")
            return ReadPdf(args, full, path, paths);
        if (IsImageExtension(ext))
            return ReadImage(full, path, ext);

        return HarnessToolExecuteResult.FromOutput(ReadText(args, full, paths));
    }

    private static HarnessToolExecuteResult ReadImage(string full, string virtualPath, string ext)
    {
        var bytes = File.ReadAllBytes(full);
        var mime = GetImageMimeType(ext);
        var fileName = Path.GetFileName(full);
        var dataUrl = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";

        var followUp = new ChatMessage
        {
            Role = "system",
            ContentParts =
            [
                new ChatContentPart
                {
                    Type = "text",
                    Text = $"The read tool has loaded `{fileName}`. Use the attached image content to answer the original request."
                },
                new ChatContentPart { Type = "image_url", ImageUrl = dataUrl }
            ]
        };

        return HarnessToolExecuteResult.WithFollowUp(
            $"File loaded: {virtualPath} ({bytes.Length} bytes, {mime})",
            [followUp]);
    }

    private static HarnessToolExecuteResult ReadPdf(JsonElement args, string full, string virtualPath, SandboxPathResolver paths)
    {
        var pagesParam = GetString(args, "pages")?.Trim() ?? "";
        var buffer = File.ReadAllBytes(full);
        var pageCount = CountPdfPages(buffer);
        var pageRange = string.IsNullOrEmpty(pagesParam) ? null : ParsePageRange(pagesParam);

        if (pageRange is null && pageCount is > PdfLargePageThreshold)
        {
            return HarnessToolExecuteResult.FromOutput(
                $"ERROR: PDF has {pageCount} pages; provide \"pages\" to read a range (e.g. \"1-5\").");
        }

        if (pageRange is not null && pageRange.Count > PdfMaxPageRange)
            return HarnessToolExecuteResult.FromOutput($"ERROR: PDF page range exceeds {PdfMaxPageRange} pages.");

        if (pageRange is not null && pageCount is not null && pageRange.End > pageCount)
            return HarnessToolExecuteResult.FromOutput($"ERROR: PDF page range exceeds total page count ({pageCount}).");

        var base64 = Convert.ToBase64String(buffer);
        var pagesLabel = pageRange is null ? "all" : $"{pageRange.Start}-{pageRange.End}";
        var output =
            $"data:application/pdf;base64,{base64}\n" +
            $"[PDF metadata: mime=application/pdf, bytes={buffer.Length}, pageCount={pageCount?.ToString() ?? "unknown"}, pages={pagesLabel}, path={paths.ToVirtual(full)}]";

        return HarnessToolExecuteResult.FromOutput(
            output.Length > MaxOutputChars ? output[..MaxOutputChars] + "\n…(输出已截断)" : output);
    }

    private static string ReadText(JsonElement args, string full, SandboxPathResolver paths)
    {
        var offset = args.TryGetProperty("offset", out var offEl) && offEl.TryGetInt32(out var o) ? Math.Max(1, o) : 1;
        var limit = args.TryGetProperty("limit", out var limEl) && limEl.TryGetInt32(out var l)
            ? Math.Clamp(l, 1, DefaultLineLimit)
            : DefaultLineLimit;

        var lines = File.ReadAllLines(full, Encoding.UTF8);
        var total = lines.Length;
        var startIdx = Math.Min(offset - 1, Math.Max(0, total - 1));
        var endIdx = Math.Min(startIdx + limit, total);
        var slice = lines.AsSpan(startIdx, endIdx - startIdx);

        var sb = new StringBuilder();
        sb.AppendLine($"File: {paths.ToVirtual(full)} (lines {startIdx + 1}-{endIdx} of {total})");
        for (var i = 0; i < slice.Length; i++)
        {
            var lineNo = startIdx + i + 1;
            var line = slice[i];
            if (line.Length > MaxLineLength)
                line = line[..MaxLineLength] + "…";
            sb.Append(lineNo.ToString().PadLeft(6)).Append('|').AppendLine(line);
        }

        if (endIdx < total)
            sb.AppendLine($"\n…({total - endIdx} more lines; use offset={endIdx + 1} to continue)");

        var text = sb.ToString();
        return text.Length > MaxOutputChars ? text[..MaxOutputChars] + "\n…(输出已截断)" : text;
    }

    internal static int? CountPdfPages(byte[] buffer)
    {
        try
        {
            var content = Encoding.Latin1.GetString(buffer);
            var matches = System.Text.RegularExpressions.Regex.Matches(content, @"/Type\s*/Page\b(?!s)");
            return matches.Count;
        }
        catch
        {
            return null;
        }
    }

    public static PageRange? ParsePageRange(string input)
    {
        var trimmed = input.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;

        if (trimmed.Contains('-'))
        {
            var parts = trimmed.Split('-', 2);
            if (!int.TryParse(parts[0].Trim(), out var start) || !int.TryParse(parts[1].Trim(), out var end))
                return null;
            if (start < 1 || end < start)
                return null;
            return new PageRange(start, end, end - start + 1);
        }

        if (int.TryParse(trimmed, out var single) && single >= 1)
            return new PageRange(single, single, 1);

        return null;
    }

    public sealed record PageRange(int Start, int End, int Count);

    private static bool IsImageExtension(string ext) =>
        ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".tif" or ".tiff" or ".svg" or ".ico" or ".avif";

    private static string GetImageMimeType(string ext) => ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".tif" or ".tiff" => "image/tiff",
        ".svg" => "image/svg+xml",
        ".ico" => "image/x-icon",
        ".avif" => "image/avif",
        _ => "image/png"
    };

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
