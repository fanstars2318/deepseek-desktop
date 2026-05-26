using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;
using DeepSeekBrowser.Services.ApiManagement;
using Xunit;

namespace DeepSeek.Core.Tests.ApiManagement;

public sealed class AccountLoadBalancerTests : TestConfigIsolation
{
    public AccountLoadBalancerTests() => AccountLoadBalancer.Instance.ResetStateForTests();

    [Fact]
    public void Round_robin_alternates_accounts()
    {
        var config = BuildConfig(AccountLoadBalancer.StrategyRoundRobin);
        SeedAccounts("acc-a", "acc-b");

        var first = AccountLoadBalancer.Instance.SelectAccount(config, "gpt-4o-mini")!.Account.Id;
        var second = AccountLoadBalancer.Instance.SelectAccount(config, "gpt-4o-mini")!.Account.Id;
        var third = AccountLoadBalancer.Instance.SelectAccount(config, "gpt-4o-mini")!.Account.Id;

        Assert.NotEqual(first, second);
        Assert.Equal(first, third);
    }

    [Fact]
    public void Fill_first_picks_lowest_today_used()
    {
        var config = BuildConfig(AccountLoadBalancer.StrategyFillFirst);
        ProviderAccountStore.Save(
        [
            ActiveAccount("low", todayUsed: 1),
            ActiveAccount("high", todayUsed: 9)
        ]);

        var picked = AccountLoadBalancer.Instance.SelectAccount(config, "gpt-4o-mini");

        Assert.NotNull(picked);
        Assert.Equal("low", picked!.Account.Id);
    }

    [Fact]
    public void Failover_skips_accounts_after_three_failures()
    {
        var config = BuildConfig(AccountLoadBalancer.StrategyFailover);
        SeedAccounts("healthy", "sick");
        for (var i = 0; i < 3; i++)
            AccountLoadBalancer.Instance.MarkAccountFailed("sick");

        var picked = AccountLoadBalancer.Instance.SelectAccount(config, "gpt-4o-mini");

        Assert.NotNull(picked);
        Assert.Equal("healthy", picked!.Account.Id);
    }

    [Fact]
    public void Preferred_account_is_honored_when_healthy()
    {
        var config = BuildConfig(AccountLoadBalancer.StrategyRoundRobin);
        SeedAccounts("acc-a", "acc-b");

        var picked = AccountLoadBalancer.Instance.SelectAccount(
            config,
            "gpt-4o-mini",
            preferredAccountId: "acc-b");

        Assert.NotNull(picked);
        Assert.Equal("acc-b", picked!.Account.Id);
    }

    private static AppConfig BuildConfig(string strategy) => new()
    {
        DsdApiLoadBalanceStrategy = strategy,
        ApiProviders =
        [
            new ApiProviderEntry
            {
                Id = "openai-test",
                DisplayName = "OpenAI Test",
                Kind = ApiProviderKinds.OpenAiCompatible,
                RouteMode = ApiRouteModes.DirectApi,
                Enabled = true,
                Models = ["gpt-4o-mini"]
            }
        ]
    };

    private static void SeedAccounts(params string[] ids)
    {
        ProviderAccountStore.Save(ids.Select(id => ActiveAccount(id)).ToList());
    }

    private static ProviderAccountRecord ActiveAccount(string id, int todayUsed = 0) => new()
    {
        Id = id,
        ProviderId = "openai-test",
        Name = id,
        Status = "active",
        Credentials = new Dictionary<string, string> { ["api_key"] = id + "-key" },
        TodayUsed = todayUsed,
        TodayKey = DateTime.UtcNow.ToString("yyyy-MM-dd")
    };
}
