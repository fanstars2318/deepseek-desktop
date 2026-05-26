using System.IO;

namespace DeepSeekBrowser.Services;

/// <summary>发布目录根路径（与 DeepSeek.exe 同目录）。</summary>
public static class PublishPaths
{
    public static string Root { get; private set; } = AppContext.BaseDirectory;

    public static void Initialize()
    {
        Root = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
