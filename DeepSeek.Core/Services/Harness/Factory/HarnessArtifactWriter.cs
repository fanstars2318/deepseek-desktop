namespace DeepSeekBrowser.Services.Harness.Factory;

public static class HarnessArtifactWriter
{
    public static string FactoryRoot(string workspaceRoot) =>
        Path.Combine(workspaceRoot, ".deepseek", "factory");

    public static string Write(string workspaceRoot, string runId, string fileName, string content)
    {
        var dir = Path.Combine(FactoryRoot(workspaceRoot), runId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    public static string RelativePath(string workspaceRoot, string absolutePath) =>
        Path.GetRelativePath(workspaceRoot, absolutePath).Replace('\\', '/');
}
