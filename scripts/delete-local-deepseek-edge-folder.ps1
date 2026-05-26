# One-shot: remove empty/archived deepseek-edge folder after Cursor releases the handle.
$ErrorActionPreference = "SilentlyContinue"
$paths = @(
    "C:\Users\xiaow\Desktop\DSD\deepseek-edge",
    "C:\Users\xiaow\Desktop\DSD\_to_delete_deepseek-edge",
    "C:\Users\xiaow\.cursor\worktrees\deepseek-edge"
)
foreach ($p in $paths) {
    if (Test-Path $p) {
        Remove-Item -LiteralPath $p -Recurse -Force
        if (Test-Path $p) { Write-Warning "Still exists (in use): $p" } else { Write-Host "Removed: $p" }
    }
}
if (-not (Test-Path "C:\Users\xiaow\Desktop\DSD\deepseek-edge")) {
    Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\RunOnce" -Name "DSD_DeleteEdge" -ErrorAction SilentlyContinue
}
