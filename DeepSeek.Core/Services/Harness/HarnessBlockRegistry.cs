using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeepSeekBrowser.Services.Harness;

public sealed class HarnessBlock
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "prompt";
    public string? Description { get; set; }
    public string? Prompt { get; set; }
    public string? Tool { get; set; }
    public string? Command { get; set; }
    public string? SkillId { get; set; }
    public string? GraphId { get; set; }
}

public static class HarnessBlockRegistry
{
    private static readonly object Gate = new();
    private static Dictionary<string, HarnessBlock> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static long _stamp;

    public static IReadOnlyList<HarnessBlock> List(string? workspaceRoot = null)
    {
        Refresh(workspaceRoot);
        return _cache.Values.OrderBy(b => b.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool TryGet(string id, string? workspaceRoot, out HarnessBlock? block)
    {
        Refresh(workspaceRoot);
        if (_cache.TryGetValue(id, out var b))
        {
            block = b;
            return true;
        }

        block = null;
        return false;
    }

    public static void InvalidateCache() => Interlocked.Exchange(ref _stamp, 0);

    private static void Refresh(string? workspaceRoot)
    {
        var stamp = ComputeStamp(workspaceRoot);
        lock (Gate)
        {
            if (stamp == _stamp && _cache.Count > 0) return;
            _cache = LoadAll(workspaceRoot);
            _stamp = stamp;
        }
    }

    private static long ComputeStamp(string? workspaceRoot)
    {
        long hash = 0;
        foreach (var dir in BlockDirectories(workspaceRoot))
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
                hash ^= File.GetLastWriteTimeUtc(file).Ticks;
        }

        return hash;
    }

    private static IEnumerable<string> BlockDirectories(string? workspaceRoot)
    {
        yield return Path.Combine(AgentDesktopConfigSync.HomeDirectory, "blocks");
        if (!string.IsNullOrWhiteSpace(workspaceRoot))
            yield return Path.Combine(workspaceRoot, ".deepseek", "blocks");
    }

    private static Dictionary<string, HarnessBlock> LoadAll(string? workspaceRoot)
    {
        var map = new Dictionary<string, HarnessBlock>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in BlockDirectories(workspaceRoot))
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is not (".json" or ".yaml" or ".yml")) continue;
                try
                {
                    var block = ParseFile(file);
                    if (string.IsNullOrWhiteSpace(block.Id)) continue;
                    map[block.Id] = block;
                }
                catch
                {
                    // skip
                }
            }
        }

        return map;
    }

    private static HarnessBlock ParseFile(string path)
    {
        var text = File.ReadAllText(path);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".yaml" or ".yml")
        {
            var root = SimpleYamlReader.ReadDocument(text);
            return new HarnessBlock
            {
                Id = root.TryGetValue("id", out var id) ? id?.ToString() ?? "" : "",
                Type = root.TryGetValue("type", out var t) ? t?.ToString() ?? "prompt" : "prompt",
                Description = root.TryGetValue("description", out var d) ? d?.ToString() : null,
                Prompt = root.TryGetValue("prompt", out var p) ? p?.ToString() : null,
                Tool = root.TryGetValue("tool", out var tool) ? tool?.ToString() : null,
                Command = root.TryGetValue("command", out var cmd) ? cmd?.ToString() : null,
                SkillId = root.TryGetValue("skill", out var sk) ? sk?.ToString() : null,
                GraphId = root.TryGetValue("graph", out var g) ? g?.ToString() : null
            };
        }

        return JsonSerializer.Deserialize<HarnessBlock>(text, JsonOptions)
               ?? throw new InvalidDataException("Block JSON empty");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };
}
