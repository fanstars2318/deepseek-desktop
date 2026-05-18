using System.IO;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.QwenCode;

/// <summary>发现 <c>.qwen/skills</c>、<c>~/.qwen/skills</c> 与 npm bundled Skills（对齐官方 Qwen Code）。</summary>
public static class QwenSkillRegistry
{
    public static IReadOnlyList<QwenSkillDefinition> Discover(AppConfig config)
    {
        var map = new Dictionary<string, QwenSkillDefinition>(StringComparer.OrdinalIgnoreCase);
        var workspace = QwenCodeBuiltinTools.ResolveWorkspaceRoot(config);

        ScanDir(Path.Combine(workspace, ".qwen", "skills"), "project", map);
        ScanDir(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".qwen", "skills"), "user", map);

        if (config.EnableQwenBundledSkills)
            ScanDir(Path.Combine(QwenCodePort.DefaultNpmPackageDir, "bundled"), "bundled", map);

        return map.Values.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static QwenSkillDefinition? Find(AppConfig config, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return Discover(config).FirstOrDefault(s =>
            s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static void ScanDir(string root, string scope, Dictionary<string, QwenSkillDefinition> map)
    {
        if (!Directory.Exists(root)) return;

        foreach (var skillDir in Directory.EnumerateDirectories(root))
        {
            var skillMd = Path.Combine(skillDir, "SKILL.md");
            if (!File.Exists(skillMd)) continue;
            try
            {
                var def = Parse(File.ReadAllText(skillMd), skillMd, scope);
                if (def is not null)
                    map[def.Name] = def;
            }
            catch
            {
                // skip invalid skill
            }
        }

    }

    private static QwenSkillDefinition? Parse(string raw, string path, string scope)
    {
        var (meta, body) = QwenMarkdownFrontmatter.Parse(raw);
        var name = meta.TryGetValue("name", out var n) ? n : Path.GetFileName(Path.GetDirectoryName(path) ?? "");
        if (string.IsNullOrWhiteSpace(name)) return null;

        var desc = meta.TryGetValue("description", out var d) ? d : "";
        return new QwenSkillDefinition
        {
            Name = name.Trim(),
            Description = desc.Trim(),
            Body = body,
            SourcePath = path,
            Scope = scope,
            AllowedTools = QwenMarkdownFrontmatter.GetList(meta, "allowedTools")
        };
    }

    public static string FormatCatalog(IReadOnlyList<QwenSkillDefinition> skills)
    {
        if (skills.Count == 0)
            return "（未发现 Skills。可在工作区 `.qwen/skills/<name>/SKILL.md` 或 `~/.qwen/skills/` 添加）";

        var sb = new System.Text.StringBuilder();
        foreach (var s in skills)
            sb.AppendLine($"- {s.Name} [{s.Scope}]: {s.Description}");
        return sb.ToString().TrimEnd();
    }
}
