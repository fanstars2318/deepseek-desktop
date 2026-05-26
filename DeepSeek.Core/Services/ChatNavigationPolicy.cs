namespace DeepSeekBrowser.Services;

/// <summary>区分整页导航与 SPA 路由，控制 Loading 遮罩与注入触发。</summary>
public static class ChatNavigationPolicy
{
    public static bool ShouldShowLoadingOverlay(
        string? currentSource,
        string navigatingUri,
        bool isUserInitiated)
    {
        if (string.IsNullOrWhiteSpace(navigatingUri))
            return false;

        if (!navigatingUri.Contains("chat.deepseek.com", StringComparison.OrdinalIgnoreCase))
            return false;

        if (IsSameDocumentLocation(currentSource, navigatingUri))
            return false;

        if (!isUserInitiated && IsMinorLocationChange(currentSource, navigatingUri))
            return false;

        return true;
    }

    public static bool IsSameDocumentLocation(string? current, string target) =>
        SameChatLocation(current, target);

    public static bool SameChatLocation(string? current, string target)
    {
        if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(target))
            return false;

        try
        {
            var c = new Uri(current);
            var t = new Uri(target);
            if (!c.Host.Equals(t.Host, StringComparison.OrdinalIgnoreCase))
                return false;

            var cp = NormalizePath(c.AbsolutePath);
            var tp = NormalizePath(t.AbsolutePath);
            return string.Equals(cp, tp, StringComparison.OrdinalIgnoreCase);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    /// <summary>仅 hash 或 query 变化（同 path）视为 SPA 微调，不展示全屏 Loading。</summary>
    public static bool IsMinorLocationChange(string? current, string target)
    {
        if (string.IsNullOrWhiteSpace(current))
            return true;

        try
        {
            var c = new Uri(current);
            var t = new Uri(target);
            if (!c.Host.Equals(t.Host, StringComparison.OrdinalIgnoreCase))
                return false;

            return string.Equals(
                NormalizePath(c.AbsolutePath),
                NormalizePath(t.AbsolutePath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static string NormalizePath(string path)
    {
        var p = path.Trim();
        if (p.Length > 1 && p.EndsWith('/'))
            p = p[..^1];
        return string.IsNullOrEmpty(p) ? "/" : p;
    }
}
