using System.Text.Json;

namespace DeepSeekBrowser.Services.Harness.Graph;

public static class HarnessGraphCondition
{
    public static bool Evaluate(string? condition, IReadOnlyDictionary<string, object?> variables)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return true;

        var expr = condition.Trim();
        if (expr.Equals("last_tool_ok", StringComparison.OrdinalIgnoreCase))
            return GetInt(variables, "last_exit_code") == 0;

        if (expr.Equals("last_tool_error", StringComparison.OrdinalIgnoreCase))
            return GetInt(variables, "last_exit_code") != 0;

        if (expr.StartsWith("last_exit_code", StringComparison.OrdinalIgnoreCase))
            return EvalNumeric(expr, "last_exit_code", variables);

        if (expr.StartsWith("variable:", StringComparison.OrdinalIgnoreCase))
        {
            var rest = expr["variable:".Length..].Trim();
            var parts = rest.Split('=', 2);
            if (parts.Length == 2)
            {
                variables.TryGetValue(parts[0].Trim(), out var val);
                return string.Equals(val?.ToString(), parts[1].Trim().Trim('"'), StringComparison.OrdinalIgnoreCase);
            }
        }

        return true;
    }

    private static bool EvalNumeric(string expr, string key, IReadOnlyDictionary<string, object?> variables)
    {
        var value = GetInt(variables, key);
        if (expr.Contains("!=", StringComparison.Ordinal))
        {
            var rhs = int.TryParse(expr.Split("!=", 2)[1].Trim(), out var n) ? n : 0;
            return value != rhs;
        }

        if (expr.Contains("==", StringComparison.Ordinal))
        {
            var rhs = int.TryParse(expr.Split("==", 2)[1].Trim(), out var n) ? n : 0;
            return value == rhs;
        }

        return true;
    }

    private static int GetInt(IReadOnlyDictionary<string, object?> variables, string key)
    {
        if (!variables.TryGetValue(key, out var val) || val is null)
            return 0;
        return val switch
        {
            int i => i,
            long l => (int)l,
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetInt32(),
            _ => int.TryParse(val.ToString(), out var n) ? n : 0
        };
    }
}
