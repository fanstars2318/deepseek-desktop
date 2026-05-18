using System.Runtime.InteropServices;
using System.Windows;

namespace DeepSeekBrowser.Views;

public partial class TrayMenuWindow : Window
{
    private readonly Action _onShowMain;
    private readonly Action _onExit;

    public TrayMenuWindow(Window owner, Action onShowMain, Action onExit)
    {
        InitializeComponent();
        Owner = owner;
        FontFamily = owner.FontFamily;
        _onShowMain = onShowMain;
        _onExit = onExit;
        Deactivated += (_, _) => Close();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape) Close();
        };
    }

    public void ShowAtCursor()
    {
        var pt = GetCursorPosition();
        var source = PresentationSource.FromVisual(this);
        var dpi = source?.CompositionTarget?.TransformToDevice ?? new System.Windows.Media.Matrix(1, 0, 0, 1, 1, 1);
        var scaleX = dpi.M11;
        var scaleY = dpi.M22;

        Show();
        UpdateLayout();

        var w = ActualWidth > 0 ? ActualWidth : Width;
        var h = ActualHeight > 0 ? ActualHeight : Height;
        var screenW = SystemParameters.PrimaryScreenWidth;
        var screenH = SystemParameters.PrimaryScreenHeight;

        var left = pt.X / scaleX;
        var top = pt.Y / scaleY;
        if (left + w > screenW) left = screenW - w - 8;
        if (top + h > screenH) top = screenH - h - 8;

        Left = Math.Max(8, left);
        Top = Math.Max(8, top);
        Activate();
    }

    private void ShowMainWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _onShowMain();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _onExit();
    }

    private static Point GetCursorPosition()
    {
        GetCursorPos(out var p);
        return new Point(p.X, p.Y);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointInt
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out PointInt lpPoint);
}
