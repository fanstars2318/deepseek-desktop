namespace DeepSeek.Desktop.Services;

/// <summary>系统托盘占位：WinUI 3 无内置托盘，后续可接入 H.NotifyIcon。</summary>
public sealed class TrayService : IDisposable
{
    public void Initialize(Microsoft.UI.Xaml.Window window)
    {
        // 双击任务栏图标仍可通过窗口正常最小化/还原
    }

    public void Dispose()
    {
    }
}
