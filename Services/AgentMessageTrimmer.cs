namespace DeepSeekBrowser.Services;

internal static class AgentMessageTrimmer
{
    private const int MaxMessages = 32;

    public static List<ChatMessage> TrimForContext(IReadOnlyList<ChatMessage> messages)
    {
        if (messages.Count <= MaxMessages)
            return messages.ToList();

        var system = messages.Where(m => m.Role == "system").ToList();
        var rest = messages.Where(m => m.Role != "system").TakeLast(MaxMessages - system.Count).ToList();
        return [.. system, .. rest];
    }
}
