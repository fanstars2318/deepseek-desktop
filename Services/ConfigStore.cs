using System.IO;
using System.Text.Json;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services;

public static class ConfigStore
{
    private static string ConfigDir => DeepSeekDesktopApp.LocalAppDataRoot;

    public static string ConfigDirectory => ConfigDir;
    public static string ConfigFilePath => Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
                return new AppConfig();

            var json = File.ReadAllText(ConfigFilePath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        var temp = ConfigFilePath + ".tmp";
        File.WriteAllText(temp, json);
        if (File.Exists(ConfigFilePath))
            File.Replace(temp, ConfigFilePath, null);
        else
            File.Move(temp, ConfigFilePath);
    }
}
