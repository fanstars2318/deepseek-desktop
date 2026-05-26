using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;
using DeepSeekBrowser.Services.ApiManagement;
using Xunit;

namespace DeepSeek.Core.Tests;

public sealed class AutoModelSelectorTests
{
    private static AppConfig ConfigWithGlm() => new()
    {
        WebUserToken = "tok",
        AgentAutoPreferProviderOrder = true,
        AgentAutoProviderOrder = ["glm", "deepseek"],
        ApiProviders =
        [
            new ApiProviderEntry
            {
                Id = "glm",
                DisplayName = "GLM",
                Enabled = true,
                RouteMode = ApiRouteModes.DirectApi,
                Kind = ApiProviderKinds.Custom,
                Models = ["GLM-5", "GLM-5-Flash"]
            },
            ApiProviderRegistry.CreateDefaultDeepSeek(new AppConfig { WebUserToken = "tok" })
        ]
    };

    [Fact]
    public void Select_ShortSimple_PrefersFirstReadyProviderFastTier()
    {
        var config = ConfigWithGlm();
        CredentialVault.Set("glm", "api_key", "k-test");

        var sel = AutoModelSelector.Select(config, new AutoModelSelector.Request("你好", false, false));

        Assert.Equal("glm", sel.ProviderId);
        Assert.Equal("GLM-5-Flash", sel.ModelId);
    }

    [Fact]
    public void Select_DeepThink_UsesReasoningModel()
    {
        var config = ConfigWithGlm();
        CredentialVault.Set("glm", "api_key", "k-test");

        var sel = AutoModelSelector.Select(config, new AutoModelSelector.Request("hi", true, false));

        Assert.Equal("glm", sel.ProviderId);
        Assert.Contains("GLM", sel.ModelId);
    }

    [Fact]
    public void PreferOrder_PutsDefaultProviderFirstWhenNoCustomOrder()
    {
        var config = new AppConfig
        {
            WebUserToken = "tok",
            AgentDefaultProviderId = "deepseek",
            AgentAutoPreferProviderOrder = true,
            AgentAutoProviderOrder = []
        };

        var order = AutoProviderPool.ResolvePreferenceOrder(config);

        Assert.Equal("deepseek", order[0]);
    }
}
