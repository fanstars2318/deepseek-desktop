using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.ApiManagement;

/// <summary>为 Auto 路由构建已启用、已就绪的供应商与模型池（含用户偏好顺序）。</summary>
public static class AutoProviderPool
{
    public enum ModelTier
    {
        Fast,
        Balanced,
        Premium,
        Reasoning,
        Search,
        SearchThink
    }

    public sealed record ProviderCandidate(
        string Id,
        string DisplayName,
        IReadOnlyList<string> Models,
        bool Ready,
        int PreferenceRank);

    public static IReadOnlyList<ProviderCandidate> BuildCandidates(AppConfig config)
    {
        var order = ResolvePreferenceOrder(config);
        var all = ApiProviderRegistry.LoadAll(config).Where(p => p.Enabled).ToList();
        var candidates = new List<ProviderCandidate>();

        for (var i = 0; i < order.Count; i++)
        {
            var providerId = order[i];
            var p = all.FirstOrDefault(x => string.Equals(x.Id, providerId, StringComparison.OrdinalIgnoreCase));
            if (p is null) continue;
            candidates.Add(ToCandidate(p, config, i, IsReady(config, p)));
        }

        foreach (var p in all)
        {
            if (candidates.Any(c => string.Equals(c.Id, p.Id, StringComparison.OrdinalIgnoreCase)))
                continue;
            candidates.Add(ToCandidate(p, config, candidates.Count + 100, IsReady(config, p)));
        }

        return candidates;
    }

    public static List<string> ResolvePreferenceOrder(AppConfig config)
    {
        var ordered = new List<string>();
        void Add(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (ordered.Any(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase))) return;
            ordered.Add(id.Trim());
        }

        if (config.AgentAutoPreferProviderOrder && config.AgentAutoProviderOrder is { Count: > 0 })
        {
            foreach (var id in config.AgentAutoProviderOrder)
                Add(id);
        }

        if (config.AgentAutoPreferProviderOrder)
            Add(config.AgentDefaultProviderId);

        foreach (var p in ApiProviderRegistry.LoadAll(config).Where(x => x.Enabled))
            Add(p.Id);

