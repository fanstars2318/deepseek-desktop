namespace DeepSeekBrowser.Services.Harness;

public enum HarnessWorkflow
{
    /// <summary>Explore → Blueprint 两阶段工作流（原 Plan 模式）。</summary>
    Blueprint,

    /// <summary>单阶段 Execute（原 React 模式）。</summary>
    Execute,

    /// <summary>多智能体 SOP（MetaGPT-style 梦之队）。</summary>
    Team,

    /// <summary>声明式 Graph 工作流（LangGraph-style）。</summary>
    Graph,

    /// <summary>并行 Explore 扇出（AutoGen 群聊式调研）。</summary>
    ParallelExplore,

    /// <summary>双 Agent 辩论（CAMEL 角色扮演）。</summary>
    Debate,

    /// <summary>软件工厂：PM → Architect → Engineer → QA + 交付物。</summary>
    SoftwareFactory
}

public sealed class HarnessStrategyProfile
{
    public required HarnessWorkflow Workflow { get; init; }
    public required HarnessPhase InitialPhase { get; init; }
}
