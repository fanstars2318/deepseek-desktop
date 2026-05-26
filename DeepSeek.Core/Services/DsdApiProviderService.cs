using System.IO;
using System.Text;
using System.Text.Json;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.ApiManagement;

namespace DeepSeekBrowser.Services;

/// <summary>
/// 桌面端 DSD API 供应商快照：网页 User Token → 本地 OpenAI 兼容 API → 进程内 Harness。
/// 对齐 DSD API 文档、DeepSeek 官方 API 模型名与 deepseek-tui.com 配置方式。
/// </summary>
public static class DsdApiProviderService
{
    private static readonly object IntegrationWriteLock = new();
    private static readonly Mutex IntegrationFileMutex =
        new(false, DeepSeekDesktopApp.IntegrationMutexName);
    private static string? _lastIntegrationJson;
    private static long _lastIntegrationWriteTick;

    public sealed record ProviderSnapshot(
        string Id,
        string Name,
        string Type,
        string Description,
        bool Online,
        bool Enabled,
        string AuthType,
        int AccountOnline,
        int AccountTotal,
        int ModelCount,
        string DsdApiBaseUrl,
        string ApiKeyForClients,
        string ApiKeyMasked,
        string TuiRuntimeUrl,
        string TuiConfigPath,
        string IntegrationFilePath);

    public static ProviderSnapshot Build(AppConfig config, DsdApiHealth? health = null)
    {
        DsdOpenAiCompat.EnsureDefaultMappings(config);
        var baseUrl = InternalChatChannel.DesktopV1;
        var accountTotal = ProviderAccountStore.ByProvider("deepseek").Count;
        var accountOnline = AccountCredentials.CountActiveAccounts("deepseek", config);
        var online = accountOnline > 0 && (health?.CanChat ?? true);
        var apiKey = ResolveApiKeyForClients(config);
        return new ProviderSnapshot(
            Id: DsdEmbeddedApiStack.ProviderId,
            Name: "DeepSeek",
            Type: "builtin",
            Description: "DeepSeek 智能对话助手；Agent API 使用手动添加的账户（与普通对话登录独立）",
            Online: online,
            Enabled: online,
            AuthType: "user_token",
            AccountOnline: accountOnline,
            AccountTotal: Math.Max(accountTotal, accountOnline),
            ModelCount: DsdOpenAiCompat.ListModels(config).Count,
            DsdApiBaseUrl: baseUrl,
            ApiKeyForClients: apiKey,
            ApiKeyMasked: MaskCredential(apiKey),
            TuiRuntimeUrl: "native://harness",
            TuiConfigPath: AgentDesktopConfigSync.ConfigPath,
            IntegrationFilePath: IntegrationFilePath);
    }

    /// <summary>供 DeepSeek-TUI / OpenAI 兼容客户端使用的 Bearer Token（网页 User Token 或本地 Key）。</summary>
    public static string ResolveApiKeyForClients(AppConfig config)
    {
        var fromStore = AccountCredentials.ResolveFirstProviderWebToken("deepseek", config);
        if (!string.IsNullOrWhiteSpace(fromStore))
            return fromStore.Trim();

        if (config.EnableLocalApiKeyAuth)
        {
            var key = config.LocalApiKeys.FirstOrDefault(k => k.Enabled);
            if (key is not null && !string.IsNullOrWhiteSpace(key.Key))
                return key.Key;
        }

        return DeepSeekDesktopApp.LocalApiKeyFallback;
    }

    public static string IntegrationFilePath => DeepSeekDesktopApp.IntegrationFilePath;

