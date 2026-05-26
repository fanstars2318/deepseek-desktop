using DeepSeekBrowser.Services;

namespace DeepSeek.Core.Tests;

public sealed class WorkModeStatePayloadTests
{
    [Theory]
    [InlineData("chat", false, "chat", "普通", false)]
    [InlineData("agent", true, "agent", "Agent", true)]
    [InlineData("plan", true, "plan", "Agent", true)]
    public void For_maps_surface_and_mode(
        string mode,
        bool agentVisible,
        string expectedMode,
        string expectedLabel,
        bool expectedHighlight)
    {
        var state = WorkModeStatePayload.For(mode, agentVisible);
        Assert.Equal(WorkModeStatePayload.MessageType, state.Type);
        Assert.Equal(expectedMode, state.Mode);
        Assert.Equal(agentVisible ? "agent" : "chat", state.Surface);
        Assert.Equal(expectedLabel, state.Label);
        Assert.Equal(expectedHighlight, state.Highlight);
        Assert.Equal(expectedMode is "agent" or "plan", state.IsAgentLike);
    }

    [Theory]
    [InlineData(true, "chat")]
    [InlineData(false, "agent")]
    public void ToggleTargetMode_flips_by_visible_surface(bool agentVisible, string expected) =>
        Assert.Equal(expected, WorkModeModes.ToggleTargetMode(agentVisible));

    [Fact]
    public void For_includes_revision_when_provided()
    {
        var state = WorkModeStatePayload.For("agent", true, revision: 42);
        Assert.Equal(42, state.Revision);
    }
}
