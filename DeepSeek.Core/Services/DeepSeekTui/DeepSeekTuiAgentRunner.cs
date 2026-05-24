using System.IO;
using System.Text;
using System.Text.Json;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.DeepSeekTui;

/// <summary>通过 DeepSeek-TUI 运行时 HTTP API 执行 Agent 回合。</summary>
public sealed class DeepSeekTuiAgentRunner
{
    private readonly DeepSeekTuiHost _host;
    private readonly Func<string, string, Task<bool>> _requestApprovalAsync;

    public DeepSeekTuiAgentRunner(
        DeepSeekTuiHost host,
        Func<string, string, Task<bool>> requestApprovalAsync)
    {
        _host = host;
        _requestApprovalAsync = requestApprovalAsync;
    }

    public async Task<DeepSeekTuiRunResult> RunAsync(
        AppConfig config,
        string prompt,
        string strategy,
        string? existingThreadId,
        Action<string> onLog,
        Action<string>? onAnswerDelta,
        CancellationToken ct,
        Action<AgentUiActivity>? onActivity = null,
        Action<string, bool>? onThinking = null)
    {
        DeepSeekTuiConfigSync.Apply(config);
        await _host.EnsureRunningAsync(config, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(_host.RuntimeBearerToken))
            throw new InvalidOperationException(
                "DeepSeek-TUI Runtime 鉴权 Token 未就绪。请完全退出并重启 DeepSeek 桌面端。");

        var workspace = AgentWorkspace.ResolveRoot(config);
        var mode = string.Equals(strategy, AgentStrategies.Plan, StringComparison.OrdinalIgnoreCase)
            ? "plan"
            : "agent";
        var model = "deepseek-v4-pro";
        var autoApprove = string.Equals(config.AgentApprovalMode, "never", StringComparison.OrdinalIgnoreCase);
        var allowShell = config.AgentAllowShell;

        var client = new DeepSeekTuiRuntimeClient(_host.BaseUrl, _host.RuntimeBearerToken);

        var threadId = string.IsNullOrWhiteSpace(existingThreadId)
            ? await CreateThreadWithAuthRetryAsync(
                client, config, workspace, mode, model, autoApprove, allowShell, onLog, ct).ConfigureAwait(false)
            : existingThreadId;

        var answer = new StringBuilder();
        var turnDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeTurn = new ActiveTurnRef();
        var reportedTools = new HashSet<string>(StringComparer.Ordinal);
        Exception? streamError = null;
        using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        using var interruptOnCancel = ct.Register(() =>
        {
            var turnId = activeTurn.Id;
            if (string.IsNullOrWhiteSpace(turnId))
                return;
            _ = Task.Run(async () =>
            {
                try
                {
                    await client.InterruptTurnAsync(threadId, turnId, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // ignore
                }
            });
        });

        var streamTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var ev in client.StreamEventsAsync(threadId, 0, streamCts.Token).ConfigureAwait(false))
                {
                    await HandleEventAsync(
                            client, ev, workspace, answer, reportedTools, onLog, onAnswerDelta, onActivity, onThinking,
                            turnDone, streamCts.Token)
                        .ConfigureAwait(false);
                    if (ev.Name == "turn.completed")
                    {
                        turnDone.TrySetResult();
                    }
                    else if (ev.Name == "turn.lifecycle")
                    {
                        var status = GetPayloadString(ev.Payload, "status");
                        if (status is "completed" or "failed" or "interrupted" or "canceled")
                            turnDone.TrySetResult();
                    }
                }
            }
            catch (OperationCanceledException) when (streamCts.IsCancellationRequested)
            {
                // turn finished; stop reading long-lived thread stream
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                streamError = ex;
                turnDone.TrySetResult();
            }
        }, ct);

        await Task.Delay(150, ct).ConfigureAwait(false);
        AgentDebugLogger.Current?.Write("TUI", $"StartTurn thread={threadId}");
        activeTurn.Id = await client.StartTurnAsync(threadId, prompt, ct).ConfigureAwait(false);
        AgentDebugLogger.Current?.Write("TUI", $"Turn started id={activeTurn.Id}");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(30));
        try
        {
            await turnDone.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            onLog("已停止。");
            throw;
        }
        catch (OperationCanceledException)
        {
            onLog("回合超时。");
        }

        if (streamError is not null)
            throw streamError;

        streamCts.Cancel();
        try
        {
            await streamTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected after turn completion
        }

        var text = answer.ToString().Trim();
        return new DeepSeekTuiRunResult(threadId, string.IsNullOrWhiteSpace(text) ? "任务已结束" : text);
    }

    private sealed class ActiveTurnRef
    {
        public string? Id;
    }

    private async Task<string> CreateThreadWithAuthRetryAsync(
        DeepSeekTuiRuntimeClient client,
        AppConfig config,
        string workspace,
        string mode,
        string model,
        bool autoApprove,
        bool allowShell,
        Action<string> onLog,
        CancellationToken ct)
    {
        try
        {
            return await client.CreateThreadAsync(workspace, mode, model, autoApprove, allowShell, ct)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (IsRuntimeAuthFailure(ex))
        {
            onLog("Runtime 鉴权失败，正在重启 DeepSeek-TUI 并同步 Token…");
            await _host.EnsureRunningAsync(config, ct).ConfigureAwait(false);
            var retry = new DeepSeekTuiRuntimeClient(_host.BaseUrl, _host.RuntimeBearerToken);
            return await retry.CreateThreadAsync(workspace, mode, model, autoApprove, allowShell, ct)
                .ConfigureAwait(false);
        }
    }

    private static bool IsRuntimeAuthFailure(Exception ex) =>
        ex.Message.Contains("401", StringComparison.Ordinal) &&
        ex.Message.Contains("bearer", StringComparison.OrdinalIgnoreCase);

    private async Task HandleEventAsync(
        DeepSeekTuiRuntimeClient client,
        RuntimeSseEvent ev,
        string workspace,
        StringBuilder answer,
        HashSet<string> reportedTools,
        Action<string> onLog,
        Action<string>? onAnswerDelta,
        Action<AgentUiActivity>? onActivity,
        Action<string, bool>? onThinking,
        TaskCompletionSource turnDone,
        CancellationToken ct)
    {
        var dbg = AgentDebugLogger.Current;
        switch (ev.Name)
        {
            case "item.started":
            case "item.completed":
            {
                var itemKind = GetPayloadString(ev.Payload, "kind");
                dbg?.LogSseEvent(ev.Name, itemKind, GetPayloadString(ev.Payload, "status"));
                if (itemKind is "tool_call" or "command_execution" or "file_change")
                    TryEmitToolActivity(ev.Payload, workspace, reportedTools, onActivity);
                break;
            }
            case "item.delta":
            {
                var delta = GetPayloadString(ev.Payload, "delta");
                var kind = GetPayloadString(ev.Payload, "kind");
                if (string.IsNullOrEmpty(delta))
                    break;

                if (kind is "reasoning" or "thinking")
                {
                    onThinking?.Invoke(delta, true);
                    break;
                }

                if (kind is null or "agent_message" or "message")
                {
                    if (delta.Length > 0 && answer.Length == 0)
                        dbg?.Write("TUI", "首条 answer delta 到达");
                    answer.Append(delta);
                    onAnswerDelta?.Invoke(delta);
                }
                else
                {
                    dbg?.LogSseEvent("item.delta", kind, $"+{delta.Length} chars");
                }

                break;
            }
            case "turn.completed":
                dbg?.LogSseEvent(
                    ev.Name,
                    GetPayloadString(ev.Payload, "status"),
                    GetPayloadString(ev.Payload, "phase"));
                break;
            case "approval.required":
            {
                var approvalId = GetPayloadString(ev.Payload, "approval_id")
                                 ?? GetPayloadString(ev.Payload, "id");
                var tool = GetPayloadString(ev.Payload, "tool_name") ?? "tool";
                var desc = GetPayloadString(ev.Payload, "description") ?? "";
                if (string.IsNullOrWhiteSpace(approvalId))
                    break;

                onActivity?.Invoke(new AgentUiActivity("Awaiting approval", tool, desc));
                var allowed = await _requestApprovalAsync(tool, desc).ConfigureAwait(false);
                await client.DecideApprovalAsync(approvalId, allowed, ct).ConfigureAwait(false);
                onActivity?.Invoke(new AgentUiActivity(allowed ? "Approved" : "Denied", tool, null));
                break;
            }
            case "item.failed":
            case "turn.lifecycle":
            {
                dbg?.LogSseEvent(
                    ev.Name,
                    GetPayloadString(ev.Payload, "status"),
                    GetPayloadString(ev.Payload, "phase"));
                var err = GetPayloadString(ev.Payload, "error")
                          ?? GetPayloadString(ev.Payload, "message");
                if (!string.IsNullOrWhiteSpace(err))
                {
                    dbg?.Write("TUI-ERR", err);
                    onLog("错误: " + err);
                }

                break;
            }
            default:
                dbg?.LogSseEvent(ev.Name, GetPayloadString(ev.Payload, "kind"));
                break;
        }
    }

    private static void TryEmitToolActivity(
        JsonElement payload,
        string workspace,
        HashSet<string> reportedTools,
        Action<AgentUiActivity>? onActivity)
    {
        if (onActivity is null)
            return;

        var tool = GetNestedString(payload, "tool_name", "name", "tool");
        if (string.IsNullOrWhiteSpace(tool))
            return;

        var path = GetNestedString(payload, "path", "file", "file_path", "target", "directory");
        var pattern = GetNestedString(payload, "pattern", "query", "regex", "glob_pattern");
        var command = GetNestedString(payload, "command", "cmd", "script");
        var startLine = GetNestedString(payload, "start_line", "line", "offset");
        var endLine = GetNestedString(payload, "end_line", "limit");

        var key = tool + "|" + (path ?? pattern ?? command ?? "");
        if (!reportedTools.Add(key))
            return;

        var activity = FormatToolActivity(tool, path, pattern, command, startLine, endLine, workspace);
        onActivity(activity);
    }

    internal static AgentUiActivity FormatToolActivity(
        string tool,
        string? path,
        string? pattern,
        string? command,
        string? startLine,
        string? endLine,
        string workspace)
    {
        var name = tool.Trim();
        var rel = RelativizePath(path, workspace);
        var detail = FormatLineRange(startLine, endLine);

        if (name.Contains("read", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "read_file", StringComparison.OrdinalIgnoreCase))
            return new AgentUiActivity("Read", WrapCode(rel ?? path ?? name), detail);

        if (name.Contains("write", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("edit", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("patch", StringComparison.OrdinalIgnoreCase))
            return new AgentUiActivity("Edited", WrapCode(rel ?? path ?? name), detail);

        if (name.Contains("grep", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("search", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("rg", StringComparison.OrdinalIgnoreCase))
        {
            var target = pattern is not null
                ? $"`{pattern}`" + (rel is not null ? $" in `{rel}`" : "")
                : rel ?? name;
            return new AgentUiActivity("Grepped", target, null);
        }

        if (name.Contains("list", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "list_dir", StringComparison.OrdinalIgnoreCase))
            return new AgentUiActivity("Listed", WrapCode(rel ?? path ?? name), null);

        if (name.Contains("glob", StringComparison.OrdinalIgnoreCase))
            return new AgentUiActivity("Searched", pattern ?? rel ?? name, null);

        if (name.Contains("shell", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("exec", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("bash", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("command", StringComparison.OrdinalIgnoreCase))
        {
            var preview = command is not null ? Truncate(command, 72) : name;
            return new AgentUiActivity("Ran", preview, "terminal");
        }

        if (name.Contains("fetch", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("http", StringComparison.OrdinalIgnoreCase))
            return new AgentUiActivity("Fetched", path ?? pattern ?? name, null);

        return new AgentUiActivity("Ran", name, rel);
    }

    private static string? RelativizePath(string? path, string workspace)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            var full = Path.GetFullPath(path.Trim());
            var root = Path.GetFullPath(workspace.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                var rel = full[root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.IsNullOrEmpty(rel) ? Path.GetFileName(full) : rel.Replace('\\', '/');
            }

            return full.Replace('\\', '/');
        }
        catch
        {
            return path.Trim().Replace('\\', '/');
        }
    }

    private static string? FormatLineRange(string? start, string? end)
    {
        if (string.IsNullOrWhiteSpace(start))
            return null;
        if (!string.IsNullOrWhiteSpace(end) && end != start)
            return $"L{start}-{end}";
        return $"L{start}";
    }

    private static string? GetNestedString(JsonElement payload, params string[] names)
    {
        foreach (var name in names)
        {
            var v = GetPayloadString(payload, name);
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }

        if (payload.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in names)
            {
                var v = GetPayloadString(args, name);
                if (!string.IsNullOrWhiteSpace(v))
                    return v;
            }
        }

        if (payload.TryGetProperty("input", out var input) && input.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in names)
            {
                var v = GetPayloadString(input, name);
                if (!string.IsNullOrWhiteSpace(v))
                    return v;
            }
        }

        if (payload.TryGetProperty("metadata", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in names)
            {
                var v = GetPayloadString(meta, name);
                if (!string.IsNullOrWhiteSpace(v))
                    return v;
            }
        }

        return null;
    }

    private static string? GetPayloadString(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            return null;
        if (!payload.TryGetProperty(name, out var el))
            return null;
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => el.GetRawText()
        };
    }

    private static string WrapCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var v = value.Trim();
        return v.StartsWith('`') ? v : "`" + v + "`";
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

public sealed record DeepSeekTuiRunResult(string ThreadId, string Answer);
