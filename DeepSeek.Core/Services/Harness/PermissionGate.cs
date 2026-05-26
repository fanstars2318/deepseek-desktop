using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness;

public sealed class PermissionGate
{
    private readonly AppConfig _config;
    private readonly ApprovalGate _approval;
    private readonly Func<string, string, IReadOnlyList<string>, Task<bool>>? _scopeApproval;
    private readonly Func<string, bool>? _roleToolFilter;

    public PermissionGate(
        AppConfig config,
        ApprovalGate approval,
        Func<string, string, IReadOnlyList<string>, Task<bool>>? scopeApproval = null,
        Func<string, bool>? roleToolFilter = null)
    {
        _config = config;
        _approval = approval;
        _scopeApproval = scopeApproval;
        _roleToolFilter = roleToolFilter;
    }

    public static PermissionGate ForSubAgentRole(
        AppConfig config,
        ApprovalGate approval,
        HarnessAgentRoleProfile role,
        Func<string, string, IReadOnlyList<string>, Task<bool>>? scopeApproval = null)
    {
        bool Filter(string toolName)
        {
            var n = BuiltinToolExecutor.NormalizeName(toolName).ToLowerInvariant();
            if (n is "delegate_agent" or "delegate")
                return role.AllowsDelegate;
            if (n.Contains("write") || n.Contains("edit"))
                return role.AllowsWrite;
            if (n is "run_shell" or "bash")
                return role.AllowsShell;
            return true;
        }

        return new PermissionGate(config, approval, scopeApproval, Filter);
    }

    public async Task<bool> AllowToolAsync(
        string toolName,
        string detail,
        string? argumentsJson,
        HarnessPhase phase,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_roleToolFilter is not null && !_roleToolFilter(toolName))
            return false;

        var plan = HarnessPermissionPlan.Compute(toolName, argumentsJson, _config);
        if (plan.Decision == HarnessPermissionDecision.Deny)
            return false;

        if (plan.Decision == HarnessPermissionDecision.Ask)
        {
            var scopeNames = plan.Scopes.Select(s => s.ToString().ToLowerInvariant()).ToList();
            var ok = _scopeApproval is not null
                ? await _scopeApproval(toolName, detail, scopeNames)
                : await _approval.AllowToolAsync(toolName, detail, phase, ct);
            if (!ok)
                return false;
            return await _approval.AllowToolAsync(toolName, detail, phase, ct, suppressPrompt: true);
        }

        return await _approval.AllowToolAsync(toolName, detail, phase, ct);
    }
}
