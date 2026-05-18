using System.Text.Json;
using System.Windows;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.QwenCode;
using DeepSeekBrowser.Views;

namespace DeepSeekBrowser.Services;

public sealed class DesktopAgentHost : IAsyncDisposable
{
    private readonly McpHub _mcpHub = new();
    private readonly QwenCodeCore _qwenCode;
    private readonly QwenCodeAgentRunner _qwenAgent;
    private readonly WebInjectService _web;
    private readonly LocalOpenAiServer _localApi;
    private readonly AgentSessionStore _agentSessions = new();
    private AppConfig _config = new();
    private CancellationTokenSource? _runCts;
    private Window? _owner;
    private AgentRunWindow? _agentWindow;

    public Func<string, Task>? NavigateToUrl { get; set; }

    public DesktopAgentHost(WebInjectService web, LocalOpenAiServer localApi)
    {
        _web = web;
        _localApi = localApi;
        _qwenCode = new QwenCodeCore(_mcpHub);
        _qwenAgent = new QwenCodeAgentRunner(_qwenCode, new LocalChat2ApiClient(localApi, web));
        _web.MessageReceived += OnWebMessage;
    }

    public void SetOwner(Window owner)
    {
        _owner = owner;
        _qwenCode.Approval.RequestApprovalAsync = RequestToolApprovalAsync;
    }

    private Task<bool> RequestToolApprovalAsync(string toolName, string detail, ToolRisk risk)
    {
        var tcs = new TaskCompletionSource<bool>();
        Application.Current.Dispatcher.Invoke(() =>
        {
            var action = risk switch
            {
                ToolRisk.Execute => "执行命令",
                ToolRisk.Write => "写入文件",
                _ => "读取"
            };
            var result = MessageBox.Show(
                _owner,
                $"Agent 请求{action}：\n\n工具: {toolName}\n\n{detail}\n\n是否允许？",
                "Qwen Code 工具审批",
                MessageBoxButton.YesNo,
                risk == ToolRisk.ReadOnly ? MessageBoxImage.Question : MessageBoxImage.Warning);
            tcs.TrySetResult(result == MessageBoxResult.Yes);
        });
        return tcs.Task;
    }

    public void Start()
    {
        _config = ConfigStore.Load();
        _localApi.UpdateConfig(_config);
        _localApi.Start();
        if (_config.AgentSessionAutoCleanup)
            _agentSessions.ApplyRetentionPolicy(_config);
        _ = ConnectEnabledMcpServersAsync();
    }

    private async Task ConnectEnabledMcpServersAsync()
    {
        await _mcpHub.ConnectEnabledAsync(_config.McpServers, _ => { }, CancellationToken.None);
    }

    public void ReloadConfig()
    {
        _config = ConfigStore.Load();
        _localApi.UpdateConfig(_config);
    }

    public async Task NotifyApiInfoAsync()
    {
        var loggedIn = !string.IsNullOrWhiteSpace(_config.WebUserToken);
        await _web.PostToPageAsync(new
        {
            type = "apiInfo",
            url = _localApi.BaseUrl,
            model = _config.Model,
            loggedIn,
            workMode = _config.DefaultWorkMode,
            agentStrategy = _config.DefaultAgentStrategy,
            hint = loggedIn
                ? (_config.DefaultWorkMode == "plan"
                    ? "计划模式：Qwen Code Core 规划 → 子 Agent + 工具"
                    : "Agent 模式：DeepSeek 外壳 + Qwen Code Core（C# 移植）")
                : "请先在网页登录以启用 Agent"
        });
    }

