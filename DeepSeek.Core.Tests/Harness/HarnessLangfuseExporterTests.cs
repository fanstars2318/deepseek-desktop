using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.Harness.Observability;
using Xunit;

namespace DeepSeek.Core.Tests.Harness;

public sealed class HarnessLangfuseExporterTests
{
    [Fact]
    public void BuildBatch_includes_trace_and_spans()
    {
        var meta = new HarnessRunMeta
        {
            RunId = "run-abc",
            TraceId = "trace-xyz",
            StartedUtc = "2026-05-25T10:00:00.0000000Z",
            EndedUtc = "2026-05-25T10:01:00.0000000Z",
            Strategy = "execute",
            PromptTokens = 100,
            CompletionTokens = 50,
            TotalTokens = 150
        };
        var spans = new List<HarnessTraceSpan>
        {
            new()
            {
                TraceId = "trace-xyz",
                SpanId = "span-root",
                Name = "run.root",
                StartUtc = meta.StartedUtc,
                EndUtc = meta.EndedUtc,
                DurationMs = 60000
            },
            new()
            {
                TraceId = "trace-xyz",
                SpanId = "span-tool",
                ParentSpanId = "span-root",
                Name = "tool.read_file",
                StartUtc = meta.StartedUtc,
                EndUtc = meta.EndedUtc,
                DurationMs = 10
            }
        };

        var batch = HarnessLangfuseExporter.BuildBatch(meta, spans);
        Assert.Contains(batch, e => e.Type == "trace-create");
        Assert.Contains(batch, e => e.Type == "span-create");
        Assert.Contains(batch, e => e.Type == "generation-create");
    }

    [Fact]
    public void BuildTraceUrl_uses_project_when_set()
    {
        var url = HarnessLangfuseExporter.BuildTraceUrl(new AppConfig
        {
            AgentLangfuseHost = "https://cloud.langfuse.com",
            AgentLangfuseProject = "my-proj"
        }, "trace-1");
        Assert.Contains("my-proj", url);
        Assert.Contains("trace-1", url);
    }
}
