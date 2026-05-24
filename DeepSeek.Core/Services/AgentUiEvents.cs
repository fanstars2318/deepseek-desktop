namespace DeepSeekBrowser.Services;

public enum AgentRunState
{
    Idle,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed class AgentMessageEventArgs(string role, string text, string? kind = null) : EventArgs
{
    public string Role { get; } = role;
    public string Text { get; } = text;
    public string? Kind { get; } = kind;
}

public sealed class AgentStreamDeltaEventArgs(string text, bool append, bool isThinking) : EventArgs
{
    public string Text { get; } = text;
    public bool Append { get; } = append;
    public bool IsThinking { get; } = isThinking;
}

public sealed class AgentToolApprovalEventArgs(
    string toolName,
    string detail,
    string action,
    string title,
    TaskCompletionSource<bool> decision) : EventArgs
{
    public string ToolName { get; } = toolName;
    public string Detail { get; } = detail;
    public string Action { get; } = action;
    public string Title { get; } = title;
    public TaskCompletionSource<bool> Decision { get; } = decision;
}

public sealed class AgentRunStateEventArgs(AgentRunState state, string? summary = null, string? answer = null)
    : EventArgs
{
    public AgentRunState State { get; } = state;
    public string? Summary { get; } = summary;
    public string? Answer { get; } = answer;
}
