using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DeepSeekBrowser.Models;
using DeepSeekBrowser.Services;
using DeepSeekBrowser.Services.Harness;
using DeepSeekBrowser.Services.Harness.Workers;

namespace DeepSeekBrowser;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains("--worker", StringComparer.OrdinalIgnoreCase))
        {
            PublishPaths.Initialize();
            var config = ConfigStore.Load();
            DsdOpenAiCompat.EnsureDefaultMappings(config);
            var exit = HarnessWorkerHost.RunAsync(e.Args, () =>
            {
                var mcp = new McpHub();
                return new HarnessSubAgentService(
                    () => AgentChatClientFactory.Create(config, null!),
                    mcp,
                    (_, _) => Task.FromResult(true),
                    maxConcurrent: 1);
            }).GetAwaiter().GetResult();
            Shutdown(exit <= 0 ? Math.Max(exit, 0) : 1);
            return;
        }

        if (!SingleInstanceService.TryAcquire())
        {
            if (!SingleInstanceService.TryNotifyRunningInstance())
                ActivateExistingInstanceFallback();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        PublishPaths.Initialize();

        if (!RuntimeStartup.EnsureReady())
        {
            SingleInstanceService.Release();
            Shutdown(1);
            return;
        }

        var main = new MainWindow();
        MainWindow = main;
        main.Show();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SingleInstanceService.Release();
        ShutdownCoordinator.RunExitCleanup();
        base.OnExit(e);
    }

    internal static void RegisterMainWindowActivation(MainWindow window)
    {
        SingleInstanceService.StartActivationServer(() =>
        {
            if (window.IsVisible && window.WindowState != WindowState.Minimized)
            {
                window.Show();
                window.WindowState = WindowState.Normal;
                window.Activate();
                window.Focus();
                BringWindowToFront(window);
                return;
            }

            window.GetTrayService()?.ShowMainWindow();
        });
    }

    private static void ActivateExistingInstanceFallback()
    {
        try
        {
            var current = Process.GetCurrentProcess();
            foreach (var proc in Process.GetProcessesByName(current.ProcessName))
            {
                if (proc.Id == current.Id)
                    continue;

                proc.Refresh();
                var handle = proc.MainWindowHandle;
                if (handle == IntPtr.Zero)
                    continue;

                NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
                NativeMethods.SetForegroundWindow(handle);
                return;
            }
        }
        catch
        {
            // ignore
        }
    }

    internal static void BringWindowToFront(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;

        NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
        NativeMethods.SetForegroundWindow(handle);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        Views.DsMessageDialog.Warning(
            null,
            $"DeepSeek 启动或运行出错：\n\n{e.Exception.Message}",
            "DeepSeek");
        e.Handled = true;
        Shutdown(-1);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogCrash(ex);
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            var path = Path.Combine(DeepSeekDesktopApp.LocalAppDataRoot, "crash.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\r\n{ex}\r\n\r\n");
        }
        catch
        {
            // ignore
        }
    }

    private static class NativeMethods
    {
        public const int SwRestore = 9;

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
