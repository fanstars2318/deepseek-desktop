using DeepSeekBrowser.Services;
using Microsoft.UI.Xaml;

namespace DeepSeek.Desktop;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
        WorkModeTrace.Write("WinUI App starting");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        WorkModeTrace.Write("WinUI OnLaunched");
        MainWindow = new MainWindow();
        MainWindow.Activate();
        WorkModeTrace.Write("WinUI window activated");

        var verify = DeepSeekDesktopApp.ResolveEnv(
            DeepSeekDesktopApp.VerifyWorkModeEnvVar,
            DeepSeekDesktopApp.LegacyVerifyWorkModeEnvVar);
        WorkModeTrace.Write("WinUI verify env=" + (verify ?? "(null)"));
        if (string.Equals(verify, "1", StringComparison.Ordinal))
            _ = RunWorkModeSelfTestAsync();
    }

    private static async Task RunWorkModeSelfTestAsync()
    {
        try
        {
            await Task.Delay(1500);
            WorkModeTrace.Write("SelfTest: begin (winui)");
            if (App.MainWindow is not MainWindow mw)
            {
                WorkModeTrace.Write("SelfTest: no main window");
                return;
            }

            await mw.EnsureHostInitializedAsync();
            WorkModeTrace.Write("SelfTest: host initialized");
            mw.NavigateToAgent();
            await Task.Delay(400);
            WorkModeTrace.Write("SelfTest: agent surface");
            mw.NavigateToChat();
            await Task.Delay(400);
            WorkModeTrace.Write("SelfTest: chat surface");
            WorkModeTrace.Write("verify self-test ok (winui)");
        }
        catch (Exception ex)
        {
            WorkModeTrace.Write("SelfTest: error " + ex.Message);
        }
    }
}
