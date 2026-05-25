namespace DeepSeekBrowser.Services.Harness.Graph;

public sealed class HarnessToolDescriptor
{
    public required string Name { get; init; }
    public required string Source { get; init; }
    public bool ReadOnly { get; init; }
    public string? Description { get; init; }
}

public static class HarnessToolDescriptorRegistry
{
    public static IReadOnlyList<HarnessToolDescriptor> ListBuiltin() =>
    [
        new() { Name = "read_file", Source = "builtin", ReadOnly = true, Description = "Read workspace file" },
        new() { Name = "write_file", Source = "builtin", ReadOnly = false, Description = "Write workspace file" },
        new() { Name = "edit_file", Source = "builtin", ReadOnly = false, Description = "Edit workspace file" },
        new() { Name = "list_dir", Source = "builtin", ReadOnly = true, Description = "List directory" },
        new() { Name = "glob", Source = "builtin", ReadOnly = true, Description = "Glob search" },
        new() { Name = "grep", Source = "builtin", ReadOnly = true, Description = "Grep search" },
        new() { Name = "run_shell", Source = "builtin", ReadOnly = false, Description = "Run shell in sandbox" },
        new() { Name = "delegate_agent", Source = "builtin", ReadOnly = false, Description = "Delegate sub-agent" },
        new() { Name = "AskUserQuestion", Source = "special", ReadOnly = true, Description = "Human-in-the-loop" }
    ];

    public static HarnessToolDescriptor? Resolve(string toolName)
    {
        var normalized = toolName.Trim().ToLowerInvariant();
        return ListBuiltin().FirstOrDefault(t =>
            t.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase)
            || (normalized is "bash" or "shell" && t.Name == "run_shell"));
    }
}
