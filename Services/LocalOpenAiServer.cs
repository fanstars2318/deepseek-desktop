using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services;

public sealed class LocalOpenAiServer : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly WebInjectService _web;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private AppConfig _config = new();

    public LocalOpenAiServer(WebInjectService web) => _web = web;

    public string BaseUrl => $"http://127.0.0.1:{_config.LocalApiPort}/v1";

    public void UpdateConfig(AppConfig config) => _config = config;

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{_config.LocalApiPort}/");
        _listener.Start();
        _ = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { /* ignore */ }
        _listener?.Close();
        _listener = null;
        _cts?.Dispose();
        _cts = null;
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is { IsListening: true })
        {
            try
            {
                var ctx = await _listener.GetContextAsync().WaitAsync(ct);
                _ = Task.Run(() => HandleRequestAsync(ctx), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch when (ct.IsCancellationRequested) { break; }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "";
            if (ctx.Request.HttpMethod == "OPTIONS")
            {
                ctx.Response.StatusCode = 204;
                ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                ctx.Response.Headers.Add("Access-Control-Allow-Headers", "Authorization, Content-Type");
                ctx.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                ctx.Response.Close();
                return;
            }

            if (ctx.Request.HttpMethod == "GET" && path.Equals("/v1/models", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(ctx, 200, new
                {
                    @object = "list",
                    data = new object[]
                    {
                        new { id = "deepseek-chat", @object = "model" },
                        new { id = "deepseek-reasoner", @object = "model" },
                        new { id = "DeepSeek-V3.2", @object = "model" },
                        new { id = "DeepSeek-R1", @object = "model" },
                        new { id = "DeepSeek-Search", @object = "model" },
                        new { id = "deepseek-web", @object = "model" }
                    }
                });
                return;
            }

            if (ctx.Request.HttpMethod == "POST" &&
                path.Equals("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                var bodyText = await reader.ReadToEndAsync();
                var response = await HandleChatCompletionAsync(bodyText);
                await WriteJsonAsync(ctx, 200, response);
                return;
            }

            await WriteJsonAsync(ctx, 404, new { error = new { message = "Not found", type = "invalid_request_error" } });
        }
        catch (Exception ex)
        {
            await WriteJsonAsync(ctx, 500, new { error = new { message = ex.Message, type = "server_error" } });
        }
    }

    private async Task<object> HandleChatCompletionAsync(string bodyText)
    {
        using var doc = JsonDocument.Parse(bodyText);
        var root = doc.RootElement;
        var model = root.TryGetProperty("model", out var m) ? m.GetString() ?? "deepseek-chat" : "deepseek-chat";
        var stream = root.TryGetProperty("stream", out var s) && s.GetBoolean();
        if (stream)
            throw new NotSupportedException("流式输出暂未实现，请设置 stream=false");

        var thinking = root.TryGetProperty("ds_thinking", out var thinkEl) && thinkEl.ValueKind == JsonValueKind.True;
        if (!thinking)
            thinking = model.Contains("reasoner", StringComparison.OrdinalIgnoreCase)
                       || model.Contains("think", StringComparison.OrdinalIgnoreCase);

        var search = root.TryGetProperty("ds_search", out var searchEl) && searchEl.ValueKind == JsonValueKind.True;
        if (!search)
            search = model.Contains("search", StringComparison.OrdinalIgnoreCase);

        var prevRefIds = _web.AgentRefFileIds;
        if (root.TryGetProperty("ref_file_ids", out var refEl) && refEl.ValueKind == JsonValueKind.Array)
        {
            var ids = new List<string>();
            foreach (var item in refEl.EnumerateArray())
            {
                var id = item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText();
                if (!string.IsNullOrWhiteSpace(id))
                    ids.Add(id.Trim().Trim('"'));
            }
            _web.AgentRefFileIds = ids;
        }

        var messages = new List<ChatMessage>();
        if (root.TryGetProperty("messages", out var msgs) && msgs.ValueKind == JsonValueKind.Array)
        {
            foreach (var msg in msgs.EnumerateArray())
            {
                var role = msg.GetProperty("role").GetString() ?? "user";
                var chatMsg = new ChatMessage { Role = role };

                if (msg.TryGetProperty("content", out var c) && c.ValueKind != JsonValueKind.Null)
                    chatMsg.Content = c.ValueKind == JsonValueKind.String ? c.GetString() : c.GetRawText();

                if (msg.TryGetProperty("tool_call_id", out var tid))
                    chatMsg.ToolCallId = tid.GetString();

                if (msg.TryGetProperty("tool_calls", out var tcs) && tcs.ValueKind == JsonValueKind.Array)
                {
                    chatMsg.ToolCalls = new List<WebToolCall>();
                    foreach (var tc in tcs.EnumerateArray())
                    {
                        chatMsg.ToolCalls.Add(new WebToolCall
                        {
                            Id = tc.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N"),
                            Name = tc.GetProperty("function").GetProperty("name").GetString() ?? "",
                            Arguments = tc.GetProperty("function").GetProperty("arguments").GetString() ?? "{}"
                        });
                    }
                }

                messages.Add(chatMsg);
            }
        }

        if (string.IsNullOrWhiteSpace(_config.WebUserToken))
            throw new InvalidOperationException("请先在 DeepSeek 网页登录，本地 API 将自动使用网页会话，无需填写 API Key。");

        WebChatResult result;
        try
        {
            result = await _web.WebChatAsync(messages, model, thinking, search, CancellationToken.None);
        }
        finally
        {
            _web.AgentRefFileIds = prevRefIds;
        }

        return BuildChatResponse(result);
    }

    private static object BuildChatResponse(WebChatResult result)
    {
        object message;
        if (result.ToolCalls is { Count: > 0 })
        {
            message = new
            {
                role = "assistant",
                content = (string?)null,
                reasoning_content = result.ReasoningContent,
                tool_calls = result.ToolCalls.Select(tc => new
                {
                    id = tc.Id,
                    type = "function",
                    function = new { name = tc.Name, arguments = tc.Arguments }
                }).ToArray()
            };
            return new
            {
                id = "chatcmpl-local",
                @object = "chat.completion",
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                model = result.Model,
                choices = new[]
                {
                    new { index = 0, message, finish_reason = "tool_calls" }
                },
                usage = new { prompt_tokens = 0, completion_tokens = 0, total_tokens = 0 }
            };
        }

        message = new
        {
            role = "assistant",
            content = result.Content ?? "",
            reasoning_content = result.ReasoningContent
        };

        return new
        {
            id = "chatcmpl-local",
            @object = "chat.completion",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            model = result.Model,
            choices = new[]
            {
                new { index = 0, message, finish_reason = result.FinishReason ?? "stop" }
            },
            usage = new { prompt_tokens = 0, completion_tokens = 0, total_tokens = 0 }
        };
    }

    private static async Task WriteJsonAsync(HttpListenerContext ctx, int status, object payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    public void Dispose() => Stop();
}
