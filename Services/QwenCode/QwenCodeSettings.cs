namespace DeepSeekBrowser.Services.QwenCode;

/// <summary>对应 Qwen Code 用户配置（~/.qwen/settings.json）的子集，保存在 DeepSeekEdge 目录。</summary>
public sealed class QwenCodeSettings
{
    public bool EnableBuiltinTools { get; set; } = true;
    public bool EnableWebFetch { get; set; } = true;
    public bool AllowShell { get; set; } = true;
    public string ApprovalMode { get; set; } = "smart";
    public string WorkspaceRoot { get; set; } = "";
    public string DefaultStrategy { get; set; } = "react";
    public int MaxAgentSteps { get; set; } = 25;

    public bool EnableAdaptiveOutputEscalation { get; set; } = true;
}
