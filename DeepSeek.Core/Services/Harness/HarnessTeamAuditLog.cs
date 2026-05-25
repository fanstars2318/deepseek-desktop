using System.Text.Json;

namespace DeepSeekBrowser.Services.Harness;

/// <summary>MetaGPT-style JSONL audit trail for multi-agent runs.</summary>
public sealed class HarnessTeamAuditLog
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly string _filePath;
    private readonly object _gate = new();

    public HarnessTeamAuditLog(string? sessionId)
    {
        var dir = Path.Combine(AgentDesktopConfigSync.HomeDirectory, "team-audit");
        Directory.CreateDirectory(dir);
        var key = string.IsNullOrWhiteSpace(sessionId) ? "ephemeral" : Sanitize(sessionId);
        _filePath = Path.Combine(dir, key + ".jsonl");
    }

    public void Append(string eventType, object payload)
    {
        var entry = new
        {
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            type = eventType,
            payload
        };
        var line = JsonSerializer.Serialize(entry, JsonOptions);
        lock (_gate)
        {
            File.AppendAllText(_filePath, line + Environment.NewLine);
        }
    }

    public string FilePath => _filePath;

    private static string Sanitize(string id) =>
        new string(id.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
}
