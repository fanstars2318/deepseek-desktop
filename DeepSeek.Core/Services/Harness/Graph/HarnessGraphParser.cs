using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeepSeekBrowser.Services.Harness.Graph;

public static class HarnessGraphParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static HarnessGraphDefinition ParseFile(string path)
    {
        var text = File.ReadAllText(path);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".yaml" or ".yml" ? ParseYaml(text) : ParseJson(text);
    }

    public static HarnessGraphDefinition ParseJson(string json)
    {
        var dto = JsonSerializer.Deserialize<HarnessGraphDto>(json, JsonOptions)
                  ?? throw new InvalidDataException("Graph JSON 为空");
        return ToModel(dto);
    }

    public static HarnessGraphDefinition ParseYaml(string yaml)
    {
        var lines = yaml.Replace("\r\n", "\n").Split('\n');
        var dto = new HarnessGraphDto();
        var inNodes = false;
        var inEdges = false;
        HarnessGraphNodeDto? currentNode = null;
        HarnessGraphEdgeDto? currentEdge = null;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                continue;

            var trimmed = line.TrimStart();
            if (!inNodes && !inEdges && trimmed.Contains(':'))
            {
                var idx = trimmed.IndexOf(':');
                var key = trimmed[..idx].Trim().ToLowerInvariant();
                var val = trimmed[(idx + 1)..].Trim().Trim('"');
                switch (key)
                {
                    case "id": dto.Id = val; break;
                    case "version": dto.Version = int.TryParse(val, out var v) ? v : 1; break;
                    case "checkpoint": dto.Checkpoint = val; break;
                    case "nodes": inNodes = true; inEdges = false; break;
                    case "edges": inEdges = true; inNodes = false; break;
                }
                continue;
            }

            if (inNodes)
            {
                if (trimmed.Equals("edges:", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentNode is not null) { dto.Nodes!.Add(currentNode); currentNode = null; }
                    inNodes = false;
                    inEdges = true;
                    continue;
                }

                if (trimmed.StartsWith("- id:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("- id ", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentNode is not null) dto.Nodes!.Add(currentNode);
                    currentNode = new HarnessGraphNodeDto
                    {
                        Id = trimmed.Contains(':')
                            ? trimmed[(trimmed.IndexOf(':') + 1)..].Trim().Trim('"')
                            : trimmed["- id".Length..].Trim().Trim('"')
                    };
                    continue;
                }

                if (currentNode is null) continue;
                if (trimmed.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
                    currentNode.Type = trimmed["type:".Length..].Trim().Trim('"');
                else if (trimmed.StartsWith("role:", StringComparison.OrdinalIgnoreCase))
                    currentNode.Role = trimmed["role:".Length..].Trim().Trim('"');
                else if (trimmed.StartsWith("tool:", StringComparison.OrdinalIgnoreCase))
                    currentNode.Tool = trimmed["tool:".Length..].Trim().Trim('"');
                else if (trimmed.StartsWith("prompt:", StringComparison.OrdinalIgnoreCase))
                    currentNode.Prompt = trimmed["prompt:".Length..].Trim().Trim('"');
                else if (trimmed.StartsWith("command:", StringComparison.OrdinalIgnoreCase))
                    currentNode.Args ??= new Dictionary<string, string>();
                else if (trimmed.Contains("command:") && trimmed.StartsWith("args:", StringComparison.OrdinalIgnoreCase) == false)
                {
                    currentNode.Args ??= new Dictionary<string, string>();
                    var cidx = trimmed.IndexOf("command:", StringComparison.OrdinalIgnoreCase);
                    if (cidx >= 0)
                        currentNode.Args["command"] = trimmed[(cidx + "command:".Length)..].Trim().Trim('"');
                }
                continue;
            }

            if (inEdges)
            {
                if (trimmed.StartsWith("- from:", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentEdge is not null) dto.Edges!.Add(currentEdge);
                    currentEdge = new HarnessGraphEdgeDto
                    {
                        From = trimmed["- from:".Length..].Trim().Trim('"')
                    };
                    continue;
                }

                if (currentEdge is null) continue;
                if (trimmed.StartsWith("to:", StringComparison.OrdinalIgnoreCase))
                    currentEdge.To = trimmed["to:".Length..].Trim().Trim('"');
                else if (trimmed.StartsWith("condition:", StringComparison.OrdinalIgnoreCase))
                    currentEdge.Condition = trimmed["condition:".Length..].Trim().Trim('"');
            }
        }

        if (currentNode is not null) dto.Nodes!.Add(currentNode);
        if (currentEdge is not null) dto.Edges!.Add(currentEdge);

        return ToModel(dto);
    }

    private static HarnessGraphDefinition ToModel(HarnessGraphDto dto)
    {
        var id = (dto.Id ?? "").Trim();
        if (string.IsNullOrEmpty(id))
            throw new InvalidDataException("Graph 缺少 id");

        return new HarnessGraphDefinition
        {
            Id = id,
            Version = dto.Version <= 0 ? 1 : dto.Version,
            Checkpoint = string.IsNullOrWhiteSpace(dto.Checkpoint) ? "after_each_node" : dto.Checkpoint.Trim(),
            Nodes = dto.Nodes?.Select(n => new HarnessGraphNode
            {
                Id = n.Id?.Trim() ?? "",
                Type = (n.Type ?? "llm").Trim().ToLowerInvariant(),
                Role = n.Role?.Trim(),
                Tool = n.Tool?.Trim(),
                Prompt = n.Prompt?.Trim(),
                Args = n.Args ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            }).Where(n => !string.IsNullOrEmpty(n.Id)).ToList() ?? [],
            Edges = dto.Edges?.Select(e => new HarnessGraphEdge
            {
                From = e.From?.Trim() ?? "",
                To = e.To?.Trim() ?? "",
                Condition = string.IsNullOrWhiteSpace(e.Condition) ? null : e.Condition.Trim()
            }).Where(e => !string.IsNullOrEmpty(e.From) && !string.IsNullOrEmpty(e.To)).ToList() ?? []
        };
    }

    private sealed class HarnessGraphDto
    {
        public string? Id { get; set; }
        public int Version { get; set; } = 1;
        public string? Checkpoint { get; set; }
        public List<HarnessGraphNodeDto>? Nodes { get; set; } = [];
        public List<HarnessGraphEdgeDto>? Edges { get; set; } = [];
    }

    private sealed class HarnessGraphNodeDto
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public string? Role { get; set; }
        public string? Tool { get; set; }
        public string? Prompt { get; set; }
        public Dictionary<string, string>? Args { get; set; }
    }

    private sealed class HarnessGraphEdgeDto
    {
        public string? From { get; set; }
        public string? To { get; set; }
        public string? Condition { get; set; }
    }
}
