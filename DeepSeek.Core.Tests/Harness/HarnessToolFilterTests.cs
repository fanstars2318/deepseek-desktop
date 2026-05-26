using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.Harness;
using Xunit;
using AgentStrategies = DeepSeekBrowser.Models.AgentStrategies;

namespace DeepSeek.Core.Tests.Harness;

public sealed class HarnessToolFilterTests
{
    [Fact]
    public void FromIntent_keeps_core_read_tools_and_planned_write()
    {
        var inv = HarnessToolInventory.Build(new AppConfig { EnableSubAgents = false }, null);
        var intent = new HarnessRunIntent
        {
            PlannedTools =
            [
                new HarnessPlannedTool { Name = "write_file", Purpose = "edit", IsAvailable = true }
            ]
        };
        var sel = HarnessToolFilter.FromIntent(intent, inv, new AppConfig());
        Assert.Contains("read", sel.BuiltinOpenAiNames);
        Assert.Contains("write", sel.BuiltinOpenAiNames);
    }

    [Fact]
    public void Intent_cache_roundtrip()
    {
        var state = new HarnessRunState();
        var intent = new HarnessRunIntent { Analysis = "test", UsedLlm = true };
        HarnessIntentCache.Save(state, "fix the bug in harness", intent);
        var req = new HarnessRunRequest
        {
            Prompt = "fix the bug in harness",
            Strategy = AgentStrategies.Execute,
            Config = new AppConfig { AgentIntentCacheEnabled = true }
        };
        var restored = HarnessIntentCache.TryRestore(req, state);
        Assert.NotNull(restored);
        Assert.Equal("test", restored!.Analysis);
    }

    [Fact]
    public void Intent_cache_misses_when_prompt_changes()
    {
        var state = new HarnessRunState();
        HarnessIntentCache.Save(state, "first prompt", new HarnessRunIntent { Analysis = "a" });
        var req = new HarnessRunRequest
        {
            Prompt = "completely different task about databases",
            Strategy = AgentStrategies.Execute,
            Config = new AppConfig { AgentIntentCacheEnabled = true }
        };
        Assert.Null(HarnessIntentCache.TryRestore(req, state));
    }
}
