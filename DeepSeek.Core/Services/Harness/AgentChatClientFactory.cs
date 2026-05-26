using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.ApiManagement;

namespace DeepSeekBrowser.Services.Harness;

public static class AgentChatClientFactory
{
    public static IAgentWebChat Create(AppConfig config, IAgentWebChat webChat) =>
        ApiRouteResolver.Resolve(config, webChat, config.AgentDefaultProviderId, config.Model).ChatClient;

    public static bool UsesDirectApi(AppConfig config) =>
        !ApiRouteResolver.UsesEmbeddedWeb(config, config.AgentDefaultProviderId);

    public static bool UsesDirectApiLegacy(AppConfig config) =>
        string.Equals(config.AgentInferenceMode, AgentInferenceModes.Api, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(ResolveApiKey(config));

    public static bool UsesOpenAiTools(AppConfig config) =>
        UsesDirectApi(config)
        || string.Equals(config.AgentToolCallingProtocol, AgentToolCallingProtocols.OpenAi, StringComparison.OrdinalIgnoreCase);

    public static string ResolveApiKey(AppConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.AgentApiKey))
            return config.AgentApiKey.Trim();
        if (!string.IsNullOrWhiteSpace(config.DeepSeekApiKey))
            return config.DeepSeekApiKey.Trim();
        return "";
    }

    public static string ResolveBaseUrl(AppConfig config)
    {
        var url = string.IsNullOrWhiteSpace(config.AgentApiBaseUrl)
            ? config.ApiBaseUrl
            : config.AgentApiBaseUrl;
        return (url ?? "https://api.deepseek.com").Trim().TrimEnd('/');
    }
}
