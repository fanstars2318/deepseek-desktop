using System.Text.Json;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services;

/// <summary>
/// 将 DSD API 管理台 <c>config:update</c> / store 配置补丁同步到桌面端 <see cref="AppConfig"/> 与 DeepSeek-TUI。
/// </summary>
public static class DsdEmbeddedConfigApplicator
{
    public static void ApplyPatch(AppConfig config, JsonElement patch)
    {
        if (patch.ValueKind != JsonValueKind.Object)
            return;

        if (patch.TryGetProperty("sessionConfig", out var sess) && sess.ValueKind == JsonValueKind.Object)
            ApplySessionPatch(config, sess);

        if (patch.TryGetProperty("enableApiKey", out var enableKey))
            config.EnableLocalApiKeyAuth = enableKey.GetBoolean();

        if (patch.TryGetProperty("apiKeys", out var keys) && keys.ValueKind == JsonValueKind.Array)
            ApplyApiKeysPatch(config, keys);

        if (patch.TryGetProperty("modelMappings", out var mappings) && mappings.ValueKind == JsonValueKind.Object)
            ApplyModelMappingsPatch(config, mappings);

        if (patch.TryGetProperty("managementApi", out var mgmt) && mgmt.ValueKind == JsonValueKind.Object)
            ApplyManagementApiPatch(config, mgmt);

        if (patch.TryGetProperty("contextManagement", out var ctx) && ctx.ValueKind == JsonValueKind.Object)
            ApplyContextManagementPatch(config, ctx);

        if (patch.TryGetProperty("proxyPort", out var portEl) && portEl.TryGetInt32(out var port) && port > 0)
            config.LocalApiPort = port;

        if (patch.TryGetProperty("requestTimeout", out var timeout) && timeout.TryGetInt32(out var ms))
            config.DsdApiSessionTimeoutMinutes = Math.Max(1, ms / 60_000);

        if (patch.TryGetProperty("retryCount", out var retry) && retry.TryGetInt32(out var retryCount))
            _ = retryCount;

        if (patch.TryGetProperty("loadBalanceStrategy", out var strategy) &&
            strategy.ValueKind == JsonValueKind.String)
        {
            var s = strategy.GetString();
            if (!string.IsNullOrWhiteSpace(s))
                config.DsdApiLoadBalanceStrategy = s.Trim();
        }

        if (patch.TryGetProperty("accountWeights", out var weights) && weights.ValueKind == JsonValueKind.Array)
            ApplyAccountWeightsPatch(config, weights);

        DsdOpenAiCompat.EnsureDefaultMappings(config);
    }

    private static void ApplyAccountWeightsPatch(AppConfig config, JsonElement weights)
    {
        var list = new List<DsdApiAccountWeightEntry>();
        foreach (var item in weights.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var accountId = item.TryGetProperty("accountId", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(accountId))
                continue;
            var weight = item.TryGetProperty("weight", out var wEl) && wEl.TryGetInt32(out var w) ? w : 100;
            list.Add(new DsdApiAccountWeightEntry
            {
                AccountId = accountId.Trim(),
                Weight = Math.Clamp(weight, 0, 100)
            });
        }

        config.DsdApiAccountWeights = list;
    }

    private static void ApplySessionPatch(AppConfig config, JsonElement sess)
    {
        if (sess.TryGetProperty("sessionTimeout", out var t) && t.TryGetInt32(out var minutes))
            config.DsdApiSessionTimeoutMinutes = Math.Max(1, minutes);
        if (sess.TryGetProperty("maxMessagesPerSession", out var m) && m.TryGetInt32(out var max))
            config.DsdApiMaxMessagesPerSession = Math.Max(1, max);
        if (sess.TryGetProperty("mode", out var mode) && mode.ValueKind == JsonValueKind.String)
        {
            config.DsdApiSessionMode = string.Equals(mode.GetString(), "multi", StringComparison.OrdinalIgnoreCase)
                ? "multi"
                : "single";
        }
    }

    private static void ApplyApiKeysPatch(AppConfig config, JsonElement keys)
    {
        var list = new List<LocalApiKey>();
        foreach (var item in keys.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id))
                id = Guid.NewGuid().ToString("N");

            var keyValue = item.TryGetProperty("key", out var keyEl) ? keyEl.GetString() ?? "" : "";
            var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(name))
                name = "API Key";

            list.Add(new LocalApiKey
            {
                Id = id!,
                Name = name,
                Key = keyValue,
                Enabled = !item.TryGetProperty("enabled", out var en) || en.GetBoolean(),
                CreatedAt = item.TryGetProperty("createdAt", out var ca) && ca.TryGetInt64(out var c)
                    ? c
                    : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                LastUsedAt = item.TryGetProperty("lastUsedAt", out var lu) && lu.ValueKind == JsonValueKind.Number
                    ? lu.GetInt64()
                    : null,
                UsageCount = item.TryGetProperty("usageCount", out var uc) && uc.TryGetInt32(out var count)
                    ? count
                    : 0,
                Description = item.TryGetProperty("description", out var desc) ? desc.GetString() : null
            });
        }

        config.LocalApiKeys = list;
    }

    private static void ApplyModelMappingsPatch(AppConfig config, JsonElement mappings)
    {
        config.ModelMappings.Clear();
        foreach (var prop in mappings.EnumerateObject())
        {
            var entry = prop.Value;
            var requestModel = prop.Name;
            var actualModel = requestModel;
            if (entry.TryGetProperty("requestModel", out var rm) && rm.ValueKind == JsonValueKind.String)
                requestModel = rm.GetString() ?? requestModel;
            if (entry.TryGetProperty("actualModel", out var am) && am.ValueKind == JsonValueKind.String)
                actualModel = am.GetString() ?? actualModel;

            if (string.IsNullOrWhiteSpace(requestModel))
                continue;

            string? preferredProviderId = null;
            string? preferredAccountId = null;
            if (entry.TryGetProperty("preferredProviderId", out var pp) && pp.ValueKind == JsonValueKind.String)
                preferredProviderId = pp.GetString();
            if (entry.TryGetProperty("preferredAccountId", out var pa) && pa.ValueKind == JsonValueKind.String)
                preferredAccountId = pa.GetString();

            config.ModelMappings.Add(new ModelMappingEntry
            {
                RequestModel = requestModel,
                ActualModel = actualModel,
                PreferredProviderId = string.IsNullOrWhiteSpace(preferredProviderId) ? null : preferredProviderId,
                PreferredAccountId = string.IsNullOrWhiteSpace(preferredAccountId) ? null : preferredAccountId
            });
        }
    }

    private static void ApplyManagementApiPatch(AppConfig config, JsonElement mgmt)
    {
        if (mgmt.TryGetProperty("enableManagementApi", out var enable))
            config.EnableExternalOpenAiApi = enable.GetBoolean();
        if (mgmt.TryGetProperty("managementApiPort", out var port) && port.TryGetInt32(out var p) && p > 0)
            config.LocalApiPort = p;
    }

    private static void ApplyContextManagementPatch(AppConfig config, JsonElement ctx)
    {
        if (ctx.TryGetProperty("strategies", out var strategies) && strategies.ValueKind == JsonValueKind.Object)
        {
            if (strategies.TryGetProperty("slidingWindow", out var sw) &&
                sw.TryGetProperty("maxMessages", out var max) &&
                max.TryGetInt32(out var maxMessages))
            {
                config.DsdApiMaxMessagesPerSession = Math.Max(1, maxMessages);
            }
        }
    }
}
