using System.Text.Json.Serialization;

namespace DeepSeekBrowser.Services.Harness.Observability;

public sealed class HarnessTraceSpan
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "span";

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = "";

    [JsonPropertyName("spanId")]
    public string SpanId { get; init; } = "";

    [JsonPropertyName("parentSpanId")]
    public string? ParentSpanId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("startUtc")]
    public string StartUtc { get; init; } = "";

    [JsonPropertyName("endUtc")]
    public string? EndUtc { get; init; }

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "ok";

    [JsonPropertyName("attributes")]
    public Dictionary<string, object?> Attributes { get; init; } = new();
}
