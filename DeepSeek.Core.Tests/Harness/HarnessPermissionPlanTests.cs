using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.Harness;

namespace DeepSeek.Core.Tests.Harness;

public sealed class HarnessPermissionPlanTests
{
    [Fact]
    public void Read_tool_defaults_to_allow()
    {
        var plan = HarnessPermissionPlan.Compute("read_file", "{}", new AppConfig());
        Assert.Equal(HarnessPermissionDecision.Allow, plan.Decision);
        Assert.Contains(HarnessPermissionScope.Read, plan.Scopes);
    }

    [Fact]
    public void Write_tool_asks_in_smart_mode()
    {
        var plan = HarnessPermissionPlan.Compute("write_file", "{}", new AppConfig { AgentApprovalMode = "smart" });
        Assert.Equal(HarnessPermissionDecision.Ask, plan.Decision);
        Assert.Contains(HarnessPermissionScope.Write, plan.Scopes);
    }

    [Fact]
    public void Bash_maps_to_bash_scope()
    {
        var scopes = HarnessPermissionPlan.ResolveScopes("bash");
        Assert.Contains(HarnessPermissionScope.Bash, scopes);
    }
}
