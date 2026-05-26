using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.Harness;

namespace DeepSeekBrowser.Services.ApiManagement;

public sealed class ProviderSidecarAdapter : IApiProviderAdapter
{
    public ProviderSidecarAdapter(ApiProviderEntry provider) => Provider = provider;

    public ApiProviderEntry Provider { get; }

    public string RouteMode => ApiRouteModes.SidecarHttp;

    public IAgentWebChat CreateChatClient(
        AppConfig config,
        IAgentWebChat? webBridge,
        ProviderAccountRecord? account = null)
    {
        ProviderSidecarHost.EnsureRunning(Provider);
        var sidecarConfig = CloneSidecarConfig(config, account);
        return new OpenAiAgentChatClient(sidecarConfig);
    }

    public async Task<ApiProviderHealth> ProbeHealthAsync(AppConfig config, CancellationToken ct = default)
    {
        var ok = await ProviderSidecarHost.ProbeHealthAsync(Provider, ct);
        return new ApiProviderHealth
        {
            Online = ok,
            Message = ok ? "Sidecar 在线" : "Sidecar 未响应"
        };
    }

    public IReadOnlyList<string> ListModels(AppConfig config) =>
        Provider.Models.Count > 0 ? Provider.Models : OpenAiCompatibleAdapterFallbackModels();

    private static IReadOnlyList<string> OpenAiCompatibleAdapterFallbackModels() =>
        ["gpt-4o-mini", "gpt-4o"];

    private AppConfig CloneSidecarConfig(AppConfig config, ProviderAccountRecord? account)
    {
        var baseUrl = string.IsNullOrWhiteSpace(Provider.BaseUrl)
            ? "http://127.0.0.1:8080/v1"
            : Provider.BaseUrl;
        return new AppConfig
        {
            AgentInferenceMode = AgentInferenceModes.Api,
            AgentToolCallingProtocol = AgentToolCallingProtocols.OpenAi,
            AgentApiKey = AccountCredentials.ResolveApiKey(account, Provider) ?? "local",
            AgentApiBaseUrl = baseUrl.TrimEnd('/'),
            Model = config.Model
        };
    }
}
