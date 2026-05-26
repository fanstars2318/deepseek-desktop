using System.Text.Json;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Composio;

/// <summary>Exposes Composio tools as OpenAI functions with <c>composio__</c> prefix.</summary>
public static class ComposioToolBridge
{
    public const string Prefix = "composio__";

    public static bool IsComposioTool(string toolName) =>
        toolName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public static string ToExposedName(string composioSlug) => Prefix + composioSlug;

    public static string ToComposioSlug(string exposedName) =>
        IsComposioTool(exposedName) ? exposedName[Prefix.Length..] : exposedName;

    public static IReadOnlyList<object> ToOpenAiFunctionTools(IEnumerable<ComposioToolDefinition> tools)
    {
        var list = new List<object>();
        foreach (var t in tools)
        {
            var parameters = t.InputSchema ?? new
            {
                type = "object",
                properties = new Dictionary<string, object>(),
                additionalProperties = true
            };
            list.Add(new
            {
                type = "function",
                function = new
                {
                    name = ToExposedName(t.Slug),
                    description = string.IsNullOrWhiteSpace(t.Description) ? t.Name : t.Description,
                    parameters
                }
            });
        }

        return list;
    }

    public static ComposioHttpClient? TryCreateClient(AppConfig config)
    {
        if (!config.AgentComposioEnabled || string.IsNullOrWhiteSpace(config.ComposioApiKey))
            return null;
        return new ComposioHttpClient(config.ComposioApiKey);
    }

    public static async Task<string> ExecuteAsync(
        AppConfig config,
        string exposedToolName,
        string argumentsJson,
        CancellationToken ct)
    {
        using var client = TryCreateClient(config);
        if (client is null)
            return "ERROR: Composio 未配置（启用 AgentComposioEnabled 并设置 ComposioApiKey）";

        var slug = ToComposioSlug(exposedToolName);
        var entityId = string.IsNullOrWhiteSpace(config.ComposioEntityId) ? "default" : config.ComposioEntityId;
        return await client.ExecuteActionAsync(slug, argumentsJson, entityId, ct);
    }
}
