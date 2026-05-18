using System.Text.Json;

namespace DeepSeekBrowser.Services;

internal static class AgentSessionJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
