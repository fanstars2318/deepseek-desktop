using DeepSeek.Desktop.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DeepSeek.Desktop.Pages;

/// <summary>保留类型以兼容旧导航；对话 WebView 已移至 MainWindow 常驻。</summary>
public sealed partial class ChatPage : Page
{
    public ChatPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (App.MainWindow is MainWindow mw)
        {
            mw.NavigateToChat();
            await mw.EnsureHostInitializedAsync();
        }
    }
}
