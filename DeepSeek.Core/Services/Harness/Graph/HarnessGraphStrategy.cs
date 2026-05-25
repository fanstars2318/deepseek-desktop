using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness.Graph;

public static class HarnessGraphStrategy
{
    public const string Prefix = AgentStrategies.GraphPrefix;

    public static bool TryParse(string? strategy, out string graphId)
    {
        graphId = "";
        var id = AgentStrategies.ParseGraphId(strategy);
        if (string.IsNullOrWhiteSpace(id)) return false;
        graphId = id;
        return true;
    }

    public static string Format(string graphId) => AgentStrategies.GraphStrategy(graphId);
}
