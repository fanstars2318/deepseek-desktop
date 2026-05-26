using DeepSeekBrowser.Services;

namespace DeepSeek.Core.Tests;

/// <summary>为并行测试隔离 LocalAppData 配置目录，避免污染用户数据与文件锁冲突。</summary>
public abstract class TestConfigIsolation : IDisposable
{
    private readonly string _tempDir;

    protected TestConfigIsolation()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "deepseek_desktop_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable(DeepSeekDesktopApp.ConfigDirEnvVar, _tempDir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(DeepSeekDesktopApp.ConfigDirEnvVar, null);
        Environment.SetEnvironmentVariable(DeepSeekDesktopApp.LegacyConfigDirEnvVar, null);
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }
}
