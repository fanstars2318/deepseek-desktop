namespace DeepSeekBrowser.Services.Runtime;

public sealed class RuntimeDependency
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string? FrameworkName { get; init; }
    public Version? MinVersion { get; init; }
    public string? WingetPackageId { get; init; }
    public string? DirectDownloadUrl { get; init; }
    public string? InstallerArgs { get; init; }
}

public sealed class RuntimeDependencyReport
{
    public IReadOnlyList<RuntimeDependency> Missing { get; init; } = Array.Empty<RuntimeDependency>();
    public IReadOnlyList<RuntimeDependency> Satisfied { get; init; } = Array.Empty<RuntimeDependency>();
}

public sealed class RuntimeInstallResult
{
    public required string DependencyId { get; init; }
    public bool Success { get; init; }
    public string Message { get; init; } = "";
}
