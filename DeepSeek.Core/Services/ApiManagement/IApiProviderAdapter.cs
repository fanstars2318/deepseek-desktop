using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.Harness;

namespace DeepSeekBrowser.Services.ApiManagement;

public interface IApiProviderAdapter
{
    ApiProviderEntry Provider { get; }
    string RouteMode { get; }
    IAgentWebChat CreateChatClient(
        AppConfig config,
        IAgentWebChat? webBridge,
        ProviderAccountRecord? account = null);
    Task<ApiProviderHealth> ProbeHealthAsync(AppConfig config, CancellationToken ct = default);
    IReadOnlyList<string> ListModels(AppConfig config);
}
