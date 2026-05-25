using System.Text.Json;
using DeepSeekBrowser.Services.Harness;

namespace DeepSeek.Core.Tests.Harness;

public sealed class HarnessOpenAiBuiltinToolsTests
{
    [Theory]
    [InlineData("read", "read_file")]
    [InlineData("write", "write_file")]
    [InlineData("edit", "edit_file")]
    [InlineData("bash", "run_shell")]
    public void MapToBuiltinExecutorName_maps_openai_aliases(string openAi, string builtin)
    {
        Assert.Equal(builtin, HarnessOpenAiBuiltinTools.MapToBuiltinExecutorName(openAi));
        Assert.Equal(builtin, HarnessOpenAiToolLoop.NormalizeToolName(openAi));
    }

    [Fact]
    public void GetDefinitions_includes_core_openai_tools()
    {
        var json = JsonSerializer.Serialize(HarnessOpenAiBuiltinTools.GetDefinitions(includeShell: true));
        Assert.Contains("\"read\"", json);
        Assert.Contains("\"write\"", json);
        Assert.Contains("\"edit\"", json);
        Assert.Contains("\"bash\"", json);
        Assert.Contains("AskUserQuestion", json);
        Assert.Contains("UpdatePlan", json);
    }
}
