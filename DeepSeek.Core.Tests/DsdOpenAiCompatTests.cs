using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;

namespace DeepSeek.Core.Tests;

public sealed class DsdOpenAiCompatTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("off", false)]
    [InlineData("none", false)]
    [InlineData("disabled", false)]
    [InlineData("low", true)]
    [InlineData("high", true)]
    public void IsReasoningEffortEnabled_parses_effort(string? effort, bool expected) =>
        Assert.Equal(expected, DsdOpenAiCompat.IsReasoningEffortEnabled(effort));

    [Fact]
    public void ApplyAgentScopeDefaults_does_not_force_thinking_during_active_agent_run()
    {
        var cfg = new AppConfig { AgentDeepThinking = true };
        var thinking = false;
        var search = false;
        using var scope = DsdAgentApiScope.Begin(deepThinking: true, webSearch: false);

        DsdOpenAiCompat.ApplyAgentScopeDefaultsForTest(cfg, ref thinking, ref search, explicitReasoningEffort: "off");

        Assert.False(thinking);
    }

    [Fact]
    public void EnsureDefaultMappings_populates_when_empty()
    {
        var cfg = new AppConfig();
        DsdOpenAiCompat.EnsureDefaultMappings(cfg);
        Assert.NotEmpty(cfg.ModelMappings);
        Assert.Contains(cfg.ModelMappings, m =>
            string.Equals(m.RequestModel, "deepseek-v4-pro", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnsureDefaultMappings_is_idempotent()
    {
        var cfg = new AppConfig();
        DsdOpenAiCompat.EnsureDefaultMappings(cfg);
        var count = cfg.ModelMappings.Count;
        DsdOpenAiCompat.EnsureDefaultMappings(cfg);
        Assert.Equal(count, cfg.ModelMappings.Count);
    }
}
