using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness;

public static class AgentChatClientFactory
{
    public static IAgentWebChat Create(AppConfig config, IAgentWebChat webChat)
    {
        if (UsesDirectApi(config))
            return new OpenAiAgentChatClient(config);
        return webChat;
    }

    public static bool UsesDirectApi(AppConfig config) =>
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
