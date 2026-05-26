using DeepSeekBrowser.Services.ApiManagement;
using Xunit;

namespace DeepSeek.Core.Tests.ApiManagement;

public sealed class ProviderAccountStoreTests : TestConfigIsolation
{
    [Fact]
    public void Delete_removes_matching_account()
    {
        var created = ProviderAccountStore.Add("glm", "Delete Me", new Dictionary<string, string> { ["apiKey"] = "x" });

        Assert.True(ProviderAccountStore.Delete(created.Id));
        Assert.Null(ProviderAccountStore.FindById(created.Id));
    }

    [Fact]
    public void ByProvider_returns_all_accounts_for_provider()
    {
        ProviderAccountStore.Save(
        [
            new ProviderAccountRecord { Id = "a1", ProviderId = "deepseek", Name = "A", Status = "active" },
            new ProviderAccountRecord { Id = "a2", ProviderId = "deepseek", Name = "B", Status = "active" },
            new ProviderAccountRecord { Id = "a3", ProviderId = "glm", Name = "C", Status = "active" }
        ]);

        var deepseek = ProviderAccountStore.ByProvider("deepseek");

        Assert.Equal(2, deepseek.Count);
    }
}
