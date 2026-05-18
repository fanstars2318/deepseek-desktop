using System.Windows;

namespace DeepSeekBrowser.Views;

public partial class McpToolsWindow : System.Windows.Window
{
    public McpToolsWindow(string serverName, IEnumerable<string> tools)
    {
        InitializeComponent();
        TitleText.Text = $"{serverName} · 已发现工具";
        ToolsList.ItemsSource = tools.ToList();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
