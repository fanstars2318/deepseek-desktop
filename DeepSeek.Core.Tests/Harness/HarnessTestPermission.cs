using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.Harness;

namespace DeepSeek.Core.Tests.Harness;

internal static class HarnessTestPermission
{
    public static PermissionGate AllowAll(AppConfig? config = null)
    {
        var c = config ?? new AppConfig { AgentApprovalMode = "never" };
        var approval = new ApprovalGate(c, (_, _) => Task.FromResult(true));
        return new PermissionGate(c, approval);
    }
}
