namespace DeepSeekBrowser.Services.QwenCode;

public sealed class QwenSubAgentDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string SystemPrompt { get; init; }
    public required string SourcePath { get; init; }
    public string Scope { get; init; } = "project";
    public string ApprovalMode { get; init; } = "auto-edit";
    public IReadOnlyList<string> AllowedTools { get; init; } = [];
    public IReadOnlyList<string> DisallowedTools { get; init; } = [];
}
