using System.Text;
using System.Text.Json;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;

namespace DeepSeekBrowser.Services.Harness;

public static class HarnessContextCompactor
{
    public static bool ShouldCompact(AppConfig config, IReadOnlyList<ChatMessage> messages)
    {
        var threshold = config.AgentContextCompactTokenThreshold;
        if (threshold <= 0)
            return false;
        return EstimateTokens(messages) > threshold;
    }

    public static async Task CompactAsync(
        IAgentWebChat chat,
        List<ChatMessage> messages,
        AppConfig config,
        string model,
        bool thinking,
        bool search,
        AgentChatOptions? options,
        CancellationToken ct)
    {
        if (messages.Count < 4)
            return;

        var keepSystem = messages.FirstOrDefault(m => m.Role == "system");
        var recent = messages.TakeLast(4).ToList();
        var middle = messages.Skip(keepSystem is null ? 0 : 1).Take(messages.Count - recent.Count - (keepSystem is null ? 0 : 1)).ToList();
        if (middle.Count == 0)
            return;

        var summaryText = config.AgentContextCompactHierarchical && middle.Count > 12
            ? await SummarizeHierarchicalAsync(chat, middle, model, options, ct)
            : await SummarizeBlockAsync(chat, middle, model, options, ct);

        if (string.IsNullOrWhiteSpace(summaryText))
            return;

        messages.Clear();
        if (keepSystem is not null)
            messages.Add(keepSystem);
        messages.Add(new ChatMessage
        {
            Role = "user",
            Content = "Earlier conversation summary:\n\n" + summaryText
        });
        messages.AddRange(recent.Where(m => !ReferenceEquals(m, keepSystem)));
    }

    private static async Task<string> SummarizeBlockAsync(
        IAgentWebChat chat,
        IReadOnlyList<ChatMessage> middle,
        string model,
        AgentChatOptions? options,
        CancellationToken ct)
    {
        var sb = new StringBuilder("Summarize briefly for continuation (facts, decisions, open items):\n\n");
        foreach (var m in middle)
            sb.Append('[').Append(m.Role).Append("] ").AppendLine(m.Content ?? "");

        var summary = await chat.CompleteAsync(
            [new ChatMessage { Role = "user", Content = sb.ToString() }],
            model, thinking: false, search: false, Array.Empty<string>(),
            allowToolCalls: false, ct, options: options);
        return (summary.Content ?? "").Trim();
    }

    private static async Task<string> SummarizeHierarchicalAsync(
        IAgentWebChat chat,
        IReadOnlyList<ChatMessage> middle,
        string model,
        AgentChatOptions? options,
        CancellationToken ct)
    {
        const int chunkSize = 8;
        var partials = new List<string>();
        for (var i = 0; i < middle.Count; i += chunkSize)
        {
            var chunk = middle.Skip(i).Take(chunkSize).ToList();
            var part = await SummarizeBlockAsync(chat, chunk, model, options, ct);
            if (!string.IsNullOrWhiteSpace(part))
                partials.Add(part);
        }

        if (partials.Count == 0)
            return "";
        if (partials.Count == 1)
            return partials[0];

        var mergeSb = new StringBuilder("Merge these segment summaries into one brief continuation summary:\n\n");
        for (var i = 0; i < partials.Count; i++)
            mergeSb.AppendLine("### Segment " + (i + 1)).AppendLine(partials[i]).AppendLine();

        var merged = await chat.CompleteAsync(
            [new ChatMessage { Role = "user", Content = mergeSb.ToString() }],
            model, thinking: false, search: false, Array.Empty<string>(),
            allowToolCalls: false, ct, options: options);
        return (merged.Content ?? "").Trim();
    }

    public static int EstimateTokens(IReadOnlyList<ChatMessage> messages)
    {
        var chars = messages.Sum(m => (m.Content?.Length ?? 0) + 80);
        return chars / 4;
    }
}

public sealed class HarnessGitFileHistory
{
    private readonly HarnessFileHistory _inner;

    public HarnessGitFileHistory(string workspaceRoot) => _inner = new HarnessFileHistory(workspaceRoot);

    public void Snapshot(string relativePath) => _inner.Snapshot(relativePath);

    public bool TryRestore(string relativePath) => _inner.TryRestoreSingleFile(relativePath);
}
