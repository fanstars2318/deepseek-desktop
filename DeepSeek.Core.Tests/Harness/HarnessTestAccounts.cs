using DeepSeekBrowser.Services.ApiManagement;

namespace DeepSeek.Core.Tests.Harness;

internal static class HarnessTestAccounts
{
    public static void EnsureDeepSeek(string token = "tok")
    {
        if (ProviderAccountStore.ByProvider("deepseek").Count > 0)
            return;
        ProviderAccountStore.Add("deepseek", "Test", new Dictionary<string, string> { ["token"] = token });
    }
}
