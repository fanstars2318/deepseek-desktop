using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.DeepSeekTui;
using DeepSeekBrowser.Views;

namespace DeepSeekBrowser.Services;

public sealed class DesktopAgentHost : IAsyncDisposable
{
    private readonly McpHub _mcpHub = new();
    private readonly DeepSeekTuiHost _tuiHost = new();
    private DeepSeekTuiAgentRunner? _tuiAgent;
    private readonly DesktopWebHost _pages;
    private readonly LocalOpenAiServer _localApi;
    private AppConfig _config = new();
    private CancellationTokenSource? _runCts;
    private readonly SemaphoreSlim _workModeGate = new(1, 1);
    private Window? _owner;
    private AgentRunWindow? _agentWindow;
    private Chat2ApiIpcBridge? _chat2ApiIpc;
    private EmbeddedSettingsSupport? _embeddedSettings;

    public Func<string, Task>? NavigateToUrl { get; set; }

    /// <summary>为 false 时不向 WebView Agent 页 postMessage（WinUI 原生 Agent 使用）。</summary>
    public bool PostToWebUi { get; set; } = true;

    public event EventHandler<AgentMessageEventArgs>? OnMessage;
    public event EventHandler<AgentStreamDeltaEventArgs>? OnStreamDelta;
    public event EventHandler<AgentRunStateEventArgs>? OnRunStateChanged;

