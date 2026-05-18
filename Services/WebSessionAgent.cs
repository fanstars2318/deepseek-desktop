using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services;

public sealed class WebSessionAgent
{
    private readonly AgentOrchestrator _orchestrator;

    public WebSessionAgent(QwenCode.QwenCodeCore qwenCode, LocalChat2ApiClient chat2Api) =>
        _orchestrator = new AgentOrchestrator(qwenCode, chat2Api);

    public Task<string?> RunAsync(
        AppConfig config,
        string userTask,
        string strategy,
        bool useMcpTools,
        bool thinking,
        bool search,
        Action<string> onLog,
        CancellationToken ct) =>
        _orchestrator.RunAsync(config, userTask, strategy, useMcpTools, thinking, search, onLog, ct);
}
