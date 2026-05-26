using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.Harness;

namespace DeepSeekBrowser.Services.ApiManagement;

public sealed class OpenAiCompatibleAdapter : IApiProviderAdapter
{
    public OpenAiCompatibleAdapter(ApiProviderEntry provider) => Provider = provider;

    public ApiProviderEntry Provider { get; }

    public string RouteMode => ApiRouteModes.DirectApi;

    public IAgentWebChat CreateChatClient(
        AppConfig config,
        IAgentWebChat? webBridge,
        ProviderAccountRecord? account = null)
    {
        var clone = CloneConfigForProvider(config, account);
        return new OpenAiAgentChatClient(clone);
    }

    public Task<ApiProviderHealth> ProbeHealthAsync(AppConfig config, CancellationToken ct = default)
    {
        var key = ApiProviderRegistry.ResolveApiKey(Provider);
        var online = !string.IsNullOrWhiteSpace(key);
        return Task.FromResult(new ApiProviderHealth
        {
            Online = online,
            Message = online ? "API Key 已配置" : "请在 API 管理中配置 API Key"
        });
    }

    public IReadOnlyList<string> ListModels(AppConfig config)
    {
        if (Provider.Models.Count > 0)
            return Provider.Models;
        return ["deepseek-chat", "deepseek-reasoner"];
    }

    private AppConfig CloneConfigForProvider(AppConfig config, ProviderAccountRecord? account)
    {
        var key = AccountCredentials.ResolveApiKey(account, Provider) ?? config.DeepSeekApiKey;
        var baseUrl = string.IsNullOrWhiteSpace(Provider.BaseUrl) ? config.ApiBaseUrl : Provider.BaseUrl;
        return new AppConfig
        {
            AgentInferenceMode = AgentInferenceModes.Api,
            AgentToolCallingProtocol = AgentToolCallingProtocols.OpenAi,
            AgentApiKey = key,
            AgentApiBaseUrl = baseUrl,
            Model = config.Model,
            AgentReasoningEffort = config.AgentReasoningEffort
        };
    }
}
