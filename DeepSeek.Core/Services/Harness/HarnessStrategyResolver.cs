using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness;

public static class HarnessStrategyResolver
{
    public static HarnessStrategyProfile Resolve(string? strategy)
    {
        if (AgentStrategies.IsGraph(strategy))
        {
            return new HarnessStrategyProfile
            {
                Workflow = HarnessWorkflow.Graph,
                InitialPhase = HarnessPhase.Execute
            };
        }

        var s = (strategy ?? AgentStrategies.Execute).Trim().ToLowerInvariant();
        return s switch
        {
            "plan" or "blueprint" => new HarnessStrategyProfile
            {
                Workflow = HarnessWorkflow.Blueprint,
                InitialPhase = HarnessPhase.Explore
            },
            "orient" => new HarnessStrategyProfile
            {
                Workflow = HarnessWorkflow.Blueprint,
                InitialPhase = HarnessPhase.Orient
            },
            "react" or "execute" => new HarnessStrategyProfile
            {
                Workflow = HarnessWorkflow.Execute,
                InitialPhase = HarnessPhase.Execute
            },
            "team" or "sop" or "multagent" or "multi-agent" => new HarnessStrategyProfile
            {
                Workflow = HarnessWorkflow.Team,
                InitialPhase = HarnessPhase.Execute
            },
            "parallel-explore" or "parallel_explore" or "fanout" or "group-explore" => new HarnessStrategyProfile
            {
                Workflow = HarnessWorkflow.ParallelExplore,
                InitialPhase = HarnessPhase.Explore
            },
            "debate" or "camel" or "dual-agent" => new HarnessStrategyProfile
            {
                Workflow = HarnessWorkflow.Debate,
                InitialPhase = HarnessPhase.Execute
            },
            _ => new HarnessStrategyProfile
            {
                Workflow = HarnessWorkflow.Execute,
                InitialPhase = HarnessPhase.Execute
            }
        };
    }

    public static bool IsBlueprintWorkflow(string? strategy) =>
        Resolve(strategy).Workflow == HarnessWorkflow.Blueprint;

    public static string Normalize(string? strategy)
    {
        var s = (strategy ?? AgentStrategies.Execute).Trim().ToLowerInvariant();
        return s switch
        {
            "plan" or "blueprint" => AgentStrategies.Blueprint,
            AgentStrategies.Orient => AgentStrategies.Orient,
            "team" or "sop" or "multagent" or "multi-agent" => AgentStrategies.Team,
            "parallel-explore" or "parallel_explore" or "fanout" or "group-explore" => AgentStrategies.ParallelExplore,
            "debate" or "camel" or "dual-agent" => AgentStrategies.Debate,
            _ when AgentStrategies.IsGraph(strategy) => strategy!.Trim(),
            _ => AgentStrategies.Execute
        };
    }
}
