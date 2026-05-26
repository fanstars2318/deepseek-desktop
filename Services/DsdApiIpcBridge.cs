using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.ApiManagement;

namespace DeepSeekBrowser.Services;

/// <summary>
/// WebView2 版 DSD API 管理台 IPC 桥：将 <c>window.electronAPI</c> 调用映射到内嵌 DSD API 栈。
/// </summary>
public sealed class DsdApiIpcBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly LocalOpenAiServer _localApi;
    private readonly WebInjectService _web;
    private readonly Func<Window?> _owner;
    private readonly Func<AppConfig, CancellationToken, Task>? _onStackSync;
    private AppConfig _config;
    private readonly long _proxyStartedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private readonly Dictionary<string, JsonElement> _uiStore = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (object Value, long ExpiresAt)> _readCache = new(StringComparer.Ordinal);

    private static readonly HashSet<string> CachedReadChannels = new(StringComparer.Ordinal)
    {
        "proxy:getStatus",
        "proxy:getStatistics",
        "config:get",
        "providers:getAll",
        "providers:getBuiltin",
        "providers:checkAllStatus",
        "statistics:get",
        "statistics:getToday",
        "logs:getStats",
        "requestLogs:getStats",
        "session:getConfig",
        "managementApi:getConfig",
        "contextManagement:getConfig",
        "accounts:getAll",
    };

    public DsdApiIpcBridge(
        LocalOpenAiServer localApi,
        WebInjectService web,
        Func<Window?> owner,
        Func<AppConfig, CancellationToken, Task>? onStackSync = null)
    {
        _localApi = localApi;
        _web = web;
        _owner = owner;
        _onStackSync = onStackSync;
        _config = ConfigStore.Load();
        if (!_localApi.IsListening)
            _localApi.Start();
    }

    public void RefreshConfig(AppConfig config)
    {
        _config = config;
        _localApi.UpdateConfig(config);
        _readCache.Clear();
    }

    public async Task<object?> InvokeAsync(string channel, JsonElement[] args, CancellationToken ct = default)
    {
        if (CachedReadChannels.Contains(channel) && args.Length == 0 && TryGetCached(channel, out var hit))
            return hit;

        var result = await InvokeCoreAsync(channel, args, ct);

        if (CachedReadChannels.Contains(channel) && args.Length == 0 && result is not null)
            SetCached(channel, result);

        return result;
    }

    private bool TryGetCached(string channel, out object? value)
    {
        value = null;
        if (!_readCache.TryGetValue(channel, out var entry)) return false;
        if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > entry.ExpiresAt)
        {
            _readCache.Remove(channel);
            return false;
        }
        value = entry.Value;
        return true;
    }

    private void SetCached(string channel, object value) =>
        _readCache[channel] = (value, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 2000);

    private async Task<object?> InvokeCoreAsync(string channel, JsonElement[] args, CancellationToken ct)
    {
        return channel switch
        {
            "proxy:start" => await ProxyStartAsync(args),
            "proxy:stop" => ProxyStop(),
            "proxy:getStatus" => ProxyStatus(),
            "proxy:getStatistics" => EmptyProxyStatistics(),
            "config:get" => BuildDsdApiConfig(),
            "config:update" => await ConfigUpdate(args),
            "store:get" => StoreGet(args),
            "store:set" => await StoreSet(args),
            "store:delete" => StoreDelete(args),
            "store:clearAll" => StoreClearAll(),
            "store:retryInit" => new { success = true },
            "providers:getAll" => GetProviders(),
            "providers:getBuiltin" => GetBuiltinProviders(),
            "providers:checkStatus" => CheckProviderStatus(args),
            "providers:checkAllStatus" => CheckAllProviderStatus(),
            "providers:getEffectiveModels" => GetEffectiveModels(args),
            "providers:updateModels" => new { success = true, modelsCount = DsdOpenAiCompat.ListModels(_config).Count },
            "providers:addCustomModel" => await AddCustomModelAsync(args),
            "providers:removeModel" => await RemoveCustomModelAsync(args),
            "providers:resetModels" => await ResetModelsAsync(args),
            "providers:add" => await AddProviderAsync(args),
            "providers:update" => await UpdateProviderAsync(args),
            "providers:delete" => DeleteProvider(args),
            "providers:duplicate" => Unsupported("内嵌模式不支持"),
            "providers:export" => "{}",
            "providers:import" => Unsupported("内嵌模式不支持"),
            "accounts:add" => await AddAccountAsync(args),
            "accounts:update" => Unsupported("Token 由主窗口同步"),
            "accounts:delete" => DeleteAccount(args),
            "accounts:getAll" => GetAccounts(args),
            "accounts:getById" => GetAccountById(args),
            "accounts:getByProvider" => GetAccountsByProvider(args),
            "accounts:validate" => await ValidateAccountAsync(args, ct),
            "accounts:validateToken" => await ValidateTokenAsync(args, ct),
            "accounts:getCredits" => null,
            "accounts:clearChats" => new { success = true },
            "session:getConfig" => SessionConfig(),
            "session:updateConfig" => await SessionUpdateConfig(args),
            "session:getAll" => ListSessions(),
            "session:getActive" => ListSessions(activeOnly: true),
            "session:getById" => SessionById(args),
            "session:delete" => SessionDelete(args),
            "session:clearAll" => SessionClearAll(),
            "session:cleanExpired" => 0,
            "logs:get" => Array.Empty<object>(),
            "logs:getStats" => EmptyLogStats(),
            "logs:getTrend" => Array.Empty<object>(),
            "logs:getAccountTrend" => Array.Empty<object>(),
            "logs:clear" => null,
            "logs:export" => "[]",
            "logs:getById" => null,
            "requestLogs:get" => RequestLogsGet(args),
            "requestLogs:getStats" => DsdApiRequestLogStore.Instance.GetStats(),
            "requestLogs:getTrend" => RequestLogsGetTrend(args),
            "requestLogs:clear" => RequestLogsClear(),
            "requestLogs:getById" => RequestLogsGetById(args),
            "statistics:get" => DsdApiRequestLogStore.Instance.BuildPersistentStatistics(),
            "statistics:getToday" => StatisticsGetToday(),
            "prompts:getAll" => Array.Empty<object>(),
            "prompts:getBuiltin" => Array.Empty<object>(),
            "prompts:getCustom" => Array.Empty<object>(),
            "prompts:getByType" => Array.Empty<object>(),
            "managementApi:getConfig" => ManagementApiConfig(),
            "managementApi:updateConfig" => await ManagementApiUpdateAsync(args, ct),
            "managementApi:generateSecret" => Guid.NewGuid().ToString("N"),
            "contextManagement:getConfig" => ContextManagementConfig(),
            "contextManagement:updateConfig" => await ContextManagementUpdateAsync(args, ct),
            "toolCalling:getStatus" => ToolCallingGetStatus(),
            "toolCalling:runSmoke" => await ToolCallingRunSmokeAsync(args, ct),
            "app:getVersion" => "1.3.0-edge",
            "app:checkUpdate" => new
            {
                hasUpdate = false,
                currentVersion = "embedded",
                latestVersion = "embedded"
            },
            "app:getUpdateStatus" => new
            {
                checking = false,
                available = false,
                downloading = false,
                downloaded = false,
                error = (string?)null,
                progress = (object?)null,
                version = (string?)null,
                releaseDate = (string?)null,
                releaseNotes = (string?)null
            },
            "app:downloadUpdate" => null,
            "app:installUpdate" => null,
            "app:minimize" => MinimizeWindow(),
            "app:maximize" => MaximizeWindow(),
            "app:close" => CloseWindow(),
            "app:showWindow" => ShowWindow(),
            "app:hideWindow" => HideWindow(),
            "app:openExternal" => OpenExternal(args),
            "oauth:startInAppLogin" => await OAuthInAppLoginAsync(args, ct),
            "oauth:cancelInAppLogin" => null,
            "oauth:inAppLoginStatus" => false,
            "oauth:getStatus" => "idle",
            "oauth:loginWithToken" => await OAuthLoginWithTokenAsync(args, ct),
            "oauth:validateToken" => await ValidateTokenAsync(args, ct),
            _ => throw new NotSupportedException($"IPC channel not supported in embedded mode: {channel}")
        };
    }

    private static object Unsupported(string message) => throw new InvalidOperationException(message);

    private Task<bool> ProxyStartAsync(JsonElement[] args)
    {
        if (!_localApi.IsListening)
            _localApi.Start();
        return Task.FromResult(true);
    }

    private bool ProxyStop()
    {
        // 内嵌模式：服务由桌面端托管，不允许从管理台停止
        return true;
    }

    private object ProxyStatus()
    {
        var externalPort = InternalChatChannel.ResolveExternalApiPort(_config);
        return new
        {
            isRunning = true,
            embedded = true,
            mode = "internal",
            internalChannel = InternalChatChannel.DesktopV1,
            port = _config.EnableExternalOpenAiApi ? externalPort : 0,
            host = _config.EnableExternalOpenAiApi ? "127.0.0.1" : "",
            externalApiEnabled = _config.EnableExternalOpenAiApi,
            externalApiBaseUrl = _config.EnableExternalOpenAiApi
                ? InternalChatChannel.GetExternalApiBaseUrl(_config)
                : null,
            uptime = Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _proxyStartedAt),
            connections = 0
        };
    }

    private static object EmptyProxyStatistics() => new
    {
        totalRequests = 0,
        successRequests = 0,
        failedRequests = 0,
        avgLatency = 0,
        requestsPerMinute = 0,
        activeConnections = 0,
        modelUsage = new Dictionary<string, int>(),
        providerUsage = new Dictionary<string, int>(),
        accountUsage = new Dictionary<string, int>()
    };

    private object BuildDsdApiConfig()
    {
        DsdOpenAiCompat.EnsureDefaultMappings(_config);
        var mappings = _config.ModelMappings.ToDictionary(
            m => m.RequestModel,
            m => new
            {
                requestModel = m.RequestModel,
                actualModel = m.ActualModel,
                preferredProviderId = m.PreferredProviderId,
                preferredAccountId = m.PreferredAccountId
            },
            StringComparer.OrdinalIgnoreCase);

        return new
        {
            proxyPort = _config.EnableExternalOpenAiApi
                ? InternalChatChannel.ResolveExternalApiPort(_config)
                : 0,
            proxyHost = "127.0.0.1",
            embeddedMode = true,
            internalChannel = InternalChatChannel.DesktopV1,
            loadBalanceStrategy = string.IsNullOrWhiteSpace(_config.DsdApiLoadBalanceStrategy)
                ? "round-robin"
                : _config.DsdApiLoadBalanceStrategy,
            accountWeights = _config.DsdApiAccountWeights.Select(w => new
            {
                accountId = w.AccountId,
                weight = w.Weight
            }).ToArray(),
            modelMappings = mappings,
            theme = "light",
            autoStart = true,
            autoStartProxy = false,
            logLevel = "info",
            logRetentionDays = 7,
            requestLogConfig = new { enabled = true, maxEntries = 500, retentionDays = 7 },
            requestTimeout = Math.Max(60_000, _config.DsdApiSessionTimeoutMinutes * 60_000),
            retryCount = 2,
            apiKeys = MapApiKeys(),
            enableApiKey = _config.EnableLocalApiKeyAuth,
            oauthProxyMode = "none",
            sessionConfig = SessionConfig(),
            toolCallingConfig = GetToolCallingConfig(),
            managementApi = ManagementApiConfig(),
            contextManagement = ContextManagementConfig(),
            language = "zh-CN",
            deepseekDesktop = BuildDesktopStackInfo()
        };
    }

    private object BuildDesktopStackInfo()
    {
        var snap = DsdApiProviderService.Build(_config);
        var apiAccounts = ProviderAccountStore.ByProvider("deepseek")
            .Count(a => a.Status == "active"
                        && !string.IsNullOrWhiteSpace(
                            AccountCredentials.ResolveWebUserToken(a, _config)));
        var loggedIn = apiAccounts > 0;

        return new
        {
            app = DeepSeekDesktopApp.DisplayName,
            loggedIn,
            loginHint = loggedIn
                ? null
                : "请在 API 管理中手动添加 DeepSeek 账户（普通对话登录不会自动同步）",
            internalChannel = InternalChatChannel.DesktopV1,
            externalApiEnabled = _config.EnableExternalOpenAiApi,
            externalApiBaseUrl = _config.EnableExternalOpenAiApi
                ? InternalChatChannel.GetExternalApiBaseUrl(_config)
                : null,
            harnessEngine = "native",
            tuiRuntimeUrl = snap.TuiRuntimeUrl,
            tuiConfigPath = AgentDesktopConfigSync.ConfigPath,
            integrationFile = DsdApiProviderService.IntegrationFilePath,
            defaultWorkMode = _config.DefaultWorkMode,
            agentStrategy = _config.DefaultAgentStrategy,
            agentDeepThinking = _config.AgentDeepThinking,
            agentWebSearch = _config.AgentWebSearch,
            sessionMode = _config.DsdApiSessionMode,
            modelMappingCount = _config.ModelMappings.Count,
            providerOnline = snap.Online,
            chat2ApiSummary = snap.Description
        };
    }

    private async Task<object> ConfigUpdate(JsonElement[] args)
    {
        if (args.Length > 0 && args[0].ValueKind == JsonValueKind.Object)
            await ApplyConfigPatchAsync(args[0]);
        return true;
    }

    private async Task ApplyConfigPatchAsync(JsonElement patch)
    {
        if (patch.TryGetProperty("toolCallingConfig", out var toolCalling) &&
            toolCalling.ValueKind == JsonValueKind.Object)
            _uiStore["toolCallingConfig"] = toolCalling.Clone();

        if (patch.TryGetProperty("requestLogConfig", out var requestLogConfig) &&
            requestLogConfig.ValueKind == JsonValueKind.Object)
        {
            _uiStore["requestLogConfig"] = requestLogConfig.Clone();
            DsdApiRequestLogStore.Instance.Configure(
                DsdApiRequestLogStore.RequestLogConfig.FromJson(requestLogConfig));
        }

        DsdEmbeddedConfigApplicator.ApplyPatch(_config, patch);
        await PersistAndSyncAsync();
    }

    private object RequestLogsGet(JsonElement[] args)
    {
        DsdApiRequestLogStore.RequestLogQuery? query = null;
        if (args.Length > 0 && args[0].ValueKind == JsonValueKind.Object)
        {
            var o = args[0];
            int? limit = null;
            if (o.TryGetProperty("limit", out var limitEl) && limitEl.TryGetInt32(out var n))
                limit = n;
            query = new DsdApiRequestLogStore.RequestLogQuery
            {
                Status = o.TryGetProperty("status", out var st) ? st.GetString() : null,
                ProviderId = o.TryGetProperty("providerId", out var pid) ? pid.GetString() : null,
                Limit = limit
            };
        }

        return DsdApiRequestLogStore.Instance.GetLogs(query);
    }

    private object RequestLogsGetTrend(JsonElement[] args)
    {
        var days = 7;
        if (args.Length > 0 && args[0].TryGetInt32(out var d))
            days = d;
        return DsdApiRequestLogStore.Instance.GetTrend(days);
    }

    private object? RequestLogsClear()
    {
        DsdApiRequestLogStore.Instance.Clear();
        _readCache.Clear();
        return null;
    }

    private object? RequestLogsGetById(JsonElement[] args)
    {
        var id = args.Length > 0 ? args[0].GetString() : null;
        return string.IsNullOrEmpty(id) ? null : DsdApiRequestLogStore.Instance.GetById(id);
    }

    private object StatisticsGetToday()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var trend = DsdApiRequestLogStore.Instance.GetTrend(1).FirstOrDefault();
        if (trend is null || trend.Date != today)
        {
            return new
            {
                date = today,
                totalRequests = 0,
                successRequests = 0,
                failedRequests = 0,
                totalLatency = 0L,
                modelUsage = new Dictionary<string, int>(),
                providerUsage = new Dictionary<string, int>()
            };
        }

        return new
        {
            date = today,
            totalRequests = trend.Total,
            successRequests = trend.Success,
            failedRequests = trend.Error,
            totalLatency = trend.Success > 0 ? (long)trend.AvgLatency * trend.Success : 0L,
            modelUsage = new Dictionary<string, int>(),
            providerUsage = new Dictionary<string, int>()
        };
    }

    private JsonElement GetToolCallingConfigElement()
    {
        if (_uiStore.TryGetValue("toolCallingConfig", out var stored) &&
            stored.ValueKind == JsonValueKind.Object)
            return stored;

        return JsonSerializer.SerializeToElement(CreateDefaultToolCallingConfig(), JsonOptions);
    }

    private object GetToolCallingConfig() => GetToolCallingConfigElement();

    private static object CreateDefaultToolCallingConfig() => new
    {
        enabled = true,
        mode = "auto",
        clientAdapterId = "standard-openai-tools",
        diagnosticsEnabled = false,
        advanced = new { promptPreviewEnabled = false, customPromptTemplate = (string?)null }
    };

    private object ToolCallingGetStatus()
    {
        var cfg = GetToolCallingConfigElement();
        var enabled = ToolCallingIsEnabled(cfg);
        return new
        {
            enabled,
            config = cfg,
            clientAdapters = new[]
            {
                new { id = "standard-openai-tools", label = "Standard OpenAI Tools", status = "ready" },
                new { id = "cherry-studio-mcp", label = "Cherry Studio MCP", status = "ready" }
            }
        };
    }

    private async Task<object> ToolCallingRunSmokeAsync(JsonElement[] args, CancellationToken ct)
    {
        if (!ToolCallingIsEnabled(GetToolCallingConfigElement()))
            return new { success = false, error = new { message = "托管工具调用未启用，请将模式设为 auto 或 force。" } };

        var token = AccountCredentials.ResolveFirstProviderWebToken("deepseek", _config);
        if (string.IsNullOrWhiteSpace(token))
            return new { success = false, error = new { message = "请先在 API 管理中为 DeepSeek 添加账户。" } };

        try
        {
            var messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "请只回复一个词：OK" }
            };
            var result = await _web.WebChatAsync(
                messages,
                _config.Model,
                thinking: false,
                search: false,
                ct,
                token).ConfigureAwait(false);

            var text = result.Content?.Trim();
            var ok = !string.IsNullOrEmpty(text) && !string.Equals(text, "(无回复)", StringComparison.Ordinal);
            return ok
                ? new { success = true }
                : new { success = false, error = new { message = "网页会话未返回正文，请确认已登录并重试。" } };
        }
        catch (Exception ex)
        {
            return new { success = false, error = new { message = ex.Message } };
        }
    }

    private static bool ToolCallingIsEnabled(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
            return true;

        var enabled = !el.TryGetProperty("enabled", out var en) || en.GetBoolean();
        var mode = el.TryGetProperty("mode", out var modeEl) ? modeEl.GetString() : "auto";
        return enabled && !string.Equals(mode, "off", StringComparison.OrdinalIgnoreCase);
    }

    private async Task PersistAndSyncAsync(CancellationToken ct = default)
    {
        ConfigStore.Save(_config);
        _localApi.UpdateConfig(_config);
        _readCache.Clear();
        if (_onStackSync is not null)
            await _onStackSync(_config, ct).ConfigureAwait(false);
    }

    private void ApplyConfigPatch(JsonElement patch) =>
        DsdEmbeddedConfigApplicator.ApplyPatch(_config, patch);

    private object? StoreGet(JsonElement[] args)
    {
        var key = args.Length > 0 ? args[0].GetString() : null;
        if (string.IsNullOrEmpty(key)) return null;
        if (key == "config") return BuildDsdApiConfig();
        return _uiStore.TryGetValue(key, out var v) ? v : null;
    }

    private async Task<object?> StoreSet(JsonElement[] args)
    {
        if (args.Length < 2) return null;
        var key = args[0].GetString();
        if (string.IsNullOrEmpty(key)) return null;
        if (key == "config" && args[1].ValueKind == JsonValueKind.Object)
            await ApplyConfigPatchAsync(args[1]);
        else
        {
            _uiStore[key] = args[1].Clone();
            await PersistAndSyncAsync();
        }

        return null;
    }

    private object? StoreDelete(JsonElement[] args)
    {
        var key = args.Length > 0 ? args[0].GetString() : null;
        if (!string.IsNullOrEmpty(key))
            _uiStore.Remove(key);
        return null;
    }

    private object? StoreClearAll()
    {
        _uiStore.Clear();
        return null;
    }

    private object[] GetProviders() =>
        ApiProviderRegistry.LoadAll(_config)
            .Select(p => ApiManagementConsoleMapper.ToUiProvider(p, _config))
            .Cast<object>()
            .ToArray();

    private async Task<object> AddProviderAsync(JsonElement[] args)
    {
        var body = args.Length > 0 ? args[0] : default;
        var entry = ApiManagementConsoleMapper.ParseProviderFromUi(body, _config);
        if (body.ValueKind == JsonValueKind.Object
            && body.TryGetProperty("apiKey", out var k)
            && !string.IsNullOrWhiteSpace(k.GetString()))
            CredentialVault.Set(entry.Id, "api_key", k.GetString()!);
        ApiProviderRegistry.AddOrUpdate(_config, entry);
        await PersistAndSyncAsync();
        return ApiManagementConsoleMapper.ToUiProvider(entry, _config);
    }

    private async Task<object> UpdateProviderAsync(JsonElement[] args)
    {
        string? id = null;
        JsonElement body = default;
        if (args.Length >= 2 && args[0].ValueKind == JsonValueKind.String)
        {
            id = args[0].GetString();
            body = args[1];
        }
        else if (args.Length > 0)
            body = args[0];

        var existing = ApiProviderRegistry.Get(_config, id ?? "")
                     ?? ApiProviderRegistry.Get(_config, body.TryGetProperty("id", out var idEl) ? idEl.GetString() : null);
        if (existing is null)
            throw new InvalidOperationException("供应商不存在");

        var entry = ApiManagementConsoleMapper.ParseProviderFromUi(body, _config);
        entry.Id = existing.Id;
        if (body.ValueKind == JsonValueKind.Object
            && body.TryGetProperty("apiKey", out var k)
            && !string.IsNullOrWhiteSpace(k.GetString()))
            CredentialVault.Set(entry.Id, "api_key", k.GetString()!);
        ApiProviderRegistry.AddOrUpdate(_config, entry);
        await PersistAndSyncAsync();
        return ApiManagementConsoleMapper.ToUiProvider(entry, _config);
    }

    private static bool DeleteProvider(JsonElement[] args)
    {
        var body = args.Length > 0 ? args[0] : default;
        var id = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("id", out var el)
            ? el.GetString()
            : null;
        return !string.IsNullOrWhiteSpace(id) && ApiProviderRegistry.Delete(id!);
    }

    private static ApiProviderEntry ParseProviderEntry(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object)
            return new ApiProviderEntry { Id = Guid.NewGuid().ToString("N")[..8], DisplayName = "Custom" };

        return new ApiProviderEntry
        {
            Id = args.TryGetProperty("id", out var id) ? id.GetString() ?? Guid.NewGuid().ToString("N")[..8] : Guid.NewGuid().ToString("N")[..8],
            DisplayName = args.TryGetProperty("name", out var n) ? n.GetString() ?? "Custom" : "Custom",
            Kind = args.TryGetProperty("kind", out var k) ? k.GetString() ?? ApiProviderKinds.OpenAiCompatible : ApiProviderKinds.OpenAiCompatible,
            RouteMode = args.TryGetProperty("routeMode", out var r) ? r.GetString() ?? ApiRouteModes.DirectApi : ApiRouteModes.DirectApi,
            BaseUrl = args.TryGetProperty("baseUrl", out var u) ? u.GetString() ?? "" : "",
            Enabled = !args.TryGetProperty("enabled", out var e) || e.GetBoolean()
        };
    }

    private object[] GetBuiltinProviders() => BuiltinProviderCatalog.ToUiBuiltinList(_config);

    private object CheckProviderStatus(JsonElement[] args)
    {
        var providerId = args.Length > 0 ? args[0].GetString() : "deepseek";
        var entry = ApiProviderRegistry.Get(_config, providerId);
        if (entry is null)
            return new { providerId, status = "offline", latency = (int?)null, error = "未知供应商" };

        if (string.Equals(providerId, "deepseek", StringComparison.OrdinalIgnoreCase))
        {
            var token = AccountCredentials.ResolveFirstProviderWebToken("deepseek", _config);
            var loggedIn = !string.IsNullOrWhiteSpace(token);
            return new
            {
                providerId,
                status = loggedIn ? "online" : "offline",
                latency = loggedIn ? 32 : (int?)null,
                error = loggedIn ? null : "未配置 DeepSeek API 账户，请在 API 管理中手动添加"
            };
        }

        var status = ApiManagementConsoleMapper.ResolveStatus(entry, _config);
        return new
        {
            providerId,
            status,
            latency = status == "online" ? 48 : (int?)null,
            error = status == "online" ? null : "未配置有效账户或 API Key"
        };
    }

    private object CheckAllProviderStatus()
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in ApiProviderRegistry.LoadAll(_config))
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(p.Id));
            dict[p.Id] = CheckProviderStatus([doc.RootElement]);
        }

        return dict;
    }

    private object GetEffectiveModels(JsonElement[] args)
    {
        DsdOpenAiCompat.EnsureDefaultMappings(_config);
        return DsdOpenAiCompat.ListModelIds(_config).Select(id => new
        {
            displayName = id,
            actualModelId = id,
            source = "builtin",
            isCustom = false
        }).ToArray();
    }

    private async Task<object> AddCustomModelAsync(JsonElement[] args)
    {
        if (args.Length >= 2 &&
            args[1].TryGetProperty("displayName", out var dn) &&
            args[1].TryGetProperty("actualModelId", out var am))
        {
            _config.ModelMappings.Add(new ModelMappingEntry
            {
                RequestModel = dn.GetString() ?? "",
                ActualModel = am.GetString() ?? ""
            });
            await PersistAndSyncAsync();
        }

        return new { success = true, models = GetEffectiveModels(args), error = (string?)null };
    }

    private async Task<object> RemoveCustomModelAsync(JsonElement[] args)
    {
        if (args.Length >= 2)
        {
            var name = args[1].GetString();
            _config.ModelMappings.RemoveAll(m =>
                string.Equals(m.RequestModel, name, StringComparison.OrdinalIgnoreCase));
            await PersistAndSyncAsync();
        }

        return new { success = true, models = GetEffectiveModels(args), error = (string?)null };
    }

    private async Task<object> ResetModelsAsync(JsonElement[] args)
    {
        _config.ModelMappings.Clear();
        DsdOpenAiCompat.EnsureDefaultMappings(_config);
        await PersistAndSyncAsync();
        return new { success = true, models = GetEffectiveModels(args), error = (string?)null };
    }

    private object[] GetAccounts(JsonElement[] args)
    {
        var includeCreds = args.Length > 0 && args[0].ValueKind == JsonValueKind.True;
        return ProviderAccountStore.Load()
            .Select(a => ApiManagementConsoleMapper.ToUiAccount(a, includeCreds))
            .Cast<object>()
            .ToArray();
    }

    private object? GetAccountById(JsonElement[] args)
    {
        var id = args.Length > 0 ? args[0].GetString() : null;
        if (string.IsNullOrWhiteSpace(id)) return null;
        var include = args.Length > 1 && args[1].ValueKind == JsonValueKind.True;

        var rec = ProviderAccountStore.FindById(id);
        return rec is null ? null : ApiManagementConsoleMapper.ToUiAccount(rec, include);
    }

    private async Task<object> AddAccountAsync(JsonElement[] args)
    {
        var body = args.Length > 0 ? args[0] : default;
        if (body.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("无效的账户数据");

        var providerId = body.TryGetProperty("providerId", out var pid) ? pid.GetString() ?? "" : "";
        var name = body.TryGetProperty("name", out var n) ? n.GetString() ?? "Account" : "Account";
        var creds = new Dictionary<string, string>(StringComparer.Ordinal);
        if (body.TryGetProperty("credentials", out var c) && c.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in c.EnumerateObject())
                creds[prop.Name] = prop.Value.GetString() ?? "";
        }

        var rec = ProviderAccountStore.Add(providerId, name, creds);
        await Task.CompletedTask;
        return ApiManagementConsoleMapper.ToUiAccount(rec, includeCredentials: false);
    }

    private object[] GetAccountsByProvider(JsonElement[] args)
    {
        var providerId = args.Length > 0 ? args[0].GetString() : null;
        if (string.IsNullOrWhiteSpace(providerId))
            return [];

        return ListAccountsForProvider(providerId, includeCredentials: false).ToArray();
    }

    private IEnumerable<object> ListAccountsForProvider(string providerId, bool includeCredentials) =>
        ProviderAccountStore.ByProvider(providerId)
            .Select(a => ApiManagementConsoleMapper.ToUiAccount(a, includeCredentials))
            .Cast<object>();

    private bool DeleteAccount(JsonElement[] args)
    {
        var id = args.Length > 0 ? args[0].GetString() : null;
        return !string.IsNullOrWhiteSpace(id) && ProviderAccountStore.Delete(id);
    }

    private static string? ResolveSessionAccountId() =>
        ProviderAccountStore.ByProvider("deepseek")
            .FirstOrDefault(a => a.Status == "active")?.Id;

    private async Task<object> ValidateAccountAsync(JsonElement[] args, CancellationToken ct)
    {
        var id = args.Length > 0 ? args[0].GetString() : null;
        if (string.IsNullOrWhiteSpace(id)) return false;

        var rec = ProviderAccountStore.FindById(id);
        if (rec is null)
            return false;

        var token = AccountCredentials.ResolveWebUserToken(rec, _config);
        if (string.IsNullOrWhiteSpace(token))
            return rec.Credentials.Values.Any(v => !string.IsNullOrWhiteSpace(v));

        var health = await _web.ProbeDsdApiHealthAsync(token, _localApi.BaseUrl, ct);
        return health?.CanChat == true;
    }

    private async Task<object> ValidateTokenAsync(JsonElement[] args, CancellationToken ct)
    {
        var token = ExtractTokenFromArgs(args);
        if (string.IsNullOrWhiteSpace(token))
        {
            return new
            {
                valid = false,
                error = "请填写 DeepSeek 用户 Token",
                userInfo = (object?)null
            };
        }

        var health = await _web.ProbeDsdApiHealthAsync(token, _localApi.BaseUrl, ct);
        var canChat = health?.CanChat == true;
        return new
        {
            valid = canChat,
            error = canChat ? null : health?.Error ?? "Token 无效",
            userInfo = canChat
                ? new { name = "DeepSeek", email = "" }
                : null
        };
    }

    private static string? ExtractTokenFromArgs(JsonElement[] args)
    {
        if (args.Length == 0)
            return null;
        var el = args[0];
        if (el.ValueKind == JsonValueKind.String)
            return el.GetString();
        if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("token", out var tok))
                return tok.GetString();
            if (el.TryGetProperty("credentials", out var creds)
                && creds.ValueKind == JsonValueKind.Object
                && creds.TryGetProperty("token", out var nested))
                return nested.GetString();
        }

        return null;
    }

    private object SessionConfig() => new
    {
        mode = string.Equals(_config.DsdApiSessionMode, "multi", StringComparison.OrdinalIgnoreCase)
            ? "multi"
            : "single",
        sessionTimeout = _config.DsdApiSessionTimeoutMinutes,
        maxMessagesPerSession = _config.DsdApiMaxMessagesPerSession,
        deleteAfterTimeout = true,
        maxSessionsPerAccount = 20
    };

    private async Task<object?> SessionUpdateConfig(JsonElement[] args)
    {
        if (args.Length > 0 && args[0].ValueKind == JsonValueKind.Object)
            await ApplyConfigPatchAsync(args[0]);
        return null;
    }

    private object[] ListSessions(bool activeOnly = false)
    {
        var list = _localApi.ListSessions();
        return list.Select(s => new
        {
            id = s.ClientSessionId,
            providerId = "deepseek",
            accountId = ResolveSessionAccountId(),
            providerSessionId = s.WebSessionId,
            sessionType = "chat",
            messages = Array.Empty<object>(),
            createdAt = s.LastUsedAt,
            lastActiveAt = s.LastUsedAt,
            status = "active",
            model = (string?)null
        }).ToArray();
    }

    private object? SessionById(JsonElement[] args)
    {
        var id = args.Length > 0 ? args[0].GetString() : null;
        if (string.IsNullOrEmpty(id)) return null;
        var hit = _localApi.ListSessions().FirstOrDefault(s => s.ClientSessionId == id);
        if (hit is null) return null;
        return new
        {
            id = hit.ClientSessionId,
            providerId = "deepseek",
            accountId = ResolveSessionAccountId(),
            providerSessionId = hit.WebSessionId,
            sessionType = "chat",
            messages = Array.Empty<object>(),
            createdAt = hit.LastUsedAt,
            lastActiveAt = hit.LastUsedAt,
            status = "active",
            model = (string?)null
        };
    }

    private object SessionDelete(JsonElement[] args)
    {
        var id = args.Length > 0 ? args[0].GetString() : null;
        return !string.IsNullOrEmpty(id) && _localApi.DeleteSession(id);
    }

    private object? SessionClearAll()
    {
        foreach (var s in _localApi.ListSessions())
            _localApi.DeleteSession(s.ClientSessionId);
        return null;
    }

    private object[] MapApiKeys() =>
        _config.LocalApiKeys.Select(k => new
        {
            id = k.Id,
            name = k.Name,
            key = k.Key,
            enabled = k.Enabled,
            createdAt = k.CreatedAt,
            lastUsedAt = k.LastUsedAt,
            usageCount = k.UsageCount,
            description = k.Description
        }).ToArray();

    private static object EmptyLogStats() => new { total = 0, info = 0, warn = 0, error = 0, debug = 0 };

    private static object EmptyRequestLogStats() => new
    {
        total = 0,
        success = 0,
        error = 0,
        todayTotal = 0,
        todaySuccess = 0,
        todayError = 0
    };

    private static object EmptyPersistentStatistics() => new
    {
        totalRequests = 0,
        successRequests = 0,
        failedRequests = 0,
        totalLatency = 0L,
        lastUpdated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        modelUsage = new Dictionary<string, int>(),
        providerUsage = new Dictionary<string, int>(),
        accountUsage = new Dictionary<string, int>(),
        dailyStats = new Dictionary<string, object>()
    };

    private static object EmptyDailyStatistics()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return new
        {
            date = today,
            totalRequests = 0,
            successRequests = 0,
            failedRequests = 0,
            totalLatency = 0L,
            modelUsage = new Dictionary<string, int>(),
            providerUsage = new Dictionary<string, int>()
        };
    }

    private object ManagementApiConfig() => new
    {
        enableManagementApi = _config.EnableExternalOpenAiApi,
        managementApiSecret = _config.EnableLocalApiKeyAuth ? DeepSeekDesktopApp.LocalApiKeyFallback : "",
        managementApiPort = InternalChatChannel.ResolveExternalApiPort(_config)
    };

    private async Task<object> ManagementApiUpdateAsync(JsonElement[] args, CancellationToken ct)
    {
        if (args.Length > 0 && args[0].ValueKind == JsonValueKind.Object)
            await ApplyConfigPatchAsync(args[0]);
        return ManagementApiConfig();
    }

    private object ContextManagementConfig() => new
    {
        enabled = true,
        strategies = new
        {
            slidingWindow = new { enabled = true, maxMessages = _config.DsdApiMaxMessagesPerSession },
            tokenLimit = new { enabled = false, maxTokens = 8000 },
            summary = new { enabled = false, keepRecentMessages = 6 }
        },
        executionOrder = new[] { "slidingWindow", "tokenLimit", "summary" }
    };

    private async Task<object> ContextManagementUpdateAsync(JsonElement[] args, CancellationToken ct)
    {
        if (args.Length > 0 && args[0].ValueKind == JsonValueKind.Object)
            await ApplyConfigPatchAsync(args[0]);
        return ContextManagementConfig();
    }

    private object? MinimizeWindow()
    {
        _owner()?.Dispatcher.Invoke(() => _owner()?.WindowState = WindowState.Minimized);
        return null;
    }

    private object? MaximizeWindow()
    {
        _owner()?.Dispatcher.Invoke(() =>
        {
            var w = _owner();
            if (w is null) return;
            w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        });
        return null;
    }

    private object? CloseWindow()
    {
        _owner()?.Dispatcher.Invoke(() => _owner()?.Close());
        return null;
    }

    private object? ShowWindow()
    {
        _owner()?.Dispatcher.Invoke(() =>
        {
            var w = _owner();
            if (w is null) return;
            w.Show();
            w.Activate();
        });
        return null;
    }

    private object? HideWindow()
    {
        _owner()?.Dispatcher.Invoke(() => _owner()?.Hide());
        return null;
    }

    private object? OpenExternal(JsonElement[] args)
    {
        var url = args.Length > 0 ? args[0].GetString() : null;
        if (string.IsNullOrWhiteSpace(url)) return null;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return null;
    }

    private Task<object> OAuthInAppLoginAsync(JsonElement[] args, CancellationToken ct) =>
        Task.FromResult<object>(new
        {
            success = false,
            providerId = "deepseek",
            providerType = "deepseek",
            error = "请使用「手动输入」粘贴网页版 userToken 添加账户；普通对话登录不会自动同步到 API 管理。"
        });

    private async Task<object> OAuthLoginWithTokenAsync(JsonElement[] args, CancellationToken ct)
    {
        if (args.Length == 0 || args[0].ValueKind != JsonValueKind.Object)
            return new { success = false, error = "invalid payload" };

        var payload = args[0];
        var token = payload.TryGetProperty("token", out var tok) && tok.ValueKind == JsonValueKind.String
            ? tok.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(token))
            return new { success = false, error = "缺少 token" };

        token = token.Trim();
        var name = payload.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
            ? nameEl.GetString() ?? "DeepSeek"
            : "DeepSeek";

        var health = await _web.ProbeDsdApiHealthAsync(token, _localApi.BaseUrl, ct);
        var canChat = health?.CanChat == true;
        if (!canChat)
        {
            return new
            {
                success = false,
                providerId = "deepseek",
                providerType = "deepseek",
                error = health?.Error ?? "Token 无效"
            };
        }

        var rec = ProviderAccountStore.Add(
            "deepseek",
            name,
            new Dictionary<string, string> { ["token"] = token });
        return new
        {
            success = true,
            providerId = "deepseek",
            providerType = "deepseek",
            account = ApiManagementConsoleMapper.ToUiAccount(rec, includeCredentials: false),
            error = (string?)null
        };
    }
}
