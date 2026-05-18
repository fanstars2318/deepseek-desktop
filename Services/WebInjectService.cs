using System.IO;
using System.Text.Json;
using DeepSeekBrowser.Services.QwenCode;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DeepSeekBrowser.Services;

public sealed class WebInjectService
{
    private readonly WebView2 _webView;
    private string? _bridgeScript;
    private string? _overlayScript;
    private string? _overlayCss;

    public event EventHandler<JsonElement>? MessageReceived;

    /// <summary>当前 Agent 任务附带的网页端文件 ID（上传后填入 ref_file_ids）。</summary>
    public IReadOnlyList<string> AgentRefFileIds { get; set; } = Array.Empty<string>();

    public WebInjectService(WebView2 webView) => _webView = webView;

    public async Task AttachAsync(CoreWebView2 core)
    {
        LoadAssets();

        var injectDir = Path.Combine(AppContext.BaseDirectory, "Assets", "inject");
        var agentDir = Path.Combine(AppContext.BaseDirectory, "Assets", "agent");
        core.SetVirtualHostNameToFolderMapping(
            "ds-inject.local",
            injectDir,
            CoreWebView2HostResourceAccessKind.Allow);
        if (Directory.Exists(agentDir))
        {
            core.SetVirtualHostNameToFolderMapping(
                "ds-agent.local",
                agentDir,
                CoreWebView2HostResourceAccessKind.Allow);
        }

        await core.AddScriptToExecuteOnDocumentCreatedAsync(_bridgeScript!);
        if (!string.IsNullOrEmpty(_overlayCss))
        {
            var cssEsc = JsonSerializer.Serialize(_overlayCss);
            await core.AddScriptToExecuteOnDocumentCreatedAsync(
                "(function(){try{"
                + "if(/^ds-agent\\.local$/i.test(location.hostname))return;"
                + "var p=document.head||document.documentElement;"
                + "if(!p)return;"
                + $"var s=document.createElement('style');s.textContent={cssEsc};"
                + "p.appendChild(s);"
                + "}catch(e){}})();");
        }

        var overlayGuarded =
            "if(!/^ds-agent\\.local$/i.test(location.hostname)){" + _overlayScript + "}";
        await core.AddScriptToExecuteOnDocumentCreatedAsync(overlayGuarded);
        core.WebMessageReceived += OnWebMessageReceived;
    }

    public bool IsAgentHostPage =>
        AppNavigation.IsAgentPage(_webView.CoreWebView2?.Source);

    public async Task TriggerInjectAsync(bool forceReset = false)
    {
        if (_webView.CoreWebView2 is null || IsAgentHostPage) return;
        var flag = forceReset ? "true" : "false";
        await _webView.CoreWebView2.ExecuteScriptAsync(
            "(function(){"
            + "if(window.__dsNativeTryInject)window.__dsNativeTryInject();"
            + "if(window.__dsNativeOnRouteChange)window.__dsNativeOnRouteChange(" + flag + ");"
            + "})();");
    }

    public async Task BurstInjectAsync(CancellationToken ct = default, bool forceReset = false)
    {
        await TriggerInjectAsync(forceReset);
        foreach (var delay in new[] { 400, 1000, 2200, 4000 })
        {
            await Task.Delay(delay, ct);
            await TriggerInjectAsync(false);
        }
    }

    public async Task ReinjectAsync() => await BurstInjectAsync();

    private static readonly Lazy<(string Bridge, string Overlay, string Css)> InjectAssets = new(() =>
    {
        var baseDir = Path.Combine(AppContext.BaseDirectory, "Assets", "inject");
        var themePath = Path.Combine(baseDir, "ds-theme.css");
        var overlayCss = File.ReadAllText(Path.Combine(baseDir, "overlay.css"));
        if (File.Exists(themePath))
            overlayCss = File.ReadAllText(themePath) + "\n" + overlayCss;
        return (
            File.ReadAllText(Path.Combine(baseDir, "bridge.js")),
            File.ReadAllText(Path.Combine(baseDir, "overlay.js")),
            overlayCss);
    });

