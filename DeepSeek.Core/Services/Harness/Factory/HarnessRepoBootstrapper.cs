using System.Diagnostics;

namespace DeepSeekBrowser.Services.Harness.Factory;

public static class HarnessRepoBootstrapper
{
    public static async Task<string> EnsureGitRepoAsync(string workspaceRoot, CancellationToken ct)
    {
        var gitDir = Path.Combine(workspaceRoot, ".git");
        if (Directory.Exists(gitDir))
            return "Git repository already present.";

        var init = await RunGitAsync(workspaceRoot, "init", ct);
        if (!init.Ok)
            return "WARNING: git init failed: " + init.Output;

        var ignorePath = Path.Combine(workspaceRoot, ".gitignore");
        if (!File.Exists(ignorePath))
        {
            await File.WriteAllTextAsync(ignorePath,
                ".deepseek/\nbin/\nobj/\n.vs/\nnode_modules/\n",
                ct);
        }

        await RunGitAsync(workspaceRoot, "add -A", ct);
        return "Initialized git repository at " + workspaceRoot;
    }

    private static async Task<(bool Ok, string Output)> RunGitAsync(
        string workspaceRoot,
        string args,
        CancellationToken ct)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = args,
                    WorkingDirectory = workspaceRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            var output = (stdout + "\n" + stderr).Trim();
            return (proc.ExitCode == 0, string.IsNullOrWhiteSpace(output) ? "(no output)" : output);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
