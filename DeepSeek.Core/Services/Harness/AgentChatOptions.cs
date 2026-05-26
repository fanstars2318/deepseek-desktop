namespace DeepSeekBrowser.Services.Harness;

public sealed class AgentChatOptions
{
    public bool UseOpenAiTools { get; init; }
    public IReadOnlyList<object>? Tools { get; init; }
    public string? ReasoningEffort { get; init; }
}
