using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.Composio;

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
        HarnessToolInventory? inventory = null,
        HarnessPhase? phase = null) =>
        BuildChatOptionsAsync(config, mcp, allowTools, ct, intent, inventory, fullMcp: intent is null, phase);

    public static async Task<AgentChatOptions> BuildChatOptionsAsync(
        AppConfig config,
        McpHub mcp,
        bool allowTools,
        CancellationToken ct,
        HarnessRunIntent? intent,
        HarnessToolInventory? inventory,
        bool fullMcp,
        HarnessPhase? phase = null)
    {
        if (!allowTools || !AgentChatClientFactory.UsesOpenAiTools(config))
            return new AgentChatOptions { UseOpenAiTools = false };

        var inv = inventory ?? HarnessToolInventory.Build(config, null);
        var selection = intent is not null
            ? HarnessToolFilter.FromIntent(intent, inv, config)
            : phase is not null
                ? HarnessToolFilter.FromPhase(phase.Value, config)
                : fullMcp
                    ? HarnessToolFilter.FullSelection(config)
                    : HarnessToolFilter.FromPhase(HarnessPhase.Execute, config);

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
            if (intent is not null && !fullMcp)
                mcpTools = RankMcpToolsByIntent(mcpTools, intent, maxMcp > 0 ? maxMcp : 64);

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

        if (config.AgentComposioEnabled)
        {
            try
            {
                using var composio = ComposioToolBridge.TryCreateClient(config);
                if (composio is not null)
                {
                    var composioTools = await composio.GetToolsAsync(
                        config.ComposioEntityId,
                        config.ComposioDefaultToolkits,
                        Math.Clamp(maxMcp > 0 ? maxMcp : 16, 1, 32),
                        ct);
                    foreach (var def in ComposioToolBridge.ToOpenAiFunctionTools(composioTools))
                    {
                        if (HarnessToolFilter.MatchesOpenAiTool(def, selection))
                            tools.Add(def);
                    }
                }
            }
            catch
            {
                // Composio optional
            }
        }

        return new AgentChatOptions
        {
            UseOpenAiTools = true,
            Tools = tools,
            ReasoningEffort = AgentReasoningEfforts.Normalize(config.AgentReasoningEffort)
        };
    }

    internal static List<object> RankMcpToolsByIntent(
        IReadOnlyList<object> mcpTools,
        HarnessRunIntent intent,
        int maxCount)
    {
        if (mcpTools.Count == 0 || maxCount <= 0)
            return mcpTools.ToList();

        var keywords = BuildIntentKeywords(intent);
        if (keywords.Count == 0)
            return mcpTools.Take(maxCount).ToList();

        var scored = mcpTools
            .Select(t =>
            {
                var name = HarnessToolFilter.ExtractFunctionName(t) ?? "";
                var score = ScoreName(name, keywords);
                return (Tool: t, Score: score);
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => HarnessToolFilter.ExtractFunctionName(x.Tool) ?? "")
            .Take(maxCount)
            .Select(x => x.Tool)
            .ToList();

        return scored;
    }

    private static HashSet<string> BuildIntentKeywords(HarnessRunIntent intent)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddWords(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            foreach (var w in text.Split([' ', '\n', '\r', '\t', ',', '.', ';'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (w.Length >= 3)
                    set.Add(w.ToLowerInvariant());
            }
        }

        AddWords(intent.Analysis);
        if (intent.PlannedTools is null) return set;
        foreach (var p in intent.PlannedTools)
        {
            AddWords(p.Name);
            AddWords(p.Purpose);
        }

        return set;
    }

    private static int ScoreName(string name, HashSet<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0;
        var lower = name.ToLowerInvariant();
        var score = 0;
        foreach (var kw in keywords)
        {
            if (lower.Contains(kw, StringComparison.Ordinal))
                score += 2;
        }

        return score;
    }

    public static string NormalizeToolName(string rawName) =>
        HarnessOpenAiBuiltinTools.MapToBuiltinExecutorName(rawName);
}
