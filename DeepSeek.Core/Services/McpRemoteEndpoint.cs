namespace DeepSeekBrowser.Services;

public static class McpRemoteEndpoint
{
    /// <summary>Streamable HTTP MCP 默认挂载在 /mcp（Unity、Cursor 等均如此）。</summary>
    public static Uri Resolve(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var endpoint)
            || endpoint.Scheme is not "http" and not "https")
            throw new InvalidOperationException($"远程 MCP URL 无效: {url}");

        return Normalize(endpoint);
    }

    public static Uri Normalize(Uri endpoint)
    {
        var path = endpoint.AbsolutePath.TrimEnd('/');
        if (string.IsNullOrEmpty(path))
        {
            var builder = new UriBuilder(endpoint) { Path = "/mcp" };
            return builder.Uri;
        }

        return endpoint;
    }

    public static bool IsTransientConnectFailure(Exception ex)
    {
        var text = ex.ToString();
        return text.Contains("404", StringComparison.Ordinal)
            || text.Contains("Not Found", StringComparison.OrdinalIgnoreCase)
            || text.Contains("does not indicate success", StringComparison.OrdinalIgnoreCase);
    }
}
