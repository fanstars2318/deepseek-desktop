using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace DeepSeekBrowser.Services.Dd;

/// <summary>DD Qt 主进程与 DeepSeek.Bridge 子进程之间的换行分隔 JSON 管道。</summary>
public sealed class DdDesktopIpc : IAsyncDisposable
{
    public const string PipeName = "dd-desktop-bridge";

    private readonly Stream _stream;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private CancellationTokenSource? _readCts;
    private Task? _readTask;

    public event Action<JsonElement>? LineReceived;

    private DdDesktopIpc(Stream stream)
    {
        _stream = stream;
        _reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };
    }

    public static async Task<DdDesktopIpc> AcceptServerAsync(CancellationToken ct = default)
    {
        var server = new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        await server.WaitForConnectionAsync(ct);
        return new DdDesktopIpc(server);
    }

    public static async Task<DdDesktopIpc> ConnectClientAsync(CancellationToken ct = default)
    {
        var client = new NamedPipeClientStream(
            ".",
            PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync(ct);
        return new DdDesktopIpc(client);
    }

    public void StartReading()
    {
        _readCts = new CancellationTokenSource();
        _readTask = Task.Run(() => ReadLoopAsync(_readCts.Token));
    }

    public async Task SendEnvelopeAsync(string channel, object payload, CancellationToken ct = default)
    {
        var envelope = new { channel, payload };
        await SendLineAsync(JsonSerializer.Serialize(envelope, AgentSessionJson.Options), ct);
    }

    public async Task SendLineAsync(string line, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            await _writer.WriteLineAsync(line.AsMemory(), ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(ct);
                if (line is null)
                    break;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    LineReceived?.Invoke(doc.RootElement.Clone());
                }
                catch (JsonException)
                {
                    // ignore malformed lines
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (IOException)
        {
            // peer disconnected
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _readCts?.Cancel();
        }
        catch
        {
            // ignore
        }

        if (_readTask is not null)
        {
            try
            {
                await _readTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // ignore
            }
        }

        _readCts?.Dispose();
        await _writer.DisposeAsync();
        _reader.Dispose();
        await _stream.DisposeAsync();
    }
}
