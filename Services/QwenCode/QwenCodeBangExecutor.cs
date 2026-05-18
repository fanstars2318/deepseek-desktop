using System.Text.Json;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.QwenCode;

/// <summary>官方 Qwen Code <c>!command</c>：直接执行 Shell，不经过模型。</summary>
public static class QwenCodeBangExecutor
{
    public static bool TryParse(string input, out string command)
    {
        command = "";
        if (string.IsNullOrWhiteSpace(input)) return false;
        var t = input.Trim();
        if (t == "!") { command = ""; return true; }
        if (!t.StartsWith('!')) return false;
        command = t[1..].Trim();
        return true;
    }

    public static async Task<string> ExecuteAsync(
        string command,
        AppConfig config,
        ToolApprovalService approval,
        Action<string>? onLog,
        CancellationToken ct)
    {
        if (!config.QwenCodeAllowShell)
            return "Shell 已在设置中禁用。请启用「允许 Shell 命令」后重试。";

        if (string.IsNullOrWhiteSpace(command))
            return "已进入 Shell 模式提示：以 ! 开头的整行将直接执行命令（例如 !git status）。";

        onLog?.Invoke($"[Qwen Code] ! {command}");
        var args = JsonSerializer.Serialize(new { command });
        try
        {
            var output = await QwenCodeBuiltinTools.ExecuteAsync(
                "run_shell_command", args, config, approval, ct);
            return $"```\n{output}\n```";
        }
        catch (Exception ex)
        {
            return "Shell 执行失败: " + ex.Message;
        }
    }
}
