namespace DeepSeekBrowser.Services.Harness;

public sealed class HarnessTrace
{
    private Observability.HarnessRunTracer? _tracer;

    public void BindTracer(Observability.HarnessRunTracer? tracer) => _tracer = tracer;

    public void Turn(int n, string note) =>
        AgentDebugLogger.Current?.Write("HARNESS", $"turn={n} {note}");

    public void Tool(string name, long ms, bool error)
    {
        AgentDebugLogger.Current?.Write("HARNESS", $"tool={name} ms={ms} err={error}");
        _tracer?.RecordToolCall(name, ms, error);
    }

    public void Sandbox(string action, string detail)
    {
        AgentDebugLogger.Current?.Write("HARNESS", $"sandbox={action} {detail}");
        _tracer?.RecordSandbox(action, detail);
    }

    public void PathMap(string virtualPath, string physicalPath) =>
        AgentDebugLogger.Current?.Write("HARNESS", $"pathmap {virtualPath} -> {physicalPath}");
}
