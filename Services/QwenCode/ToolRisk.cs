namespace DeepSeekBrowser.Services.QwenCode;

/// <summary>Qwen Code 工具风险等级（对应 Core 包审批策略）。</summary>
public enum ToolRisk
{
    ReadOnly,
    Write,
    Execute
}
