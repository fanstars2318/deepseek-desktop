using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.QwenCode;

/// <summary>Qwen Code 工具执行前审批（写文件、Shell 等）。</summary>
public sealed class ToolApprovalService
{
    public Func<string, string, ToolRisk, Task<bool>>? RequestApprovalAsync { get; set; }

    public async Task<bool> EnsureApprovedAsync(
        string toolName,
        string detail,
        ToolRisk risk,
        AppConfig config,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (risk == ToolRisk.ReadOnly && config.QwenCodeAutoApproveReadOnly)
            return true;

        if (string.Equals(config.QwenCodeApprovalMode, "never", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(config.QwenCodeApprovalMode, "readonly", StringComparison.OrdinalIgnoreCase)
            && risk == ToolRisk.ReadOnly)
            return true;

        var handler = RequestApprovalAsync;
        if (handler is null)
            return risk == ToolRisk.ReadOnly;

        return await handler(toolName, detail, risk);
    }
}
