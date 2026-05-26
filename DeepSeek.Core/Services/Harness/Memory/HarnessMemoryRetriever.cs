using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness.Memory;

public sealed class HarnessMemoryRetriever
{
    private readonly HarnessSemanticMemoryStore _store;
    private readonly HarnessEmbeddingClient _embeddings;

    public HarnessMemoryRetriever(HarnessSemanticMemoryStore store, HarnessEmbeddingClient embeddings)
    {
        _store = store;
        _embeddings = embeddings;
    }

    public async Task<IReadOnlyList<string>> RetrieveAsync(
        AppConfig config,
        string userPrompt,
        string? workspaceRoot,
        string? sessionId,
        CancellationToken ct)
    {
        if (!config.AgentSemanticMemoryEnabled)
            return Array.Empty<string>();

        var vector = await _embeddings.EmbedAsync(userPrompt, ct);
        if (vector is null || vector.Length == 0)
            return Array.Empty<string>();

        var scopes = BuildScopes(workspaceRoot, sessionId);
        var topK = Math.Clamp(config.AgentSemanticMemoryTopK, 1, 32);
        var records = _store.ListByScopes(scopes, 500);
        if (records.Count == 0)
            return Array.Empty<string>();

        var semanticHits = _store.Search(vector, scopes, topK);
        var bm25Index = HarnessBm25Index.Build(records.Select(r => (r.Id, r.Text)).ToList());
        var bm25Hits = bm25Index.Rank(userPrompt, topK);

        var fused = FuseHits(records, semanticHits, bm25Hits, topK);
        if (fused.Count == 0)
            return Array.Empty<string>();

        var maxChars = Math.Clamp(config.AgentSemanticMemoryMaxChars, 500, 20_000);
        var result = new List<string>();
        var used = 0;
        foreach (var r in fused)
        {
            var line = "- " + r.Text.Trim();
            if (used + line.Length > maxChars)
                break;
            result.Add(line);
            used += line.Length + 1;
        }

        return result;
    }

    internal static List<HarnessMemoryRecord> FuseHits(
        IReadOnlyList<HarnessMemoryRecord> allRecords,
        IReadOnlyList<HarnessMemoryRecord> semantic,
        IReadOnlyList<(string Id, double Score)> bm25,
        int topK)
    {
        var byId = allRecords.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var semScores = semantic
            .Select((r, i) => (r.Id, Score: Math.Max(0.1, 1.0 - i * 0.08)))
            .ToDictionary(x => x.Id, x => x.Score);
        var maxBm25 = bm25.Count > 0 ? bm25.Max(x => x.Score) : 1.0;
        if (maxBm25 <= 0) maxBm25 = 1.0;

        var bm25Norm = bm25.ToDictionary(x => x.Id, x => x.Score / maxBm25);
        var ids = semScores.Keys.Union(bm25Norm.Keys).ToList();
        return ids
            .Select(id =>
            {
                var sem = semScores.GetValueOrDefault(id);
                var bm = bm25Norm.GetValueOrDefault(id);
                return (Id: id, Fused: 0.7 * sem + 0.3 * bm);
            })
            .Where(x => byId.ContainsKey(x.Id))
            .OrderByDescending(x => x.Fused)
            .Take(topK)
            .Select(x => byId[x.Id])
            .ToList();
    }

    private static List<string> BuildScopes(string? workspaceRoot, string? sessionId)
    {
        var scopes = new List<string> { "user" };
        if (!string.IsNullOrWhiteSpace(workspaceRoot))
            scopes.Add("workspace:" + NormalizeScopeKey(workspaceRoot));
        if (!string.IsNullOrWhiteSpace(sessionId))
            scopes.Add("session:" + sessionId);
        return scopes;
    }

    internal static string WorkspaceScope(string workspaceRoot) =>
        "workspace:" + NormalizeScopeKey(workspaceRoot);

    internal static string SessionScope(string sessionId) => "session:" + sessionId;

    private static string NormalizeScopeKey(string path) =>
        path.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
}
