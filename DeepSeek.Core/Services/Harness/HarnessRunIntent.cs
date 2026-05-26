namespace DeepSeekBrowser.Services.Harness;

public sealed class HarnessRunIntent
{
    public string Analysis { get; init; } = "";
    public string? SelectedSkillId { get; init; }
    public string? SelectedSkillName { get; init; }
    public IReadOnlyList<HarnessPlannedTool> PlannedTools { get; init; } = Array.Empty<HarnessPlannedTool>();
    public string? ExecutionNotes { get; init; }
    public bool UsedLlm { get; init; }
    public bool AutoSelectedSkill { get; init; }

    public string BuildPromptSection(bool includeToolHints = true, int maxPlannedTools = 5, bool minimal = false)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("【运行前意图分析】");
        if (!string.IsNullOrWhiteSpace(Analysis))
            sb.AppendLine("需求：" + Analysis.Trim());

        if (!string.IsNullOrWhiteSpace(SelectedSkillId))
            sb.AppendLine("Skill：" + SelectedSkillId +
                          (string.IsNullOrWhiteSpace(SelectedSkillName) ? "" : "（" + SelectedSkillName + "）"));
        else if (includeToolHints && !minimal)
            sb.AppendLine("Skill：未强匹配，按通用能力执行。");

        if (includeToolHints && PlannedTools.Count > 0)
        {
            sb.Append("计划工具：");
            var limit = Math.Clamp(maxPlannedTools, 1, 8);
            var parts = PlannedTools.Take(limit).Select(t =>
            {
                var s = t.Name + (t.IsAvailable ? "" : "✗");
                return string.IsNullOrWhiteSpace(t.Fallback) ? s : s + "→" + t.Fallback;
            });
            sb.AppendLine(string.Join("; ", parts));
            if (PlannedTools.Count > limit)
                sb.AppendLine($"(另有 {PlannedTools.Count - limit} 个工具未列出)");
        }

        if (!string.IsNullOrWhiteSpace(ExecutionNotes))
            sb.AppendLine("要点：" + ExecutionNotes.Trim());

        if (includeToolHints && !minimal)
            sb.AppendLine("禁止编造未列出的 MCP 工具名。");
        return sb.ToString().TrimEnd();
    }
}

public sealed class HarnessPlannedTool
{
    public required string Name { get; init; }
    public required string Purpose { get; init; }
    public bool IsAvailable { get; init; }
    public string? Fallback { get; init; }
}
