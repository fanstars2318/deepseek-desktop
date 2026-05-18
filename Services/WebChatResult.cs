namespace DeepSeekBrowser.Services;

public sealed class WebChatResult
{
    public string? Content { get; init; }
    public string? ReasoningContent { get; init; }
    public List<WebToolCall>? ToolCalls { get; init; }
    public string Model { get; init; } = "deepseek-chat";
    /// <summary>OpenAI 风格：stop / length / tool_calls 等。</summary>
    public string? FinishReason { get; init; }
    public bool IsLikelyTruncated { get; init; }
}

public sealed class WebToolCall
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Arguments { get; init; } = "{}";
}
