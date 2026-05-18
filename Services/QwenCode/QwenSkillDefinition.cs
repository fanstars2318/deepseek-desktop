namespace DeepSeekBrowser.Services.QwenCode;

public sealed class QwenSkillDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Body { get; init; }
    public required string SourcePath { get; init; }
    public string Scope { get; init; } = "project";
    public IReadOnlyList<string> AllowedTools { get; init; } = [];
}
