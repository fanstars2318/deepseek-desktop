namespace DeepSeekBrowser.Services.Harness;

using DeepSeekBrowser.Models;

/// <summary>Prompt / context 字符与 token 预算工具（减少重复注入与过长系统提示）。</summary>
public static class HarnessPromptBudget
{
    public static string TrimMcpCatalog(string? catalog, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(catalog) || maxLines <= 0)
            return catalog ?? "";

        var lines = catalog.Split('\n');
        if (lines.Length <= maxLines + 2)
            return catalog.TrimEnd();

        var sb = new System.Text.StringBuilder();
        var kept = 0;
        foreach (var line in lines)
        {
            if (kept >= maxLines && line.TrimStart().StartsWith('-'))
                continue;
            sb.AppendLine(line);
            if (line.TrimStart().StartsWith('-'))
                kept++;
        }

        if (kept >= maxLines)
            sb.AppendLine("…(MCP 工具列表已截断，调用名须与已连接工具完全一致)");

        return sb.ToString().TrimEnd();
    }

    public static bool IsLikelyCasualPrompt(string? prompt) =>
        HarnessRunIntentPlanner.IsCasualOnly(prompt);

    public static int DefaultSkillMaxChars(AppConfig config) =>
        Math.Clamp(config.AgentSkillMaxChars, 800, 20_000);

    /// <summary>有意图且开启精简模式时，系统提示不再重复注入完整工具目录。</summary>
    public static bool ShouldSkipToolCatalog(AppConfig config, HarnessRunIntent? intent, bool includeXmlTools) =>
        config.AgentPromptMinimalMode
        && intent is not null
        && intent.PlannedTools.Count > 0
        && includeXmlTools;

    public static bool UseMinimalIntentSection(AppConfig config) => config.AgentPromptMinimalMode;

    public static int IntentPlannedToolLimit(AppConfig config) =>
        config.AgentPromptMinimalMode ? 3 : 5;
}