    private static Task RunOnUiAsync(Func<Task> action)
    {
        var dispatcher = Application.Current.Dispatcher;
        return dispatcher.CheckAccess()
            ? action()
            : dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    private async Task NavigateForWorkModeAsync(string targetUrl, string mode) =>
        await ApplyWorkModeAsync(default, mode);

    public DesktopAgentHost(DesktopWebHost pages, LocalOpenAiServer localApi)
    {
        _pages = pages;
        _localApi = localApi;
        _pages.MessageReceived += OnWebMessage;
    }

    public void SetOwner(Window owner)
    {
        _owner = owner;
    }

    private Task<bool> RequestToolApprovalAsync(string toolName, string detail) =>
        RequestToolApprovalAsync(toolName, detail, "执行工具", "DeepSeek-TUI 工具审批");

    private Task<bool> RequestToolApprovalAsync(string toolName, string detail, string action, string title)
    {
        var tcs = new TaskCompletionSource<bool>();
        Application.Current.Dispatcher.Invoke(() =>
        {
            var allowed = Views.DsMessageDialog.Confirm(
                _owner,
                $"Agent 请求{action}：\n\n工具: {toolName}\n\n{detail}\n\n是否允许？",
                title,
                "允许",
                "拒绝");
            tcs.TrySetResult(allowed);
        });
        return tcs.Task;
    }

    private DeepSeekTuiAgentRunner GetTuiAgent() =>
        _tuiAgent ??= new DeepSeekTuiAgentRunner(_tuiHost, (tool, desc) => RequestToolApprovalAsync(tool, desc));

    /// <summary>供 DEEPSEEK_DESKTOP_VERIFY_WORKMODE=1 自检切换链路。</summary>
    public Task VerifyWorkModeSwitchAsync(string mode) => ApplyWorkModeAsync(default, mode);

    public void Start()
    {
        _config = ConfigStore.Load();
        Chat2ApiCompat.EnsureDefaultMappings(_config);
        _localApi.UpdateConfig(_config);
        if (_config.EnableExternalOpenAiApi)
            _localApi.EnsureExternalApiListening();
        _ = ConnectEnabledMcpServersAsync();
        _ = EnsureEmbeddedStackLinkedAsync();
    }

    /// <summary>启动后自动联通内嵌对话服务与 DeepSeek-TUI（无需手动启代理）。</summary>
    public Task EnsureEmbeddedStackLinkedAsync(CancellationToken ct = default)
    {
        ReloadConfig();
        return EmbeddedStackCoordinator.EnsureLinkedAsync(_config, _localApi, _tuiHost, _pages.Chat, ct);
    }

    public async Task WarmChat2ApiBridgeAsync()
    {
        ReloadConfig();
        if (!string.IsNullOrWhiteSpace(_config.WebUserToken))
            await _pages.SyncApiBridgeTokenAsync(_config.WebUserToken);
        try
        {
            await _pages.EnsureApiBridgeReadyAsync();
        }
        catch
        {
            // 启动时网络未就绪可忽略，发消息前会再次探测
        }
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
        ReloadConfig();
        var loggedIn = !string.IsNullOrWhiteSpace(_config.WebUserToken);
        Chat2ApiHealth? health = null;
        try
        {
            health = await _pages.ProbeChat2ApiHealthAsync(_config.WebUserToken, InternalChatChannel.DesktopV1);
        }
        catch
        {
            // ignore
        }

        Chat2ApiProviderService.WriteIntegrationFile(_config, health);

        await _pages.PostToPageAsync(new
        {
            type = "loginState",
            loggedIn,
            workMode = _config.DefaultWorkMode,
            agentStrategy = _config.DefaultAgentStrategy,
            agentDeepThinking = _config.AgentDeepThinking,
            agentWebSearch = _config.AgentWebSearch,
            hint = loggedIn
                ? "网页会话已同步，可使用 Agent 与深度思考"
                : "请先在普通对话登录 DeepSeek 网页",
            agentEngine = "deepseek-tui"
        });
    }

    private Task ApplyWorkModeAsync(JsonElement msg, string mode, bool navigate = true) =>
        RunOnUiAsync(() => ApplyWorkModeOnUiAsync(msg, mode, navigate));

    private async Task ApplyWorkModeOnUiAsync(JsonElement msg, string mode, bool navigate)
    {
        await _workModeGate.WaitAsync();
        try
        {
            mode = WorkModeCoordinator.NormalizeMode(mode);
            var toAgent = mode is "agent" or "plan";
            WorkModeTrace.Write($"ApplyWorkMode start mode={mode} agentVisible={_pages.IsAgentVisible}");

            _pages.WorkMode.CancelScheduledRetries();

            _config.DefaultWorkMode = mode;
            if (mode == "plan")
                _config.DefaultAgentStrategy = AgentStrategies.Plan;
            else if (mode == "agent")
                _config.DefaultAgentStrategy = AgentStrategies.React;
            _localApi.UpdateConfig(_config);
            _pages.WorkMode.SetModeFromConfig(mode);

            var skipNavigate = TryGetMessageProperty(msg, "skipNavigate", out var snEl)
                               && snEl.ValueKind == JsonValueKind.True;

            if (toAgent)
            {
                await _pages.WorkMode.ShowAgentSurfaceAsync();
                await _pages.WorkMode.BroadcastImmediateAsync();
                WorkModeTrace.Write($"ApplyWorkMode done agent visible={_pages.IsAgentVisible}");
                QueueAgentSurfaceFollowUp(msg);
                return;
            }

            await _pages.WorkMode.ShowChatSurfaceAsync();
            await _pages.WorkMode.BroadcastImmediateAsync();
            if (navigate && !skipNavigate && NavigateToUrl is not null)
                _ = NavigateToUrl(AppNavigation.DeepSeekUrl);

            WorkModeTrace.Write($"ApplyWorkMode done chat visible={!_pages.IsAgentVisible}");
            QueueChatSurfaceFollowUp(msg);
        }
        catch (Exception ex)
        {
            WorkModeTrace.Write("ApplyWorkMode error: " + ex);
            throw;
        }
        finally
        {
            _workModeGate.Release();
        }
    }

    private async void OnWebMessage(object? sender, JsonElement msg)
    {
        if (!msg.TryGetProperty("type", out var typeEl)) return;
        var type = typeEl.GetString();
        if (type is "setWorkMode" or "toggleWorkMode")
            WorkModeTrace.Write("OnWebMessage " + msg.GetRawText());

        switch (type)
        {
            case "nativeReady":
                await _pages.WorkMode.BroadcastImmediateAsync();
                _pages.WorkMode.ScheduleBroadcastRetries();
                _ = RefreshLoginStateBackgroundAsync();
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
            case "prepareEmbeddedPanel":
                ReloadConfig();
                GetChat2ApiIpc().RefreshConfig(_config);
                break;
            case "openSettings":
                await EnsureAgentAndShowEmbeddedPanelAsync("settings");
                break;
            case "openChat2Api":
                await EnsureAgentAndShowEmbeddedPanelAsync("chat2api");
                break;
            case "ipcInvoke":
                await HandleEmbeddedIpcInvokeAsync(msg);
                break;
            case "consoleUiReady":
                await _pages.Agent.PostToPageAsync(new { type = "embeddedPanelReady", panel = "chat2api" });
                break;
            case "openDeepSeekLogin":
                if (NavigateToUrl is not null)
                    await NavigateToUrl(AppNavigation.DeepSeekUrl);
                await RefreshLoginStateAsync();
                break;
            case "syncDesktopStack":
                ReloadConfig();
                GetChat2ApiIpc().RefreshConfig(_config);
                await SyncChat2ApiStackAsync(_config, CancellationToken.None);
                await _pages.Agent.PostToPageAsync(new { type = "desktopStackSynced", ok = true });
                break;
            case "openTuiConfigFile":
                OpenTuiConfigFile();
                break;
            case "openAgentFromChat2Api":
                await _pages.Agent.PostToPageAsync(new { type = "hideEmbeddedPanel" });
                Application.Current.Dispatcher.Invoke(EnsureAgentWindow);
                break;
            case "settingsLoad":
            case "settingsSave":
            case "settingsConnectAllMcp":
            case "settingsMcpToggle":
            case "settingsMcpAdd":
            case "settingsMcpEdit":
            case "settingsMcpRemove":
            case "settingsRunDoctor":
            case "settingsBuildTui":
            case "settingsOpenDeepSeekHome":
            case "settingsOpenDocs":
            case "settingsCopyConfigPath":
            case "settingsOpenConfig":
                await GetEmbeddedSettings().HandleAsync(msg);
                if (type == "settingsSave")
                {
                    _localApi.UpdateConfig(_config);
                    GetChat2ApiIpc().RefreshConfig(_config);
                    await NotifyApiInfoAsync();
                }
                break;
            case "openAgentWorkspace":
                Application.Current.Dispatcher.Invoke(EnsureAgentWindow);
                break;
            case "showProviderCard":
                await NotifyApiInfoAsync();
                break;
            case "requestWorkModeState":
                await _pages.WorkMode.BroadcastImmediateAsync();
                _pages.WorkMode.ScheduleBroadcastRetries();
                break;
            case "toggleWorkMode":
                await ApplyWorkModeAsync(msg, _pages.WorkMode.ToggleTargetMode());
                break;
            case "setWorkMode":
                if (msg.TryGetProperty("mode", out var modeEl))
                {
                    var mode = modeEl.GetString();
                    if (mode is "chat" or "agent" or "plan")
                        await ApplyWorkModeAsync(msg, mode);
                }
                break;
            case "navigateToAgent":
                await ApplyTokenFromMessageAsync(msg);
                await SyncTokenFromChatPageAsync();
                _config.DefaultWorkMode = "agent";
                ConfigStore.Save(_config);
                if (NavigateToUrl is not null)
                    await NavigateForWorkModeAsync(AppNavigation.AgentPageUrl, "agent");
                await RefreshLoginStateAsync();
                _ = PushLoginStateAfterAgentNavAsync();
                break;
            case "navigateToChat":
                _config.DefaultWorkMode = "chat";
                ConfigStore.Save(_config);
                if (NavigateToUrl is not null)
                    await NavigateForWorkModeAsync(AppNavigation.DeepSeekUrl, "chat");
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

                var sessionId = msg.TryGetProperty("sessionId", out var sidEl) ? sidEl.GetString() : null;
                var tuiThreadId = msg.TryGetProperty("tuiThreadId", out var ttEl) ? ttEl.GetString() : null;

                _ = RunAgentAsync(
                    text!, chatMode ?? "专家", deepThink, smartSearch, mcpOn,
                    strategy ?? AgentStrategies.React, refIds, sessionId, tuiThreadId);
                break;
            case "agentStop":
                _runCts?.Cancel();
                break;
            case "setAgentFeatures":
                if (msg.TryGetProperty("deepThink", out var dtEl))
                    _config.AgentDeepThinking = dtEl.GetBoolean();
                if (msg.TryGetProperty("smartSearch", out var ssEl))
                    _config.AgentWebSearch = ssEl.GetBoolean();
                ConfigStore.Save(_config);
                DeepSeekTuiConfigSync.Apply(_config);
                await NotifyApiInfoAsync();
                break;
        }
    }

    public async Task SyncTokenFromChatPageAsync()
    {
        if (_pages.IsAgentVisible)
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

    public Task OnAgentNavigationCompletedAsync()
    {
        _ = PushCachedLoginStateAsync();
        _ = PushLoginStateAfterAgentNavAsync();
        _ = RefreshLoginStateBackgroundAsync();
        return Task.CompletedTask;
    }

    private async Task PushLoginStateAfterAgentNavAsync()
    {
        foreach (var delay in new[] { 0, 120, 400, 900 })
        {
            if (delay > 0)
                await Task.Delay(delay);
            if (!_pages.IsAgentVisible)
                return;
            await PushLoginStateAsync();
            await NotifyApiInfoAsync();
        }
    }

    public async Task RefreshLoginStateAsync()
    {
        ReloadConfig();
        // 先用已保存的 token 更新 Agent 页，避免桥接未就绪时长期停在「检测中…」
        await PushLoginStateAsync();
        await NotifyApiInfoAsync();

        _ = RefreshLoginStateBackgroundAsync();
    }

    private async Task RefreshLoginStateBackgroundAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_config.WebUserToken))
                await _pages.SyncApiBridgeTokenAsync(_config.WebUserToken);

