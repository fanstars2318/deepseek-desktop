using DeepSeekBrowser.Services.Harness;
using Xunit;

namespace DeepSeek.Core.Tests.Harness;

public sealed class HarnessTeamSopLoaderTests
{
    [Fact]
    public void Load_default_has_four_roles()
    {
        var sop = HarnessTeamSopLoader.Load();
        Assert.True(sop.Roles.Count >= 4);
        Assert.Contains(sop.Roles, r => r.Id.Contains("product", StringComparison.OrdinalIgnoreCase));
    }
}
