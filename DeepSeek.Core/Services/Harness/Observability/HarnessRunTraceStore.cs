using System.Text.Json;

namespace DeepSeekBrowser.Services.Harness.Observability;

public static class HarnessRunTraceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<HarnessRunMeta> ListRuns(string workspaceRoot, int maxCount = 50)
    {
        var runsDir = Path.Combine(workspaceRoot, ".deepseek", "runs");
        if (!Directory.Exists(runsDir))
            return Array.Empty<HarnessRunMeta>();

        var metas = new List<HarnessRunMeta>();
        foreach (var dir in Directory.EnumerateDirectories(runsDir).OrderByDescending(Directory.GetCreationTimeUtc))
        {
            var metaPath = Path.Combine(dir, "meta.json");
            if (!File.Exists(metaPath))
                continue;
            try
            {
                var json = File.ReadAllText(metaPath);
                var meta = JsonSerializer.Deserialize<HarnessRunMeta>(json, JsonOptions);
                if (meta is not null)
                    metas.Add(meta);
            }
            catch
            {
                // skip corrupt meta
            }

            if (metas.Count >= maxCount)
                break;
        }

        return metas;
    }

    public static HarnessRunTraceLoadResult? LoadRun(string workspaceRoot, string runId, int maxSpans = 200)
    {
        if (string.IsNullOrWhiteSpace(runId))
            return null;

        var runDir = Path.Combine(workspaceRoot, ".deepseek", "runs", runId);
        if (!Directory.Exists(runDir))
            return null;

        HarnessRunMeta? meta = null;
        var metaPath = Path.Combine(runDir, "meta.json");
        if (File.Exists(metaPath))
        {
            try
            {
                meta = JsonSerializer.Deserialize<HarnessRunMeta>(File.ReadAllText(metaPath), JsonOptions);
            }
            catch
            {
                // ignore
            }
        }

        var spans = new List<HarnessTraceSpan>();
        var tracePath = Path.Combine(runDir, "trace.jsonl");
        if (File.Exists(tracePath))
        {
            foreach (var line in File.ReadLines(tracePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var span = JsonSerializer.Deserialize<HarnessTraceSpan>(line, JsonOptions);
                    if (span is not null)
                        spans.Add(span);
                }
                catch
                {
                    // skip bad line
                }
            }

            if (spans.Count > maxSpans)
                spans = spans.TakeLast(maxSpans).ToList();
        }

        return new HarnessRunTraceLoadResult
        {
            RunId = runId,
            Meta = meta,
            Spans = spans
        };
    }

    public static void PruneOldRuns(string workspaceRoot, int retentionDays)
    {
        if (retentionDays <= 0) return;
        var runsDir = Path.Combine(workspaceRoot, ".deepseek", "runs");
        if (!Directory.Exists(runsDir)) return;

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        foreach (var dir in Directory.EnumerateDirectories(runsDir))
        {
            try
            {
                var metaPath = Path.Combine(dir, "meta.json");
                var refTime = File.Exists(metaPath)
                    ? File.GetLastWriteTimeUtc(metaPath)
                    : Directory.GetLastWriteTimeUtc(dir);
                if (refTime < cutoff)
                    Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // best effort
            }
        }
    }
}

public sealed class HarnessRunTraceLoadResult
{
    public string RunId { get; init; } = "";
    public HarnessRunMeta? Meta { get; init; }
    public IReadOnlyList<HarnessTraceSpan> Spans { get; init; } = Array.Empty<HarnessTraceSpan>();
}
