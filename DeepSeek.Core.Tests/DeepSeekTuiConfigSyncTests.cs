using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;
using DeepSeekBrowser.Services.DeepSeekTui;

namespace DeepSeek.Core.Tests;

[Collection("Chat2ApiScope")]
public sealed class DeepSeekTuiConfigSyncTests
{
    [Fact]
    public void Apply_writes_agent_scoped_base_url_during_agent_run()
    {
        var cfg = new AppConfig { WebUserToken = "web-tok", LocalApiPort = 0 };
        using var scope = Chat2ApiFeatureScope.Begin(deepThinking: false, webSearch: false);

        DeepSeekTuiConfigSync.Apply(cfg);

        var toml = File.ReadAllText(DeepSeekTuiConfigSync.ConfigPath);
        Assert.Contains("base_url = \"http://127.0.0.1:17425/v1\"", toml);
    }

    [Fact]
    public void Apply_omits_base_url_for_web_session_outside_agent_run()
    {
        var cfg = new AppConfig { WebUserToken = "web-tok" };

        DeepSeekTuiConfigSync.Apply(cfg);

        var toml = File.ReadAllText(DeepSeekTuiConfigSync.ConfigPath);
        Assert.DoesNotContain("base_url = \"http://", toml);
    }
}
