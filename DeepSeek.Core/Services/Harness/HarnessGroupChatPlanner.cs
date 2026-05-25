using System.Text.Json;
using System.Text.RegularExpressions;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;

namespace DeepSeekBrowser.Services.Harness;

/// <summary>AutoGen 式：根据并行探索汇总选择下一子 Agent 角色。</summary>
public static class HarnessGroupChatPlanner
{
    private static readonly string[] AllowedRoles = ["explore", "implementer", "review", "plan"];

    public static async Task<string> PickNextRoleAsync(
        IAgentWebChat chat,
        AppConfig config,
        string userPrompt,
        string exploreSummary,
        CancellationToken ct)
    {
        var model = string.IsNullOrWhiteSpace(config.Model) ? AgentModeHelper.AgentModel : config.Model;
        var messages = new List<ChatMessage>
        {
            new()
            {
                Role = "system",
                Content =
                    "Pick the next sub-agent role after parallel exploration. Reply with ONE word only: explore, implementer, review, or plan."
            },
            new()
            {
                Role = "user",
                Content = "User task:\n" + userPrompt + "\n\nExplore summary:\n" + Truncate(exploreSummary, 3000)
            }
        };

        try
        {
            var result = await chat.CompleteAsync(
                messages, model, false, false, Array.Empty<string>(), false, ct);
            var pick = NormalizeRole(result.Content);
            if (pick is not null) return pick;
        }
        catch
        {
            // fallback
        }

        return userPrompt.Contains("review", StringComparison.OrdinalIgnoreCase) ? "review" : "implementer";
    }

    private static string? NormalizeRole(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim().ToLowerInvariant();
        foreach (var r in AllowedRoles)
        {
            if (t == r || t.Contains(r, StringComparison.Ordinal)) return r;
        }

        return Regex.Match(t, @"\b(explore|implementer|review|plan)\b").Success
            ? Regex.Match(t, @"\b(explore|implementer|review|plan)\b").Value
            : null;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
