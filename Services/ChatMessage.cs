namespace DeepSeekBrowser.Services;

public sealed class ChatMessage
{
    public string Role { get; set; } = "user";
    public string? Content { get; set; }
    public List<WebToolCall>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }
}
