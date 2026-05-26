using DeepSeekBrowser.Services.Harness.Graph;
using DeepSeekBrowser.Services.Harness.Interop;
namespace DeepSeekBrowser.Services.Harness;

public static class HarnessRegistryReload
{
    private static DateTime _lastReloadUtc = DateTime.MinValue;

    public static DateTime LastReloadUtc => _lastReloadUtc;

    public static void ReloadAll()
    {
        HarnessPlaybookRegistry.InvalidateCache();
        HarnessSkillRegistry.InvalidateCache();
        HarnessGraphRegistry.InvalidateCache();
        HarnessBlockRegistry.InvalidateCache();
        _lastReloadUtc = DateTime.UtcNow;
    }
}
