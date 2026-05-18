using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.QwenCode;

/// <summary>
/// DeepSeek Agent 外壳下的 Qwen Code Core 运行入口（C# 移植，非 npm 子进程）。
/// </summary>
public sealed class QwenCodeAgentRunner
{
    private readonly QwenCodeCore _core;
    private readonly AgentOrchestrator _orchestrator;
    private readonly NamedSubAgentRunner _namedSubAgent;

    public QwenCodeAgentRunner(QwenCodeCore core, LocalChat2ApiClient chat2Api)
    {
        _core = core;
        _orchestrator = new AgentOrchestrator(core, chat2Api);
        _namedSubAgent = new NamedSubAgentRunner(chat2Api, core);
    }

    public QwenCodeCore Core => _core;

    public async Task<string?> RunAsync(
        AppConfig config,
        string userTask,
        string strategy,
        bool useTools,
        bool thinking,
        bool search,
        Action<string> onLog,
        CancellationToken ct)
    {
        QwenCodeSettingsStore.ApplyToAppConfig(QwenCodeSettingsStore.FromAppConfig(config), config);

        var npmVer = QwenCodePort.TryReadInstalledNpmVersion();
        onLog($"[{QwenCodePort.DescribePort()}]");
        if (npmVer is not null)
            onLog($"[Qwen Code] 本机 npm 参考: {QwenCodePort.ReferencePackage}@{npmVer}");

        var processed = await QwenCodeInputProcessor.ProcessAsync(
            userTask, config, _core.Approval, onLog, ct);

        if (processed.HandledWithoutAgent)
            return processed.DirectReply;

        onLog("[Qwen Code Core] 推理: DeepSeek 网页会话 (Chat2API)");
        onLog($"[Qwen Code Core] 工作区: {QwenCodeBuiltinTools.ResolveWorkspaceRoot(config)}");

        if (useTools)
        {
            var tools = await _core.Tools.ListAllToolsAsync(config, ct);
            if (tools.Count == 0)
            {
                onLog("错误: 无可用工具。请在设置中启用 Qwen Code 内置工具或连接 MCP。");
                return "失败: 无可用工具";
            }

            var builtin = tools.Count(t => QwenCodePort.IsOfficialBuiltin(t.ExposedName));
            onLog($"[Qwen Code Core] 工具 {tools.Count} 个（Core {builtin} + MCP {tools.Count - builtin}）");
        }

        if (!string.IsNullOrWhiteSpace(processed.ActiveSubAgent)
            && config.EnableQwenSubAgentConfigs
            && QwenSubAgentRegistry.Find(config, processed.ActiveSubAgent) is { } agentDef)
        {
            return await _namedSubAgent.RunAsync(
                config, agentDef, processed.TaskText, useTools, thinking, search, onLog, ct);
        }

        return await _orchestrator.RunAsync(
            config, processed.TaskText, strategy, useTools, thinking, search, onLog, ct);
    }
}
