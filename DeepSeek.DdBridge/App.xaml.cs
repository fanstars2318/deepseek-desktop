using System.Windows;

namespace DeepSeekBrowser.DdBridge;

public partial class BridgeApp : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        BridgeStartupContext.BeginAcceptPipe();

        if (e.Args.Contains("--ipc-smoke", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var code = await BridgeIpcSmoke.RunAsync();
            Shutdown(code);
            return;
        }

        var window = new BridgeHostWindow();
        MainWindow = window;
        window.Show();
        base.OnStartup(e);
    }
}
