using System.Text.Json.Serialization;

namespace DeepSeekBrowser.Services.Harness.Observability;

public sealed class HarnessRunMeta
{
    [JsonPropertyName("runId")]
    public string RunId { get; init; } = "";

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = "";

    [JsonPropertyName("startedUtc")]
    public string StartedUtc { get; init; } = "";

    [JsonPropertyName("endedUtc")]
    public string? EndedUtc { get; init; }

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("strategy")]
    public string? Strategy { get; init; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    [JsonPropertyName("promptPreview")]
    public string? PromptPreview { get; init; }

    [JsonPropertyName("answerPreview")]
    public string? AnswerPreview { get; init; }

    [JsonPropertyName("promptTokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completionTokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("totalTokens")]
    public int TotalTokens { get; init; }

    [JsonPropertyName("tokenSource")]
    public string TokenSource { get; init; } = "unknown";

    [JsonPropertyName("toolCallCount")]
    public int ToolCallCount { get; init; }

    [JsonPropertyName("toolErrorCount")]
    public int ToolErrorCount { get; init; }

    [JsonPropertyName("llmCallCount")]
    public int LlmCallCount { get; init; }

    [JsonPropertyName("phase")]
    public string? Phase { get; init; }

    [JsonPropertyName("domainId")]
    public string? DomainId { get; init; }
}
