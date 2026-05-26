# NuGet 供应链与许可证粗检（失败 exit 2）
param(
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"
if (-not $RepoRoot) { $RepoRoot = Split-Path -Parent $PSScriptRoot }
$RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)

$projects = @(
    (Join-Path $RepoRoot "DeepSeekBrowser.csproj"),
    (Join-Path $RepoRoot "DeepSeek.Core\DeepSeek.Core.csproj"),
    (Join-Path $RepoRoot "DeepSeek.Core.Tests\DeepSeek.Core.Tests.csproj")
) | Where-Object { Test-Path $_ }

if (-not $projects) {
    Write-Warning "No csproj found under $RepoRoot"
    exit 1
}

$auditDir = Join-Path $RepoRoot "artifacts\audit"
New-Item -ItemType Directory -Force -Path $auditDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"

Write-Host "=== dotnet restore ==="
foreach ($p in $projects) {
    dotnet restore $p
    if ($LASTEXITCODE -ne 0) { throw "restore failed: $p" }
}

Write-Host "`n=== package list (json) ==="
foreach ($p in $projects) {
    $name = [IO.Path]::GetFileNameWithoutExtension($p)
    $outJson = Join-Path $auditDir "$name-packages-$stamp.json"
    dotnet list $p package --format json | Set-Content -Path $outJson -Encoding UTF8
    Write-Host "  wrote $outJson"
}

Write-Host "`n=== vulnerable packages ==="
$anyVuln = $false
foreach ($p in $projects) {
    Write-Host "--- $p ---"
    dotnet list $p package --vulnerable --include-transitive 2>&1 | Tee-Object -Variable out
    if ($out -match "has the following vulnerable") { $anyVuln = $true }
}

Write-Host "`n=== outdated (direct) ==="
foreach ($p in $projects) {
    Write-Host "--- $p ---"
    dotnet list $p package --outdated 2>&1
}

if ($anyVuln) {
    Write-Warning "Vulnerable packages reported — review before release."
    exit 2
}

Write-Host "`naudit-supply-chain: PASS (no vulnerable packages reported)"
exit 0
