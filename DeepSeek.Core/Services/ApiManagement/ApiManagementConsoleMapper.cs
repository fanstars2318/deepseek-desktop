using System.Text.Json;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.ApiManagement;

public static class ApiManagementConsoleMapper
{
    public static object ToUiProvider(ApiProviderEntry entry, AppConfig config, bool probeHealth = true)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var builtin = BuiltinProviderCatalog.Find(entry.Id);
        var accounts = ProviderAccountStore.ByProvider(entry.Id);
        var active = accounts.Count(a => a.Status == "active");

        string authType = builtin?.AuthType ?? MapAuthType(entry);
        string description = builtin?.DescriptionZh ?? entry.DisplayName;
        string[] models = entry.Models.Count > 0
            ? entry.Models.ToArray()
            : builtin?.Models ?? Array.Empty<string>();

        if (entry.Id == "deepseek" && models.Length == 0)
            models = DsdOpenAiCompat.ListModelIds(config).ToArray();

        var adapter = ApiRouteResolver.CreateAdapterForEntry(entry);
        var health = probeHealth
            ? adapter.ProbeHealthAsync(config).GetAwaiter().GetResult()
            : new ApiProviderHealth { Online = false, Message = "" };

        if (accounts.Any(a => a.Status == "active"))
            health = new ApiProviderHealth { Online = true, Message = "账户已配置" };

        var uiType = entry.Kind switch
        {
            ApiProviderKinds.BuiltinWeb => "builtin",
            ApiProviderKinds.Sidecar => "builtin",
            _ => string.IsNullOrWhiteSpace(builtin?.Id) ? "custom" : "builtin"
        };

        return new
        {
            id = entry.Id,
            name = entry.DisplayName,
            type = uiType,
            authType,
            apiEndpoint = string.IsNullOrWhiteSpace(entry.BaseUrl) ? config.ApiBaseUrl : entry.BaseUrl,
            headers = new Dictionary<string, string>(),
            enabled = entry.Enabled,
            createdAt = now,
            updatedAt = now,
            description,
            supportedModels = models,
            status = health.Online ? "online" : "offline",
            lastStatusCheck = now,
            credentialFields = builtin?.CredentialFields ?? Array.Empty<object>()
        };
    }

    public static object ToUiAccount(ProviderAccountRecord rec, bool includeCredentials)
    {
        var creds = new Dictionary<string, string>();
        foreach (var kv in rec.Credentials)
            creds[kv.Key] = includeCredentials ? kv.Value : Mask(kv.Value);

        return new
        {
            id = rec.Id,
            providerId = rec.ProviderId,
            name = rec.Name,
            email = rec.Email,
            credentials = creds,
            status = rec.Status,
            lastUsed = rec.LastUsed,
            createdAt = rec.CreatedAt,
            updatedAt = rec.UpdatedAt,
            requestCount = rec.RequestCount
        };
    }

    public static string ResolveStatus(ApiProviderEntry entry, AppConfig config)
    {
        if (entry.Kind == ApiProviderKinds.BuiltinWeb)
        {
            return ProviderAccountStore.ByProvider(entry.Id).Any(a =>
                       a.Status == "active"
                       && !string.IsNullOrWhiteSpace(
                           AccountCredentials.ResolveWebUserToken(a, config)))
                ? "online"
                : "offline";
        }

        if (ProviderAccountStore.ByProvider(entry.Id).Any(a => a.Status == "active"))
            return "online";

        if (!string.IsNullOrWhiteSpace(CredentialVault.TryGet(entry.Id, "api_key")))
            return "online";

        var adapter = ApiRouteResolver.CreateAdapterForEntry(entry);
        var health = adapter.ProbeHealthAsync(config).GetAwaiter().GetResult();
        return health.Online ? "online" : "offline";
    }

    private static string MapAuthType(ApiProviderEntry entry) =>
        entry.RouteMode switch
        {
            ApiRouteModes.EmbeddedWeb => "userToken",
            _ => "apiKey"
        };

    private static string Mask(string v) =>
        string.IsNullOrWhiteSpace(v) ? "" : "••••••••";

    public static ApiProviderEntry ParseProviderFromUi(JsonElement body, AppConfig config)
    {
        if (body.ValueKind != JsonValueKind.Object)
            return new ApiProviderEntry { Id = Guid.NewGuid().ToString("N")[..8], DisplayName = "Custom" };

        var id = body.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var name = body.TryGetProperty("name", out var n) ? n.GetString() : null;
        var type = body.TryGetProperty("type", out var t) ? t.GetString() : null;
        var builtin = id is not null ? BuiltinProviderCatalog.Find(id) : null;

        var models = new List<string>();
        if (body.TryGetProperty("supportedModels", out var sm) && sm.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in sm.EnumerateArray())
            {
                if (m.ValueKind == JsonValueKind.String)
                {
                    var s = m.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        models.Add(s!);
                }
            }
        }

        var route = ApiRouteModes.DirectApi;
        var kind = ApiProviderKinds.OpenAiCompatible;
        if (string.Equals(type, "builtin", StringComparison.OrdinalIgnoreCase) && builtin is not null)
        {
            if (string.Equals(builtin.Id, "deepseek", StringComparison.OrdinalIgnoreCase))
            {
                kind = ApiProviderKinds.BuiltinWeb;
                route = ApiRouteModes.EmbeddedWeb;
            }
            else
            {
                kind = ApiProviderKinds.Custom;
                route = ApiRouteModes.DirectApi;
            }
        }

        return new ApiProviderEntry
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N")[..8] : id!,
            DisplayName = name ?? builtin?.Name ?? "Custom",
            Kind = kind,
            RouteMode = route,
            BaseUrl = body.TryGetProperty("apiEndpoint", out var ep) ? ep.GetString() ?? ""
                      : builtin?.ApiEndpoint ?? "",
            Enabled = !body.TryGetProperty("enabled", out var en) || en.GetBoolean(),
            Models = models.Count > 0 ? models : builtin?.Models.ToList() ?? new List<string>()
        };
    }
}
