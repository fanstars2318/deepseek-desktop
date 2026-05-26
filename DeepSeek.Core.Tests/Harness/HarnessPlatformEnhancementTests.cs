using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services.Harness.Graph;
using DeepSeekBrowser.Services.Harness.Observability;

namespace DeepSeek.Core.Tests.Harness;

public sealed class HarnessRunTracerTests
{
    [Fact]
    public void Tracer_writes_meta_and_spans()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "dsd-tracer-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(workspace);
        try
        {
            var runId = "run-test";
            using var tracer = HarnessRunTracer.TryBegin(
                workspace,
                runId,
                new AppConfig { AgentStructuredTraceEnabled = true },
                new HarnessRunTracerContext { Strategy = "execute", PromptPreview = "hello" });
            Assert.NotNull(tracer);

            using (tracer!.StartSpan("tool.execute", null, new Dictionary<string, object?> { ["tool"] = "read_file" }))
            {
            }

            tracer.FinalizeRun(new HarnessRunMetaFinalizeArgs
            {
                WorkspaceRoot = workspace,
                Strategy = "execute",
                PromptPreview = "hello",
                AnswerPreview = "world",
                RetentionDays = 0
            });

            var runDir = Path.Combine(workspace, ".deepseek", "runs", runId);
            Assert.True(File.Exists(Path.Combine(runDir, "meta.json")));
            Assert.True(File.Exists(Path.Combine(runDir, "trace.jsonl")));

            var loaded = HarnessRunTraceStore.LoadRun(workspace, runId);
            Assert.NotNull(loaded);
            Assert.NotEmpty(loaded!.Spans);
        }
        finally
        {
            try { Directory.Delete(workspace, true); } catch { /* ignore */ }
        }
    }
}

public sealed class HarnessGraphDefinitionTests
{
    [Fact]
    public void ParseJson_graph_roundtrip()
    {
        const string json = """
            {
              "id": "demo",
              "nodes": [
                { "id": "a", "type": "llm", "prompt": "hi" },
                { "id": "b", "type": "subagent", "role": "explore" }
              ],
              "edges": [
                { "from": "a", "to": "b" }
              ]
            }
            """;
        var graph = HarnessGraphDefinitionParser.ParseJson(json);
        Assert.Equal("demo", graph.Id);
        Assert.Equal(2, graph.Nodes.Count);
        Assert.Single(graph.Edges);
    }

    [Fact]
    public void Condition_last_exit_code()
    {
        var vars = new Dictionary<string, object?> { ["last_exit_code"] = 1 };
        Assert.True(HarnessGraphCondition.Evaluate("last_exit_code != 0", vars));
        vars["last_exit_code"] = 0;
        Assert.False(HarnessGraphCondition.Evaluate("last_exit_code != 0", vars));
    }
}

public sealed class HarnessGraphCheckpointTests
{
    [Fact]
    public void Save_and_load_roundtrip()
    {
        var threadId = "thread-" + Guid.NewGuid().ToString("N")[..8];
        var state = new HarnessGraphCheckpointState
        {
            ThreadId = threadId,
            GraphId = "demo",
            CurrentNodeId = "human",
            Status = "interrupted",
            LastAnswer = "partial",
            Variables = new Dictionary<string, object?> { ["last_tool_ok"] = true }
        };
        HarnessGraphCheckpoint.Save(state);
        var loaded = HarnessGraphCheckpoint.Load(threadId);
        Assert.NotNull(loaded);
        Assert.Equal("demo", loaded!.GraphId);
        Assert.Equal("interrupted", loaded.Status);
        HarnessGraphCheckpoint.Clear(threadId);
    }
}

public sealed class HarnessSemanticMemoryStoreTests
{
    [Fact]
    public void Add_search_dedupe_by_hash()
    {
        var db = Path.Combine(Path.GetTempPath(), "sem-" + Guid.NewGuid().ToString("N")[..8] + ".db");
        using var store = new DeepSeekBrowser.Services.Harness.Memory.HarnessSemanticMemoryStore(db);
        var vector = new[] { 1f, 0f, 0f };
        var hash = DeepSeekBrowser.Services.Harness.Memory.HarnessEmbeddingClient.ComputeHash("prefer tabs");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        store.AddOrUpdate(new DeepSeekBrowser.Services.Harness.Memory.HarnessMemoryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Scope = "user",
            Text = "prefer tabs",
            Embedding = vector,
            ContentHash = hash,
            CreatedAtUnix = now,
            UpdatedAtUnix = now
        });
        store.AddOrUpdate(new DeepSeekBrowser.Services.Harness.Memory.HarnessMemoryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Scope = "user",
            Text = "prefer tabs updated",
            Embedding = vector,
            ContentHash = hash,
            CreatedAtUnix = now,
            UpdatedAtUnix = now + 1
        });
        var hits = store.Search(vector, ["user"], 3);
        Assert.Single(hits);
        Assert.Equal(1, store.Count());
    }
}
