using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness;

/// <summary>Trim verbose sub-agent answers before parent handoff.</summary>
public static class HarnessSubAgentResultCompressor
{
    public static string Compress(string answer, AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(answer)) return answer;
        var max = Math.Clamp(config.AgentSubAgentAnswerMaxChars, 2000, 80_000);
        if (answer.Length <= max) return answer;

        var head = max * 2 / 3;
        var tail = max - head - 40;
        return answer[..head]
               + "\n\n…[sub-agent output truncated]…\n\n"
               + answer[^tail..];
    }
}
