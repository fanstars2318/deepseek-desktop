using System.Text;
using System.Text.RegularExpressions;

namespace DeepSeekBrowser.Services.QwenCode;

internal static class QwenMarkdownFrontmatter
{
    private static readonly Regex FrontmatterRe = new(
        @"\A---\s*\r?\n([\s\S]*?)\r?\n---\s*\r?\n([\s\S]*)\z",
        RegexOptions.Compiled);

    public static (IReadOnlyDictionary<string, string> Meta, string Body) Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (new Dictionary<string, string>(), "");

        var m = FrontmatterRe.Match(raw.TrimStart('\uFEFF'));
        if (!m.Success)
            return (new Dictionary<string, string>(), raw.Trim());

        var meta = ParseYamlLike(m.Groups[1].Value);
        return (meta, m.Groups[2].Value.Trim());
    }

    private static Dictionary<string, string> ParseYamlLike(string yaml)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? listKey = null;
        var listItems = new List<string>();

        void FlushList()
        {
            if (listKey is null) return;
            dict[listKey] = string.Join(",", listItems);
            listKey = null;
            listItems.Clear();
        }

        foreach (var line in yaml.Split('\n'))
        {
            var t = line.TrimEnd();
            if (string.IsNullOrWhiteSpace(t))
            {
                FlushList();
                continue;
            }

            if (t.StartsWith("- ") && listKey is not null)
            {
                listItems.Add(t[2..].Trim());
                continue;
            }

            FlushList();
            var colon = t.IndexOf(':');
            if (colon <= 0) continue;
            var key = t[..colon].Trim();
            var val = t[(colon + 1)..].Trim().Trim('"');
            if (string.IsNullOrEmpty(val) && colon == t.Length - 1)
            {
                listKey = key;
                continue;
            }

            dict[key] = val;
        }

        FlushList();
        return dict;
    }

    public static IReadOnlyList<string> GetList(IReadOnlyDictionary<string, string> meta, string key)
    {
        if (!meta.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return [];

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }
}
