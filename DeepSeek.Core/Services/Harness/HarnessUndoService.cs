using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness;

public sealed class HarnessUndoService
{
    private readonly HarnessFileHistory _history;
    private readonly AgentSessionStore _store;

    public HarnessUndoService(string workspaceRoot, AgentSessionStore? store = null)
    {
        _history = new HarnessFileHistory(workspaceRoot);
        _store = store ?? new AgentSessionStore();
    }

    public IReadOnlyList<HarnessUndoTarget> ListTargets(string sessionId)
    {
        var session = _store.Load(sessionId);
        if (session is null)
            return Array.Empty<HarnessUndoTarget>();

        var targets = new List<HarnessUndoTarget>();
        for (var i = 0; i < session.Messages.Count; i++)
        {
            var m = session.Messages[i];
            if (!string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                continue;
            var id = string.IsNullOrWhiteSpace(m.Id) ? $"msg-{i}" : m.Id;
            var canCode = !string.IsNullOrWhiteSpace(m.CheckpointHash)
                          && _history.CanRestore(sessionId, m.CheckpointHash!);
            targets.Add(new HarnessUndoTarget
            {
                MessageId = id,
                Index = i,
                Preview = Truncate(m.Text ?? "", 120),
                CanRestoreCode = canCode,
                CheckpointHash = m.CheckpointHash
            });
        }

        return targets;
    }

    public AgentSessionData? RestoreConversation(string sessionId, string messageId, bool includeCode)
    {
        var session = _store.Load(sessionId);
        if (session is null)
            return null;

        var idx = -1;
        for (var i = 0; i < session.Messages.Count; i++)
        {
            var m = session.Messages[i];
            var mid = string.IsNullOrWhiteSpace(m.Id) ? $"msg-{i}" : m.Id;
            if (mid == messageId)
            {
                idx = i;
                break;
            }
        }

        if (idx < 0)
            return null;

        if (includeCode)
        {
            var hash = session.Messages[idx].CheckpointHash;
            if (!string.IsNullOrWhiteSpace(hash))
                _history.RestoreCheckpoint(sessionId, hash!);
        }

        session.Messages = session.Messages.Take(idx + 1).ToList();
        if (includeCode)
        {
            for (var j = idx + 1; j < session.Messages.Count; j++)
                session.Messages[j].CheckpointHash = null;
        }

        session.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _store.Save(session);
        return session;
    }

    public bool RestoreCodeOnly(string sessionId, string checkpointHash) =>
        _history.RestoreCheckpoint(sessionId, checkpointHash);

    public string? RecordAfterWrite(string sessionId, string relativePath) =>
        _history.RecordCheckpoint(sessionId, [relativePath], "tool write");

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}

public sealed class HarnessUndoTarget
{
    public string MessageId { get; init; } = "";
    public int Index { get; init; }
    public string Preview { get; init; } = "";
    public bool CanRestoreCode { get; init; }
    public string? CheckpointHash { get; init; }
}
