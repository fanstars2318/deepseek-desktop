using DeepSeekBrowser.Services;

namespace DeepSeek.Core.Tests;

public sealed class WorkModeModesTests
{
    [Theory]
    [InlineData(null, "chat")]
    [InlineData("", "chat")]
    [InlineData("chat", "chat")]
    [InlineData("agent", "agent")]
    [InlineData("plan", "plan")]
    [InlineData("invalid", "chat")]
    public void NormalizeMode_maps_expected(string? input, string expected) =>
        Assert.Equal(expected, WorkModeModes.NormalizeMode(input));
}
