using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.QwenCode;

/// <summary>
/// Qwen Code CLI 输入：@file、!shell、/skills、/agents（推理仍走 DeepSeek Chat2API）。
/// </summary>
public static class QwenCodeInputProcessor
{
    private static readonly Regex AtFileRe = new(
        @"@([^\s@""']+\.[A-Za-z0-9]+)",
        RegexOptions.Compiled);

    public static async Task<QwenInputProcessResult> ProcessAsync(
        string userTask,
        AppConfig config,
        ToolApprovalService? approval,
        Action<string>? onLog,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userTask))
            return QwenInputProcessResult.ForAgent("");

        var trimmed = userTask.Trim();

        if (QwenCodeBangExecutor.TryParse(trimmed, out var bangCmd))
        {
            if (approval is null)
                return QwenInputProcessResult.Handled("Shell 需要审批服务，请通过 Agent 运行。");
            var output = await QwenCodeBangExecutor.ExecuteAsync(bangCmd, config, approval, onLog, ct);
            return QwenInputProcessResult.Handled(output);
        }

        if (trimmed.StartsWith('/'))
            return ProcessSlash(trimmed, config, onLog);

        var task = InjectAtFiles(trimmed, config, onLog);
        return QwenInputProcessResult.ForAgent(task);
    }

    public static string PrepareTask(string userTask, AppConfig config, Action<string>? onLog) =>
        ProcessAsync(userTask, config, null, onLog).GetAwaiter().GetResult().TaskText;

    private static QwenInputProcessResult ProcessSlash(string text, AppConfig config, Action<string>? onLog)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();

        if (cmd is "/skills" or "/skill")
        {
            if (!config.EnableQwenSkills)
                return QwenInputProcessResult.Handled("Skills 已在设置中关闭。");

            var skills = QwenSkillRegistry.Discover(config);
            if (parts.Length < 2)
                return QwenInputProcessResult.Handled(
                    "可用 Skills：\n" + QwenSkillRegistry.FormatCatalog(skills)
                    + "\n\n用法: /skills <name> [你的任务…]");

            var name = parts[1];
            var skill = QwenSkillRegistry.Find(config, name);
            if (skill is null)
                return QwenInputProcessResult.Handled($"未找到 Skill「{name}」。\n\n" + QwenSkillRegistry.FormatCatalog(skills));

            var rest = parts.Length > 2 ? string.Join(' ', parts.Skip(2)) : "请按该 Skill 的说明执行。";
            onLog?.Invoke($"[Qwen Code] 已加载 Skill: {skill.Name} ({skill.Scope})");
            var body = new StringBuilder();
            body.AppendLine(rest);
            body.AppendLine();
            body.AppendLine($"--- Skill: {skill.Name} ---");
            body.AppendLine(skill.Body);
            body.AppendLine("--- Skill 结束 ---");
            return new QwenInputProcessResult
            {
                TaskText = body.ToString().Trim(),
                ActiveSkill = skill.Name
            };
        }

        if (cmd is "/agents" or "/agent")
        {
            if (!config.EnableQwenSubAgentConfigs)
                return QwenInputProcessResult.Handled("Subagent 配置发现已在设置中关闭。");

            var agents = QwenSubAgentRegistry.Discover(config);
            if (parts.Length < 2)
                return QwenInputProcessResult.Handled(
                    "可用 Subagents：\n" + QwenSubAgentRegistry.FormatCatalog(agents)
                    + "\n\n用法: /agents <name> <任务…>");

            var name = parts[1];
            var agent = QwenSubAgentRegistry.Find(config, name);
            if (agent is null)
                return QwenInputProcessResult.Handled($"未找到 Subagent「{name}」。\n\n" + QwenSubAgentRegistry.FormatCatalog(agents));

            var task = parts.Length > 2 ? string.Join(' ', parts.Skip(2)) : "请执行你的专长任务。";
            onLog?.Invoke($"[Qwen Code] 委派 Subagent: {agent.Name}");
            return new QwenInputProcessResult
            {
                TaskText = task,
                ActiveSubAgent = agent.Name
            };
        }

        return QwenInputProcessResult.ForAgent(text);
    }

    private static string InjectAtFiles(string userTask, AppConfig config, Action<string>? onLog)
    {
        var root = QwenCodeBuiltinTools.ResolveWorkspaceRoot(config);
        var sb = new StringBuilder(userTask.Trim());
        var injected = 0;

        foreach (Match m in AtFileRe.Matches(userTask))
        {
            var rel = m.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(rel)) continue;

            try
            {
                var full = Path.IsPathRooted(rel)
                    ? Path.GetFullPath(rel)
                    : Path.GetFullPath(Path.Combine(root, rel));
                var rootFull = Path.GetFullPath(root);
                if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                {
                    onLog?.Invoke($"@file 跳过（超出工作区）: {rel}");
                    continue;
                }

                if (!File.Exists(full))
                {
                    onLog?.Invoke($"@file 未找到: {rel}");
                    continue;
                }

                var info = new FileInfo(full);
                if (info.Length > config.QwenCodeMaxFileReadChars)
                {
                    onLog?.Invoke($"@file 过大已跳过: {rel}");
                    continue;
                }

                var content = File.ReadAllText(full);
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine($"--- 附件 @{rel} ---");
                sb.AppendLine(content);
                sb.AppendLine("--- 附件结束 ---");
                injected++;
            }
            catch (Exception ex)
            {
                onLog?.Invoke($"@file 读取失败 {rel}: {ex.Message}");
            }
        }

        if (injected > 0)
            onLog?.Invoke($"[Qwen Code] 已注入 {injected} 个 @file 附件");

        return sb.ToString().Trim();
    }

    public static string HelpText() =>
        """
        DeepSeek Agent + Qwen Code Core（C# 移植）
        /help          本帮助
        /clear         清空对话
        /react         ReAct 单 Agent
        /plan          计划 + 子 Agent
        /chat          普通网页对话
        /skills        列出 Skills
        /skills <名>   加载 Skill 并执行任务
        /agents        列出 Subagents
        /agents <名>   委派命名 Subagent
        !<命令>        直接执行 Shell（不经模型）
        @路径/文件     附加工作区文件

        配置目录：工作区 .qwen/skills、.qwen/agents；用户 ~/.qwen/
        推理：Chat2API + DeepSeek 登录 · 工具：Core + MCP
        """.Trim();
}
