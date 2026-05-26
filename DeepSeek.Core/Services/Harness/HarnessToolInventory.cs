using System.Text;
using System.Text.RegularExpressions;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness;

/// <summary>当前 Run 可用的内置 + MCP 工具清单（供意图分析与存在性校验）。</summary>
public sealed class HarnessToolInventory
{
    private readonly HashSet<string> _names;

    public HarnessToolInventory(IEnumerable<string> names)
    {
        _names = new HashSet<string>(names.Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> Names => _names;

    public bool IsAvailable(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName)) return false;
        var n = Normalize(toolName);
        if (_names.Contains(n)) return true;
        return Aliases.TryGetValue(n, out var mapped) && _names.Contains(mapped);
    }

    public string? ResolveAvailableName(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName)) return null;
        var n = Normalize(toolName);
        if (_names.Contains(n)) return n;
        if (Aliases.TryGetValue(n, out var mapped) && _names.Contains(mapped))
            return mapped;
        return null;
    }

    public string BuildCompactNameList(int maxNames = 40)
    {
        var names = _names.OrderBy(n => n).Take(Math.Clamp(maxNames, 8, 120));
        return "可用工具名（tools[].name 必须从中选）：" + string.Join(", ", names);
    }

    public string BuildPromptSection(int maxMcpLines = 20)
    {
        var builtins = _names.Where(n => !n.Contains("__", StringComparison.Ordinal)).OrderBy(n => n).ToList();
        var mcp = _names.Where(n => n.Contains("__", StringComparison.Ordinal)).OrderBy(n => n).Take(maxMcpLines).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("【当前可用工具 · 仅可调用下列名称】");
        sb.AppendLine("内置：" + (builtins.Count > 0 ? string.Join(", ", builtins) : "（无）"));
        if (mcp.Count > 0)
            sb.AppendLine("MCP：" + string.Join(", ", mcp));
        else
            sb.AppendLine("MCP：（未连接或无工具）");
        return sb.ToString().TrimEnd();
    }

    public static HarnessToolInventory Build(AppConfig config, string? mcpCatalogText)
    {
        var names = new List<string>
        {
            "read_file", "read", "write_file", "write", "list_dir", "glob", "grep",
            "run_shell", "bash", "image_analyze", "AskUserQuestion", "askuserquestion",
            "UpdatePlan", "updateplan", "WebSearch", "web_search"
        };

        if (config.EnableSubAgents)
        {
            names.Add("delegate_agent");
            names.Add("DelegateAgent");
            if (config.EnableParallelExplore)
            {
                names.Add("parallel_explore");
                names.Add("ParallelExplore");
            }
        }

        if (!string.IsNullOrWhiteSpace(mcpCatalogText))
            names.AddRange(ParseMcpNamesFromCatalog(mcpCatalogText));

        return new HarnessToolInventory(names);
    }

    public static IReadOnlyList<string> ParseMcpNamesFromCatalog(string catalog)
    {
        var list = new List<string>();
        foreach (var line in catalog.Split('\n'))
        {
            var t = line.Trim();
            if (!t.StartsWith('-')) continue;
            var body = t[1..].Trim();
            if (body.Length == 0) continue;
            var name = body.Split([' ', '—', '-'], 2, StringSplitOptions.TrimEntries)[0];
            if (!string.IsNullOrWhiteSpace(name) && !name.StartsWith('（'))
                list.Add(name);
        }

        return list;
    }

    private static string Normalize(string raw) =>
        BuiltinToolExecutor.NormalizeName(raw.Trim());

    private static readonly Dictionary<string, string> Aliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["read"] = "read_file",
            ["write"] = "write_file",
            ["bash"] = "run_shell",
            ["delegate"] = "delegate_agent",
            ["websearch"] = "web_search"
        };
}
