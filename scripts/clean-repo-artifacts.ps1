# Remove build/publish artifacts from the source tree (output lives under ../DDpublish and ../DDbuilding).
param(
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"
if (-not $RepoRoot) {
    $RepoRoot = Split-Path -Parent $PSScriptRoot
}
$RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)

$removeDirs = @(
    (Join-Path $RepoRoot "publish"),
    (Join-Path $RepoRoot "publish-dd-test"),
    (Join-Path $RepoRoot "bin"),
    (Join-Path $RepoRoot "obj"),
    (Join-Path $RepoRoot "artifacts"),
    (Join-Path $RepoRoot "DeepSeek.Qt\build")
)

Get-ChildItem $RepoRoot -Directory -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -eq "bin" -or $_.Name -eq "obj" } |
    ForEach-Object { $removeDirs += $_.FullName }

$removeDirs = $removeDirs | Select-Object -Unique

foreach ($dir in $removeDirs) {
    if (Test-Path $dir) {
        Write-Host "Removing $dir"
        Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue
    }
}

Get-ChildItem $RepoRoot -File -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -eq ".log" -or $_.Name -like "DeepSeek-Desktop-*.zip" } |
    ForEach-Object {
        Write-Host "Removing $($_.FullName)"
        Remove-Item -Force $_.FullName
    }

Write-Host "Repo artifact cleanup done: $RepoRoot"
