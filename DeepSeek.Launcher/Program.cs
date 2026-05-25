using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using DeepSeekBrowser.Services;
using DeepSeekBrowser.Services.Runtime;

namespace DeepSeekBrowser.Launcher;

internal static class Program
{
    private static string ResolveMainExeName(string publishDir)
    {
        var qtExe = Path.Combine(publishDir, "DeepSeek.Qt.exe");
        return File.Exists(qtExe) ? "DeepSeek.Qt.exe" : "DeepSeek.App.exe";
    }
    private const string SkipCheckEnv = DeepSeekDesktopApp.SkipRuntimeCheckEnvVar;

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var publishDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (ShouldSkipRuntimeCheck())
            return LaunchMainApp(publishDir, args);

        var report = RuntimeDependencyChecker.Analyze(publishDir);
        if (report.Missing.Count == 0 || RuntimeDependencyChecker.IsMainAppRunnable(publishDir))
            return LaunchMainApp(publishDir, args);

        var missingText = string.Join("\n", report.Missing.Select(d => "• " + d.DisplayName));
        var prompt =
            "DeepSeek 需要以下运行库才能启动：\n\n" + missingText +
            "\n\n是否自动下载并安装？（需要 winget 或网络连接）";

        var choice = MessageBox.Show(
            prompt,
            DeepSeekDesktopApp.DisplayName,
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Information);

        if (choice == DialogResult.Cancel)
            return 1;

        if (choice == DialogResult.Yes)
        {
            using var progress = new RuntimeInstallProgressForm();
            progress.Show();
            Application.DoEvents();

            var installTask = RuntimeDependencyInstaller.InstallMissingAsync(
                report.Missing,
                DeepSeekDesktopApp.LogsDirectory,
                line => progress.AppendLog(line));

            while (!installTask.IsCompleted)
            {
                Application.DoEvents();
                Thread.Sleep(50);
            }

            progress.Close();
            report = RuntimeDependencyChecker.Analyze(publishDir);
            if (report.Missing.Count > 0 && !RuntimeDependencyChecker.IsMainAppRunnable(publishDir))
            {
                var stillMissing = string.Join("\n", report.Missing.Select(d => "• " + d.DisplayName));
                var manual = string.Join("\n", report.Missing
                    .Where(d => !string.IsNullOrWhiteSpace(d.DirectDownloadUrl))
                    .Select(d => d.DirectDownloadUrl));
                MessageBox.Show(
                    "以下运行库仍未就绪：\n\n" + stillMissing +
                    (string.IsNullOrWhiteSpace(manual) ? "" : "\n\n请手动安装：\n" + manual) +
                    "\n\n详细日志：" + Path.Combine(DeepSeekDesktopApp.LogsDirectory, "runtime-install.log"),
                    DeepSeekDesktopApp.DisplayName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return 2;
            }
        }
        else
        {
            return 1;
        }

        return LaunchMainApp(publishDir, args);
    }

    private static bool ShouldSkipRuntimeCheck()
    {
        if (string.Equals(Environment.GetEnvironmentVariable(SkipCheckEnv), "1", StringComparison.Ordinal))
            return true;

        return DeepSeekDesktopApp.IsEnvEnabled(
            DeepSeekDesktopApp.VerifyShutdownEnvVar,
            DeepSeekDesktopApp.VerifyShutdownEnvVar)
               || DeepSeekDesktopApp.IsEnvEnabled(
                   DeepSeekDesktopApp.VerifyWorkModeEnvVar,
                   DeepSeekDesktopApp.LegacyVerifyWorkModeEnvVar)
               || DeepSeekDesktopApp.IsEnvEnabled(
                   DeepSeekDesktopApp.VerifyAgentEnvVar,
                   DeepSeekDesktopApp.LegacyVerifyAgentEnvVar)
               || DeepSeekDesktopApp.IsEnvEnabled(
                   DeepSeekDesktopApp.VerifyAgentTaskEnvVar,
                   DeepSeekDesktopApp.VerifyAgentTaskEnvVar);
    }

    private static int LaunchMainApp(string publishDir, string[] args)
    {
        var mainExeName = ResolveMainExeName(publishDir);
        var mainExe = Path.Combine(publishDir, mainExeName);
        if (!File.Exists(mainExe))
        {
            MessageBox.Show(
                "找不到主程序 " + mainExeName + "。\n请重新运行 build.ps1 发布。",
                DeepSeekDesktopApp.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 3;
        }

        var psi = new ProcessStartInfo
        {
            FileName = mainExe,
            WorkingDirectory = publishDir,
            UseShellExecute = true
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var proc = Process.Start(psi);
        if (proc is null)
            return 4;

        if (ShouldWaitForMainAppExit())
        {
            proc.WaitForExit();
            return proc.ExitCode;
        }

        return 0;
    }

    private static bool ShouldWaitForMainAppExit() =>
        DeepSeekDesktopApp.IsEnvEnabled(
            DeepSeekDesktopApp.VerifyShutdownEnvVar,
            DeepSeekDesktopApp.VerifyShutdownEnvVar);
}

internal sealed class RuntimeInstallProgressForm : Form
{
    private readonly TextBox _logBox;

    public RuntimeInstallProgressForm()
    {
        Text = "正在安装运行库…";
        Width = 560;
        Height = 360;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _logBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9f)
        };
        Controls.Add(_logBox);
    }

    public void AppendLog(string line)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(line));
            return;
        }

        _logBox.AppendText(line + Environment.NewLine);
    }
}
