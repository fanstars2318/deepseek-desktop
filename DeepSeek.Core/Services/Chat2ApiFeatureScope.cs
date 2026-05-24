namespace DeepSeekBrowser.Services;

/// <summary>
/// Agent 运行期间为经 Chat2API 的补全请求注入默认特性（深度思考 / 联网搜索）。
/// 对齐 <see href="https://chat2api-doc.vercel.app/en/docs/web-search-thinking/"/>。
/// </summary>
public sealed class Chat2ApiFeatureScope : IDisposable
{
    private static readonly AsyncLocal<Chat2ApiFeatureScope?> CurrentScope = new();
    private static int _activeAgentRuns;

    public static Chat2ApiFeatureScope? Current => CurrentScope.Value;

    /// <summary>DeepSeek-TUI 经 HTTP 调用本机 Chat2API 时无 AsyncLocal，用运行计数判断 Agent 会话。</summary>
    public static bool HasActiveAgentRun => Volatile.Read(ref _activeAgentRuns) > 0;

    public bool DeepThinking { get; }
    public bool WebSearch { get; }
    public string ReasoningEffort { get; }

    private Chat2ApiFeatureScope(bool deepThinking, bool webSearch, string reasoningEffort)
    {
        DeepThinking = deepThinking;
        WebSearch = webSearch;
        ReasoningEffort = reasoningEffort;
    }

    public static Chat2ApiFeatureScope Begin(bool deepThinking, bool webSearch)
    {
        var effort = deepThinking ? "high" : "off";
        var scope = new Chat2ApiFeatureScope(deepThinking, webSearch, effort);
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
