using System.Text.Json;
using System.Text.Json.Serialization;
using DeepSeekBrowser.Services;

namespace DeepSeekBrowser.Services.Harness.Graph;

public sealed class HarnessGraphCheckpointState
{
    [JsonPropertyName("threadId")]
    public string ThreadId { get; set; } = "";

    [JsonPropertyName("graphId")]
    public string GraphId { get; set; } = "";

    [JsonPropertyName("currentNodeId")]
    public string? CurrentNodeId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "running";

    [JsonPropertyName("variables")]
    public Dictionary<string, object?> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("runId")]
    public string? RunId { get; set; }

    [JsonPropertyName("userPrompt")]
    public string? UserPrompt { get; set; }

    [JsonPropertyName("lastAnswer")]
    public string? LastAnswer { get; set; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("updatedUtc")]
    public string UpdatedUtc { get; set; } = DateTime.UtcNow.ToString("O");

    public bool IsPaused =>
        string.Equals(Status, "paused", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Status, "interrupted", StringComparison.OrdinalIgnoreCase);
}

public static class HarnessGraphCheckpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string ThreadsRoot =>
        Path.Combine(AgentDesktopConfigSync.HomeDirectory, "threads");

    public static string GetPath(string threadId) =>
        Path.Combine(ThreadsRoot, threadId, "checkpoint.json");

    public static void Save(HarnessGraphCheckpointState state)
    {
        state.UpdatedUtc = DateTime.UtcNow.ToString("O");
        var dir = Path.Combine(ThreadsRoot, state.ThreadId);
        Directory.CreateDirectory(dir);
        File.WriteAllText(GetPath(state.ThreadId), JsonSerializer.Serialize(state, JsonOptions));
    }

    public static HarnessGraphCheckpointState? Load(string threadId)
    {
        if (string.IsNullOrWhiteSpace(threadId)) return null;
        var path = GetPath(threadId);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<HarnessGraphCheckpointState>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static void Clear(string threadId)
    {
        if (string.IsNullOrWhiteSpace(threadId)) return;
        var dir = Path.Combine(ThreadsRoot, threadId);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    public static IReadOnlyList<HarnessGraphCheckpointState> ListPaused(int max = 20)
    {
        if (!Directory.Exists(ThreadsRoot))
            return Array.Empty<HarnessGraphCheckpointState>();

        var list = new List<HarnessGraphCheckpointState>();
        foreach (var dir in Directory.EnumerateDirectories(ThreadsRoot))
        {
            var cp = Load(Path.GetFileName(dir));
            if (cp is not null && cp.IsPaused)
                list.Add(cp);
            if (list.Count >= max) break;
        }

        return list.OrderByDescending(c => c.UpdatedUtc).ToList();
    }
}
