namespace DeepSeekBrowser.Services.Harness;

/// <summary>Flatten multimodal messages for web bridge (no image_url support).</summary>
public static class HarnessWebChatMessageAdapter
{
    public static List<ChatMessage> FlattenForWeb(IReadOnlyList<ChatMessage> messages)
    {
        var list = new List<ChatMessage>(messages.Count);
        foreach (var m in messages)
        {
            if (m.ContentParts is not { Count: > 0 } parts)
            {
                list.Add(m);
                continue;
            }

            if (parts.Any(p => string.Equals(p.Type, "image_url", StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(new ChatMessage
                {
                    Role = m.Role,
                    Content = BuildFlattenedText(parts) +
                              "\n[Multimodal image omitted in web mode — switch to API inference or use image_analyze.]"
                });
                continue;
            }

            list.Add(new ChatMessage
            {
                Role = m.Role,
                Content = BuildFlattenedText(parts)
            });
        }

        return list;
    }

    private static string BuildFlattenedText(IReadOnlyList<ChatContentPart> parts) =>
        string.Join("\n", parts
            .Where(p => string.Equals(p.Type, "text", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Text ?? "")
            .Where(t => !string.IsNullOrWhiteSpace(t)));
}
