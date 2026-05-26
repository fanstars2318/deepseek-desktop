using System.Diagnostics;
using System.Text.Json;
using DeepSeekBrowser.Models;

namespace DeepSeekBrowser.Services.Harness.Workers;

public sealed class HarnessWorkerProcessPool : IDisposable
{
    private readonly int _poolSize;
    private readonly string _exePath;
    private readonly Queue<Process> _idle = new();
    private readonly object _lock = new();

    public HarnessWorkerProcessPool(AppConfig config, string? exePath = null)
    {
        _exePath = exePath ?? Environment.ProcessPath ?? "DeepSeek.exe";
        var size = config.AgentWorkerPoolSize;
        if (size <= 0)
            size = Math.Clamp(Environment.ProcessorCount / 2, 1, 3);
        _poolSize = size;
    }

    public async Task<HarnessWorkerLease> AcquireAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            while (_idle.Count > 0)
            {
                var p = _idle.Dequeue();
                if (!p.HasExited)
                    return new HarnessWorkerLease(p, this);
                p.Dispose();
            }
        }

        var psi = new ProcessStartInfo
        {
            FileName = _exePath,
            Arguments = "--worker",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start worker");
        await Task.Delay(500, ct);
        return new HarnessWorkerLease(proc, this);
    }

    internal void Release(Process proc)
    {
        lock (_lock)
        {
            if (!proc.HasExited && _idle.Count < _poolSize)
            {
                _idle.Enqueue(proc);
                return;
            }
        }

        try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
        proc.Dispose();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            while (_idle.Count > 0)
            {
                var p = _idle.Dequeue();
                try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
                p.Dispose();
            }
        }
    }
}

public sealed class HarnessWorkerLease : IDisposable
{
    private readonly HarnessWorkerProcessPool _pool;
    public Process Process { get; }

    public HarnessWorkerLease(Process process, HarnessWorkerProcessPool pool)
    {
        Process = process;
        _pool = pool;
    }

    public async Task<HarnessSubAgentResult> RunSubAgentAsync(HarnessSubAgentRequest request, CancellationToken ct)
    {
        var job = JsonSerializer.Serialize(request);
        await Process.StandardInput.WriteLineAsync(job.AsMemory(), ct);
        await Process.StandardInput.FlushAsync(ct);
        var line = await Process.StandardOutput.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(line))
            return HarnessSubAgentResult.Fail("Worker empty response");
        return JsonSerializer.Deserialize<HarnessSubAgentResult>(line)
               ?? HarnessSubAgentResult.Fail("Worker invalid JSON");
    }

    public void Dispose() => _pool.Release(Process);
}
