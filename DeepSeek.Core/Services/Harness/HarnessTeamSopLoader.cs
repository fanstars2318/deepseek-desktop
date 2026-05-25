using System.Text.RegularExpressions;

namespace DeepSeekBrowser.Services.Harness;

public sealed class HarnessTeamSopRole
{
    public string Id { get; init; } = "";
    public string Task { get; init; } = "";
}

public sealed class HarnessTeamSopDefinition
{
    public string Id { get; init; } = "default";
    public IReadOnlyList<HarnessTeamSopRole> Roles { get; init; } = Array.Empty<HarnessTeamSopRole>();
}

public static class HarnessTeamSopLoader
{
    private static readonly HarnessTeamSopDefinition DefaultSop = new()
    {
        Id = "default",
        Roles =
        [
            new() { Id = "product-manager", Task = "Write a concise PRD (goals, user stories, acceptance criteria, out-of-scope) for the user request." },
            new() { Id = "architect", Task = "Produce technical design from the PRD: components, files to touch, APIs, risks. Read workspace as needed." },
            new() { Id = "engineer", Task = "Implement the design in the workspace. Use tools; keep changes focused and verifiable." },
            new() { Id = "reviewer", Task = "Review the implementation against the PRD. List findings by severity; note test gaps." }
        ]
    };

    public static HarnessTeamSopDefinition Load(string? sopId = null)
    {
        var dir = Path.Combine(AgentDesktopConfigSync.HomeDirectory, "team-sops");
        if (!Directory.Exists(dir))
            return DefaultSop;

        var files = Directory.EnumerateFiles(dir, "*.yaml")
            .Concat(Directory.EnumerateFiles(dir, "*.yml"))
            .ToList();
        if (files.Count == 0)
            return DefaultSop;

        string? pick = null;
        if (!string.IsNullOrWhiteSpace(sopId))
            pick = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f)
                .Equals(sopId, StringComparison.OrdinalIgnoreCase));

        pick ??= files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        if (pick is null) return DefaultSop;

        try
        {
            var parsed = ParseYamlRoles(File.ReadAllText(pick));
            return parsed.Roles.Count > 0 ? parsed : DefaultSop;
        }
        catch
        {
            return DefaultSop;
        }
    }

    private static HarnessTeamSopDefinition ParseYamlRoles(string yaml)
    {
        var roles = new List<HarnessTeamSopRole>();
        string? currentId = null;
        var taskLines = new List<string>();

        void FlushRole()
        {
            if (string.IsNullOrWhiteSpace(currentId)) return;
            roles.Add(new HarnessTeamSopRole
            {
                Id = currentId.Trim(),
                Task = string.Join(" ", taskLines).Trim()
            });
            currentId = null;
            taskLines.Clear();
        }

        foreach (var raw in yaml.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var idMatch = Regex.Match(line, @"^-\s*id:\s*(.+)$", RegexOptions.IgnoreCase);
            if (idMatch.Success)
            {
                FlushRole();
                currentId = idMatch.Groups[1].Value.Trim().Trim('"');
                continue;
            }

            var taskMatch = Regex.Match(line, @"^task:\s*(.*)$", RegexOptions.IgnoreCase);
            if (taskMatch.Success && currentId is not null)
            {
                taskLines.Add(taskMatch.Groups[1].Value.Trim().Trim('"'));
            }
        }

        FlushRole();
        return new HarnessTeamSopDefinition
        {
            Id = Path.GetFileNameWithoutExtension("sop"),
            Roles = roles
        };
    }
}
