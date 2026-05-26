using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;

namespace DeepSeek.Core.Tests;

[Collection("DsdApiScope")]
public sealed class InternalChatChannelTests
{
    [Fact]
    public void DesktopV1_is_internal_scheme()
    {
        Assert.True(InternalChatChannel.IsInternal(InternalChatChannel.DesktopV1));
        Assert.False(InternalChatChannel.IsInternal("http://127.0.0.1:5111/v1"));
    }

    [Fact]
    public void ResolveTuiLlmBaseUrl_uses_official_api_when_key_present()
    {
        var cfg = new AppConfig
        {
            DeepSeekApiKey = "sk-test",
            ApiBaseUrl = "https://api.deepseek.com"
        };
        Assert.Equal("https://api.deepseek.com/v1", InternalChatChannel.ResolveTuiLlmBaseUrl(cfg));
    }

    [Fact]
    public void ResolveTuiLlmBaseUrl_null_for_web_session_only()
    {
        var cfg = new AppConfig { WebUserToken = "web-tok" };
        Assert.Null(InternalChatChannel.ResolveTuiLlmBaseUrl(cfg));
    }

    [Fact]
    public void External_api_port_defaults_when_zero()
    {
        var cfg = new AppConfig { LocalApiPort = 0 };
        Assert.Equal(InternalChatChannel.ExternalApiDefaultPort, InternalChatChannel.ResolveExternalApiPort(cfg));
    }

    [Fact]
    public void ResolveTuiLlmBaseUrl_uses_agent_scoped_loopback_during_agent_run()
    {
        var cfg = new AppConfig { WebUserToken = "web-tok" };
        using var scope = DsdAgentApiScope.Begin(deepThinking: true, webSearch: false);
        Assert.Equal("http://127.0.0.1:17425/v1", InternalChatChannel.ResolveTuiLlmBaseUrl(cfg));
    }
}
