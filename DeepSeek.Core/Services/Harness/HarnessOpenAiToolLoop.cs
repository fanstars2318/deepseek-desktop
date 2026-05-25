using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness;

/// <summary>OpenAI function calling 工具列表与 chat options 构建。</summary>
public static class HarnessOpenAiToolLoop
{
    public static Task<AgentChatOptions> BuildChatOptionsAsync(
        AppConfig config,
        McpHub mcp,
        bool allowTools,
        CancellationToken ct,
        HarnessRunIntent? intent = null,
        HarnessToolInventory? inventory = null) =>
        BuildChatOptionsAsync(config, mcp, allowTools, ct, intent, inventory, fullMcp: intent is null);

    public static async Task<AgentChatOptions> BuildChatOptionsAsync(
        AppConfig config,
        McpHub mcp,
        bool allowTools,
        CancellationToken ct,
        HarnessRunIntent? intent,
        HarnessToolInventory? inventory,
        bool fullMcp)
    {
        if (!allowTools || !AgentChatClientFactory.UsesOpenAiTools(config))
            return new AgentChatOptions { UseOpenAiTools = false };

        var inv = inventory ?? HarnessToolInventory.Build(config, null);
        var selection = fullMcp || intent is null
            ? HarnessToolFilter.FullSelection(config)
            : HarnessToolFilter.FromIntent(intent, inv, config);

        var allBuiltin = HarnessOpenAiBuiltinTools.GetDefinitions(
            config.AgentAllowShell,
            config.EnableSubAgents,
            config.EnableSubAgents && config.EnableParallelExplore);

        var tools = new List<object>();
        foreach (var def in allBuiltin)
        {
            if (HarnessToolFilter.MatchesOpenAiTool(def, selection))
                tools.Add(def);
        }

        if (!string.IsNullOrWhiteSpace(config.AgentWebSearchScript)
            && selection.BuiltinOpenAiNames.Contains("WebSearch"))
        {
            tools.Add(new
            {
                type = "function",
                function = new
                {
                    name = "WebSearch",
                    description = "Perform web search using configured script.",
                    parameters = new
                    {
                        type = "object",
                        properties = new { query = new { type = "string", description = "Search query" } },
                        required = new[] { "query" },
                        additionalProperties = false
                    }
                }
            });
        }

        var maxMcp = Math.Clamp(config.AgentMcpToolsMaxInRequest, 0, 64);
        try
        {
            var mcpTools = await mcp.GetOpenAiToolsAsync(ct);
            var mcpAdded = 0;
            foreach (var mcpTool in mcpTools)
            {
                if (!HarnessToolFilter.MatchesOpenAiTool(mcpTool, selection))
                    continue;
                tools.Add(mcpTool);
                mcpAdded++;
                if (maxMcp > 0 && mcpAdded >= maxMcp)
                    break;
            }
        }
        catch
        {
            // MCP tools optional
        }

        return new AgentChatOptions
        {
            UseOpenAiTools = true,
            Tools = tools,
            ReasoningEffort = AgentReasoningEfforts.Normalize(config.AgentReasoningEffort)
        };
    }

    public static string NormalizeToolName(string rawName) =>
        HarnessOpenAiBuiltinTools.MapToBuiltinExecutorName(rawName);
}
