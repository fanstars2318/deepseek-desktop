using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;

namespace DeepSeek.Core.Tests;

public sealed class Chat2ApiCompatTests
{
    [Fact]
    public void EnsureDefaultMappings_populates_when_empty()
    {
        var cfg = new AppConfig();
        Chat2ApiCompat.EnsureDefaultMappings(cfg);
        Assert.NotEmpty(cfg.ModelMappings);
        Assert.Contains(cfg.ModelMappings, m =>
            string.Equals(m.RequestModel, "deepseek-v4-pro", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnsureDefaultMappings_is_idempotent()
    {
        var cfg = new AppConfig();
        Chat2ApiCompat.EnsureDefaultMappings(cfg);
        var count = cfg.ModelMappings.Count;
        Chat2ApiCompat.EnsureDefaultMappings(cfg);
        Assert.Equal(count, cfg.ModelMappings.Count);
    }
}
