using System.Text.Json;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness;

public enum HarnessPermissionScope
{
    Read,
    Write,
    Bash,
    Mcp,
    Network
}

public enum HarnessPermissionDecision
{
    Allow,
    Ask,
    Deny
}

public sealed class HarnessPermissionPlanResult
{
    public HarnessPermissionDecision Decision { get; init; }
    public IReadOnlyList<HarnessPermissionScope> Scopes { get; init; } = Array.Empty<HarnessPermissionScope>();
}

public static class HarnessPermissionPlan
{
    public static HarnessPermissionPlanResult Compute(string toolName, string? argumentsJson, AppConfig config)
    {
        var scopes = ResolveScopes(toolName);
        var policy = LoadScopePolicy(config);
        var decision = HarnessPermissionDecision.Allow;

        foreach (var scope in scopes)
        {
            var mode = policy.GetValueOrDefault(scope, DefaultForScope(scope, config));
            if (mode == "deny")
                return new HarnessPermissionPlanResult { Decision = HarnessPermissionDecision.Deny, Scopes = scopes };
            if (mode == "ask")
                decision = HarnessPermissionDecision.Ask;
        }

        if (decision == HarnessPermissionDecision.Ask)
            return new HarnessPermissionPlanResult { Decision = HarnessPermissionDecision.Ask, Scopes = scopes };

        return new HarnessPermissionPlanResult { Decision = HarnessPermissionDecision.Allow, Scopes = scopes };
    }

    public static IReadOnlyList<HarnessPermissionScope> ResolveScopes(string toolName)
    {
        var n = BuiltinToolExecutor.NormalizeName(toolName).ToLowerInvariant();
        if (n is "run_shell" or "bash")
            return [HarnessPermissionScope.Bash];
        if (n.Contains("write") || n.Contains("edit"))
            return [HarnessPermissionScope.Write];
        if (n is "read_file" or "read" or "list_dir" or "grep" or "glob" or "image_analyze")
            return [HarnessPermissionScope.Read];
        if (n is "websearch" or "web_search")
            return [HarnessPermissionScope.Network];
        if (n.StartsWith("mcp_", StringComparison.OrdinalIgnoreCase) || toolName.Contains(':'))
            return [HarnessPermissionScope.Mcp];
        return [HarnessPermissionScope.Read];
    }

    private static string DefaultForScope(HarnessPermissionScope scope, AppConfig config)
    {
        var mode = (config.AgentApprovalMode ?? "smart").Trim().ToLowerInvariant();
        return scope switch
        {
            HarnessPermissionScope.Read => "allow",
            HarnessPermissionScope.Write or HarnessPermissionScope.Bash => mode is "never" ? "allow" : "ask",
            HarnessPermissionScope.Mcp => mode is "never" ? "allow" : "ask",
            HarnessPermissionScope.Network => "ask",
            _ => "ask"
        };
    }

    private static Dictionary<HarnessPermissionScope, string> LoadScopePolicy(AppConfig config)
    {
        var map = new Dictionary<HarnessPermissionScope, string>();
        if (string.IsNullOrWhiteSpace(config.AgentPermissionScopesJson))
            return map;
        try
        {
            using var doc = JsonDocument.Parse(config.AgentPermissionScopesJson);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!Enum.TryParse<HarnessPermissionScope>(prop.Name, ignoreCase: true, out var scope))
                    continue;
                if (prop.Value.ValueKind == JsonValueKind.String)
                    map[scope] = prop.Value.GetString() ?? "ask";
            }
        }
        catch
        {
            // ignore invalid json
        }

        return map;
    }
}