    public static void WriteIntegrationFile(AppConfig config, DsdApiHealth? health = null)
    {
        try
        {
            var snap = Build(config, health);
            var externalBaseUrl = config.EnableExternalOpenAiApi
                ? InternalChatChannel.GetExternalApiBaseUrl(config)
                : null;
            var payload = new
            {
                updated_at = DateTimeOffset.UtcNow.ToString("o"),
                provider = new
                {
                    snap.Id,
                    snap.Name,
                    snap.Type,
                    snap.Online,
                    snap.AuthType,
                    snap.ModelCount
                },
                dsd_api = new
                {
                    channel = snap.DsdApiBaseUrl,
                    base_url = snap.DsdApiBaseUrl,
                    external_base_url = externalBaseUrl,
                    api_key = snap.ApiKeyForClients,
                    auth_header = $"Bearer {snap.ApiKeyForClients}",
                    docs = new[]
                    {
                        DsdEmbeddedApiStack.DocsUrl,
                        "https://api-docs.deepseek.com/zh-cn/"
                    }
                },
                deepseek_tui = new
                {
                    runtime_url = snap.TuiRuntimeUrl,
                    serve_http = $"{snap.TuiRuntimeUrl} (deepseek serve --http)",
                    config_path = snap.TuiConfigPath,
                    env = new
                    {
                        DEEPSEEK_BASE_URL = InternalChatChannel.ResolveTuiLlmBaseUrl(config),
                        DEEPSEEK_API_KEY = snap.ApiKeyForClients,
                        DEEPSEEK_MODEL = "deepseek-v4-pro"
                    },
                    docs = "https://deepseek-tui.com/zh"
                }
            };

            var json = JsonSerializer.Serialize(payload,
                new JsonSerializerOptions { WriteIndented = true });
            WriteIntegrationFileText(snap.IntegrationFilePath, json);
        }
        catch (IOException)
        {
            // 多实例/杀毒扫描同时占用时跳过，避免启动弹错；下次刷新会重试
        }
    }

    private static void WriteIntegrationFileText(string path, string json)
    {
        lock (IntegrationWriteLock)
        {
            var now = Environment.TickCount64;
            if (json == _lastIntegrationJson && now - _lastIntegrationWriteTick < 400)
                return;

            var acquired = false;
            try
            {
                try
                {
                    acquired = IntegrationFileMutex.WaitOne(3000);
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }

                WriteAllTextAtomic(path, json);
                _lastIntegrationJson = json;
                _lastIntegrationWriteTick = now;
            }
            finally
            {
                if (acquired)
                {
                    try { IntegrationFileMutex.ReleaseMutex(); }
                    catch { /* ignore */ }
                }
            }
        }
    }

    private static void WriteAllTextAtomic(string path, string content)
    {
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        const int maxAttempts = 8;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                File.WriteAllText(tmp, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (File.Exists(path))
                    File.Replace(tmp, path, destinationBackupFileName: null);
                else
                    File.Move(tmp, path);
                return;
            }
            catch (IOException) when (attempt < maxAttempts - 1)
            {
                Thread.Sleep(30 * (attempt + 1));
            }
            finally
            {
                if (File.Exists(tmp))
                {
                    try { File.Delete(tmp); }
                    catch { /* ignore */ }
                }
            }
        }
    }

    public static object ToApiPayload(ProviderSnapshot snap) => new
    {
        providers = new[]
        {
            new
            {
                id = snap.Id,
                name = snap.Name,
                type = snap.Type,
                description = snap.Description,
                online = snap.Online,
                enabled = snap.Enabled,
                auth_type = snap.AuthType,
                account_online = snap.AccountOnline,
                account_total = snap.AccountTotal,
                model_count = snap.ModelCount,
                base_url = snap.DsdApiBaseUrl,
                api_key_masked = snap.ApiKeyMasked
            }
        },
        stats = new
        {
            total = 1,
            online = snap.Online ? 1 : 0,
            enabled = snap.Enabled ? 1 : 0,
            builtin = 1,
            custom = 0
        },
        deepseek_tui = new
        {
            runtime_url = snap.TuiRuntimeUrl,
            config_path = snap.TuiConfigPath,
            integration_file = snap.IntegrationFilePath,
            llm_channel = snap.DsdApiBaseUrl,
                    api_key_hint = "使用 API 管理中配置的 DeepSeek 账户 Token；与普通对话网页登录独立"
        }
    };

    public static string MaskCredential(string value)
    {
        if (string.IsNullOrEmpty(value)) return "—";
        if (value.Length <= 12) return value[..3] + "…";
        return value[..8] + "…" + value[^4..];
    }
}
