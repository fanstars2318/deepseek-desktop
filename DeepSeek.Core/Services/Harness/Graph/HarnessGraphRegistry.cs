using DeepSeekBrowser.Services;

namespace DeepSeekBrowser.Services.Harness.Graph;

public static class HarnessGraphRegistry
{
    private static readonly object Gate = new();
    private static Dictionary<string, (HarnessGraphDefinition Graph, HarnessGraphSummary Summary)> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static long _loadedStamp;

    public static IReadOnlyList<HarnessGraphSummary> List(string? workspaceRoot = null)
    {
        RefreshIfNeeded(workspaceRoot);
        return _cache.Values.Select(x => x.Summary).OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool TryGet(string id, string? workspaceRoot, out HarnessGraphDefinition? graph)
    {
        RefreshIfNeeded(workspaceRoot);
        if (_cache.TryGetValue(id, out var entry))
        {
            graph = entry.Graph;
            return true;
        }

        graph = null;
        return false;
    }

    public static void InvalidateCache() => Interlocked.Exchange(ref _loadedStamp, 0);

    private static void RefreshIfNeeded(string? workspaceRoot)
    {
        var stamp = ComputeStamp(workspaceRoot);
        lock (Gate)
        {
            if (stamp == _loadedStamp && _cache.Count > 0)
                return;
            _cache = LoadAll(workspaceRoot);
            _loadedStamp = stamp;
        }
    }

    private static long ComputeStamp(string? workspaceRoot)
    {
        long hash = 0;
        foreach (var dir in GetSearchDirectories(workspaceRoot))
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is not ".json" and not ".yaml" and not ".yml") continue;
                hash ^= File.GetLastWriteTimeUtc(file).Ticks;
            }
        }

        return hash;
    }

    private static Dictionary<string, (HarnessGraphDefinition, HarnessGraphSummary)> LoadAll(string? workspaceRoot)
    {
        var map = new Dictionary<string, (HarnessGraphDefinition, HarnessGraphSummary)>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in GetSearchDirectories(workspaceRoot).Reverse())
        {
            if (!Directory.Exists(dir)) continue;
            var source = dir.StartsWith(AgentDesktopConfigSync.HomeDirectory, StringComparison.OrdinalIgnoreCase)
                ? "user"
                : "workspace";

            foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is not ".json" and not ".yaml" and not ".yml") continue;
                try
                {
                    var graph = HarnessGraphParser.ParseFile(file);
                    var summary = new HarnessGraphSummary
                    {
                        Id = graph.Id,
                        Version = graph.Version,
                        NodeCount = graph.Nodes.Count,
                        EdgeCount = graph.Edges.Count,
                        Source = source,
                        FilePath = file
                    };
                    map[graph.Id] = (graph, summary);
                }
                catch
                {
                    // skip corrupt graph
                }
            }
        }

        return map;
    }

    private static IEnumerable<string> GetSearchDirectories(string? workspaceRoot)
    {
        yield return Path.Combine(AgentDesktopConfigSync.HomeDirectory, "graphs");
        if (!string.IsNullOrWhiteSpace(workspaceRoot))
            yield return Path.Combine(workspaceRoot, ".deepseek", "graphs");
    }
}
