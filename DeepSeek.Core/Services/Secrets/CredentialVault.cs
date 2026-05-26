using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DeepSeekBrowser.Services;

/// <summary>Windows DPAPI 保护 ~/.deepseek/credentials.dat。</summary>
public static class CredentialVault
{
    private static readonly object Lock = new();
    private static Dictionary<string, string>? _cache;

    private static string FilePath => Path.Combine(ConfigStore.ConfigDirectory, "credentials.dat");

    public static bool TryGet(string providerId, string keyName, out string? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(providerId) || string.IsNullOrWhiteSpace(keyName))
            return false;

        var map = Load();
        return map.TryGetValue(Key(providerId, keyName), out value);
    }

    public static string? TryGet(string providerId, string keyName)
    {
        TryGet(providerId, keyName, out var v);
        return v;
    }

    public static void Set(string providerId, string keyName, string value)
    {
        var map = Load();
        map[Key(providerId, keyName)] = value;
        Save(map);
    }

    private static string Key(string providerId, string keyName) =>
        providerId.Trim().ToLowerInvariant() + "::" + keyName.Trim().ToLowerInvariant();

    private static Dictionary<string, string> Load()
    {
        lock (Lock)
        {
            if (_cache is not null)
                return _cache;

            if (!File.Exists(FilePath))
                return _cache = new Dictionary<string, string>(StringComparer.Ordinal);

            try
            {
                var protectedBytes = File.ReadAllBytes(FilePath);
                var plain = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(plain);
                return _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                                ?? new Dictionary<string, string>(StringComparer.Ordinal);
            }
            catch
            {
                return _cache = new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }
    }

    private static void Save(Dictionary<string, string> map)
    {
        lock (Lock)
        {
            _cache = map;
            Directory.CreateDirectory(ConfigStore.ConfigDirectory);
            var json = JsonSerializer.Serialize(map);
            var plain = Encoding.UTF8.GetBytes(json);
            var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, protectedBytes);
        }
    }
}
