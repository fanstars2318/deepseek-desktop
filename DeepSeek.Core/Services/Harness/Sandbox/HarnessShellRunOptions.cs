namespace DeepSeekBrowser.Services.Harness.Sandbox;

public sealed class HarnessShellRunOptions
{
    public int TimeoutMs { get; init; } = 600_000;
    public Action<string>? OnOutput { get; init; }
}
