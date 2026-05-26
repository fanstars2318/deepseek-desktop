using System.Text;
using System.Text.Json;
using DeepSeekBrowser.Services.Harness.Sandbox;

namespace DeepSeekBrowser.Services.Harness;

public static class HarnessEditFileTool
{
    public static string Execute(JsonElement args, SandboxPathResolver paths)
    {
        var path = GetString(args, "file_path") ?? GetString(args, "path")
                   ?? throw new ArgumentException("edit 需要 file_path");
        var oldString = GetString(args, "old_string") ?? "";
        var newString = GetString(args, "new_string") ?? "";
        var replaceAll = args.TryGetProperty("replace_all", out var ra) && ra.ValueKind == JsonValueKind.True;

        string full;
        try
        {
            full = paths.ResolveWrite(path);
        }
        catch (Exception ex)
        {
            return "ERROR: " + ex.Message;
        }

        if (!File.Exists(full))
            return "ERROR: 文件不存在: " + path;

        var text = File.ReadAllText(full, Encoding.UTF8);
        if (!text.Contains(oldString, StringComparison.Ordinal))
            return "ERROR: old_string 未在文件中找到";

        string updated;
        if (replaceAll)
        {
            updated = text.Replace(oldString, newString, StringComparison.Ordinal);
        }
        else
        {
            var index = text.IndexOf(oldString, StringComparison.Ordinal);
            updated = text[..index] + newString + text[(index + oldString.Length)..];
        }

        File.WriteAllText(full, updated, Encoding.UTF8);
        return "已编辑 " + paths.ToVirtual(full);
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
