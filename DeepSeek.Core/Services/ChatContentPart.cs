namespace DeepSeekBrowser.Services;

/// <summary>OpenAI-compatible multimodal content part (text / image_url).</summary>
public sealed class ChatContentPart
{
    public string Type { get; set; } = "text";
    public string? Text { get; set; }
    public string? ImageUrl { get; set; }
}
