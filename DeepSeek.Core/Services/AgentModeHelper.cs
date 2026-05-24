using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services;

public static class AgentModeHelper
{
    /// <summary>Agent 专用页默认：专家模型 + 深度思考（不展示开关）。</summary>
    public static void ApplyAgentDefaults(AppConfig config)
    {
        config.Model = "deepseek-reasoner";
        if (config.MaxAgentSteps < 25)
            config.MaxAgentSteps = 25;
    }

    public static void ApplyChatMode(AppConfig config, string mode, bool deepThink)
    {
        switch (mode)
        {
            case "专家":
                config.Model = "deepseek-reasoner";
                config.MaxAgentSteps = 30;
                break;
            case "识图":
                config.Model = "deepseek-chat";
                config.MaxAgentSteps = 20;
                break;
            default:
                config.Model = "deepseek-chat";
                config.MaxAgentSteps = 15;
                break;
        }

        if (deepThink)
            config.Model = "deepseek-reasoner";
    }
}
