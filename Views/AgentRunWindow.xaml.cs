using System.Windows;

namespace DeepSeekBrowser.Views;

public partial class AgentRunWindow : Window
{
    public event EventHandler? StopRequested;

    public AgentRunWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (Owner is null) return;
            Left = Owner.Left + Owner.Width - Width - 24;
            Top = Owner.Top + 80;
        };
    }

    public void SetTask(string task)
    {
        TaskText.Text = string.IsNullOrWhiteSpace(task) ? "（无任务描述）" : task;
    }

    public void SetRunning(bool running)
    {
        StopBtn.IsEnabled = running;
    }

    public void AppendLog(string line)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => AppendLog(line));
            return;
        }

        LogList.Items.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
        if (LogList.Items.Count > 0)
            LogList.ScrollIntoView(LogList.Items[^1]!);
    }

    public void ClearLog() => LogList.Items.Clear();

    private void Stop_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke(this, EventArgs.Empty);

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
}
