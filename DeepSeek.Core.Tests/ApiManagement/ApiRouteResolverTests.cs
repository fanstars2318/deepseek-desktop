using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;
using DeepSeekBrowser.Services.ApiManagement;
using DeepSeekBrowser.Services.Harness;
using Xunit;

namespace DeepSeek.Core.Tests.ApiManagement;

public sealed class ApiRouteResolverTests : TestConfigIsolation
{
    [Fact]
    public void Resolve_returns_builtin_web_by_default()
    {
        var config = new AppConfig();
        var resolution = ApiRouteResolver.Resolve(config, new StubWebChat());

        Assert.Equal("deepseek", resolution.Provider.Id);
        Assert.Equal(ApiRouteModes.EmbeddedWeb, resolution.RouteMode);
        Assert.Equal(ApiProviderKinds.BuiltinWeb, resolution.Provider.Kind);
    }

    private sealed class StubWebChat : IAgentWebChat
    {
        public Task<WebChatResult> CompleteAsync(
            IReadOnlyList<ChatMessage> messages,
            string model,
            bool thinking,
            bool search,
            IReadOnlyList<string> refFileIds,
            bool allowToolCalls,
            CancellationToken ct,
            string? webUserToken = null,
            string? webChatSessionId = null,
            AgentChatOptions? options = null) =>
            Task.FromResult(new WebChatResult { Content = "ok" });

        public async IAsyncEnumerable<WebChatStreamEvent> StreamAsync(
            IReadOnlyList<ChatMessage> messages,
            string model,
            bool thinking,
            bool search,
            IReadOnlyList<string> refFileIds,
            bool allowToolCalls,
            CancellationToken ct,
            string? webUserToken = null,
            string? webChatSessionId = null,
            AgentChatOptions? options = null)
        {
            await Task.CompletedTask;
            yield return new WebChatStreamDone(new WebChatResult { Content = "ok" });
        }
    }

    [Fact]
    public void ResolveProviderForModel_matches_configured_mapping()
    {
        var config = new AppConfig
        {
            ApiProviders =
            [
                new ApiProviderEntry
                {
                    Id = "openai-test",
                    DisplayName = "OpenAI Test",
                    Kind = ApiProviderKinds.OpenAiCompatible,
                    RouteMode = ApiRouteModes.DirectApi,
                    Enabled = true,
                    ModelMappings =
                    [
                        new ModelMappingEntry { RequestModel = "gpt-test", ActualModel = "gpt-4o-mini" }
                    ]
                }
            ]
        };

        var provider = ApiRouteResolver.ResolveProviderForModel(config, "gpt-test");

        Assert.NotNull(provider);
        Assert.Equal("openai-test", provider!.Id);
    }
}
