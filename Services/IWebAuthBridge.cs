namespace DeepSeekBrowser.Services;

/// <summary>网页 User Token 与 DSD API 健康探测（普通对话路径）。</summary>
public interface IWebAuthBridge
{
    Task SyncApiBridgeTokenAsync(string? token);

    Task EnsureApiBridgeReadyAsync(CancellationToken ct = default);

    Task<DsdApiHealth?> ProbeDsdApiHealthAsync(string? configWebUserToken, string baseUrl,
        CancellationToken ct = default);

    Task<string?> TryReadUserTokenAsync();

    Task<string?> GetUserTokenAsync(bool waitForBridge = true);
}
