using System.IO;
using System.Text.Json;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services;

/// <summary>
/// Agent 模式对话持久化：%LocalAppData%\DeepSeekEdge\agent-sessions\
/// </summary>
public sealed class AgentSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _rootDir;
    private readonly string _indexPath;

    public AgentSessionStore()
    {
        _rootDir = Path.Combine(ConfigStore.ConfigDirectory, "agent-sessions");
        _indexPath = Path.Combine(_rootDir, "index.json");
        Directory.CreateDirectory(_rootDir);
    }

    public string StorageDirectory => _rootDir;

    public IReadOnlyList<AgentSessionMeta> ListMetas()
    {
        var index = LoadIndex();
        return index.OrderByDescending(m => m.UpdatedAt).ToList();
    }

    public AgentSessionFile? Load(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var path = SessionPath(id);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AgentSessionFile>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(AgentSessionFile session)
    {
        if (string.IsNullOrWhiteSpace(session.Id))
            session.Id = "s_" + Guid.NewGuid().ToString("N")[..12];

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (session.CreatedAt <= 0) session.CreatedAt = now;
        session.UpdatedAt = now;

        var path = SessionPath(session.Id);
        var json = JsonSerializer.Serialize(session, JsonOptions);
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        if (File.Exists(path))
            File.Replace(temp, path, null);
        else
            File.Move(temp, path);

        var size = new FileInfo(path).Length;
        UpsertIndex(new AgentSessionMeta
        {
            Id = session.Id,
            Title = string.IsNullOrWhiteSpace(session.Title) ? "新对话" : session.Title,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            SizeBytes = size
        });
    }

    public void Delete(IEnumerable<string> ids)
    {
        var idSet = ids.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.Ordinal);
        if (idSet.Count == 0) return;

        var index = LoadIndex();
        index.RemoveAll(m => idSet.Contains(m.Id));
        SaveIndex(index);

        foreach (var id in idSet)
        {
            try
            {
                var path = SessionPath(id);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { /* ignore */ }
        }
    }

    public (long TotalBytes, int Count) GetStats()
    {
        var index = LoadIndex();
        return (index.Sum(m => m.SizeBytes), index.Count);
    }

    public IReadOnlyList<string> ApplyRetentionPolicy(AppConfig config)
    {
        var deleted = new List<string>();
        var index = LoadIndex();
        if (index.Count == 0) return deleted;

        var now = DateTimeOffset.UtcNow;

        if (config.AgentSessionRetentionDays > 0)
        {
            var cutoff = now.AddDays(-config.AgentSessionRetentionDays).ToUnixTimeMilliseconds();
            var byAge = index.Where(m => m.UpdatedAt < cutoff).Select(m => m.Id).ToList();
            if (byAge.Count > 0)
            {
                Delete(byAge);
                deleted.AddRange(byAge);
                index = LoadIndex();
            }
        }

        var maxBytes = config.AgentSessionMaxStorageGb > 0
            ? (long)(config.AgentSessionMaxStorageGb * 1024 * 1024 * 1024)
            : 0;

        if (maxBytes > 0)
        {
            var total = index.Sum(m => m.SizeBytes);
            var ordered = index.OrderBy(m => m.UpdatedAt).ToList();
            var toRemove = new List<string>();
            foreach (var meta in ordered)
            {
                if (total <= maxBytes) break;
                toRemove.Add(meta.Id);
                total -= meta.SizeBytes;
            }

            if (toRemove.Count > 0)
            {
                Delete(toRemove);
                deleted.AddRange(toRemove);
            }
        }

        return deleted;
    }

    public void ImportLegacySessions(IEnumerable<AgentSessionFile> sessions)
    {
        foreach (var s in sessions)
        {
            if (string.IsNullOrWhiteSpace(s.Id)) continue;
            Save(s);
        }
    }

    private string SessionPath(string id)
    {
        var safe = string.Concat(id.Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(_rootDir, safe + ".json");
    }

    private List<AgentSessionMeta> LoadIndex()
    {
        try
        {
            if (!File.Exists(_indexPath)) return new List<AgentSessionMeta>();
            var json = File.ReadAllText(_indexPath);
            return JsonSerializer.Deserialize<List<AgentSessionMeta>>(json, JsonOptions) ?? new List<AgentSessionMeta>();
        }
        catch
        {
            return new List<AgentSessionMeta>();
        }
    }

    private void SaveIndex(List<AgentSessionMeta> index)
    {
        var json = JsonSerializer.Serialize(index, JsonOptions);
        var temp = _indexPath + ".tmp";
        File.WriteAllText(temp, json);
        if (File.Exists(_indexPath))
            File.Replace(temp, _indexPath, null);
        else
            File.Move(temp, _indexPath);
    }

    private void UpsertIndex(AgentSessionMeta meta)
    {
        var index = LoadIndex();
        var i = index.FindIndex(m => m.Id == meta.Id);
        if (i >= 0) index[i] = meta;
        else index.Add(meta);
        SaveIndex(index);
    }
}
