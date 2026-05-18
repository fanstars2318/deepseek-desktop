using System.IO;
using System.Text.Json;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.QwenCode;

public static class QwenCodeSettingsStore
{
    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeepSeekEdge",
        "qwen-code.settings.json");

    public static QwenCodeSettings Load() =>
        File.Exists(SettingsPath)
            ? JsonSerializer.Deserialize<QwenCodeSettings>(File.ReadAllText(SettingsPath)) ?? new()
            : new();

    public static void Save(QwenCodeSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void ApplyToAppConfig(QwenCodeSettings q, AppConfig app)
    {
        app.EnableQwenCodeBuiltinTools = q.EnableBuiltinTools;
        app.QwenCodeAllowShell = q.AllowShell;
        app.QwenCodeApprovalMode = q.ApprovalMode;
        app.QwenCodeWorkspaceRoot = q.WorkspaceRoot;
        app.DefaultAgentStrategy = q.DefaultStrategy;
        app.MaxAgentSteps = Math.Clamp(q.MaxAgentSteps, 1, 100);
        app.QwenCodeAutoApproveReadOnly = q.ApprovalMode is "smart" or "readonly";
        app.EnableQwenCodeWebFetch = q.EnableWebFetch;
        app.EnableAdaptiveOutputEscalation = q.EnableAdaptiveOutputEscalation;
    }

    public static QwenCodeSettings FromAppConfig(AppConfig app) => new()
    {
        EnableBuiltinTools = app.EnableQwenCodeBuiltinTools,
        EnableWebFetch = app.EnableQwenCodeWebFetch,
        AllowShell = app.QwenCodeAllowShell,
        ApprovalMode = app.QwenCodeApprovalMode,
        WorkspaceRoot = app.QwenCodeWorkspaceRoot,
        DefaultStrategy = app.DefaultAgentStrategy,
        MaxAgentSteps = app.MaxAgentSteps,
        EnableAdaptiveOutputEscalation = app.EnableAdaptiveOutputEscalation
    };
}
