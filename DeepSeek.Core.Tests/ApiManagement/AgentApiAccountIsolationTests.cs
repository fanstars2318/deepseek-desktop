using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.ApiManagement;
using Xunit;

namespace DeepSeek.Core.Tests.ApiManagement;

public sealed class AgentApiAccountIsolationTests : TestConfigIsolation
{
    [Fact]
    public void ResolveWebUserToken_does_not_fallback_to_config_by_default()
    {
        var config = new AppConfig { WebUserToken = "web-only-token" };

        var token = AccountCredentials.ResolveWebUserToken(null, config);

        Assert.Null(token);
    }

    [Fact]
    public void ResolveWebUserToken_reads_manual_account_credentials()
    {
        var config = new AppConfig { WebUserToken = "web-only-token" };
        var account = new ProviderAccountRecord
        {
            Id = "acc-test",
            ProviderId = "deepseek",
            Credentials = new Dictionary<string, string> { ["token"] = "manual-token" }
        };

        var token = AccountCredentials.ResolveWebUserToken(account, config);

        Assert.Equal("manual-token", token);
    }

    [Fact]
    public void ResolveFirstProviderWebToken_ignores_config_web_token()
    {
        ProviderAccountStore.Save([]);
        var config = new AppConfig { WebUserToken = "web-only-token" };

        var token = AccountCredentials.ResolveFirstProviderWebToken("deepseek", config);

        Assert.Null(token);
    }
}
