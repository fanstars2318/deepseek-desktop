using DeepSeekBrowser.Services;

namespace DeepSeekBrowser.Services.Harness;

public sealed class HarnessToolExecuteResult
{
    public string Output { get; init; } = "";
    public IReadOnlyList<ChatMessage> FollowUpMessages { get; init; } = Array.Empty<ChatMessage>();

    public static HarnessToolExecuteResult FromOutput(string output) => new() { Output = output };

    public static HarnessToolExecuteResult WithFollowUp(string output, IEnumerable<ChatMessage> followUps) =>
        new()
        {
            Output = output,
            FollowUpMessages = followUps.ToList()
        };
}
