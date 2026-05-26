namespace DeepSeekBrowser.Services.Harness.Interop;

public static class HarnessSkillRegistry
{
    private static readonly object Gate = new();
    private static Dictionary<string, (HarnessSkill Skill, HarnessSkillSummary Summary)> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static long _loadedStamp;

    public static IReadOnlyList<HarnessSkillSummary> List(string? workspaceRoot = null, IReadOnlyList<string>? extraRoots = null)
    {
        RefreshIfNeeded(workspaceRoot, extraRoots);
        return _cache.Values.Select(x => x.Summary).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool TryGet(string id, string? workspaceRoot, out HarnessSkill? skill, IReadOnlyList<string>? extraRoots = null)
    {
        RefreshIfNeeded(workspaceRoot, extraRoots);
        if (_cache.TryGetValue(id, out var entry))
        {
            skill = entry.Skill;
            return true;
        }

        skill = null;
        return false;
    }

    public static void InvalidateCache() => Interlocked.Exchange(ref _loadedStamp, 0);

    private static void RefreshIfNeeded(string? workspaceRoot, IReadOnlyList<string>? extraRoots = null)
    {
        var stamp = ComputeStamp(workspaceRoot, extraRoots);
        lock (Gate)
        {
            if (stamp == _loadedStamp && _cache.Count > 0)
                return;
            _cache = LoadAll(workspaceRoot, extraRoots);
            _loadedStamp = stamp;
        }
    }

    private static long ComputeStamp(string? workspaceRoot, IReadOnlyList<string>? extraRoots)
    {
        long hash = 0;
        foreach (var root in HarnessInteropPaths.SkillScanRoots(workspaceRoot, extraRoots))
        {
            if (!Directory.Exists(root)) continue;
            foreach (var file in Directory.EnumerateFiles(root, "SKILL.md", SearchOption.AllDirectories))
                hash ^= File.GetLastWriteTimeUtc(file).Ticks;
        }

        return hash;
    }

    private static Dictionary<string, (HarnessSkill, HarnessSkillSummary)> LoadAll(
        string? workspaceRoot,
        IReadOnlyList<string>? extraRoots)
    {
        var map = new Dictionary<string, (HarnessSkill, HarnessSkillSummary)>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in HarnessInteropPaths.SkillScanRoots(workspaceRoot, extraRoots))
        {
            if (!Directory.Exists(root)) continue;
            var source = InferSource(root);

            foreach (var file in Directory.EnumerateFiles(root, "SKILL.md", SearchOption.AllDirectories))
            {
                try
                {
                    var skill = HarnessSkillParser.ParseFile(file, source);
                    if (string.IsNullOrWhiteSpace(skill.Id)) continue;

                    var summary = new HarnessSkillSummary
                    {
                        Id = skill.Id,
                        Name = skill.Name,
                        Description = skill.Description,
                        Source = source,
                        FilePath = skill.FilePath
                    };
                    map[skill.Id] = (skill, summary);
                }
                catch
                {
                    // 跳过损坏 skill
                }
            }
        }

        return map;
    }

    private static string InferSource(string root)
    {
        var n = root.Replace('\\', '/').ToLowerInvariant();
        if (n.Contains("/.cursor/")) return "cursor";
        if (n.Contains("/.claude/")) return "claude";
        if (n.Contains("/.agents/")) return "agents";
        if (n.Contains("/.deepseek/")) return "deepseek";
        if (n.Contains("/.codex/")) return "codex";
        return "project";
    }
}
