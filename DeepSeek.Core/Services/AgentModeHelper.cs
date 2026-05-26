using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services;

public static class AgentModeHelper
{
    public const string AgentModel = "deepseek-v4-pro";

    /// <summary>Agent 专用页默认模型与步数；深度思考/联网由配置或 UI 传入，不由客户端预判消息类型。</summary>
    public static void ApplyAgentDefaults(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.AgentManualModel))
            config.AgentManualModel = AgentModel;
        if (string.IsNullOrWhiteSpace(config.AgentManualProviderId))
            config.AgentManualProviderId = config.AgentDefaultProviderId;
        if (!config.AgentModelAuto)
        {
            config.AgentDefaultProviderId = config.AgentManualProviderId.Trim();
            config.Model = config.AgentManualModel;
        }
        if (config.MaxAgentSteps < 25)
            config.MaxAgentSteps = 25;
    }

    public static AutoModelSelector.Selection? ApplyChatMode(
        AppConfig config,
        string mode,
        bool deepThink,
        bool smartSearch,
        string? taskText = null,
        string? strategy = null,
        int refFileCount = 0,
        int historyMessageCount = 0)
    {
        config.AgentDeepThinking = deepThink;
        config.AgentWebSearch = smartSearch;

        AutoModelSelector.Selection? autoSel = null;
        if (config.AgentModelAuto)
        {
            autoSel = AutoModelSelector.Select(config, new AutoModelSelector.Request(
                taskText ?? "",
                deepThink,
                smartSearch,
                strategy ?? config.DefaultAgentStrategy,
                refFileCount,
                historyMessageCount));
            config.AgentDefaultProviderId = autoSel.ProviderId;
            config.Model = autoSel.ModelId;
        }
        else if (!string.IsNullOrWhiteSpace(config.AgentManualModel))
        {
            config.AgentDefaultProviderId = string.IsNullOrWhiteSpace(config.AgentManualProviderId)
                ? config.AgentDefaultProviderId
                : config.AgentManualProviderId.Trim();
            config.Model = config.AgentManualModel.Trim();
        }
        else
        {
            config.Model = AgentModel;
        }

        switch (mode)
        {
            case "专家":
                config.MaxAgentSteps = 30;
                break;
            case "识图":
                config.MaxAgentSteps = 20;
                break;
            default:
                config.MaxAgentSteps = 15;
                break;
        }

        return autoSel;
    }
}
