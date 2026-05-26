using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.Harness.Sandbox;

namespace DeepSeekBrowser.Services.Harness;

/// <summary>image_analyze — vision model analysis for workspace images (CodeWhale-style).</summary>
public static class HarnessImageAnalyzeTool
{
    public static async Task<HarnessToolExecuteResult> RunAsync(
        JsonElement args,
        SandboxPathResolver paths,
        AppConfig config,
        CancellationToken ct)
    {
        var path = args.TryGetProperty("file_path", out var fp) && fp.ValueKind == JsonValueKind.String
            ? fp.GetString()
            : args.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString()
                : null;
        if (string.IsNullOrWhiteSpace(path))
            return HarnessToolExecuteResult.FromOutput("ERROR: image_analyze 需要 file_path");

        var prompt = args.TryGetProperty("prompt", out var pr) && pr.ValueKind == JsonValueKind.String
            ? pr.GetString() ?? "Describe this image in detail."
            : "Describe this image in detail.";

        string full;
        try
        {
            full = paths.ResolveRead(path);
        }
        catch (Exception ex)
        {
            return HarnessToolExecuteResult.FromOutput("ERROR: " + ex.Message);
        }

        if (!File.Exists(full))
            return HarnessToolExecuteResult.FromOutput("ERROR: 文件不存在: " + path);

        var ext = Path.GetExtension(full).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp"))
            return HarnessToolExecuteResult.FromOutput("ERROR: 不支持的图片格式: " + ext);

        var apiKey = ResolveVisionApiKey(config);
        if (string.IsNullOrWhiteSpace(apiKey))
            return HarnessToolExecuteResult.FromOutput("ERROR: 请在设置中配置 Vision API Key（或 Agent API Key）");

        var model = string.IsNullOrWhiteSpace(config.AgentVisionModel)
            ? "gpt-4o-mini"
            : config.AgentVisionModel.Trim();
        var baseUrl = (string.IsNullOrWhiteSpace(config.AgentVisionApiBaseUrl)
            ? AgentChatClientFactory.ResolveBaseUrl(config)
            : config.AgentVisionApiBaseUrl).Trim().TrimEnd('/');

        var mime = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/png"
        };

        var bytes = await File.ReadAllBytesAsync(full, ct);
        var dataUrl = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        var body = new
        {
            model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new { type = "image_url", image_url = new { url = dataUrl } }
                    }
                }
            },
            max_tokens = 4096
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = await http.SendAsync(req, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            return HarnessToolExecuteResult.FromOutput($"ERROR: Vision API ({(int)resp.StatusCode}): {Truncate(json, 400)}");

        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        return HarnessToolExecuteResult.FromOutput(
            $"Image analysis ({paths.ToVirtual(full)}):\n{content.Trim()}");
    }

    private static string ResolveVisionApiKey(AppConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.AgentVisionApiKey))
            return config.AgentVisionApiKey.Trim();
        return AgentChatClientFactory.ResolveApiKey(config);
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
