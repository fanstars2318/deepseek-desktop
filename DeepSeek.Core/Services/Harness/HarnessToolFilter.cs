using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness;

/// <summary>按运行前意图裁剪 OpenAI tools（内置 + MCP），减少 schema token。</summary>
public sealed class HarnessToolSelection
{
    public HashSet<string> BuiltinOpenAiNames { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>null = 允许全部已连接 MCP（受 maxMcp 限制）。</summary>
    public HashSet<string>? McpExposedNames { get; init; }
    public bool RestrictMcp { get; init; }
}

public static class HarnessToolFilter
{
    private static readonly string[] CoreReadOnlyOpenAi =
        ["read", "list_dir", "grep", "glob"];

    public static HarnessToolSelection FromIntent(
        HarnessRunIntent? intent,
        HarnessToolInventory inventory,
        AppConfig config)
    {
        if (intent?.PlannedTools is null || intent.PlannedTools.Count == 0)
            return FullSelection(config);

        var builtins = new HashSet<string>(CoreReadOnlyOpenAi, StringComparer.OrdinalIgnoreCase);
        var mcp = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var planned in intent.PlannedTools)
        {
            var resolved = inventory.ResolveAvailableName(planned.Name);
            if (string.IsNullOrWhiteSpace(resolved)) continue;

            if (resolved.Contains("__", StringComparison.Ordinal))
            {
                mcp.Add(resolved);
                continue;
            }

            var openAi = ToOpenAiBuiltinName(resolved);
            if (!string.IsNullOrWhiteSpace(openAi))
                builtins.Add(openAi);
        }

        if (config.AgentAllowShell)
            builtins.Add("bash");
        if (config.EnableSubAgents)
        {
            builtins.Add("delegate_agent");
            if (config.EnableParallelExplore)
                builtins.Add("parallel_explore");
        }

        if (!string.IsNullOrWhiteSpace(config.AgentWebSearchScript))
            builtins.Add("WebSearch");

        return new HarnessToolSelection
        {
            BuiltinOpenAiNames = builtins,
            McpExposedNames = mcp.Count > 0 ? mcp : null,
            RestrictMcp = mcp.Count > 0
        };
    }

    public static HarnessToolSelection FromPhase(HarnessPhase phase, AppConfig config)
    {
        if (HarnessPhasePolicy.IsReadonlyPhase(phase))
        {
            var builtins = new HashSet<string>(CoreReadOnlyOpenAi, StringComparer.OrdinalIgnoreCase);
            if (config.EnableSubAgents)
            {
                builtins.Add("delegate_agent");
                if (config.EnableParallelExplore)
                    builtins.Add("parallel_explore");
            }
            if (!string.IsNullOrWhiteSpace(config.AgentWebSearchScript))
                builtins.Add("WebSearch");
            return new HarnessToolSelection { BuiltinOpenAiNames = builtins, McpExposedNames = null, RestrictMcp = false };
        }

        return FullSelection(config);
    }

    public static HarnessToolSelection FullSelection(AppConfig config)
    {
        var builtins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "read", "write", "edit", "list_dir", "grep", "glob", "image_analyze", "AskUserQuestion", "UpdatePlan"
        };
        if (config.AgentAllowShell) builtins.Add("bash");
        if (config.EnableSubAgents)
        {
            builtins.Add("delegate_agent");
            if (config.EnableParallelExplore) builtins.Add("parallel_explore");
        }
        if (!string.IsNullOrWhiteSpace(config.AgentWebSearchScript))
            builtins.Add("WebSearch");
        return new HarnessToolSelection { BuiltinOpenAiNames = builtins, McpExposedNames = null, RestrictMcp = false };
    }

    public static string? ToOpenAiBuiltinName(string executorName) =>
        executorName.ToLowerInvariant() switch
        {
            "read_file" or "read" => "read",
            "write_file" or "write" => "write",
            "edit_file" or "edit" => "edit",
            "run_shell" or "bash" => "bash",
            "list_dir" => "list_dir",
            "grep" => "grep",
            "glob" => "glob",
            "image_analyze" => "image_analyze",
            "delegate_agent" => "delegate_agent",
            "parallel_explore" => "parallel_explore",
            "askuserquestion" => "AskUserQuestion",
            "updateplan" => "UpdatePlan",
            "web_search" or "websearch" => "WebSearch",
            _ => executorName.Contains("__", StringComparison.Ordinal) ? null : executorName
        };

    public static bool MatchesOpenAiTool(object toolObj, HarnessToolSelection selection)
    {
        var name = ExtractFunctionName(toolObj);
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (selection.BuiltinOpenAiNames.Contains(name)) return true;
        if (name.Contains("__", StringComparison.Ordinal))
        {
            if (name.StartsWith("composio__", StringComparison.OrdinalIgnoreCase))
                return true;
            if (!selection.RestrictMcp) return true;
            return selection.McpExposedNames?.Contains(name) == true;
        }
        return false;
    }

    public static string? ExtractFunctionName(object toolObj)
    {
        try
        {
            var t = toolObj.GetType();
            var fnProp = t.GetProperty("function");
            if (fnProp?.GetValue(toolObj) is { } fn)
            {
                var nameProp = fn.GetType().GetProperty("name");
                return nameProp?.GetValue(fn)?.ToString();
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }
}
