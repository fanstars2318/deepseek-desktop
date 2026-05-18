using System.Windows;

namespace DeepSeekBrowser.Views;

public partial class ClosePromptWindow : System.Windows.Window
{
    public enum CloseChoice
    {
        Cancel,
        MinimizeToTray,
        ExitProcess
    }

    public CloseChoice Choice { get; private set; } = CloseChoice.Cancel;

    public ClosePromptWindow()
    {
        InitializeComponent();
    }

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        Choice = CloseChoice.MinimizeToTray;
        DialogResult = true;
        Close();
    }

    private void ExitBtn_Click(object sender, RoutedEventArgs e)
    {
        Choice = CloseChoice.ExitProcess;
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        Choice = CloseChoice.Cancel;
        DialogResult = false;
        Close();
    }
}
