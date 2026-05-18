using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.QwenCode;

/// <summary>
/// Qwen Code Core 统一工具枢纽：内置工具 + MCP（对应 architecture 中 Core 的 Tool Registry）。
/// </summary>
public sealed class UnifiedToolHub
{
    private readonly McpHub _mcp;
    private readonly ToolApprovalService _approval;

    public UnifiedToolHub(McpHub mcp, ToolApprovalService approval)
    {
        _mcp = mcp;
        _approval = approval;
    }

    public bool HasAnyTools(AppConfig config) =>
        (config.EnableQwenCodeBuiltinTools && QwenCodeBuiltinTools.GetDescriptors(config).Count > 0)
        || _mcp.ConnectedCount > 0;

    public async Task<IReadOnlyList<AgentToolDescriptor>> ListAllToolsAsync(AppConfig config, CancellationToken ct)
    {
        var list = new List<AgentToolDescriptor>();
        if (config.EnableQwenCodeBuiltinTools)
            list.AddRange(QwenCodeBuiltinTools.GetDescriptors(config));
        if (_mcp.ConnectedCount > 0)
            list.AddRange(await _mcp.ListAllToolsAsync(ct));
        return list;
    }

    public async Task<string> CallToolAsync(
        string exposedName,
        string argumentsJson,
        AppConfig config,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(exposedName))
            throw new InvalidOperationException("工具名为空");

        if (QwenCodePort.IsOfficialBuiltin(exposedName))
        {
            var official = QwenCodePort.NormalizeToolName(exposedName)!;
            return await QwenCodeBuiltinTools.ExecuteAsync(official, argumentsJson, config, _approval, ct);
        }

        var resolved = _mcp.ResolveExposedToolName(exposedName);
        return await _mcp.CallToolAsync(resolved, argumentsJson, ct);
    }

    public string ResolveToolName(string raw) =>
        QwenCodePort.IsOfficialBuiltin(raw)
            ? QwenCodePort.NormalizeToolName(raw)!
            : _mcp.ResolveExposedToolName(raw);

    private static string TruncateArgs(string json) =>
        json.Length <= 400 ? json : json[..400] + "…";
}
