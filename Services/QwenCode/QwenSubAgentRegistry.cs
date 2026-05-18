using System.IO;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.QwenCode;

/// <summary>发现 <c>.qwen/agents/*.md</c> 与 <c>~/.qwen/agents/</c>（对齐官方 Subagents）。</summary>
public static class QwenSubAgentRegistry
{
    public static IReadOnlyList<QwenSubAgentDefinition> Discover(AppConfig config)
    {
        var map = new Dictionary<string, QwenSubAgentDefinition>(StringComparer.OrdinalIgnoreCase);
        var workspace = QwenCodeBuiltinTools.ResolveWorkspaceRoot(config);

        ScanDir(Path.Combine(workspace, ".qwen", "agents"), "project", map);
        ScanDir(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".qwen", "agents"), "user", map);

        return map.Values.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static QwenSubAgentDefinition? Find(AppConfig config, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return Discover(config).FirstOrDefault(a =>
            a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static void ScanDir(string root, string scope, Dictionary<string, QwenSubAgentDefinition> map)
    {
        if (!Directory.Exists(root)) return;

        foreach (var file in Directory.EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var def = Parse(File.ReadAllText(file), file, scope);
                if (def is not null)
                    map[def.Name] = def;
            }
            catch
            {
                // skip
            }
        }
    }

    private static QwenSubAgentDefinition? Parse(string raw, string path, string scope)
    {
        var (meta, body) = QwenMarkdownFrontmatter.Parse(raw);
        var name = meta.TryGetValue("name", out var n)
            ? n
            : Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(name)) return null;

        return new QwenSubAgentDefinition
        {
            Name = name.Trim(),
            Description = meta.TryGetValue("description", out var d) ? d.Trim() : "",
            SystemPrompt = body,
            SourcePath = path,
            Scope = scope,
            ApprovalMode = meta.TryGetValue("approvalMode", out var am) ? am.Trim() : "auto-edit",
            AllowedTools = QwenMarkdownFrontmatter.GetList(meta, "tools"),
            DisallowedTools = QwenMarkdownFrontmatter.GetList(meta, "disallowedTools")
        };
    }

    public static string FormatCatalog(IReadOnlyList<QwenSubAgentDefinition> agents)
    {
        if (agents.Count == 0)
            return "（未发现 Subagent 配置。可在 `.qwen/agents/<name>.md` 添加 YAML frontmatter + 系统提示）";

        var sb = new System.Text.StringBuilder();
        foreach (var a in agents)
            sb.AppendLine($"- {a.Name} [{a.Scope}]: {a.Description}");
        return sb.ToString().TrimEnd();
    }
}
