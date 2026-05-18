namespace DeepSeekBrowser.Services.QwenCode;

public sealed class QwenInputProcessResult
{
    public static QwenInputProcessResult ForAgent(string task) => new() { TaskText = task };

    public static QwenInputProcessResult Handled(string reply) => new()
    {
        HandledWithoutAgent = true,
        DirectReply = reply
    };

    public bool HandledWithoutAgent { get; init; }
    public string? DirectReply { get; init; }
    public string TaskText { get; init; } = "";
    public string? ActiveSkill { get; init; }
    public string? ActiveSubAgent { get; init; }
}
