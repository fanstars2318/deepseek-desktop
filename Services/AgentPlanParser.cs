using System.Text.Json;
using System.Text.RegularExpressions;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services;

internal static class AgentPlanParser
{
    public static AgentPlan Parse(string? content)
    {
        var plan = new AgentPlan();
        if (string.IsNullOrWhiteSpace(content))
            return plan;

        var json = ExtractJsonObject(content);
        if (string.IsNullOrWhiteSpace(json))
            return FallbackFromMarkdown(content);

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("steps", out var stepsEl)
                || stepsEl.ValueKind != JsonValueKind.Array)
                return FallbackFromMarkdown(content);

            foreach (var stepEl in stepsEl.EnumerateArray())
            {
                var step = new AgentPlanStep
                {
                    Id = stepEl.TryGetProperty("id", out var idEl)
                        ? idEl.ToString()
                        : (plan.Steps.Count + 1).ToString(),
                    Title = GetString(stepEl, "title") ?? GetString(stepEl, "name") ?? "步骤",
                    Description = GetString(stepEl, "description") ?? GetString(stepEl, "task") ?? ""
                };

                step.SubAgentName = GetString(stepEl, "subagent")
                                    ?? GetString(stepEl, "subagent_name")
                                    ?? GetString(stepEl, "subagent_type");

                if (stepEl.TryGetProperty("tool_hints", out var hints) && hints.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in hints.EnumerateArray())
                    {
                        var s = h.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                            step.ToolHints.Add(s);
                    }
                }
                else if (stepEl.TryGetProperty("tools", out var tools) && tools.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in tools.EnumerateArray())
                    {
                        var s = h.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                            step.ToolHints.Add(s);
                    }
                }

                if (!string.IsNullOrWhiteSpace(step.Description))
                    plan.Steps.Add(step);
            }
        }
        catch
        {
            return FallbackFromMarkdown(content);
        }

        return plan;
    }

    private static AgentPlan FallbackFromMarkdown(string content)
    {
        var plan = new AgentPlan();
        var matches = Regex.Matches(content,
            @"(?:^|\n)\s*(?:\d+[\.\)、]|[-*])\s*(.+?)(?=\n\s*(?:\d+[\.\)、]|[-*])|\n*$)",
            RegexOptions.Singleline);

        var i = 0;
        foreach (Match m in matches)
        {
            var line = m.Groups[1].Value.Trim();
            if (line.Length < 4) continue;
            plan.Steps.Add(new AgentPlanStep
            {
                Id = (++i).ToString(),
                Title = line.Length > 60 ? line[..60] + "…" : line,
                Description = line
            });
        }

        return plan;
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string? ExtractJsonObject(string text)
    {
        var fence = Regex.Match(text, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```", RegexOptions.IgnoreCase);
        if (fence.Success)
            return fence.Groups[1].Value.Trim();

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text[start..(end + 1)];
        return null;
    }
}
