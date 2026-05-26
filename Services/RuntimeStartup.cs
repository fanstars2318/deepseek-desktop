using System.IO;
using System.Drawing;
using System.Windows.Forms;
using DeepSeekBrowser.Services.Runtime;

namespace DeepSeekBrowser.Services;

/// <summary>主程序启动前检测 .NET / WebView2 运行库（原 Launcher 逻辑，进程内执行）。</summary>
public static class RuntimeStartup
{
    public static bool EnsureReady()
    {
        if (ShouldSkipRuntimeCheck())
            return true;

        var publishDir = PublishPaths.Root;
        if (string.IsNullOrWhiteSpace(publishDir))
            publishDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var report = RuntimeDependencyChecker.Analyze(publishDir);
        if (report.Missing.Count == 0 || RuntimeDependencyChecker.IsMainAppRunnable(publishDir))
            return true;

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
            return false;

        if (choice != DialogResult.Yes)
            return false;

        using var progress = new RuntimeInstallProgressForm();
        progress.Show();
        Application.DoEvents();

        var installTask = RuntimeDependencyInstaller.InstallMissingAsync(
            report.Missing,
            DeepSeekDesktopApp.LogsDirectory,
            progress.AppendLog);

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
            return false;
        }

        return true;
    }

    private static bool ShouldSkipRuntimeCheck()
    {
        if (string.Equals(Environment.GetEnvironmentVariable(DeepSeekDesktopApp.SkipRuntimeCheckEnvVar), "1", StringComparison.Ordinal))
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

    private sealed class RuntimeInstallProgressForm : Form
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
}
