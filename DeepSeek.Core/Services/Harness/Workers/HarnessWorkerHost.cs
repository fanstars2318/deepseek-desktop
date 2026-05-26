using System.Text.Json;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness.Workers;

/// <summary>--worker 模式：从 stdin 读 HarnessSubAgentRequest JSON，stdout 写结果。</summary>
public static class HarnessWorkerHost
{
    public static async Task<int> RunAsync(string[] args, Func<HarnessSubAgentService> subAgentFactory)
    {
        if (!args.Contains("--worker", StringComparer.OrdinalIgnoreCase))
            return -1;

        var line = await Console.In.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(line))
            return 1;

        var request = JsonSerializer.Deserialize<HarnessSubAgentRequest>(line);
        if (request is null)
            return 1;

        var sub = subAgentFactory();
        var result = await sub.RunAsync(request, null, CancellationToken.None);
        Console.WriteLine(JsonSerializer.Serialize(result));
        return 0;
    }
}
