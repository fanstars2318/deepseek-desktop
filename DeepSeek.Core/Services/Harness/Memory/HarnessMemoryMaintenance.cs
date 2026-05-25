using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness.Memory;

public static class HarnessMemoryMaintenance
{
    public static int PruneExpiredSessions(AppConfig config)
    {
        if (config.AgentSemanticMemorySessionTtlDays <= 0)
            return 0;

        var cutoff = DateTimeOffset.UtcNow
            .AddDays(-config.AgentSemanticMemorySessionTtlDays)
            .ToUnixTimeSeconds();

        using var store = new HarnessSemanticMemoryStore();
        return store.PruneScopesOlderThan(["session:"], cutoff);
    }
}
