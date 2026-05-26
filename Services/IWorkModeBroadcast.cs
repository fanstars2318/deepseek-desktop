namespace DeepSeekBrowser.Services;

/// <summary>向 Chat / Agent 双 WebView 广播工作模式状态。</summary>
public interface IWorkModeBroadcast
{
    Task PushWorkModeStateNowAsync(WorkModeStatePayload state, CancellationToken ct = default);

    Task BroadcastWorkModeStateAsync(CancellationToken ct = default);

    Task BroadcastWorkModeStateAsync(bool includeImmediate, CancellationToken ct = default);

    void ScheduleWorkModeBroadcastRetries(CancellationToken ct = default);
}
