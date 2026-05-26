namespace DeepSeekBrowser.Services.Harness;

public static class HarnessPlaybookExpander
{
    public static HarnessPlaybookApplyResult Apply(HarnessPlaybook playbook, string? workspaceRoot)
    {
        if (playbook.Blocks.Count == 0)
            return new HarnessPlaybookApplyResult { Playbook = playbook };

        var systemParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(playbook.SystemAppend))
            systemParts.Add(playbook.SystemAppend.Trim());

        string? graphId = null;
        string? skillId = null;
        var steps = playbook.Steps.ToList();

        foreach (var blockRef in playbook.Blocks)
        {
            if (string.IsNullOrWhiteSpace(blockRef)) continue;
            var refId = blockRef.Trim();
            if (refId.StartsWith("run-graph:", StringComparison.OrdinalIgnoreCase))
            {
                graphId = refId["run-graph:".Length..].Trim();
                continue;
            }

            if (refId.StartsWith("skill:", StringComparison.OrdinalIgnoreCase))
            {
                skillId = refId["skill:".Length..].Trim();
                continue;
            }

            if (!HarnessBlockRegistry.TryGet(refId, workspaceRoot, out var block) || block is null)
            {
                steps.Add("[missing block: " + refId + "]");
                continue;
            }

            switch (block.Type.ToLowerInvariant())
            {
                case "prompt":
                    if (!string.IsNullOrWhiteSpace(block.Prompt))
                        systemParts.Add(block.Prompt.Trim());
                    break;
                case "skill":
                    if (!string.IsNullOrWhiteSpace(block.SkillId))
                        skillId = block.SkillId;
                    break;
                case "graph":
                    if (!string.IsNullOrWhiteSpace(block.GraphId))
                        graphId = block.GraphId;
                    break;
                case "verify":
                    if (!string.IsNullOrWhiteSpace(block.Command))
                        steps.Add("Verify: " + block.Command);
                    break;
                default:
                    steps.Add("[block:" + block.Id + "]");
                    break;
            }
        }

        var expanded = new HarnessPlaybook
        {
            Id = playbook.Id,
            Name = playbook.Name,
            Description = playbook.Description,
            Strategy = playbook.Strategy,
            SystemAppend = systemParts.Count == 0 ? null : string.Join("\n\n", systemParts),
            Steps = steps,
            Blocks = playbook.Blocks,
            Verify = playbook.Verify
        };

        return new HarnessPlaybookApplyResult
        {
            Playbook = expanded,
            GraphId = graphId,
            SkillId = skillId
        };
    }
}

public sealed class HarnessPlaybookApplyResult
{
    public required HarnessPlaybook Playbook { get; init; }
    public string? GraphId { get; init; }
    public string? SkillId { get; init; }
}
