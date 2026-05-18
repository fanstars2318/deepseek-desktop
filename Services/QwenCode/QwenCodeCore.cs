using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.QwenCode;

/// <summary>
/// 将 npm <c>@qwen-code/qwen-code</c> 的 Core 架构移植到 C#（DeepSeek 桌面 Agent 为外壳，见 <see cref="QwenCodePort"/>）：
/// <list type="bullet">
/// <item>CLI (packages/cli) → Assets/agent 页 + overlay（模式切换、@file、/help）</item>
/// <item>Core (packages/core) → <see cref="AgentOrchestrator"/> + <see cref="UnifiedToolHub"/> + <see cref="LocalChat2ApiClient"/></item>
/// <item>Tools (packages/core/src/tools) → <see cref="QwenCodeBuiltinTools"/> + <see cref="McpHub"/></item>
/// <item>自适应 Token 扩容 → <see cref="AdaptiveOutputTokenEscalation"/></item>
/// </list>
/// </summary>
public sealed class QwenCodeCore
{
    public UnifiedToolHub Tools { get; }
    public ToolApprovalService Approval { get; }

    public QwenCodeCore(McpHub mcp)
    {
        Approval = new ToolApprovalService();
        Tools = new UnifiedToolHub(mcp, Approval);
    }

    public static McpToolRegistry BuildToolRegistry(IReadOnlyList<AgentToolDescriptor> tools) =>
        McpToolRegistry.FromDescriptors(tools);

    public async Task<McpToolRegistry> GetRegistryAsync(AppConfig config, CancellationToken ct)
    {
        var tools = await Tools.ListAllToolsAsync(config, ct);
        return BuildToolRegistry(tools);
    }

    public QwenCodeAgentRunner CreateRunner(LocalChat2ApiClient chat2Api) =>
        new(this, chat2Api);
}
