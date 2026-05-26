namespace DeepSeekBrowser.Services.Harness.Memory;

public sealed class HarnessMemoryRecord
{
    public string Id { get; init; } = "";
    public string Scope { get; init; } = "workspace";
    public string Text { get; init; } = "";
    public float[] Embedding { get; init; } = Array.Empty<float>();
    public string? MetadataJson { get; init; }
    public string ContentHash { get; init; } = "";
    public long CreatedAtUnix { get; init; }
    public long UpdatedAtUnix { get; init; }
}

public enum HarnessMemoryScope
{
    User,
    Workspace,
    Session
}
