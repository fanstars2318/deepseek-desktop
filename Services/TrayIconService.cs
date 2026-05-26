using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using DeepSeekBrowser.Views;

namespace DeepSeekBrowser.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Window _owner;
    private readonly Action _onExit;
    private TrayMenuWindow? _menu;

    public TrayIconService(System.Windows.Window owner, Action onExit)
    {
        _owner = owner;
        _onExit = onExit;

        _notifyIcon = new NotifyIcon
        {
            Text = "DeepSeek",
            Visible = false
        };

        var iconPath = Path.Combine(PublishPaths.Root, "Assets", "deepseek.ico");
        if (!File.Exists(iconPath))
            iconPath = Path.Combine(PublishPaths.Root, "deepseek.ico");

        if (File.Exists(iconPath))
            _notifyIcon.Icon = new Icon(iconPath);

        _notifyIcon.MouseUp += OnTrayMouseUp;
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void OnTrayMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        _owner.Dispatcher.Invoke(ShowTrayMenu);
    }

    private void ShowTrayMenu()
    {
        _menu?.Close();
        _menu = new TrayMenuWindow(_owner, ShowMainWindow, _onExit);
        _menu.Closed += (_, _) => _menu = null;
        _menu.ShowAtCursor();
    }

    public void ShowInTray()
    {
        _notifyIcon.Visible = true;
        try
        {
            _notifyIcon.ShowBalloonTip(
                2500,
                "DeepSeek",
                "已最小化到托盘，本地 API 继续在后台运行。",
                ToolTipIcon.Info);
        }
        catch
        {
            // balloon tips may fail on some systems
        }
    }

    public void HideFromTray()
    {
        _notifyIcon.Visible = false;
        _menu?.Close();
    }

    public void ShowMainWindow()
    {
        _owner.Dispatcher.Invoke(() =>
        {
            _menu?.Close();
            _owner.Show();
            _owner.WindowState = WindowState.Normal;
            _owner.Activate();
            _owner.Focus();
        });
    }

    public void Dispose()
    {
        _menu?.Close();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