    private async void OnWebMessage(object? sender, JsonElement msg)
    {
        if (!msg.TryGetProperty("type", out var typeEl)) return;
        var type = typeEl.GetString();

        switch (type)
        {
            case "nativeReady":
                await RefreshLoginStateAsync();
                if (!string.IsNullOrWhiteSpace(_config.WebUserToken) && _mcpHub.ConnectedCount == 0)
                    _ = ConnectEnabledMcpServersAsync();
                break;
            case "syncToken":
                if (msg.TryGetProperty("token", out var tok) && tok.ValueKind == JsonValueKind.String)
                {
                    var t = tok.GetString();
                    if (!string.IsNullOrWhiteSpace(t))
                        await ApplyWebUserTokenAsync(t);
                }
                break;
            case "refreshLoginState":
                await RefreshLoginStateAsync();
                break;
            case "openSettings":
                Application.Current.Dispatcher.Invoke(() => OpenSettings());
                break;
            case "openAgentWorkspace":
                Application.Current.Dispatcher.Invoke(EnsureAgentWindow);
                break;
            case "showProviderCard":
                await NotifyApiInfoAsync();
                await _web.PostToPageAsync(new
                {
                    type = "showProviderCard",
                    url = _localApi.BaseUrl,
                    loggedIn = !string.IsNullOrWhiteSpace(_config.WebUserToken)
                });
                break;
            case "setWorkMode":
                if (msg.TryGetProperty("mode", out var modeEl))
                {
                    var mode = modeEl.GetString();
                    if (mode is "chat" or "agent" or "plan")
                    {
                        var toAgent = mode is "agent" or "plan";
                        await ApplyTokenFromMessageAsync(msg);
                        if (toAgent && !_web.IsAgentHostPage)
                            await SyncTokenFromPageAsync();

                        _config.DefaultWorkMode = mode;
                        if (mode == "plan")
                            _config.DefaultAgentStrategy = AgentStrategies.Plan;
                        else if (mode == "agent")
                            _config.DefaultAgentStrategy = AgentStrategies.React;
                        ConfigStore.Save(_config);
                        _localApi.UpdateConfig(_config);

                        var targetUrl = mode == "chat"
                            ? AppNavigation.DeepSeekUrl
                            : AppNavigation.AgentPageUrl;
                        var skipNavigate = msg.TryGetProperty("skipNavigate", out var snEl)
                                           && snEl.ValueKind == JsonValueKind.True;

                        if (NavigateToUrl is not null && !skipNavigate)
                        {
                            await Application.Current.Dispatcher.Invoke(async () =>
                            {
                                await NavigateToUrl(targetUrl);
                                if (mode == "chat")
                                    await _web.TriggerInjectAsync(forceReset: false);
                            });
                        }
                        else if (mode == "chat")
                        {
                            _ = _web.TriggerInjectAsync(forceReset: false);
                        }

                        await RefreshLoginStateAsync();
                        if (toAgent && !skipNavigate)
                            _ = PushLoginStateAfterAgentNavAsync();
                    }
                }
                break;
            case "navigateToAgent":
                await ApplyTokenFromMessageAsync(msg);
                await SyncTokenFromChatPageAsync();
                _config.DefaultWorkMode = "agent";
                ConfigStore.Save(_config);
                if (NavigateToUrl is not null)
                {
                    await Application.Current.Dispatcher.Invoke(async () =>
                    {
                        await NavigateToUrl(AppNavigation.AgentPageUrl);
                    });
                }
                await RefreshLoginStateAsync();
                _ = PushLoginStateAfterAgentNavAsync();
                break;
            case "navigateToChat":
                _config.DefaultWorkMode = "chat";
                ConfigStore.Save(_config);
                if (NavigateToUrl is not null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(
                        async () => await NavigateToUrl(AppNavigation.DeepSeekUrl));
                }
                break;
            case "agentRun":
                var text = msg.TryGetProperty("text", out var tEl) ? tEl.GetString() : "";
                if (string.IsNullOrWhiteSpace(text)) return;
                var chatMode = msg.TryGetProperty("mode", out var mEl) ? mEl.GetString() : "专家";
                var deepThink = !msg.TryGetProperty("deepThink", out var dEl) || dEl.GetBoolean();
                var smartSearch = !msg.TryGetProperty("smartSearch", out var sEl) || sEl.GetBoolean();
                var mcpOn = !msg.TryGetProperty("mcpOn", out var mcEl) || mcEl.GetBoolean();
                var strategy = msg.TryGetProperty("strategy", out var stEl)
                    ? stEl.GetString()
                    : _config.DefaultAgentStrategy;
                var refIds = new List<string>();
                if (msg.TryGetProperty("refFileIds", out var rfEl) && rfEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in rfEl.EnumerateArray())
                    {
                        var id = item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText();
                        if (!string.IsNullOrWhiteSpace(id))
                            refIds.Add(id.Trim().Trim('"'));
                    }
                }

                _ = RunAgentAsync(
                    text!, chatMode ?? "专家", deepThink, smartSearch, mcpOn,
                    strategy ?? AgentStrategies.React, refIds);
                break;
            case "agentStorageList":
                await HandleAgentStorageListAsync(msg);
                break;
            case "agentStorageLoad":
                await HandleAgentStorageLoadAsync(msg);
                break;
            case "agentStorageSave":
                await HandleAgentStorageSaveAsync(msg);
                break;
            case "agentStorageDelete":
                await HandleAgentStorageDeleteAsync(msg);
                break;
            case "agentStorageCleanup":
                await HandleAgentStorageCleanupAsync(msg);
                break;
            case "agentStorageMigrate":
                await HandleAgentStorageMigrateAsync(msg);
                break;
        }
    }

    private static string? GetReqId(JsonElement msg) =>
        msg.TryGetProperty("reqId", out var r) ? r.GetString() : null;

    private Task PostStorageReplyAsync(string? reqId, object payload) =>
        _web.PostToPageAsync(MergeReqId(reqId, payload));

    private static object MergeReqId(string? reqId, object payload)
    {
        if (string.IsNullOrEmpty(reqId)) return payload;
        var json = JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);
        var dict = new Dictionary<string, object?> { ["reqId"] = reqId };
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => JsonSerializer.Deserialize<object>(prop.Value.GetRawText())
            };
        }
        return dict;
    }

    private async Task HandleAgentStorageListAsync(JsonElement msg)
    {
        var reqId = GetReqId(msg);
        var (bytes, count) = _agentSessions.GetStats();
        var metas = _agentSessions.ListMetas();
        await PostStorageReplyAsync(reqId, new
        {
            type = "agentStorageList",
            sessions = metas,
            totalBytes = bytes,
            count,
            storagePath = _agentSessions.StorageDirectory,
            retentionDays = _config.AgentSessionRetentionDays,
            maxStorageGb = _config.AgentSessionMaxStorageGb
        });
    }

    private async Task HandleAgentStorageLoadAsync(JsonElement msg)
    {
        var reqId = GetReqId(msg);
        var id = msg.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var session = string.IsNullOrWhiteSpace(id) ? null : _agentSessions.Load(id!);
        await PostStorageReplyAsync(reqId, new { type = "agentStorageLoad", session });
    }

    private async Task HandleAgentStorageSaveAsync(JsonElement msg)
    {
        var reqId = GetReqId(msg);
        AgentSessionFile? session = null;
        if (msg.TryGetProperty("session", out var sEl))
            session = JsonSerializer.Deserialize<AgentSessionFile>(sEl.GetRawText(), AgentSessionJson.Options);

        if (session is not null)
        {
            _agentSessions.Save(session);
            if (_config.AgentSessionAutoCleanup)
                _agentSessions.ApplyRetentionPolicy(_config);
        }

        var (bytes, count) = _agentSessions.GetStats();
        await PostStorageReplyAsync(reqId, new
        {
            type = "agentStorageSave",
            ok = session is not null,
            totalBytes = bytes,
            count
        });
    }

    private async Task HandleAgentStorageDeleteAsync(JsonElement msg)
    {
        var reqId = GetReqId(msg);
        var ids = new List<string>();
        if (msg.TryGetProperty("ids", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                var id = item.GetString();
                if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
            }
        }

        _agentSessions.Delete(ids);
        var (bytes, count) = _agentSessions.GetStats();
        await PostStorageReplyAsync(reqId, new
        {
            type = "agentStorageDelete",
            deleted = ids,
            sessions = _agentSessions.ListMetas(),
            totalBytes = bytes,
            count
        });
    }

    private async Task HandleAgentStorageCleanupAsync(JsonElement msg)
    {
        var reqId = GetReqId(msg);
        var deleted = _agentSessions.ApplyRetentionPolicy(_config);
        var (bytes, count) = _agentSessions.GetStats();
        await PostStorageReplyAsync(reqId, new
        {
            type = "agentStorageCleanup",
            deleted,
            sessions = _agentSessions.ListMetas(),
            totalBytes = bytes,
            count
        });
    }

    private async Task HandleAgentStorageMigrateAsync(JsonElement msg)
    {
        var reqId = GetReqId(msg);
        var imported = 0;
        if (msg.TryGetProperty("sessions", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            var list = new List<AgentSessionFile>();
            foreach (var item in arr.EnumerateArray())
            {
                var s = JsonSerializer.Deserialize<AgentSessionFile>(item.GetRawText(), AgentSessionJson.Options);
                if (s is not null) list.Add(s);
            }

            _agentSessions.ImportLegacySessions(list);
            imported = list.Count;
        }

        var (bytes, count) = _agentSessions.GetStats();
        await PostStorageReplyAsync(reqId, new
        {
            type = "agentStorageMigrate",
            imported,
            sessions = _agentSessions.ListMetas(),
            totalBytes = bytes,
            count
        });
    }

    public async Task SyncTokenFromChatPageAsync()
    {
        if (_web.IsAgentHostPage)
            return;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            await SyncTokenFromPageAsync();
            ReloadConfig();
            if (!string.IsNullOrWhiteSpace(_config.WebUserToken))
                return;
            if (attempt < 9)
                await Task.Delay(attempt < 3 ? 60 : 150);
        }
    }

    public async Task OnChatNavigationCompletedAsync() =>
        await RefreshLoginStateAsync();

    public async Task OnAgentNavigationCompletedAsync()
    {
        await RefreshLoginStateAsync();
        _ = PushLoginStateAfterAgentNavAsync();
    }

    private async Task PushLoginStateAfterAgentNavAsync()
    {
        foreach (var delay in new[] { 0, 120, 400, 900 })
        {
            if (delay > 0)
                await Task.Delay(delay);
            if (!_web.IsAgentHostPage)
                return;
            await PushLoginStateAsync();
            await NotifyApiInfoAsync();
        }
    }

    public async Task RefreshLoginStateAsync()
    {
        ReloadConfig();
        if (!_web.IsAgentHostPage)
            await SyncTokenFromPageAsync();
        ReloadConfig();

        await NotifyApiInfoAsync();
        await PushLoginStateAsync();
    }

    public async Task PushLoginStateToPageAsync() => await PushLoginStateAsync();

    private async Task PushLoginStateAsync()
    {
        ReloadConfig();
        var loggedIn = !string.IsNullOrWhiteSpace(_config.WebUserToken);
        await _web.PushAgentAuthHintAsync(loggedIn);
        await _web.PostToPageAsync(new { type = "loginState", loggedIn });
    }

    private async Task ApplyTokenFromMessageAsync(JsonElement msg)
    {
        if (!msg.TryGetProperty("token", out var tokenEl) || tokenEl.ValueKind != JsonValueKind.String)
            return;
        var token = tokenEl.GetString();
        if (!string.IsNullOrWhiteSpace(token))
            await ApplyWebUserTokenAsync(token);
    }

    private async Task ApplyWebUserTokenAsync(string token)
    {
        var normalized = NormalizeUserToken(token);
        if (string.IsNullOrWhiteSpace(normalized))
            return;
        _config.WebUserToken = normalized;
        ConfigStore.Save(_config);
        _localApi.UpdateConfig(_config);
        await NotifyApiInfoAsync();
        await PushLoginStateAsync();
    }

    private static string? NormalizeUserToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var trimmed = raw.Trim();
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind == JsonValueKind.String)
                return doc.RootElement.GetString();
        }
        catch
        {
            // plain token string
        }

        return trimmed.Trim('"');
    }

    private async Task SyncTokenFromPageAsync()
    {
        try
        {
            if (_web.IsAgentHostPage)
                return;

            var raw = await _web.GetUserTokenAsync();
            if (string.IsNullOrWhiteSpace(raw))
                return;
            var token = NormalizeUserToken(raw);
            if (string.IsNullOrWhiteSpace(token))
                return;
            if (token == _config.WebUserToken)
                return;
            await ApplyWebUserTokenAsync(token);
        }
        catch
        {
            // page may not be ready
        }
    }

    private void OpenSettings()
    {
        var dlg = new DesktopSettingsWindow(_config, _mcpHub) { Owner = _owner };
        if (dlg.ShowDialog() == true && dlg.Config is not null)
        {
            _config = dlg.Config;
            ConfigStore.Save(_config);
            _localApi.UpdateConfig(_config);
            _ = NotifyApiInfoAsync();
        }
    }

    private void EnsureAgentWindow()
    {
        if (_agentWindow is { IsLoaded: true })
        {
            _agentWindow.Show();
            _agentWindow.Activate();
            return;
        }

        _agentWindow = new AgentRunWindow { Owner = _owner };
        _agentWindow.StopRequested += (_, _) => _runCts?.Cancel();
        _agentWindow.Closed += (_, _) => _agentWindow = null;
        _agentWindow.Show();
    }

    private async Task RunAgentAsync(
        string task, string mode, bool deepThink, bool smartSearch, bool mcpOn, string strategy,
        IReadOnlyList<string>? refFileIds = null)
    {
        _runCts?.Cancel();
        _runCts = new CancellationTokenSource();
        var ct = _runCts.Token;

        if (_agentWindow is { IsLoaded: true })
        {
            _agentWindow.ClearLog();
            _agentWindow.SetTask(task);
            _agentWindow.SetRunning(true);
        }

        ReloadConfig();
        await SyncTokenFromPageAsync();
        AgentModeHelper.ApplyAgentDefaults(_config);
        AgentModeHelper.ApplyChatMode(_config, "专家", deepThink: true);
        deepThink = true;
        smartSearch = true;
        _web.AgentRefFileIds = refFileIds ?? Array.Empty<string>();
        ConfigStore.Save(_config);
        _localApi.UpdateConfig(_config);

        void Log(string line)
        {
            Application.Current.Dispatcher.Invoke(() => _agentWindow?.AppendLog(line));
            _ = _web.PostToPageAsync(new { type = "agentLog", text = line });
            if (line.StartsWith("Final Answer: ", StringComparison.OrdinalIgnoreCase))
            {
                var ans = line["Final Answer: ".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(ans))
                    _ = _web.PostToPageAsync(new { type = "agentAnswer", text = ans });
            }
        }

        try
        {
            await _web.PostToPageAsync(new { type = "agentStarted", task });

            if (string.IsNullOrWhiteSpace(_config.WebUserToken))
            {
                Log("请先在网页登录 DeepSeek。");
                return;
            }

            Log($"Qwen Code × DeepSeek：{_localApi.BaseUrl}/chat/completions → 网页 Token");
            Log($"工作区: {QwenCodeBuiltinTools.ResolveWorkspaceRoot(_config)}");

            if (mcpOn)
            {
                if (_config.EnableQwenCodeBuiltinTools)
                    Log($"内置工具: {QwenCodeBuiltinTools.GetDescriptors(_config).Count} 个（read/write/glob/grep/shell）");

                if (_mcpHub.ConnectedCount == 0)
                    await ConnectMcpAsync(Log, ct);
            }

            var answer = await _qwenAgent.RunAsync(
                _config, task, strategy, mcpOn, deepThink, smartSearch, Log, ct);
            var summary = string.IsNullOrWhiteSpace(answer) ? "任务已结束" : answer;
            await _web.PostToPageAsync(new { type = "agentDone", summary, answer });
        }
        catch (OperationCanceledException)
        {
            Log("已停止。");
            await _web.PostToPageAsync(new { type = "agentDone", summary = "已停止" });
        }
        catch (Exception ex)
        {
            Log("错误: " + ex.Message);
            await _web.PostToPageAsync(new { type = "agentDone", summary = "失败: " + ex.Message });
        }
        finally
        {
            _web.AgentRefFileIds = Array.Empty<string>();
            _agentWindow?.SetRunning(false);
        }
    }

    private async Task ConnectMcpAsync(Action<string> onLog, CancellationToken ct)
    {
        await _mcpHub.DisconnectAllAsync(ct);
        var errors = await _mcpHub.ConnectEnabledAsync(_config.McpServers, onLog, ct);
        onLog($"已连接 {_mcpHub.ConnectedCount} 个 MCP 服务");
        foreach (var err in errors)
            onLog("连接失败: " + err);
    }

    public async ValueTask DisposeAsync()
    {
        _web.MessageReceived -= OnWebMessage;
        _runCts?.Cancel();
        _runCts?.Dispose();
        _runCts = null;
        await _mcpHub.DisposeAsync();
    }
}
