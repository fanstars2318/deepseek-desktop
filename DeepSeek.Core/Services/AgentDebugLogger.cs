using System.Diagnostics;
using System.IO;
using System.Text;

namespace DeepSeekBrowser.Services;

/// <summary>
/// Agent 运行调试日志：写入文件并在 CMD 窗口实时 tail（分析思考/联网耗时）。
/// </summary>
public sealed class AgentDebugLogger : IDisposable
{
    private static AgentDebugLogger? _current;

    public static AgentDebugLogger? Current => _current;

    private readonly StreamWriter _writer;
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly object _lock = new();
    private readonly bool _openConsole;
    private readonly string _logPath;
    private Process? _consoleProcess;
    private int _thinkingChars;
    private DateTimeOffset _lastThinkLogAt = DateTimeOffset.MinValue;

    private AgentDebugLogger(string logPath, bool openConsole)
    {
        _logPath = logPath;
        _openConsole = openConsole;
        var dir = Path.GetDirectoryName(logPath)!;
        Directory.CreateDirectory(dir);
        _writer = new StreamWriter(logPath, append: false, Encoding.UTF8) { AutoFlush = true };
    }

    public static string LogDirectory => DeepSeekDesktopApp.LogsDirectory;

    public string LogFilePath => _logPath;

    public static AgentDebugLogger Begin(string taskPreview, bool openConsole = true)
    {
        _current?.Dispose();
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var path = Path.Combine(LogDirectory, $"agent-{stamp}.log");
        var logger = new AgentDebugLogger(path, openConsole);
        _current = logger;
        logger.Write("SESSION", "========== Agent 调试会话开始 ==========");
        logger.Write("SESSION", $"日志文件: {path}");
        if (!string.IsNullOrWhiteSpace(taskPreview))
            logger.Write("SESSION", "任务: " + Truncate(taskPreview, 240));
        if (openConsole)
            logger.TryOpenTailConsole();
        return logger;
    }

    public void Write(string category, string message)
    {
        lock (_lock)
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] +{_sw.ElapsedMilliseconds,7}ms [{category,-10}] {message}";
            _writer.WriteLine(line);
        }
    }

    public void LogThinkingDelta(string delta)
    {
        if (string.IsNullOrEmpty(delta))
            return;

        _thinkingChars += delta.Length;
        var now = DateTimeOffset.UtcNow;
        var shouldLog = _thinkingChars <= delta.Length
                        || _thinkingChars % 800 < delta.Length
                        || (now - _lastThinkLogAt).TotalSeconds >= 2;

        if (!shouldLog)
            return;

        _lastThinkLogAt = now;
        var preview = Truncate(delta.Replace('\r', ' ').Replace('\n', ' '), 100);
        Write("THINK", $"reasoning +{delta.Length} chars (累计 {_thinkingChars}): {preview}");
    }

    public void LogSseEvent(string eventName, string? kind, string? extra = null)
    {
        var parts = new List<string> { eventName };
        if (!string.IsNullOrWhiteSpace(kind))
            parts.Add($"kind={kind}");
        if (!string.IsNullOrWhiteSpace(extra))
            parts.Add(extra);
        Write("TUI-SSE", string.Join(" ", parts));
    }

    public void LogDsdApiRequest(string model, bool thinking, bool search, int messageCount, bool stream)
    {
        Write(
            "CHAT2API",
            $"POST /v1/chat/completions model={model} thinking={thinking} web_search={search} msgs={messageCount} stream={stream}");
    }

    public void LogDsdApiDone(string model, long elapsedMs, int? answerChars = null, string? note = null)
    {
        var tail = answerChars is not null ? $" answerChars={answerChars}" : "";
        var extra = string.IsNullOrWhiteSpace(note) ? "" : " " + note;
        Write("CHAT2API", $"完成 model={model} 耗时 {elapsedMs}ms{tail}{extra}");
    }

    private void TryOpenTailConsole()
    {
        try
        {
            WriteTailHelperBat();

            var logArg = EscapePowerShellSingleQuoted(_logPath);
            _consoleProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    "-NoProfile -ExecutionPolicy Bypass -NoExit -Command "
                    + "\"$host.UI.RawUI.WindowTitle='DeepSeek Agent Debug'; "
                    + $"Write-Host 'Log: {logArg}' -ForegroundColor Cyan; "
                    + $"Get-Content -LiteralPath '{logArg}' -Wait -Tail 120\"",
                WorkingDirectory = LogDirectory,
                UseShellExecute = true,
            });
            Write("SESSION", "已打开 PowerShell 实时日志窗口");
        }
        catch (Exception ex)
        {
            Write("SESSION", "无法打开日志窗口: " + ex.Message);
        }
    }

    /// <summary>
    /// 供手动双击/在 CMD 中调用；必须用系统 ANSI（GBK）编码，不能用 UTF-8 BOM，否则 cmd 会把 @echo 读成乱码。
    /// </summary>
    private static void WriteTailHelperBat()
    {
        var batPath = Path.Combine(LogDirectory, "_tail-agent-log.bat");
        const string script = """
            @echo off
            chcp 65001 >nul
            title DeepSeek Agent Debug
            if "%~1"=="" goto usage
            echo Log: %~1
            echo Close this window to stop tail only, not the Agent.
            echo.
            powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-Content -LiteralPath '%~1' -Wait -Tail 120"
            goto end
            :usage
            echo Usage: _tail-agent-log.bat "path\to\agent-YYYYMMDD-HHmmss.log"
            pause
            :end
            """;
        File.WriteAllText(batPath, script, Encoding.Default);
    }

    private static string EscapePowerShellSingleQuoted(string value) =>
        value.Replace("'", "''");

    public void Dispose()
    {
        lock (_lock)
        {
            Write("SESSION", $"========== 会话结束，总耗时 {_sw.Elapsed.TotalSeconds:F1}s ==========");
            _writer.Dispose();
        }

        if (ReferenceEquals(_current, this))
            _current = null;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
