namespace DeepSeekBrowser.Services;

/// <summary>
/// Agent 运行期间为经 DSD API 的补全请求注入默认特性（深度思考 / 联网搜索）。
/// DeepSeek Desktop Agent API 作用域（深度思考 / 联网搜索等）。
/// </summary>
public sealed class DsdAgentApiScope : IDisposable
{
    private static readonly AsyncLocal<DsdAgentApiScope?> CurrentScope = new();
    private static int _activeAgentRuns;

    public static DsdAgentApiScope? Current => CurrentScope.Value;

    /// <summary>DeepSeek-TUI 经 HTTP 调用本机 DSD API 时无 AsyncLocal，用运行计数判断 Agent 会话。</summary>
    public static bool HasActiveAgentRun => Volatile.Read(ref _activeAgentRuns) > 0;

    public bool DeepThinking { get; }
    public bool WebSearch { get; }
    public string ReasoningEffort { get; }

    private DsdAgentApiScope(bool deepThinking, bool webSearch, string reasoningEffort)
    {
        DeepThinking = deepThinking;
        WebSearch = webSearch;
        ReasoningEffort = reasoningEffort;
    }

    public static DsdAgentApiScope Begin(bool deepThinking, bool webSearch)
    {
        var effort = deepThinking ? "high" : "off";
        var scope = new DsdAgentApiScope(deepThinking, webSearch, effort);
        Interlocked.Increment(ref _activeAgentRuns);
        CurrentScope.Value = scope;
        return scope;
    }

    public void Dispose()
    {
        CurrentScope.Value = null;
        Interlocked.Decrement(ref _activeAgentRuns);
    }
}
