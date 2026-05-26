namespace DeepSeekBrowser.Services.Harness.Factory;

public static class HarnessDeliveryReport
{
    public static string Build(
        string userPrompt,
        IReadOnlyDictionary<string, string> phaseOutputs,
        IReadOnlyList<string> artifactPaths,
        string? gitSummary,
        string? verifySummary)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Software factory delivery");
        sb.AppendLine();
        sb.AppendLine("## User request");
        sb.AppendLine(userPrompt.Trim());
        sb.AppendLine();

        foreach (var kv in phaseOutputs)
        {
            sb.AppendLine("## " + kv.Key);
            sb.AppendLine(kv.Value.Trim());
            sb.AppendLine();
        }

        if (artifactPaths.Count > 0)
        {
            sb.AppendLine("## Artifacts");
            foreach (var p in artifactPaths)
                sb.AppendLine("- " + p);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(gitSummary))
        {
            sb.AppendLine("## Repository");
            sb.AppendLine(gitSummary.Trim());
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(verifySummary))
        {
            sb.AppendLine("## QA / Verify");
            sb.AppendLine(verifySummary.Trim());
        }

        return sb.ToString().Trim();
    }

    public static string WriteFile(string workspaceRoot, string runId, string markdown) =>
        HarnessArtifactWriter.Write(workspaceRoot, runId, "DELIVERY.md", markdown);
}
