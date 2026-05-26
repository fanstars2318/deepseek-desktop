namespace DeepSeekBrowser.Services;

/// <summary>Web 注入桥接能力（由 WebView 层实现，Core 仅依赖此抽象）。</summary>
public interface IWebInjectBridge
{
    Task SyncApiBridgeTokenAsync(string? webUserToken, CancellationToken ct = default);
    Task EnsureApiBridgeReadyAsync(CancellationToken ct = default);
    Task<DsdApiHealth?> ProbeDsdApiHealthAsync(
        string? configWebUserToken,
        string baseUrl,
        CancellationToken ct = default);
}
