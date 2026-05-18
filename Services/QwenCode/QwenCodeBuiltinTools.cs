using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.QwenCode;

/// <summary>
/// Qwen Code Core 内置工具（对应 packages/core/src/tools/ 文件、Shell、搜索能力）。
/// 工具描述格式对齐 Qwen-main/examples/react_demo.py 的 TOOL_DESC。
/// </summary>
public static class QwenCodeBuiltinTools
{
    /// <summary>旧版工具名前缀，仍接受以便兼容历史对话。</summary>
    public const string LegacyPrefix = "builtin__";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static IReadOnlyList<AgentToolDescriptor> GetDescriptors(AppConfig config)
    {
        if (!config.EnableQwenCodeBuiltinTools)
            return [];

        var tools = new List<AgentToolDescriptor>
        {
            Desc("read_file",
                "读取工作区内文本文件内容（只读）。",
                """{"type":"object","properties":{"path":{"type":"string","description":"相对工作区或绝对路径"}},"required":["path"]}"""),
            Desc("write_file",
                "写入或覆盖工作区内文本文件（需用户批准）。",
                """{"type":"object","properties":{"path":{"type":"string"},"content":{"type":"string"}},"required":["path","content"]}"""),
            Desc("list_directory",
                "列出目录下的文件与子目录（只读）。",
                """{"type":"object","properties":{"path":{"type":"string","description":"目录路径，默认工作区根"},"recursive":{"type":"boolean"}}}"""),
            Desc("glob",
                "按 glob 模式查找文件（只读），如 **/*.cs。",
                """{"type":"object","properties":{"pattern":{"type":"string"},"max_results":{"type":"integer"}},"required":["pattern"]}"""),
            Desc("grep_search",
                "在工作区内搜索文本/正则（只读）。",
                """{"type":"object","properties":{"pattern":{"type":"string"},"path":{"type":"string"},"max_matches":{"type":"integer"}},"required":["pattern"]}"""),
            Desc("run_shell_command",
                "执行 Shell 命令（需用户批准；工作目录为工作区根）。",
                """{"type":"object","properties":{"command":{"type":"string"},"timeout_seconds":{"type":"integer"}},"required":["command"]}"""),
            Desc("edit",
                "在文件中替换一段文本（需用户批准）。",
                """{"type":"object","properties":{"path":{"type":"string"},"old_string":{"type":"string"},"new_string":{"type":"string"}},"required":["path","old_string","new_string"]}"""),
            Desc("web_fetch",
                "获取 URL 的文本内容（只读，有长度限制）。",
                """{"type":"object","properties":{"url":{"type":"string"}},"required":["url"]}""")
        };

        if (!config.EnableQwenCodeWebFetch)
            tools.RemoveAll(d => d.ExposedName == "web_fetch");

        return tools;
    }

    public static async Task<string> ExecuteAsync(
        string exposedName,
        string argumentsJson,
        AppConfig config,
        ToolApprovalService approval,
        CancellationToken ct)
    {
        var tool = QwenCodePort.NormalizeToolName(exposedName);
        if (tool is null || !QwenCodePort.OfficialCoreToolNames.Contains(tool))
            throw new InvalidOperationException("非内置工具: " + exposedName);
        var args = ParseArgs(argumentsJson);
        var root = ResolveWorkspaceRoot(config);

        return tool switch
        {
            "read_file" => await ReadFileAsync(root, args, approval, config, ct),
            "write_file" => await WriteFileAsync(root, args, approval, config, ct),
            "list_directory" => await ListDirectoryAsync(root, args, approval, config, ct),
            "glob" => await GlobFilesAsync(root, args, approval, config, ct),
            "grep_search" => await GrepContentAsync(root, args, approval, config, ct),
            "run_shell_command" => await RunShellAsync(root, args, approval, config, ct),
            "edit" => await EditFileAsync(root, args, approval, config, ct),
            "web_fetch" => await WebFetchAsync(args, approval, config, ct),
            _ => "ERROR: 未知内置工具 " + tool
        };
    }

    public static ToolRisk GetRisk(string exposedName)
    {
        var tool = QwenCodePort.NormalizeToolName(exposedName);
        if (tool is null)
            return ToolRisk.ReadOnly;

        return tool switch
        {
            "write_file" or "edit" => ToolRisk.Write,
            "run_shell_command" => ToolRisk.Execute,
            "web_fetch" => ToolRisk.ReadOnly,
            _ => ToolRisk.ReadOnly
        };
    }

    private static AgentToolDescriptor Desc(string name, string description, string schema) =>
        new()
        {
            ExposedName = name,
            Description = "[Qwen Code Core] " + description,
            ParametersJson = schema
        };

    public static string ResolveWorkspaceRoot(AppConfig config)
    {
        var root = config.QwenCodeWorkspaceRoot?.Trim();
        if (string.IsNullOrWhiteSpace(root))
            root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.GetFullPath(root);
    }

