using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness.Memory;

public sealed class HarnessEmbeddingClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly AppConfig _config;
    private readonly HttpClient _http;
    private readonly Dictionary<string, float[]> _cache = new(StringComparer.Ordinal);

    public HarnessEmbeddingClient(AppConfig config, HttpClient? http = null)
    {
        _config = config;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
    }

    public async Task<float[]?> EmbedAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var hash = ComputeHash(text);
        if (_cache.TryGetValue(hash, out var cached))
            return cached;

        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var model = string.IsNullOrWhiteSpace(_config.AgentEmbeddingModel)
            ? "text-embedding-3-small"
            : _config.AgentEmbeddingModel;

        var url = ResolveBaseUrl() + "/v1/embeddings";
        var body = new { model, input = text };
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            return null;

        var vector = ParseEmbedding(json);
        if (vector is not null)
            _cache[hash] = vector;
        return vector;
    }

    public static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private string ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(_config.AgentEmbeddingApiKey))
            return _config.AgentEmbeddingApiKey;
        if (!string.IsNullOrWhiteSpace(_config.AgentApiKey))
            return _config.AgentApiKey;
        return _config.DeepSeekApiKey ?? "";
    }

    private string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(_config.AgentEmbeddingApiBaseUrl))
            return _config.AgentEmbeddingApiBaseUrl.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(_config.AgentApiBaseUrl))
            return _config.AgentApiBaseUrl.TrimEnd('/');
        return (_config.ApiBaseUrl ?? "https://api.deepseek.com").TrimEnd('/');
    }

    private static float[]? ParseEmbedding(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
            return null;
        var first = data[0];
        if (!first.TryGetProperty("embedding", out var emb) || emb.ValueKind != JsonValueKind.Array)
            return null;
        var list = new List<float>();
        foreach (var v in emb.EnumerateArray())
            list.Add(v.GetSingle());
        return list.Count == 0 ? null : list.ToArray();
    }

    public void Dispose() => _http.Dispose();
}
