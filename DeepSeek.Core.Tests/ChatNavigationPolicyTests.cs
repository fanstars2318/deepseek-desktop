using DeepSeekBrowser.Services;
using Xunit;

namespace DeepSeek.Core.Tests;

public sealed class ChatNavigationPolicyTests
{
    [Fact]
    public void SameChatLocation_ignores_trailing_slash()
    {
        Assert.True(ChatNavigationPolicy.SameChatLocation(
            "https://chat.deepseek.com/a/chat/",
            "https://chat.deepseek.com/a/chat"));
    }

    [Fact]
    public void IsMinorLocationChange_true_for_hash_only()
    {
        Assert.True(ChatNavigationPolicy.IsMinorLocationChange(
            "https://chat.deepseek.com/a/chat",
            "https://chat.deepseek.com/a/chat#session-1"));
    }

    [Fact]
    public void ShouldShowLoadingOverlay_false_for_spa_hash_change()
    {
        Assert.False(ChatNavigationPolicy.ShouldShowLoadingOverlay(
            "https://chat.deepseek.com/a/chat",
            "https://chat.deepseek.com/a/chat#x",
            isUserInitiated: false));
    }

    [Fact]
    public void ShouldShowLoadingOverlay_true_for_path_change()
    {
        Assert.True(ChatNavigationPolicy.ShouldShowLoadingOverlay(
            "https://chat.deepseek.com/a/chat",
            "https://chat.deepseek.com/a/settings",
            isUserInitiated: false));
    }
}
