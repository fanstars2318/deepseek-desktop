using System.Diagnostics;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.ApiManagement;

public static class ProviderSidecarHost
{
    private static readonly Dictionary<string, Process> Running = new(StringComparer.OrdinalIgnoreCase);

    public static void EnsureRunning(ApiProviderEntry provider)
    {
        if (string.IsNullOrWhiteSpace(provider.SidecarCommand))
            return;

        lock (Running)
        {
            if (Running.TryGetValue(provider.Id, out var existing) && !existing.HasExited)
                return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = provider.SidecarCommand,
            Arguments = provider.SidecarArgs ?? "",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var proc = Process.Start(psi);
        if (proc is not null)
        {
            lock (Running)
                Running[provider.Id] = proc;
        }
    }

    public static async Task<bool> ProbeHealthAsync(ApiProviderEntry provider, CancellationToken ct)
    {
        var url = provider.SidecarHealthUrl;
        if (string.IsNullOrWhiteSpace(url))
            return !string.IsNullOrWhiteSpace(provider.SidecarCommand);

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var resp = await http.GetAsync(url, ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
