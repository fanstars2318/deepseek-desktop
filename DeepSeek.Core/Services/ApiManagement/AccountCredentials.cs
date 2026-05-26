using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.ApiManagement;

public static class AccountCredentials
{
    private static readonly string[] ApiKeyFields = ["api_key", "apiKey", "token", "access_token"];
    private static readonly string[] WebTokenFields = ["token", "userToken", "web_token", "user_token"];

    public static string? ResolveApiKey(ProviderAccountRecord? account, ApiProviderEntry provider)
    {
        var fromAccount = ReadCredential(account, ApiKeyFields);
        return fromAccount ?? ApiProviderRegistry.ResolveApiKey(provider);
    }

    public static string? ResolveWebUserToken(
        ProviderAccountRecord? account,
        AppConfig config,
        bool allowConfigFallback = false)
    {
        var fromAccount = ReadCredential(account, WebTokenFields);
        if (!string.IsNullOrWhiteSpace(fromAccount))
            return fromAccount;
        return allowConfigFallback && !string.IsNullOrWhiteSpace(config.WebUserToken)
            ? config.WebUserToken.Trim()
            : null;
    }

    public static string? ResolveFirstProviderWebToken(string providerId, AppConfig config) =>
        ProviderAccountStore.ByProvider(providerId)
            .Select(a => ResolveWebUserToken(a, config))
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));

    public static int CountActiveAccounts(string providerId, AppConfig config) =>
        ProviderAccountStore.ByProvider(providerId).Count(a =>
            a.Status == "active"
            && !string.IsNullOrWhiteSpace(ResolveWebUserToken(a, config)));

    public static bool HasActiveDeepSeekApiAccount(AppConfig config) =>
        CountActiveAccounts("deepseek", config) > 0;

    private static string? ReadCredential(ProviderAccountRecord? account, string[] keys)
    {
        if (account is null) return null;
        foreach (var key in keys)
        {
            if (account.Credentials.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
