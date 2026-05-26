using System.Text.Json;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;

namespace DeepSeekBrowser.Services.Harness.Memory;

public sealed class HarnessMemoryExtractor
{
    private readonly HarnessSemanticMemoryStore _store;
    private readonly HarnessEmbeddingClient _embeddings;
    private readonly Func<IAgentWebChat> _chatFactory;

    public HarnessMemoryExtractor(
        HarnessSemanticMemoryStore store,
        HarnessEmbeddingClient embeddings,
        Func<IAgentWebChat> chatFactory)
    {
        _store = store;
        _embeddings = embeddings;
        _chatFactory = chatFactory;
    }

    public async Task<int> ExtractAfterRunAsync(
        AppConfig config,
        string userPrompt,
        string answer,
        string? workspaceRoot,
        string? sessionId,
        CancellationToken ct)
    {
        if (!config.AgentSemanticMemoryEnabled || !config.AgentSemanticMemoryAutoExtract)
            return 0;

        var chat = _chatFactory();
        var model = string.IsNullOrWhiteSpace(config.Model) ? AgentModeHelper.AgentModel : config.Model;
        var extractPrompt = """
            Extract durable memories from this agent conversation. Return ONLY JSON array:
            [{"kind":"fact|preference|pitfall","text":"..."}]
            Max 5 items. Skip trivial greetings. User prompt and answer:
            """ + "\nUSER:\n" + Truncate(userPrompt, 1500) + "\n\nASSISTANT:\n" + Truncate(answer, 2500);

        WebChatResult result;
        try
        {
            result = await chat.CompleteAsync(
                [new ChatMessage { Role = "user", Content = extractPrompt }],
                model,
                thinking: false,
                search: false,
                Array.Empty<string>(),
                allowToolCalls: false,
                ct);
        }
        catch
        {
            return 0;
        }

        var items = ParseItems(result.Content);
        if (items.Count == 0)
            return 0;

        var scopes = new List<string> { "user" };
        if (!string.IsNullOrWhiteSpace(workspaceRoot))
            scopes.Add(HarnessMemoryRetriever.WorkspaceScope(workspaceRoot));
        if (!string.IsNullOrWhiteSpace(sessionId))
            scopes.Add(HarnessMemoryRetriever.SessionScope(sessionId));

        var written = 0;
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Text)) continue;
            var vector = await _embeddings.EmbedAsync(item.Text, ct);
            if (vector is null || vector.Length == 0) continue;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var hash = HarnessEmbeddingClient.ComputeHash(item.Text);
            foreach (var scope in scopes)
            {
                var draft = new HarnessMemoryRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Scope = scope,
                    Text = item.Text.Trim(),
                    Embedding = vector,
                    ContentHash = hash,
                    MetadataJson = JsonSerializer.Serialize(new { kind = item.Kind }),
                    CreatedAtUnix = now,
                    UpdatedAtUnix = now
                };
                var record = HarnessMemoryDeduper.PrepareInsert(_store, draft, vector);
                _store.AddOrUpdate(record);
                written++;
            }
        }

        return written;
    }

    private static List<ExtractItem> ParseItems(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return [];
        var json = content.Trim();
        var start = json.IndexOf('[');
        var end = json.LastIndexOf(']');
        if (start < 0 || end <= start) return [];
        json = json[start..(end + 1)];
        try
        {
            return JsonSerializer.Deserialize<List<ExtractItem>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private sealed class ExtractItem
    {
        public string Kind { get; set; } = "fact";
        public string Text { get; set; } = "";
    }
}
