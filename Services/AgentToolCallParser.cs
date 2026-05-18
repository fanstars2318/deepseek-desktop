using System.Text.RegularExpressions;

namespace DeepSeekBrowser.Services;

internal sealed class ParsedAssistantTurn
{
    public string? Text { get; init; }
    public List<WebToolCall>? ToolCalls { get; init; }
    public bool IsFinalAnswer { get; init; }
}

/// <summary>
/// 解析模型输出中的工具调用（XML 与 Qwen ReAct 格式），参考 Qwen-main/examples/react_demo.py。
/// </summary>
internal static class AgentToolCallParser
{
    private static readonly Regex XmlToolRe = new(
        @"<tool_calling>\s*<name>([^<]+)</name>\s*<arguments>([\s\S]*?)</arguments>\s*</tool_calling>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ParsedAssistantTurn Parse(WebChatResult result)
    {
        if (result.ToolCalls is { Count: > 0 })
        {
            return new ParsedAssistantTurn
            {
                Text = result.Content,
                ToolCalls = result.ToolCalls
            };
        }

        var content = result.Content ?? "";
        var fromXml = ParseXmlToolCalls(content);
        if (fromXml.Count > 0)
        {
            return new ParsedAssistantTurn
            {
                Text = StripXmlTools(content),
                ToolCalls = fromXml
            };
        }

        var (reactName, reactArgs, reactPrefix) = ParseReActAction(content);
        if (!string.IsNullOrWhiteSpace(reactName))
        {
            return new ParsedAssistantTurn
            {
                Text = reactPrefix,
                ToolCalls =
                [
                    new WebToolCall
                    {
                        Id = "call_" + Guid.NewGuid().ToString("N")[..8],
                        Name = reactName.Trim(),
                        Arguments = reactArgs.Trim()
                    }
                ]
            };
        }

        return new ParsedAssistantTurn
        {
            Text = content,
            IsFinalAnswer = IsFinalAnswer(content)
        };
    }

    public static string? ExtractThought(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        var m = Regex.Match(content, @"(?:^|\n)Thought:\s*(.+?)(?=\n(?:Action:|Final Answer:)|$)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static bool IsFinalAnswer(string content) =>
        content.Contains("Final Answer:", StringComparison.OrdinalIgnoreCase);

    private static List<WebToolCall> ParseXmlToolCalls(string text)
    {
        var list = new List<WebToolCall>();
        foreach (Match m in XmlToolRe.Matches(text))
        {
            list.Add(new WebToolCall
            {
                Id = "call_" + Guid.NewGuid().ToString("N")[..8],
                Name = m.Groups[1].Value.Trim(),
                Arguments = m.Groups[2].Value.Trim()
            });
        }

        return list;
    }

    private static string StripXmlTools(string text) => XmlToolRe.Replace(text, "").Trim();

    /// <summary>Qwen ReAct: Action / Action Input（见 react_prompt.md）</summary>
    private static (string Name, string Args, string Prefix) ParseReActAction(string text)
    {
        var i = text.LastIndexOf("\nAction:", StringComparison.OrdinalIgnoreCase);
        if (i < 0) i = text.LastIndexOf("Action:", StringComparison.OrdinalIgnoreCase);
        var j = text.LastIndexOf("\nAction Input:", StringComparison.OrdinalIgnoreCase);
        if (j < 0) j = text.LastIndexOf("Action Input:", StringComparison.OrdinalIgnoreCase);
        var k = text.LastIndexOf("\nObservation:", StringComparison.OrdinalIgnoreCase);

        if (i < 0 || j < 0 || j <= i) return ("", "", text);

        if (k < j)
            text = text.TrimEnd() + "\nObservation:";

        k = text.LastIndexOf("\nObservation:", StringComparison.OrdinalIgnoreCase);
        if (k < j) k = text.Length;

        var name = text[(text.IndexOf(':', i) + 1)..j].Trim();
        var args = text[(text.IndexOf(':', j) + 1)..k].Trim();
        var prefix = text[..i].Trim();
        return (name, args, prefix);
    }
}
