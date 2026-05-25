using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DeepSeekBrowser.Services.Harness;

public static class HarnessIntentCache
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static void Save(HarnessRunState state, string prompt, HarnessRunIntent intent)
    {
        state.CachedIntentJson = JsonSerializer.Serialize(intent, JsonOptions);
        state.CachedIntentPromptHash = HashPrompt(prompt);
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

        try
        {
            return JsonSerializer.Deserialize<HarnessRunIntent>(state.CachedIntentJson, JsonOptions);
        }
        catch
        {
            return null;
        }
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
