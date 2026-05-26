using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.Harness;

namespace DeepSeek.Core.Tests.Harness;

public sealed class HarnessMultiAgentModesTests
{
    [Theory]
    [InlineData("parallel-explore", HarnessWorkflow.ParallelExplore)]
    [InlineData("parallel_explore", HarnessWorkflow.ParallelExplore)]
    [InlineData("fanout", HarnessWorkflow.ParallelExplore)]
    [InlineData("debate", HarnessWorkflow.Debate)]
    [InlineData("camel", HarnessWorkflow.Debate)]
    [InlineData("team", HarnessWorkflow.Team)]
    public void Strategy_resolver_maps_multi_agent_workflows(string strategy, HarnessWorkflow expected)
    {
        var profile = HarnessStrategyResolver.Resolve(strategy);
        Assert.Equal(expected, profile.Workflow);
    }

    [Fact]
    public void Agent_strategies_expose_parallel_and_debate_constants()
    {
        Assert.Equal("parallel-explore", AgentStrategies.ParallelExplore);
        Assert.Equal("debate", AgentStrategies.Debate);
    }

    [Fact]
    public void Role_registry_resolves_advocate_and_critic()
    {
        Assert.Equal("advocate", HarnessAgentRoleRegistry.Resolve("proposer").Id);
        Assert.Equal("critic", HarnessAgentRoleRegistry.Resolve("skeptic").Id);
    }
}
