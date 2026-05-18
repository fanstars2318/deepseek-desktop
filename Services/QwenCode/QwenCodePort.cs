using System.IO;
using System.Text.Json;

namespace DeepSeekBrowser.Services.QwenCode;

/// <summary>
/// 将 npm 包 <c>@qwen-code/qwen-code</c> 的 CLI/Core 架构移植到 DeepSeek 桌面端 C# 实现中的对照表。
/// 桌面 Agent UI + Chat2API 为外壳；不启动 <c>qwen.cmd</c> 子进程。
/// </summary>
/// <remarks>
/// 官方文档：<see href="https://qwenlm.github.io/qwen-code-docs/zh/developers/architecture/"/>。
/// 自适应扩容：<see href="https://qwenlm.github.io/qwen-code-docs/zh/design/adaptive-output-token-escalation/adaptive-output-token-escalation-design/"/>。
/// </remarks>
public static class QwenCodePort
{
    public const string ReferencePackage = "@qwen-code/qwen-code";
    public const string ReferenceVersion = "0.14.5";

    /// <summary>默认 npm 全局安装路径（仅用于显示「参考版本」，运行时不依赖）。</summary>
    public static string DefaultNpmPackageDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "npm",
            "node_modules",
            "@qwen-code",
            "qwen-code");

    public static IReadOnlySet<string> OfficialCoreToolNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "read_file",
        "write_file",
        "edit",
        "list_directory",
        "glob",
        "grep_search",
        "run_shell_command",
        "web_fetch"
    };

    public static bool IsOfficialBuiltin(string? name)
    {
        var n = NormalizeToolName(name);
        return n is not null && OfficialCoreToolNames.Contains(n);
    }

    /// <summary>将模型/旧版名称规范为官方 Core 工具名。</summary>
    public static string? NormalizeToolName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var name = raw.Trim();
        if (name.StartsWith(QwenCodeBuiltinTools.LegacyPrefix, StringComparison.Ordinal))
            name = name[QwenCodeBuiltinTools.LegacyPrefix.Length..];

        return name switch
        {
            "glob_files" => "glob",
            "grep_content" => "grep_search",
            "run_shell" => "run_shell_command",
            "edit_file" => "edit",
            _ => name
        };
    }

    public static string? TryReadInstalledNpmVersion()
    {
        try
        {
            var pkg = Path.Combine(DefaultNpmPackageDir, "package.json");
            if (!File.Exists(pkg))
                return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(pkg));
            return doc.RootElement.TryGetProperty("version", out var v)
                ? v.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    public static string DescribePort() =>
        $"Qwen Code Core（C# 移植，对齐 {ReferencePackage} {ReferenceVersion}）";
}