        return ordered;
    }

    public static string? PickModel(ProviderCandidate provider, ModelTier tier)
    {
        if (provider.Models.Count == 0) return null;

        string? Match(Func<string, bool> pred) =>
            provider.Models.FirstOrDefault(m => pred(m));

        return tier switch
        {
            ModelTier.SearchThink => Match(IsSearchThink) ?? Match(IsSearch) ?? Match(IsReasoning),
            ModelTier.Search => Match(IsSearch) ?? Match(IsPremium) ?? provider.Models[0],
            ModelTier.Reasoning => Match(IsReasoning) ?? Match(IsPremium) ?? provider.Models[0],
            ModelTier.Fast => Match(IsFast) ?? Match(IsBalanced) ?? provider.Models[0],
            ModelTier.Premium => Match(IsPremium) ?? Match(IsBalanced) ?? provider.Models[0],
            _ => Match(IsBalanced) ?? Match(IsPremium) ?? provider.Models[0]
        };
    }

    public static ModelTier TierForScore(int score, bool deepThink, bool smartSearch)
    {
        if (smartSearch && deepThink) return ModelTier.SearchThink;
        if (smartSearch) return ModelTier.Search;
        if (deepThink || score >= 72) return ModelTier.Reasoning;
        if (score >= 48) return ModelTier.Premium;
        if (score < 18) return ModelTier.Fast;
        return ModelTier.Balanced;
    }

    private static ProviderCandidate ToCandidate(
        ApiProviderEntry p,
        AppConfig config,
        int rank,
        bool ready)
    {
        var models = ResolveModels(p, config);
        var name = string.IsNullOrWhiteSpace(p.DisplayName)
            ? BuiltinProviderCatalog.Find(p.Id)?.Name ?? p.Id
            : p.DisplayName;
        return new ProviderCandidate(p.Id, name, models, ready, rank);
    }

    private static IReadOnlyList<string> ResolveModels(ApiProviderEntry p, AppConfig config)
    {
        if (p.Models.Count > 0)
            return p.Models;

        if (string.Equals(p.Id, "deepseek", StringComparison.OrdinalIgnoreCase))
            return DsdOpenAiCompat.ListModelIds(config).ToArray();

        return BuiltinProviderCatalog.Find(p.Id)?.Models ?? Array.Empty<string>();
    }

    private static bool IsReady(AppConfig config, ApiProviderEntry p)
    {
        if (!p.Enabled) return false;

        if (p.RouteMode == ApiRouteModes.EmbeddedWeb)
            return ProviderAccountStore.ByProvider(p.Id).Any(a =>
                       a.Status == "active"
                       && !string.IsNullOrWhiteSpace(
                           AccountCredentials.ResolveWebUserToken(a, config)))
                   || !string.IsNullOrWhiteSpace(CredentialVault.TryGet(p.Id, "api_key"));

        if (!string.IsNullOrWhiteSpace(CredentialVault.TryGet(p.Id, "api_key")))
            return true;

        return ProviderAccountStore.ByProvider(p.Id)
            .Any(a => a.Status == "active" && a.Credentials.Values.Any(v => !string.IsNullOrWhiteSpace(v)));
    }

    private static bool IsSearch(string m) =>
        m.Contains("search", StringComparison.OrdinalIgnoreCase) ||
        m.Contains("Search", StringComparison.Ordinal);

    private static bool IsSearchThink(string m) =>
        IsSearch(m) && (m.Contains("think", StringComparison.OrdinalIgnoreCase) ||
                        m.Contains("Think", StringComparison.Ordinal) ||
                        m.Contains("reason", StringComparison.OrdinalIgnoreCase));

    private static bool IsReasoning(string m) =>
        m.Contains("reason", StringComparison.OrdinalIgnoreCase) ||
        m.Contains("think", StringComparison.OrdinalIgnoreCase) ||
        m.Contains("R1", StringComparison.Ordinal) ||
        m.Contains("research", StringComparison.OrdinalIgnoreCase);

    private static bool IsFast(string m) =>
        m.Contains("flash", StringComparison.OrdinalIgnoreCase) ||
        m.Contains("turbo", StringComparison.OrdinalIgnoreCase) ||
        m.Contains("mini", StringComparison.OrdinalIgnoreCase) ||
        m.Contains("fast", StringComparison.OrdinalIgnoreCase);

    private static bool IsPremium(string m) =>
        m.Contains("pro", StringComparison.OrdinalIgnoreCase) ||
        m.Contains("max", StringComparison.OrdinalIgnoreCase) ||
        m.Contains("plus", StringComparison.OrdinalIgnoreCase) ||
        m.Contains("flagship", StringComparison.OrdinalIgnoreCase) ||
        m.Contains("gpt-5", StringComparison.OrdinalIgnoreCase);

    private static bool IsBalanced(string m) =>
        m.Contains("chat", StringComparison.OrdinalIgnoreCase) ||
        m.Contains("v3", StringComparison.OrdinalIgnoreCase) ||
        m.Contains("v4", StringComparison.OrdinalIgnoreCase) ||
        m.Contains("glm", StringComparison.OrdinalIgnoreCase) ||
        m.Contains("kimi", StringComparison.OrdinalIgnoreCase);

    public static object[] ToCatalogDto(AppConfig config) =>
        BuildCandidates(config)
            .Select(c => (object)new
            {
                id = c.Id,
                name = c.DisplayName,
                models = c.Models,
                ready = c.Ready,
                preferenceRank = c.PreferenceRank,
                preferred = config.AgentAutoProviderOrder.Any(id =>
                    string.Equals(id, c.Id, StringComparison.OrdinalIgnoreCase))
            })
            .ToArray();
}