    private void LoadAssets()
    {
        var assets = InjectAssets.Value;
        _bridgeScript = assets.Bridge;
        _overlayScript = assets.Overlay;
        _overlayCss = assets.Css;
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.TryGetWebMessageAsString();
            if (string.IsNullOrWhiteSpace(json)) return;
            using var doc = JsonDocument.Parse(json);
            MessageReceived?.Invoke(this, doc.RootElement.Clone());
        }
        catch
        {
            // ignore malformed messages
        }
    }

    public async Task PostToPageAsync(object message)
    {
        if (_webView.CoreWebView2 is null) return;
        var json = JsonSerializer.Serialize(message);
        await _webView.CoreWebView2.ExecuteScriptAsync(
            "(function(m){"
            + "try{"
            + "if(typeof window.dsDesktopOnMessage==='function'){window.dsDesktopOnMessage(m);return;}"
            + "window.__dsPendingNativeMessages=window.__dsPendingNativeMessages||[];"
            + "window.__dsPendingNativeMessages.push(m);"
            + "}catch(e){console.warn('[DeepSeek Edge] native msg',e);}"
            + "})(" + json + ");");
    }

    public async Task PushAgentAuthHintAsync(bool loggedIn)
    {
        if (_webView.CoreWebView2 is null || !IsAgentHostPage) return;
        var lit = loggedIn ? "true" : "false";
        await _webView.CoreWebView2.ExecuteScriptAsync(
            "window.__dsLoggedIn=" + lit + ";"
            + "if(typeof window.dsAgentApplyAuth==='function')window.dsAgentApplyAuth(" + lit + ");");
    }

    private static JsonElement ParseBridgeEnvelope(string raw)
    {
        using var outer = JsonDocument.Parse(raw);
        var root = outer.RootElement;

        if (root.ValueKind == JsonValueKind.String)
        {
            var inner = root.GetString();
            if (string.IsNullOrWhiteSpace(inner))
                throw new InvalidOperationException("网页桥接无响应");
            using var innerDoc = JsonDocument.Parse(inner);
            return innerDoc.RootElement.Clone();
        }

        return root.Clone();
    }

    private static bool LooksLikeChatResult(JsonElement el) =>
        el.ValueKind == JsonValueKind.Object &&
        (el.TryGetProperty("content", out _) ||
         el.TryGetProperty("tool_calls", out _) ||
         el.TryGetProperty("reasoning_content", out _));

    private static bool IsBridgeOk(JsonElement envelope)
    {
        if (!envelope.TryGetProperty("ok", out var okEl)) return false;
        return okEl.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => string.Equals(okEl.GetString(), "true", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static string ExtractBridgeData(string raw)
    {
        var envelope = ParseBridgeEnvelope(raw);
        return ExtractBridgePayload(envelope, raw, 0);
    }

    private static string ExtractBridgePayload(JsonElement envelope, string rawHint, int depth)
    {
        if (depth > 4)
            throw new InvalidOperationException("网页桥接响应嵌套过深");

        if (LooksLikeChatResult(envelope))
            return envelope.GetRawText();

        if (envelope.TryGetProperty("data", out var dataEl))
        {
            if (LooksLikeChatResult(dataEl))
                return dataEl.GetRawText();

            if (dataEl.ValueKind == JsonValueKind.String)
            {
                var inner = dataEl.GetString();
                if (string.IsNullOrWhiteSpace(inner))
                    throw new InvalidOperationException("网页桥接 data 为空");

                if (inner.TrimStart().StartsWith('{') || inner.TrimStart().StartsWith('['))
                {
                    using var innerDoc = JsonDocument.Parse(inner);
                    return ExtractBridgePayload(innerDoc.RootElement, rawHint, depth + 1);
                }

                return JsonSerializer.Serialize(inner);
            }

            if (dataEl.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                return dataEl.GetRawText();
        }

        if (IsBridgeOk(envelope) && envelope.TryGetProperty("data", out var okData))
            return ExtractBridgePayload(okData, rawHint, depth + 1);

        if (!IsBridgeOk(envelope))
        {
            var err = envelope.TryGetProperty("error", out var errEl) ? JsonElementToText(errEl) : null;
            if (string.IsNullOrWhiteSpace(err) && envelope.TryGetProperty("msg", out var msgEl))
                err = JsonElementToText(msgEl);
            if (string.IsNullOrWhiteSpace(err))
            {
                var snippet = rawHint.Length > 280 ? rawHint[..280] + "…" : rawHint;
                err = "网页桥接失败: " + snippet;
            }

            throw new InvalidOperationException(err ?? "网页桥接失败");
        }

        throw new InvalidOperationException("网页桥接无 data 字段");
    }

    private static string? JsonElementToText(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => el.GetRawText()
        };

    public async Task<string?> ExecuteBridgeAsync(string expression, CancellationToken ct = default)
    {
        if (_webView.CoreWebView2 is null) return null;
        var wrapped =
            "(async function(){ try { return JSON.stringify({ ok: true, data: await (" + expression + ") }); } catch(e) { return JSON.stringify({ ok: false, error: String(e.message || e) }); } })()";
        var raw = await _webView.CoreWebView2.ExecuteScriptAsync(wrapped);
        if (string.IsNullOrWhiteSpace(raw) || raw == "null") return null;
        return ExtractBridgeData(raw);
    }

    public Task<string?> GetUserTokenAsync() =>
        ExecuteBridgeAsync("window.dsDesktopBridge && window.dsDesktopBridge.getToken()");

    public async Task<WebChatResult> WebChatAsync(
        IReadOnlyList<ChatMessage> messages,
        string model,
        bool thinking,
        bool search,
        CancellationToken ct)
    {
        var payload = new List<object>();
        foreach (var m in messages)
        {
            if (m.ToolCalls is { Count: > 0 })
            {
                payload.Add(new
                {
                    role = m.Role,
                    content = (string?)null,
                    tool_calls = m.ToolCalls.Select(tc => new
                    {
                        id = tc.Id,
                        type = "function",
                        function = new { name = tc.Name, arguments = tc.Arguments }
                    }).ToArray()
                });
            }
            else if (m.Role == "tool")
            {
                payload.Add(new { role = m.Role, content = m.Content, tool_call_id = m.ToolCallId });
            }
            else
            {
                payload.Add(new { role = m.Role, content = m.Content });
            }
        }

        var msgJson = JsonSerializer.Serialize(payload);
        var optsJson = JsonSerializer.Serialize(new
        {
            thinking,
            search,
            modelType = "expert",
            refFileIds = AgentRefFileIds
        });
        var expr = $"window.dsDesktopBridge.webChatCompletion({msgJson}, {JsonSerializer.Serialize(model)}, {optsJson})";
        var raw = await ExecuteBridgeAsync(expr, ct)
                  ?? throw new InvalidOperationException("网页桥接无响应");

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        List<WebToolCall>? toolCalls = null;
        if (root.TryGetProperty("tool_calls", out var tcEl) && tcEl.ValueKind == JsonValueKind.Array)
        {
            toolCalls = new List<WebToolCall>();
            foreach (var tc in tcEl.EnumerateArray())
            {
                toolCalls.Add(new WebToolCall
                {
                    Id = tc.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N"),
                    Name = tc.GetProperty("function").GetProperty("name").GetString() ?? "",
                    Arguments = JsonElementToText(tc.GetProperty("function").GetProperty("arguments")) ?? "{}"
                });
            }
        }

        var content = root.TryGetProperty("content", out var c) && c.ValueKind != JsonValueKind.Null
            ? JsonElementToText(c)
            : null;
        var finishReason = root.TryGetProperty("finish_reason", out var fr)
            ? JsonElementToText(fr)
            : null;
        var likelyFromBridge = root.TryGetProperty("is_likely_truncated", out var lt) && lt.ValueKind == JsonValueKind.True;
        var likely = likelyFromBridge || AdaptiveOutputTokenEscalation.DetectHeuristicTruncation(content);

        return new WebChatResult
        {
            Content = content,
            ReasoningContent = root.TryGetProperty("reasoning_content", out var r)
                ? JsonElementToText(r)
                : null,
            ToolCalls = toolCalls,
            Model = root.TryGetProperty("model", out var modelEl)
                ? JsonElementToText(modelEl) ?? model
                : model,
            FinishReason = finishReason ?? (likely ? "length" : "stop"),
            IsLikelyTruncated = likely
        };
    }
}
