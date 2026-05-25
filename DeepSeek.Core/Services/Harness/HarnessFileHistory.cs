using System.Security.Cryptography;
using System.Text;

namespace DeepSeekBrowser.Services.Harness;

/// <summary>Per-session git checkpoint history (deepcode-cli file-history style).</summary>
public sealed class HarnessFileHistory
{
    private readonly string _workspace;
    private readonly string _gitDir;

    public HarnessFileHistory(string workspaceRoot)
    {
        _workspace = Path.GetFullPath(workspaceRoot);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_workspace)))[..16];
        _gitDir = Path.Combine(AgentDesktopConfigSync.HomeDirectory, "file-history", hash);
    }

    public void EnsureSession(string sessionId)
    {
        if (!IsValidSessionId(sessionId))
            return;

        Directory.CreateDirectory(_gitDir);
        if (!Directory.Exists(Path.Combine(_gitDir, ".git")))
            RunGit("init");

        var branch = BranchName(sessionId);
        if (!BranchExists(branch))
        {
            RunGit($"checkout --orphan {branch}");
            RunGit("commit --allow-empty -m \"init\" --no-verify");
            return;
        }

        RunGit($"checkout {branch}");
    }

    public string? RecordCheckpoint(string sessionId, IReadOnlyList<string> relativePaths, string message)
    {
        if (!IsValidSessionId(sessionId) || relativePaths.Count == 0)
            return null;

        EnsureSession(sessionId);
        var added = false;
        foreach (var rel in relativePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var full = Path.Combine(_workspace, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
                continue;
            RunGit($"add -- {Quote(rel)}");
            added = true;
        }

        if (!added)
            return GetCurrentCheckpointHash(sessionId);

        RunGit($"commit -m {Quote(message)} --no-verify");
        return GetCurrentCheckpointHash(sessionId);
    }

    public string? GetCurrentCheckpointHash(string sessionId)
    {
        if (!IsValidSessionId(sessionId) || !BranchExists(BranchName(sessionId)))
            return null;
        var hash = RunGit($"rev-parse {BranchName(sessionId)}").Trim();
        return IsCommitHash(hash) ? hash : null;
    }

    public bool CanRestore(string sessionId, string checkpointHash)
    {
        if (!IsValidSessionId(sessionId) || !IsCommitHash(checkpointHash))
            return false;
        var outText = RunGit($"cat-file -e {checkpointHash}^{{commit}}");
        return !outText.Contains("fatal", StringComparison.OrdinalIgnoreCase);
    }

    public bool RestoreCheckpoint(string sessionId, string checkpointHash)
    {
        if (!CanRestore(sessionId, checkpointHash))
            return false;
        try
        {
            EnsureSession(sessionId);
            RunGit($"reset --hard {checkpointHash}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool TryRestoreSingleFile(string relativePath)
    {
        EnsureSession("_legacy");
        var result = RunGit($"checkout HEAD -- {Quote(relativePath)}");
        return !result.Contains("error", StringComparison.OrdinalIgnoreCase)
               && !result.Contains("fatal", StringComparison.OrdinalIgnoreCase);
    }

    public void Snapshot(string relativePath) =>
        RecordCheckpoint("_legacy", [relativePath], "snapshot " + relativePath);

    private bool BranchExists(string branch)
    {
        var result = RunGit($"rev-parse --verify refs/heads/{branch}");
        return !result.Contains("fatal", StringComparison.OrdinalIgnoreCase);
    }

    private static string BranchName(string sessionId) => "dsd-" + Sanitize(sessionId);

    private static string Sanitize(string sessionId) =>
        new string(sessionId.Where(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-').ToArray());

    private static bool IsValidSessionId(string sessionId) =>
        !string.IsNullOrWhiteSpace(sessionId) && sessionId.Length <= 64;

    private static bool IsCommitHash(string hash) =>
        hash.Length is 40 or 64 && hash.All(static c => char.IsAsciiHexDigit(c));

    private string RunGit(string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"-C \"{_workspace}\" --git-dir=\"{_gitDir}\" --work-tree=\"{_workspace}\" {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = System.Diagnostics.Process.Start(psi)!;
        proc.WaitForExit();
        return proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
    }

    private static string Quote(string path) => "\"" + path.Replace("\"", "\\\"") + "\"";
}
