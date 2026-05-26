using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness;

public static class HarnessIntentCache
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static void Save(HarnessRunState state, string prompt, HarnessRunIntent intent)
    {
        state.CachedIntentJson = JsonSerializer.Serialize(intent, JsonOptions);
        state.CachedIntentPromptHash = HashPrompt(prompt);
        state.CachedIntentSavedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public static HarnessRunIntent? TryRestore(HarnessRunRequest request, HarnessRunState? state)
    {
        if (!request.Config.AgentIntentCacheEnabled || state is null)
            return null;
        if (string.IsNullOrWhiteSpace(state.CachedIntentJson))
            return null;
        if (string.IsNullOrWhiteSpace(state.CachedIntentPromptHash))
            return null;
        if (!PromptSimilar(state.CachedIntentPromptHash, request.Prompt))
            return null;
        if (!IsWithinTtl(request.Config, state))
            return null;

        try
        {
            return JsonSerializer.Deserialize<HarnessRunIntent>(state.CachedIntentJson, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsWithinTtl(AppConfig config, HarnessRunState state)
    {
        var ttlMinutes = Math.Clamp(config.AgentIntentCacheTtlMinutes, 1, 24 * 60);
        if (state.CachedIntentSavedAtUnix <= 0)
            return true;
        var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - state.CachedIntentSavedAtUnix;
        return age <= ttlMinutes * 60L;
    }

    public static bool PromptSimilar(string cachedHash, string? newPrompt)
    {
        if (string.IsNullOrWhiteSpace(cachedHash)) return false;
        return string.Equals(cachedHash, HashPrompt(newPrompt), StringComparison.Ordinal);
    }

    public static string HashPrompt(string? prompt)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes((prompt ?? "").Trim()));
        return Convert.ToHexString(bytes)[..16];
    }
}