            await SyncTokenFromBridgeOrChatAsync();
            ReloadConfig();
            await PushLoginStateAsync();
            await NotifyApiInfoAsync();
        }
        catch
        {
            // 后台同步失败不影响已展示的登录状态
        }
    }

    private async Task SyncTokenFromBridgeOrChatAsync()
    {
        try
        {
            var token = await _pages.TryReadUserTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
                return;
            var normalized = NormalizeUserToken(token);
            if (string.IsNullOrWhiteSpace(normalized) || normalized == _config.WebUserToken)
                return;
            await ApplyWebUserTokenAsync(normalized);
        }
        catch
        {
            // 页面或桥接尚未就绪
        }
    }

    public async Task PushLoginStateToPageAsync() => await PushLoginStateAsync();

    private async Task PushLoginStateAsync()
    {
        ReloadConfig();
        var loggedIn = !string.IsNullOrWhiteSpace(_config.WebUserToken);
        await _pages.PushAgentAuthHintAsync(loggedIn);
        await _pages.PostToPageAsync(new { type = "loginState", loggedIn });
    }

    private void QueueAgentSurfaceFollowUp(JsonElement msg)
    {
        ConfigStore.Save(_config);
        _pages.WorkMode.ScheduleBroadcastRetries();
        _ = ApplyTokenFromMessageAsync(msg);
        _ = SyncTokenFromPageAsync();
        _ = PushCachedLoginStateAsync();
        _ = PushLoginStateAfterAgentNavAsync();
        _ = RefreshLoginStateBackgroundAsync();
    }

    private void QueueChatSurfaceFollowUp(JsonElement msg)
    {
        ConfigStore.Save(_config);
        _pages.WorkMode.ScheduleBroadcastRetries();
        _ = ApplyTokenFromMessageAsync(msg);
        _ = RefreshLoginStateBackgroundAsync();
    }

    private async Task PushCachedLoginStateAsync()
    {
        try
        {
            ReloadConfig();
            var loggedIn = !string.IsNullOrWhiteSpace(_config.WebUserToken);
            await _pages.PushAgentAuthHintAsync(loggedIn);
            await _pages.PostToPageAsync(new { type = "loginState", loggedIn });
        }
        catch
        {
            // 页面尚未就绪时由后续重试补齐
        }
    }

    private static bool TryGetMessageProperty(JsonElement msg, string name, out JsonElement value)
    {
        value = default;
        if (msg.ValueKind != JsonValueKind.Object)
            return false;
        return msg.TryGetProperty(name, out value);
    }

    private async Task ApplyTokenFromMessageAsync(JsonElement msg)
    {
        if (!TryGetMessageProperty(msg, "token", out var tokenEl) || tokenEl.ValueKind != JsonValueKind.String)
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
        await Chat2ApiStackBootstrap.OnWebLoginAsync(_config, _localApi, _tuiHost, _pages.Chat);
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
            if (_pages.IsAgentVisible)
                return;

            var raw = await _pages.GetUserTokenAsync();
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

    private Chat2ApiIpcBridge GetChat2ApiIpc() =>
        _chat2ApiIpc ??= new Chat2ApiIpcBridge(_localApi, _pages.Agent, () => _owner, SyncChat2ApiStackAsync);

    private async Task SyncChat2ApiStackAsync(AppConfig config, CancellationToken ct)
    {
        DeepSeekTuiConfigSync.Apply(config);
        await EmbeddedStackCoordinator.EnsureLinkedAsync(config, _localApi, _tuiHost, _pages.Chat, ct)
            .ConfigureAwait(false);
        Chat2ApiHealth? health = null;
        try
        {
            health = await _pages.ProbeChat2ApiHealthAsync(config.WebUserToken, InternalChatChannel.DesktopV1, ct)
                .ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        Chat2ApiProviderService.WriteIntegrationFile(config, health);
    }

    private EmbeddedSettingsSupport GetEmbeddedSettings() =>
        _embeddedSettings ??= new EmbeddedSettingsSupport(
            _mcpHub,
            () => _owner,
            () => _config,
            cfg => _config = cfg,
            () => _pages.Agent);

    private async Task EnsureAgentAndShowEmbeddedPanelAsync(string panel)
    {
        if (!_pages.IsAgentVisible)
        {
            await _pages.WorkMode.ShowAgentSurfaceAsync();
            await _pages.WorkMode.BroadcastImmediateAsync();
        }
        await ShowEmbeddedPanelAsync(panel);
    }

    private async Task ShowEmbeddedPanelAsync(string panel)
    {
        ReloadConfig();
        GetChat2ApiIpc().RefreshConfig(_config);
        await _pages.Agent.PostToPageAsync(new { type = "showEmbeddedPanel", panel });
    }

    private async Task HandleEmbeddedIpcInvokeAsync(JsonElement msg)
    {
        var id = msg.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var parsed) ? parsed : 0;
        var channel = msg.TryGetProperty("channel", out var chEl) ? chEl.GetString() ?? "" : "";
        var args = msg.TryGetProperty("args", out var argsEl) && argsEl.ValueKind == JsonValueKind.Array
            ? argsEl.EnumerateArray().Select(x => x.Clone()).ToArray()
            : Array.Empty<JsonElement>();

        object? result = null;
        string? error = null;
        try
        {
            result = await GetChat2ApiIpc().InvokeAsync(channel, args);
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        await _pages.Agent.PostToPageAsync(new { type = "ipcResult", id, result, error });
    }

    private void OpenTuiConfigFile()
    {
        DeepSeekTuiConfigSync.Apply(_config);
        var path = DeepSeekTuiConfigSync.ConfigPath;
        Directory.CreateDirectory(DeepSeekTuiConfigSync.HomeDirectory);
        if (!File.Exists(path))
            File.WriteAllText(path, "# Generated by DeepSeek Desktop\n", Encoding.UTF8);
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void OpenSettingsWindow()
    {
        var dlg = new DesktopSettingsWindow(_config, _mcpHub) { Owner = _owner };
        var saved = dlg.ShowDialog() == true;
        if (saved && dlg.Config is not null)
        {
            _config = dlg.Config;
            ConfigStore.Save(_config);
        }
        else
            ReloadConfig();

        _localApi.UpdateConfig(_config);
        _ = NotifyApiInfoAsync();
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
        IReadOnlyList<string>? refFileIds = null,
        string? sessionId = null,
        string? tuiThreadId = null)
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
        AgentModeHelper.ApplyChatMode(_config, "专家", deepThink);
        _config.AgentDeepThinking = deepThink;
        _config.AgentWebSearch = smartSearch;
        _pages.AgentRefFileIds = refFileIds ?? Array.Empty<string>();
        ConfigStore.Save(_config);
        _localApi.UpdateConfig(_config);

        using var featureScope = Chat2ApiFeatureScope.Begin(deepThink, smartSearch);
        _localApi.EnsureAgentScopedListening();
        DeepSeekTuiConfigSync.Apply(_config);
        using var debugLog = _config.AgentDebugLogEnabled
            ? AgentDebugLogger.Begin(task, _config.AgentDebugLogConsole)
            : null;

        debugLog?.Write("CONFIG", $"深度思考={deepThink} 智能搜索={smartSearch} strategy={strategy} mcp={mcpOn}");
        debugLog?.Write("CONFIG", $"TUI port={_config.DeepSeekTuiRuntimePort} Chat2API port={_config.LocalApiPort}");

        void Log(string line)
        {
            debugLog?.Write("AGENT", line);
            Application.Current.Dispatcher.Invoke(() => _agentWindow?.AppendLog(line));
            OnMessage?.Invoke(this, new AgentMessageEventArgs("log", line, "log"));
            if (PostToWebUi)
                _ = _pages.PostToPageAsync(new { type = "agentLog", text = line });
            if (line.StartsWith("Final Answer: ", StringComparison.OrdinalIgnoreCase))
            {
                var ans = line["Final Answer: ".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(ans))
                {
                    OnMessage?.Invoke(this, new AgentMessageEventArgs("assistant", ans));
                    if (PostToWebUi)
                        _ = _pages.PostToPageAsync(new { type = "agentAnswer", text = ans });
                }
            }
        }

        try
        {
            OnRunStateChanged?.Invoke(this, new AgentRunStateEventArgs(AgentRunState.Running));
            if (PostToWebUi)
                await _pages.PostToPageAsync(new { type = "agentStarted", task });

            if (string.IsNullOrWhiteSpace(_config.WebUserToken))
            {
                Log("请先在网页登录 DeepSeek。");
                return;
            }

            if (debugLog is not null)
                Log("调试日志: " + debugLog.LogFilePath);

            if (deepThink || smartSearch)
            {
                var flags = new List<string>();
                if (deepThink) flags.Add("深度思考");
                if (smartSearch) flags.Add("智能搜索");
                Log("特性: " + string.Join(" · ", flags) + "（Chat2API）");
            }

            void PostActivity(AgentUiActivity a)
            {
                debugLog?.Write("ACTIVITY", $"{a.Verb} {a.Target}" + (a.Detail is not null ? $" ({a.Detail})" : ""));
                _ = _pages.PostToPageAsync(new
                {
                    type = "agentActivity",
                    verb = a.Verb,
                    target = a.Target,
                    detail = a.Detail
                });
            }

            void PostThinking(string text, bool append)
            {
                debugLog?.LogThinkingDelta(text);
                OnStreamDelta?.Invoke(this, new AgentStreamDeltaEventArgs(text, append, isThinking: true));
                if (PostToWebUi)
                    _ = _pages.PostToPageAsync(new { type = "agentThinking", text, append });
            }

            var result = await GetTuiAgent().RunAsync(
                _config, task, strategy, tuiThreadId, Log,
                delta =>
                {
                    if (string.IsNullOrEmpty(delta)) return;
                    OnStreamDelta?.Invoke(this, new AgentStreamDeltaEventArgs(delta, append: true, isThinking: false));
                    if (PostToWebUi)
                        _ = _pages.PostToPageAsync(new { type = "agentAnswer", text = delta, append = true });
                },
                ct,
                PostActivity,
                PostThinking);

            var answer = result.Answer;
            var summary = answer;

            await _pages.PostToPageAsync(new
            {
                type = "agentTuiThread",
                sessionId,
                tuiThreadId = result.ThreadId
            });

            OnRunStateChanged?.Invoke(this, new AgentRunStateEventArgs(AgentRunState.Completed, summary, answer));
            if (PostToWebUi)
                await _pages.PostToPageAsync(new { type = "agentDone", summary, answer });
        }
        catch (OperationCanceledException)
        {
            Log("已停止。");
            OnRunStateChanged?.Invoke(this, new AgentRunStateEventArgs(AgentRunState.Cancelled, "已停止"));
            if (PostToWebUi)
                await _pages.PostToPageAsync(new { type = "agentDone", summary = "已停止", answer = "" });
        }
        catch (Exception ex)
        {
            Log("错误: " + ex.Message);
            OnRunStateChanged?.Invoke(this, new AgentRunStateEventArgs(AgentRunState.Failed, ex.Message));
            if (PostToWebUi)
                await _pages.PostToPageAsync(new { type = "agentDone", summary = "失败: " + ex.Message });
        }
        finally
        {
            _localApi.ReleaseAgentScopedListening();
            _pages.AgentRefFileIds = Array.Empty<string>();
            _agentWindow?.SetRunning(false);
        }
    }

    /// <summary>供 DEEPSEEK_DESKTOP_VERIFY_AGENT=1 自检 Agent HELLO 链路。</summary>
    public async Task VerifyAgentHelloAsync(CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<AgentRunStateEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? _, AgentRunStateEventArgs e)
        {
            if (e.State is AgentRunState.Completed or AgentRunState.Failed or AgentRunState.Cancelled)
                tcs.TrySetResult(e);
        }

        OnRunStateChanged += Handler;
        try
        {
            WorkModeTrace.Write("AgentSelfTest: starting hello run");
            await RunAgentAsync("hello", "专家", deepThink: false, smartSearch: false, mcpOn: false, strategy: AgentStrategies.React)
                .ConfigureAwait(false);
            using var reg = ct.Register(() => tcs.TrySetCanceled());
            var result = await tcs.Task.ConfigureAwait(false);
            if (result.State == AgentRunState.Completed &&
                !string.IsNullOrWhiteSpace(result.Answer) &&
                !LooksLikeAgentError(result.Answer))
            {
                WorkModeTrace.Write($"AgentSelfTest: PASS answer={TrimForLog(result.Answer)}");
                return;
            }

            var msg = result.State == AgentRunState.Completed
                ? "empty or error-like answer: " + (result.Answer ?? "")
                : result.Summary ?? result.State.ToString();
            WorkModeTrace.Write("AgentSelfTest: FAIL " + msg);
            throw new InvalidOperationException(msg);
        }
        finally
        {
            OnRunStateChanged -= Handler;
        }
    }

    private static bool LooksLikeAgentError(string text) =>
        text.Contains("错误", StringComparison.Ordinal) ||
        text.Contains("error", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("Win32", StringComparison.Ordinal) ||
        text.StartsWith("失败", StringComparison.Ordinal);

    private static string TrimForLog(string s) =>
        s.Length <= 120 ? s : s[..120] + "…";

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
        _pages.MessageReceived -= OnWebMessage;
        _runCts?.Cancel();
        _runCts?.Dispose();
        _runCts = null;
        await _mcpHub.DisposeAsync();
        await _tuiHost.DisposeAsync();
    }
}
