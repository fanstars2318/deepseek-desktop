using DeepSeekBrowser.Services.Harness;

namespace DeepSeek.Core.Tests.Harness;

public sealed class HarnessAgentRoleTests
{
    [Theory]
    [InlineData("explorer", "explore")]
    [InlineData("product-manager", "product-manager")]
    [InlineData("pm", "product-manager")]
    public void Resolve_maps_aliases(string input, string expectedId)
    {
        var role = HarnessAgentRoleRegistry.Resolve(input);
        Assert.Equal(expectedId, role.Id);
    }

    [Fact]
    public void Explore_disallows_write()
    {
        var role = HarnessAgentRoleRegistry.Resolve("explore");
        Assert.False(role.AllowsWrite);
    }
}
