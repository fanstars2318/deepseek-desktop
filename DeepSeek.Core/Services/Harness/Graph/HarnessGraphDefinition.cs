namespace DeepSeekBrowser.Services.Harness.Graph;

public sealed class HarnessGraphDefinition
{
    public string Id { get; set; } = "";
    public int Version { get; set; } = 1;
    public string Checkpoint { get; set; } = "after_each_node";
    public List<HarnessGraphNode> Nodes { get; set; } = [];
    public List<HarnessGraphEdge> Edges { get; set; } = [];
}

public sealed class HarnessGraphNode
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "llm";
    public string? Role { get; set; }
    public string? Tool { get; set; }
    public Dictionary<string, string> Args { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Prompt { get; set; }
}

public sealed class HarnessGraphEdge
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string? Condition { get; set; }
}

public sealed class HarnessGraphSummary
{
    public required string Id { get; init; }
    public int Version { get; init; }
    public int NodeCount { get; init; }
    public int EdgeCount { get; init; }
    public string Source { get; init; } = "user";
    public string? FilePath { get; init; }
}
