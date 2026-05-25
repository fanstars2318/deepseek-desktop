using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.Harness;

namespace DeepSeekBrowser.Services.Harness.Sandbox;

public sealed class LocalWorkspaceSandbox : IHarnessSandbox
{
    private readonly SandboxPathResolver _paths;
    private readonly int _bashTimeoutMs;

    public LocalWorkspaceSandbox(string sessionId, string workspaceRoot, AppConfig? config = null)
    {
        SessionId = sessionId;
        WorkspaceRoot = workspaceRoot;
        _bashTimeoutMs = ResolveTimeoutMs(config);
        HarnessVirtualPathMapper.EnsureLayoutDirectories(workspaceRoot);
        _paths = new SandboxPathResolver(workspaceRoot);
    }

    public string SessionId { get; }
    public HarnessSandboxKind Kind => HarnessSandboxKind.Local;
    public string WorkspaceRoot { get; }
    public bool IsAlive => true;

    public Task<string> ExecuteShellAsync(string command, CancellationToken ct, HarnessShellRunOptions? options = null)
    {
        var timeout = options?.TimeoutMs ?? _bashTimeoutMs;
        return HarnessShellRunner.RunAsync(
            command,
            WorkspaceRoot,
            _paths,
            timeout,
            ct,
            options?.OnOutput);
    }

    public void Dispose()
    {
        // 本地沙盒无容器生命周期
    }

    private static int ResolveTimeoutMs(AppConfig? config)
    {
        if (config is null)
            return 600_000;
        var min = Math.Clamp(config.AgentBashMinTimeoutMs, 1000, 3_600_000);
        return Math.Clamp(config.AgentBashTimeoutMs, min, 3_600_000);
    }
}
