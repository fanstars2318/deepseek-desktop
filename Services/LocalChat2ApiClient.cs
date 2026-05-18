using System.Net.Http;
using System.Text;
using System.Text.Json;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.QwenCode;

namespace DeepSeekBrowser.Services;

/// <summary>
/// Agent 推理走本地 Chat2API（127.0.0.1:5111），与独立 Chat2API 进程的 OpenAI 兼容路径一致。
/// </summary>
public sealed class LocalChat2ApiClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(20)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly LocalOpenAiServer _server;
    private readonly WebInjectService _web;

    public LocalChat2ApiClient(LocalOpenAiServer server, WebInjectService web)
    {
        _server = server;
        _web = web;
    }

    public string BaseUrl => _server.BaseUrl;

    public Task<WebChatResult> ChatAsync(
        IReadOnlyList<ChatMessage> messages,
        AppConfig config,
        bool thinking,
        bool search,
        CancellationToken ct,
        Action<string>? onLog = null) =>
        AdaptiveOutputTokenEscalation.CompleteAsync(
            messages,
            config,
            (msgs, innerCt) => ChatOnceAsync(msgs, config, thinking, search, innerCt),
            onLog,
            ct);

    private async Task<WebChatResult> ChatOnceAsync(
        IReadOnlyList<ChatMessage> messages,
        AppConfig config,
        bool thinking,
        bool search,
        CancellationToken ct)
    {
        var refIds = _web.AgentRefFileIds;
        var url = BaseUrl.TrimEnd('/') + "/chat/completions";
        var body = new Dictionary<string, object?>
        {
            ["model"] = MapModel(config.Model, thinking, search),
            ["stream"] = false,
            ["messages"] = BuildOpenAiMessages(messages),
            ["ds_thinking"] = thinking,
            ["ds_search"] = search,
            ["ds_model_type"] = "expert"
        };
        if (refIds is { Count: > 0 })
            body["ref_file_ids"] = refIds.ToArray();

        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"本地 Chat2API 失败 ({(int)resp.StatusCode}): {Truncate(text, 400)}");

        return ParseCompletionResponse(text, config.Model);
    }

    private static string MapModel(string model, bool thinking, bool search)
    {
        if (search) return "DeepSeek-Search";
        if (thinking) return "deepseek-reasoner";
        return string.IsNullOrWhiteSpace(model) ? "deepseek-chat" : model;
    }

    private static List<object> BuildOpenAiMessages(IReadOnlyList<ChatMessage> messages)
    {
        var list = new List<object>();
        foreach (var m in messages)
        {
            if (m.ToolCalls is { Count: > 0 })
            {
                list.Add(new
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
                list.Add(new { role = m.Role, content = m.Content, tool_call_id = m.ToolCallId });
            }
            else
            {
                list.Add(new { role = m.Role, content = m.Content });
            }
        }
        return list;
    }

    private static WebChatResult ParseCompletionResponse(string json, string fallbackModel)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var err))
        {
            var msg = err.TryGetProperty("message", out var m) ? m.GetString() : err.GetRawText();
            throw new InvalidOperationException(msg ?? "Chat2API 返回错误");
        }

        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            throw new InvalidOperationException("Chat2API 响应无 choices");

        var message = choices[0].GetProperty("message");
        var model = root.TryGetProperty("model", out var modelEl) ? modelEl.GetString() : fallbackModel;

        List<WebToolCall>? toolCalls = null;
        if (message.TryGetProperty("tool_calls", out var tcEl) && tcEl.ValueKind == JsonValueKind.Array)
        {
            toolCalls = new List<WebToolCall>();
            foreach (var tc in tcEl.EnumerateArray())
            {
                toolCalls.Add(new WebToolCall
                {
                    Id = tc.TryGetProperty("id", out var id) ? id.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N"),
                    Name = tc.GetProperty("function").GetProperty("name").GetString() ?? "",
                    Arguments = tc.GetProperty("function").TryGetProperty("arguments", out var args)
                        ? args.ValueKind == JsonValueKind.String ? args.GetString() ?? "{}" : args.GetRawText()
                        : "{}"
                });
            }
        }

        string? content = null;
        if (message.TryGetProperty("content", out var c) && c.ValueKind != JsonValueKind.Null)
            content = c.ValueKind == JsonValueKind.String ? c.GetString() : c.GetRawText();

        string? reasoning = null;
        if (message.TryGetProperty("reasoning_content", out var r) && r.ValueKind != JsonValueKind.Null)
            reasoning = r.ValueKind == JsonValueKind.String ? r.GetString() : r.GetRawText();

        var finishReason = choices[0].TryGetProperty("finish_reason", out var fr)
            ? fr.GetString()
            : null;
        var likely = AdaptiveOutputTokenEscalation.DetectHeuristicTruncation(content)
                     || string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase);

        return new WebChatResult
        {
            Content = content,
            ReasoningContent = reasoning,
            ToolCalls = toolCalls,
            Model = model ?? fallbackModel,
            FinishReason = finishReason ?? (likely ? "length" : "stop"),
            IsLikelyTruncated = likely
        };
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
