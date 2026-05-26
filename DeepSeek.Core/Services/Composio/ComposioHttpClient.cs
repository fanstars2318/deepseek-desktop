using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DeepSeekBrowser.Services.Composio;

public sealed class ComposioHttpClient : IDisposable
{
    public const string ApiBase = "https://backend.composio.dev/api/v3";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http;

    public ComposioHttpClient(string apiKey, HttpClient? http = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Composio API key is required.", nameof(apiKey));

        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(ApiBase + "/tools?limit=1", ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<ComposioToolDefinition>> GetToolsAsync(
        string? entityId,
        IReadOnlyList<string>? toolkitSlugs,
        int limit,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var query = new List<string> { "limit=" + limit };
        if (!string.IsNullOrWhiteSpace(entityId))
            query.Add("user_id=" + Uri.EscapeDataString(entityId));
        if (toolkitSlugs is { Count: > 0 })
        {
            foreach (var slug in toolkitSlugs.Where(s => !string.IsNullOrWhiteSpace(s)))
                query.Add("toolkit_slug=" + Uri.EscapeDataString(slug.Trim()));
        }

        var url = ApiBase + "/tools?" + string.Join('&', query);
        using var resp = await _http.GetAsync(url, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Composio list tools failed ({(int)resp.StatusCode}): {Truncate(json, 400)}");

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return Array.Empty<ComposioToolDefinition>();

        var list = new List<ComposioToolDefinition>();
        foreach (var item in items.EnumerateArray())
        {
            var slug = item.TryGetProperty("slug", out var s) ? s.GetString() : null;
            if (string.IsNullOrWhiteSpace(slug)) continue;
            var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? slug : slug;
            var desc = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            object? schema = null;
            if (item.TryGetProperty("input_parameters", out var ip) && ip.ValueKind == JsonValueKind.Object)
                schema = JsonSerializer.Deserialize<object>(ip.GetRawText(), JsonOptions);
            list.Add(new ComposioToolDefinition(slug, name, desc, schema));
        }

        return list;
    }

    public async Task<string> ExecuteActionAsync(
        string toolSlug,
        string argumentsJson,
        string entityId,
        CancellationToken ct = default)
    {
        object? argsObj;
        try
        {
            argsObj = JsonSerializer.Deserialize<object>(
                string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson, JsonOptions);
        }
        catch
        {
            argsObj = new Dictionary<string, object?>();
        }

        var body = new Dictionary<string, object?>
        {
            ["user_id"] = string.IsNullOrWhiteSpace(entityId) ? "default" : entityId,
            ["arguments"] = argsObj ?? new Dictionary<string, object?>()
        };

        var url = ApiBase + "/tools/execute/" + Uri.EscapeDataString(toolSlug);
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };
        using var resp = await _http.SendAsync(req, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            return "ERROR: Composio execute failed (" + (int)resp.StatusCode + "): " + Truncate(json, 500);

        return FormatExecuteResult(json);
    }

    private static string FormatExecuteResult(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("data", out var data))
                return data.ValueKind == JsonValueKind.String ? data.GetString() ?? json : data.GetRawText();
            if (root.TryGetProperty("successful", out var ok) && ok.ValueKind == JsonValueKind.False)
            {
                var err = root.TryGetProperty("error", out var e) ? e.GetRawText() : json;
                return "ERROR: " + err;
            }
        }
        catch
        {
            // return raw
        }

        return json.Length <= 8000 ? json : json[..8000] + "…";
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    public void Dispose() => _http.Dispose();
}

public sealed record ComposioToolDefinition(string Slug, string Name, string Description, object? InputSchema);
