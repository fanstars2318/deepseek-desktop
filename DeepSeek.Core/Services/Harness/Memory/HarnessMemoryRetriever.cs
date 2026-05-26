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
        var records = _store.Search(vector, scopes, topK);
        if (records.Count == 0)
            return Array.Empty<string>();

        var maxChars = Math.Clamp(config.AgentSemanticMemoryMaxChars, 500, 20_000);
        var result = new List<string>();
        var used = 0;
        foreach (var r in records)
        {
            var line = "- " + r.Text.Trim();
            if (used + line.Length > maxChars)
                break;
            result.Add(line);
            used += line.Length + 1;
        }

        return result;
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
