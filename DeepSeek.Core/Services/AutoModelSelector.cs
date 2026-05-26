using System.Text.RegularExpressions;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.ApiManagement;

namespace DeepSeekBrowser.Services;

/// <summary>
/// Cursor 式 Auto：先按用户偏好顺序选供应商，再按任务复杂度选该供应商下的模型。
/// </summary>
public static class AutoModelSelector
{
    public sealed record Request(
        string UserText,
        bool DeepThink,
        bool SmartSearch,
        string? Strategy = null,
        int RefFileCount = 0,
        int HistoryMessageCount = 0);

    public sealed record Selection(
        string ProviderId,
        string ProviderName,
        string ModelId,
        string Tier,
        string ReasonZh);

    private static readonly Regex CodeFence = new("```", RegexOptions.Compiled);
    private static readonly Regex ComplexHint = new(
        @"\b(refactor|architect|debug|implement|optimize|migrate|review|analyze|design|fix\s+bug|unit\s+test|integration)\b|" +
        @"(重构|架构|调试|实现|优化|迁移|审查|分析|设计|修复|单元测试|集成|多文件|并发|性能|安全漏洞)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Selection Select(AppConfig config, Request request)
    {
        var score = ScoreComplexity(request.UserText ?? "", request);
        var tier = AutoProviderPool.TierForScore(score, request.DeepThink, request.SmartSearch);
        var candidates = AutoProviderPool.BuildCandidates(config);
        var ready = candidates.Where(c => c.Ready && c.Models.Count > 0).OrderBy(c => c.PreferenceRank).ToList();

        if (ready.Count == 0)
            return FallbackDeepSeek(config, tier, "无可用供应商，回退 DeepSeek");

        foreach (var provider in ready)
        {
            var model = AutoProviderPool.PickModel(provider, tier);
            if (string.IsNullOrWhiteSpace(model)) continue;

            return new Selection(
                provider.Id,
                provider.DisplayName,
                model,
                tier.ToString().ToLowerInvariant(),
                $"{provider.DisplayName} · {TierReasonZh(tier, score, request)}");
        }

        var first = ready[0];
        return new Selection(
            first.Id,
            first.DisplayName,
            first.Models[0],
            tier.ToString().ToLowerInvariant(),
            $"{first.DisplayName} · 使用可用模型");
    }

    private static Selection FallbackDeepSeek(AppConfig config, AutoProviderPool.ModelTier tier, string reason)
    {
        var models = DsdOpenAiCompat.ListModelIds(config).ToList();
        var pseudo = new AutoProviderPool.ProviderCandidate(
            "deepseek",
            "DeepSeek",
            models,
            true,
            0);
        var model = AutoProviderPool.PickModel(pseudo, tier) ?? models.FirstOrDefault() ?? AgentModeHelper.AgentModel;
        return new Selection("deepseek", "DeepSeek", model, tier.ToString().ToLowerInvariant(), reason);
    }

    private static string TierReasonZh(AutoProviderPool.ModelTier tier, int score, Request request) =>
        tier switch
        {
            AutoProviderPool.ModelTier.SearchThink => "深度思考 + 联网",
            AutoProviderPool.ModelTier.Search => "联网搜索",
            AutoProviderPool.ModelTier.Reasoning => request.DeepThink ? "深度思考" : $"复杂任务（{score}）",
            AutoProviderPool.ModelTier.Fast => "轻量快速",
            AutoProviderPool.ModelTier.Premium => $"中高复杂度（{score}）",
            _ => "均衡默认"
        };

    internal static int ScoreComplexity(string text, Request request)
    {
        var score = 0;
        var len = text.Length;
        if (len > 40) score += 8;
        if (len > 200) score += 12;
        if (len > 600) score += 15;
        if (len > 1500) score += 10;

        var lines = text.Split('\n').Length;
        if (lines > 8) score += 8;
        if (lines > 25) score += 10;

        var fences = CodeFence.Matches(text).Count;
        if (fences >= 2) score += 18;
        else if (fences == 1) score += 10;

        if (ComplexHint.IsMatch(text)) score += 22;

        if (request.RefFileCount > 0) score += Math.Min(20, request.RefFileCount * 6);
        if (request.HistoryMessageCount > 6) score += 8;

        var strategy = (request.Strategy ?? "").ToLowerInvariant();
        if (strategy is "blueprint" or "orient" or "plan") score += 12;
        if (strategy is "software-factory" or "factory") score += 15;

        if (request.DeepThink) score += 25;
        if (request.SmartSearch) score += 12;

        return Math.Clamp(score, 0, 100);
    }
}
