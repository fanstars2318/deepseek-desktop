using System.Text.Json;

namespace DeepSeekBrowser.Services.ApiManagement;

public sealed class ProviderAccountRecord
{
    public string Id { get; set; } = "";
    public string ProviderId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public Dictionary<string, string> Credentials { get; set; } = new();
    public string Status { get; set; } = "active";
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    public long LastUsed { get; set; }
    public int RequestCount { get; set; }
    public int TodayUsed { get; set; }
    public int? DailyLimit { get; set; }
    public string TodayKey { get; set; } = "";
}

public static class ProviderAccountStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static string FilePath => Path.Combine(ConfigStore.ConfigDirectory, "provider-accounts.json");

    public static List<ProviderAccountRecord> Load()
    {
        if (!File.Exists(FilePath))
            return new List<ProviderAccountRecord>();
        try
        {
            return JsonSerializer.Deserialize<List<ProviderAccountRecord>>(File.ReadAllText(FilePath), JsonOptions)
                   ?? new List<ProviderAccountRecord>();
        }
        catch
        {
            return new List<ProviderAccountRecord>();
        }
    }

    public static void Save(List<ProviderAccountRecord> accounts)
    {
        Directory.CreateDirectory(ConfigStore.ConfigDirectory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(accounts, JsonOptions));
    }

    public static ProviderAccountRecord Add(string providerId, string name, Dictionary<string, string> credentials)
    {
        var list = Load();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var rec = new ProviderAccountRecord
        {
            Id = "acc-" + Guid.NewGuid().ToString("N")[..10],
            ProviderId = providerId,
            Name = name,
            Credentials = credentials,
            Status = credentials.Values.Any(v => !string.IsNullOrWhiteSpace(v)) ? "active" : "inactive",
            CreatedAt = now,
            UpdatedAt = now,
            LastUsed = now
        };
        list.Add(rec);
        Save(list);
        return rec;
    }

    public static ProviderAccountRecord? FindById(string accountId) =>
        Load().FirstOrDefault(a => string.Equals(a.Id, accountId, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<ProviderAccountRecord> ByProvider(string providerId) =>
        Load().Where(a => string.Equals(a.ProviderId, providerId, StringComparison.OrdinalIgnoreCase)).ToList();

    public static bool Delete(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return false;
        var list = Load();
        var removed = list.RemoveAll(a => string.Equals(a.Id, accountId, StringComparison.OrdinalIgnoreCase));
        if (removed <= 0) return false;
        Save(list);
        return true;
    }

    public static void RecordSuccess(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return;
        var list = Load();
        var rec = list.FirstOrDefault(a => string.Equals(a.Id, accountId, StringComparison.OrdinalIgnoreCase));
        if (rec is null) return;

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (!string.Equals(rec.TodayKey, today, StringComparison.Ordinal))
        {
            rec.TodayKey = today;
            rec.TodayUsed = 0;
        }

        rec.LastUsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        rec.RequestCount++;
        rec.TodayUsed++;
        rec.UpdatedAt = rec.LastUsed;
        Save(list);
        AccountLoadBalancer.Instance.ClearAccountFailure(accountId);
    }

    public static void RecordFailure(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return;
        AccountLoadBalancer.Instance.MarkAccountFailed(accountId);
    }
}