    private static Dictionary<string, JsonElement> ParseArgs(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                   ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string GetString(Dictionary<string, JsonElement> args, string key, string defaultValue = "")
    {
        if (!args.TryGetValue(key, out var el))
            return defaultValue;
        return el.ValueKind == JsonValueKind.String ? el.GetString() ?? defaultValue : el.ToString();
    }

    private static bool GetBool(Dictionary<string, JsonElement> args, string key, bool defaultValue = false)
    {
        if (!args.TryGetValue(key, out var el))
            return defaultValue;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(el.GetString(), out var b) && b,
            _ => defaultValue
        };
    }

    private static int GetInt(Dictionary<string, JsonElement> args, string key, int defaultValue)
    {
        if (!args.TryGetValue(key, out var el))
            return defaultValue;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
            return n;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var p))
            return p;
        return defaultValue;
    }

    private static string ResolveSafePath(string root, string path)
    {
        var full = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(root, path));
        var rootFull = Path.GetFullPath(root);
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("路径超出工作区: " + path);
        return full;
    }

    private static async Task<string> ReadFileAsync(
        string root,
        Dictionary<string, JsonElement> args,
        ToolApprovalService approval,
        AppConfig config,
        CancellationToken ct)
    {
        var path = GetString(args, "path");
        if (string.IsNullOrWhiteSpace(path))
            return "ERROR: 缺少 path";

        var full = ResolveSafePath(root, path);
        if (!File.Exists(full))
            return "ERROR: 文件不存在: " + path;

        if (!await approval.EnsureApprovedAsync("read_file", full, ToolRisk.ReadOnly, config, ct))
            return "ERROR: 用户拒绝执行 read_file";

        var text = await File.ReadAllTextAsync(full, ct);
        if (text.Length > config.QwenCodeMaxFileReadChars)
            text = text[..config.QwenCodeMaxFileReadChars] + "\n…(已截断)";
        return text;
    }

    private static async Task<string> WriteFileAsync(
        string root,
        Dictionary<string, JsonElement> args,
        ToolApprovalService approval,
        AppConfig config,
        CancellationToken ct)
    {
        var path = GetString(args, "path");
        var content = GetString(args, "content");
        if (string.IsNullOrWhiteSpace(path))
            return "ERROR: 缺少 path";

        var full = ResolveSafePath(root, path);
        var preview = content.Length > 200 ? content[..200] + "…" : content;
        if (!await approval.EnsureApprovedAsync("write_file", $"{full}\n---\n{preview}", ToolRisk.Write, config, ct))
            return "ERROR: 用户拒绝写入";

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, content, ct);
        return $"已写入 {path}（{content.Length} 字符）";
    }

    private static async Task<string> ListDirectoryAsync(
        string root,
        Dictionary<string, JsonElement> args,
        ToolApprovalService approval,
        AppConfig config,
        CancellationToken ct)
    {
        var rel = GetString(args, "path", ".");
        var full = ResolveSafePath(root, rel);
        if (!Directory.Exists(full))
            return "ERROR: 目录不存在: " + rel;

        if (!await approval.EnsureApprovedAsync("list_directory", full, ToolRisk.ReadOnly, config, ct))
            return "ERROR: 用户拒绝 list_directory";

        var recursive = GetBool(args, "recursive");
        var lines = new List<string>();
        if (recursive)
        {
            foreach (var f in Directory.EnumerateFileSystemEntries(full, "*", SearchOption.AllDirectories))
            {
                lines.Add(Path.GetRelativePath(root, f).Replace('\\', '/'));
                if (lines.Count >= 500) break;
            }
        }
        else
        {
            foreach (var f in Directory.EnumerateFileSystemEntries(full))
                lines.Add(Path.GetRelativePath(root, f).Replace('\\', '/'));
        }

        return string.Join("\n", lines);
    }

    private static async Task<string> GlobFilesAsync(
        string root,
        Dictionary<string, JsonElement> args,
        ToolApprovalService approval,
        AppConfig config,
        CancellationToken ct)
    {
        var pattern = GetString(args, "pattern");
        if (string.IsNullOrWhiteSpace(pattern))
            return "ERROR: 缺少 pattern";

        if (!await approval.EnsureApprovedAsync("glob", pattern, ToolRisk.ReadOnly, config, ct))
            return "ERROR: 用户拒绝 glob_files";

        var max = Math.Clamp(GetInt(args, "max_results", 100), 1, 500);
        var lines = new List<string>();
        foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, f).Replace('\\', '/');
            if (GlobMatch(rel, pattern))
                lines.Add(rel);
            if (lines.Count >= max) break;
        }

        return lines.Count == 0 ? "（无匹配）" : string.Join("\n", lines);
    }

    private static bool GlobMatch(string path, string pattern)
    {
        var regex = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*/", "(.*/)?")
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*")
            .Replace(@"\?", ".") + "$";
        return Regex.IsMatch(path, regex, RegexOptions.IgnoreCase);
    }

    private static async Task<string> GrepContentAsync(
        string root,
        Dictionary<string, JsonElement> args,
        ToolApprovalService approval,
        AppConfig config,
        CancellationToken ct)
    {
        var pattern = GetString(args, "pattern");
        if (string.IsNullOrWhiteSpace(pattern))
            return "ERROR: 缺少 pattern";

        var subPath = GetString(args, "path", ".");
        var searchRoot = ResolveSafePath(root, subPath);
        if (!await approval.EnsureApprovedAsync("grep_search", $"{pattern} @ {searchRoot}", ToolRisk.ReadOnly, config, ct))
            return "ERROR: 用户拒绝 grep_content";

        Regex re;
        try
        {
            re = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(2));
        }
        catch
        {
            re = new Regex(Regex.Escape(pattern), RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        var max = Math.Clamp(GetInt(args, "max_matches", 80), 1, 300);
        var hits = new List<string>();
        if (!Directory.Exists(searchRoot))
            return "ERROR: 路径不存在";

        foreach (var file in Directory.EnumerateFiles(searchRoot, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            if (hits.Count >= max) break;
            try
            {
                var info = new FileInfo(file);
                if (info.Length > 512 * 1024) continue;
                var lines = await File.ReadAllLinesAsync(file, ct);
                for (var i = 0; i < lines.Length && hits.Count < max; i++)
                {
                    if (re.IsMatch(lines[i]))
                        hits.Add($"{Path.GetRelativePath(root, file).Replace('\\', '/')}:{i + 1}: {lines[i].Trim()}");
                }
            }
            catch
            {
                // skip unreadable
            }
        }

        return hits.Count == 0 ? "（无匹配）" : string.Join("\n", hits);
    }

    private static async Task<string> RunShellAsync(
        string root,
        Dictionary<string, JsonElement> args,
        ToolApprovalService approval,
        AppConfig config,
        CancellationToken ct)
    {
        var command = GetString(args, "command");
        if (string.IsNullOrWhiteSpace(command))
            return "ERROR: 缺少 command";

        if (!config.QwenCodeAllowShell)
            return "ERROR: Shell 已在设置中禁用";

        if (!await approval.EnsureApprovedAsync("run_shell_command", command, ToolRisk.Execute, config, ct))
            return "ERROR: 用户拒绝执行 Shell";

        var timeout = Math.Clamp(GetInt(args, "timeout_seconds", 60), 5, 300);
        using var proc = new System.Diagnostics.Process();
        proc.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c " + command,
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        proc.Start();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(cts.Token);
        var stderrTask = proc.StandardError.ReadToEndAsync(cts.Token);
        try
        {
            await proc.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            return "ERROR: Shell 超时";
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var sb = new StringBuilder();
        sb.AppendLine($"exit={proc.ExitCode}");
        if (!string.IsNullOrWhiteSpace(stdout))
            sb.AppendLine(stdout.TrimEnd());
        if (!string.IsNullOrWhiteSpace(stderr))
            sb.AppendLine("stderr: " + stderr.TrimEnd());
        var text = sb.ToString().Trim();
        if (text.Length > config.QwenCodeMaxShellOutputChars)
            text = text[..config.QwenCodeMaxShellOutputChars] + "\n…(已截断)";
        return text;
    }

    private static async Task<string> EditFileAsync(
        string root,
        Dictionary<string, JsonElement> args,
        ToolApprovalService approval,
        AppConfig config,
        CancellationToken ct)
    {
        var path = GetString(args, "path");
        var oldText = GetString(args, "old_string");
        var newText = GetString(args, "new_string");
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrEmpty(oldText))
            return "ERROR: 缺少 path 或 old_string";

        var full = ResolveSafePath(root, path);
        if (!File.Exists(full))
            return "ERROR: 文件不存在: " + path;

        var preview = $"替换 {path}\n旧: {TruncateInline(oldText, 80)}\n新: {TruncateInline(newText, 80)}";
        if (!await approval.EnsureApprovedAsync("edit", preview, ToolRisk.Write, config, ct))
            return "ERROR: 用户拒绝 edit_file";

        var content = await File.ReadAllTextAsync(full, ct);
        if (!content.Contains(oldText, StringComparison.Ordinal))
            return "ERROR: old_string 在文件中未找到";

        await File.WriteAllTextAsync(full, content.Replace(oldText, newText), ct);
        return $"已编辑 {path}";
    }

    private static async Task<string> WebFetchAsync(
        Dictionary<string, JsonElement> args,
        ToolApprovalService approval,
        AppConfig config,
        CancellationToken ct)
    {
        if (!config.EnableQwenCodeWebFetch)
            return "ERROR: web_fetch 已在设置中禁用";

        var url = GetString(args, "url");
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not "http" and not "https")
            return "ERROR: 无效 URL";

        if (!await approval.EnsureApprovedAsync("web_fetch", url, ToolRisk.ReadOnly, config, ct))
            return "ERROR: 用户拒绝 web_fetch";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            req.Headers.TryAddWithoutValidation("User-Agent", "DeepSeek-Edge-QwenCode/1.0");
            using var resp = await Http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            var head = $"HTTP {(int)resp.StatusCode}\n";
            var text = head + body;
            if (text.Length > config.QwenCodeMaxFileReadChars)
                text = text[..config.QwenCodeMaxFileReadChars] + "\n…(已截断)";
            return text;
        }
        catch (Exception ex)
        {
            return "ERROR: " + ex.Message;
        }
    }

    private static string TruncateInline(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
