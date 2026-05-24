namespace DeepSeekBrowser.Services;

public static class WorkModeModes
{
    public static string NormalizeMode(string? mode) =>
        mode is "agent" or "plan" ? mode : "chat";
}

public sealed record WorkModeStatePayload(
    string Type,
    string Mode,
    string Surface,
    string Label,
    string Title,
    bool Highlight,
    bool IsAgentLike)
{
    public const string MessageType = "workModeState";

    public static WorkModeStatePayload For(string mode, bool agentSurfaceVisible)
    {
        var normalized = WorkModeModes.NormalizeMode(mode);
        var agentLike = normalized is "agent" or "plan";

        return new WorkModeStatePayload(
            Type: MessageType,
            Mode: normalized,
            Surface: agentSurfaceVisible ? "agent" : "chat",
            Label: agentSurfaceVisible ? "Agent" : "普通",
            Title: agentSurfaceVisible
                ? "当前为 Agent 模式，点击切换到普通对话"
                : "当前为普通对话，点击切换到 Agent",
            Highlight: agentSurfaceVisible,
            IsAgentLike: agentLike);
    }
}
