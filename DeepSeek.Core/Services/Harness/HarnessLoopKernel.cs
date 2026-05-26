using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.Harness.Interop;
using DeepSeekBrowser.Services.Harness.Observability;
using DeepSeekBrowser.Services.Harness.Sandbox;

namespace DeepSeekBrowser.Services.Harness;

/// <summary>统一 Agent Loop 内核：Prepare → Plan → Compose → Compact → Tool options。</summary>
public sealed class HarnessLoopKernel
{
    private readonly IAgentWebChat _chat;
    private readonly McpHub _mcp;
    private readonly HarnessToolExecutor _tools;

    public HarnessLoopKernel(IAgentWebChat chat, McpHub mcp, HarnessToolExecutor tools)
    {
        _chat = chat;
        _mcp = mcp;
        _tools = tools;
    }

    public async Task<HarnessRunPrep> PrepareRunContextAsync(
        HarnessRunRequest request,
        HarnessRunCallbacks callbacks,
        HarnessRunState state,
        CancellationToken ct)
    {
        var config = request.Config;
        var workspace = AgentWorkspace.ResolveRoot(config);
        _tools.SetAgentSessionId(request.AgentSessionId);

        var sandboxCoord = await HarnessSandboxCoordinator.BeginRunAsync(
            state, config, workspace, new HarnessTrace(), callbacks.OnLog, ct);

        var memory = HarnessMemoryLoader.Load(request.Prompt, workspace);
        string? mcpCatalog = null;
        try
        {
            using var mcpTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            mcpTimeout.CancelAfter(TimeSpan.FromSeconds(5));
            mcpCatalog = await _mcp.BuildToolCatalogTextAsync(mcpTimeout.Token);
        }
        catch
        {
            mcpCatalog = "（MCP 工具目录加载失败或超时）";
        }

        var mcpMaxLines = Math.Clamp(config.AgentMcpCatalogMaxLines, 8, 120);
        if (!string.IsNullOrWhiteSpace(mcpCatalog))
            mcpCatalog = HarnessPromptBudget.TrimMcpCatalog(mcpCatalog, mcpMaxLines);

        return new HarnessRunPrep
        {
            Workspace = workspace,
            Memory = memory,
            McpCatalog = mcpCatalog,
            SandboxCoordinator = sandboxCoord
        };
    }

    public async Task<HarnessRunIntent?> PlanIntentAsync(
        HarnessRunRequest request,
        HarnessRunState? state,
        string? mcpCatalog,
        string workspace,
        CancellationToken ct)
    {
        if (!HarnessRunIntentPlanner.ShouldPlan(request, state))
            return HarnessIntentCache.TryRestore(request, state);

        return await HarnessRunIntentPlanner.PlanAsync(request, _chat, mcpCatalog, workspace, ct);
    }

    public static List<ChatMessage> ComposeInitialMessages(
        HarnessRunRequest request,
        HarnessRunPrep prep,
        HarnessStrategyProfile profile,
        HarnessPlaybook? playbook,
        HarnessMemoryContext memory,
        HarnessSkill? skill,
        HarnessRunIntent? intent,
        bool includeXmlTools) =>
        HarnessComposer.BuildInitialMessages(
            request, prep.Workspace, prep.McpCatalog, prep.WorkspaceSnapshot ?? "",
            profile.InitialPhase, profile, playbook, memory, skill, intent, includeXmlTools).ToList();

    public async Task MaybeCompactAsync(
        List<ChatMessage> messages,
        AppConfig config,
        string model,
        bool thinking,
        bool search,
        HarnessRunTracer? tracer,
        HarnessRunCallbacks callbacks,
        CancellationToken ct)
    {
        if (!HarnessContextCompactor.ShouldCompact(config, messages))
            return;

        callbacks.OnLog?.Invoke("对话较长，正在压缩上下文…");
        var tokensBefore = HarnessContextCompactor.EstimateTokens(messages);
        await HarnessContextCompactor.CompactAsync(
            _chat, messages, config, model, thinking, search,
            await HarnessOpenAiToolLoop.BuildChatOptionsAsync(config, _mcp, allowTools: false, ct),
            ct);
        tracer?.RecordCompact(tokensBefore, HarnessContextCompactor.EstimateTokens(messages));
    }

    public Task<AgentChatOptions> BuildToolOptionsAsync(
        AppConfig config,
        bool allowTools,
        HarnessRunIntent? intent,
        HarnessToolInventory? inventory,
        CancellationToken ct) =>
        HarnessOpenAiToolLoop.BuildChatOptionsAsync(config, _mcp, allowTools, ct, intent, inventory);
}

public sealed class HarnessRunPrep : IAsyncDisposable
{
    public required string Workspace { get; init; }
    public required HarnessMemoryContext Memory { get; init; }
    public string? McpCatalog { get; init; }
    public string? WorkspaceSnapshot { get; set; }
    public required HarnessSandboxCoordinator SandboxCoordinator { get; init; }

    public ValueTask DisposeAsync() => SandboxCoordinator.DisposeAsync();
}
