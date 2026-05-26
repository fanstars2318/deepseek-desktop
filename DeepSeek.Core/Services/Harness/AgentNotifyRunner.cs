using System.Diagnostics;
using System.Text;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness;

public static class AgentNotifyRunner
{
    public static void TryLaunch(AppConfig config, string summary)
    {
        var script = (config.AgentNotifyScript ?? "").Trim();
        if (string.IsNullOrWhiteSpace(script) || !File.Exists(script))
            return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = script,
                Arguments = Quote(summary),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi);
        }
        catch
        {
            // notify 失败不阻断
        }
    }

    private static string Quote(string text) =>
        "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}

public static class HarnessWebSearchTool
{
    public static async Task<string> RunAsync(AppConfig config, string query, CancellationToken ct)
    {
        var script = (config.AgentWebSearchScript ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(script) && File.Exists(script))
        {
            var psi = new ProcessStartInfo
            {
                FileName = script,
                Arguments = Quote(query),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 web search 脚本");
            await proc.WaitForExitAsync(ct);
            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);
            var text = stdout.Trim();
            if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(stderr))
                text = stderr.Trim();
            return string.IsNullOrWhiteSpace(text) ? "（web search 无结果）" : text;
        }

        return "WebSearch 未配置脚本；请启用 smartSearch 或在设置中配置 AgentWebSearchScript。";
    }

    private static string Quote(string text) =>
        "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}

public static class HarnessAgentsMdInit
{
    public static string WriteDefault(string workspaceRoot)
    {
        var path = Path.Combine(workspaceRoot, ".deepseek", "AGENTS.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
            return path;

        var content =
            "# AGENTS.md\n\n" +
            "本文件为 DeepSeek Desktop Agent 提供项目级指令。\n\n" +
            "## 工作区\n\n" +
            "- 优先使用只读工具调研，再执行修改。\n" +
            "- Shell 命令限制在工作区沙盒内。\n\n" +
            "## 验收\n\n" +
            "- 修改后运行项目测试或构建命令。\n";
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }
}
