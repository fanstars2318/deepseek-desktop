using System.Text.Json;
using System.Text.Json.Serialization;
using DeepSeekBrowser.Services.Harness.Interop;

namespace DeepSeekBrowser.Services.Harness.Interop;

public sealed class HarnessSkillCatalogEntry
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string Source { get; init; } = "";
    public string FilePath { get; init; } = "";
    public List<string> Tags { get; init; } = [];
}

public static class HarnessSkillCatalogIndexer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string IndexPath =>
        Path.Combine(AgentDesktopConfigSync.HomeDirectory, "skills", "index.json");

    public static HarnessSkillCatalogIndexResult Reindex(string? workspaceRoot, IReadOnlyList<string>? extraRoots = null)
    {
        var entries = new List<HarnessSkillCatalogEntry>();
        var skipped = 0;
        foreach (var root in HarnessInteropPaths.SkillScanRoots(workspaceRoot, extraRoots))
        {
            if (!Directory.Exists(root)) continue;
            var source = InferSource(root);
            foreach (var file in Directory.EnumerateFiles(root, "SKILL.md", SearchOption.AllDirectories))
            {
                try
                {
                    var skill = HarnessSkillParser.ParseFile(file, source);
                    if (string.IsNullOrWhiteSpace(skill.Id)
                        || string.IsNullOrWhiteSpace(skill.Description))
                    {
                        skipped++;
                        continue;
                    }

                    entries.Add(new HarnessSkillCatalogEntry
                    {
                        Id = skill.Id,
                        Name = skill.Name,
                        Description = skill.Description,
                        Source = source,
                        FilePath = skill.FilePath,
                        Tags = ExtractTags(skill)
                    });
                }
                catch
                {
                    skipped++;
                }
            }
        }

        var deduped = entries
            .GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var index = new HarnessSkillCatalogIndexFile
        {
            UpdatedUtc = DateTime.UtcNow.ToString("O"),
            Count = deduped.Count,
            Skipped = skipped,
            Skills = deduped
        };

        Directory.CreateDirectory(Path.GetDirectoryName(IndexPath)!);
        File.WriteAllText(IndexPath, JsonSerializer.Serialize(index, JsonOptions));
        return new HarnessSkillCatalogIndexResult { Count = deduped.Count, Skipped = skipped };
    }

    public static Task<HarnessSkillCatalogIndexResult> ReindexAsync(
        string? workspaceRoot,
        IReadOnlyList<string>? extraRoots = null,
        Action<HarnessSkillReindexProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = Reindex(workspaceRoot, extraRoots);
            progress?.Invoke(new HarnessSkillReindexProgress
            {
                Scanned = result.Count + result.Skipped,
                Indexed = result.Count,
                Skipped = result.Skipped,
                Done = true
            });
            return result;
        }, cancellationToken);

    public static IReadOnlyList<HarnessSkillCatalogEntry> Search(string query, int limit = 20)
    {
        if (!File.Exists(IndexPath))
            return Array.Empty<HarnessSkillCatalogEntry>();

        HarnessSkillCatalogIndexFile? index;
        try
        {
            index = JsonSerializer.Deserialize<HarnessSkillCatalogIndexFile>(File.ReadAllText(IndexPath), JsonOptions);
        }
        catch
        {
            return Array.Empty<HarnessSkillCatalogEntry>();
        }

        if (index?.Skills is null || index.Skills.Count == 0)
            return Array.Empty<HarnessSkillCatalogEntry>();

        if (string.IsNullOrWhiteSpace(query))
            return index.Skills.Take(limit).ToList();

        var q = query.Trim();
        return index.Skills
            .Where(s =>
                s.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                || s.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (s.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || s.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .Take(limit)
            .ToList();
    }

    private static List<string> ExtractTags(HarnessSkill skill)
    {
        var tags = new List<string> { skill.Source };
        if (!string.IsNullOrWhiteSpace(skill.Description))
        {
            foreach (var word in skill.Description.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (word.Length >= 4 && char.IsLetter(word[0]))
                    tags.Add(word.Trim('.', ',', ':', ';').ToLowerInvariant());
            }
        }

        return tags.Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();
    }

    private static string InferSource(string root)
    {
        var n = root.Replace('\\', '/').ToLowerInvariant();
        if (n.Contains("antigravity")) return "antigravity";
        if (n.Contains("awesome-claude")) return "awesome-claude";
        if (n.Contains("/.cursor/")) return "cursor";
        if (n.Contains("/.claude/")) return "claude";
        if (n.Contains("/.deepseek/")) return "deepseek";
        return "extra";
    }

    private sealed class HarnessSkillCatalogIndexFile
    {
        public string UpdatedUtc { get; set; } = "";
        public int Count { get; set; }
        public int Skipped { get; set; }
        public List<HarnessSkillCatalogEntry> Skills { get; set; } = [];
    }
}

public sealed class HarnessSkillCatalogIndexResult
{
    public int Count { get; init; }
    public int Skipped { get; init; }
}

public sealed class HarnessSkillReindexProgress
{
    public int Scanned { get; init; }
    public int Indexed { get; init; }
    public int Skipped { get; init; }
    public bool Done { get; init; }
}
