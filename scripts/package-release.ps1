# 将 publish/ 打成发布 zip（仅含运行目录 + DISCLAIMER）
param(
    [string]$RepoRoot = "",
    [string]$PublishDir = "",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
if (-not $RepoRoot) { $RepoRoot = Split-Path -Parent $PSScriptRoot }
. (Join-Path $RepoRoot "scripts\Get-PublishDir.ps1")
if (-not $PublishDir) { $PublishDir = Get-DeepSeekPublishDir -RepoRoot $RepoRoot }
$PublishDir = [System.IO.Path]::GetFullPath($PublishDir)

if (-not (Test-Path (Join-Path $PublishDir "DeepSeek.exe"))) {
    throw "Run .\build.ps1 first — missing $(Join-Path $PublishDir 'DeepSeek.exe')"
}

if (-not $Version) {
    $csproj = Join-Path $RepoRoot "DeepSeekBrowser.csproj"
    if (Test-Path $csproj) {
        $m = Select-String -Path $csproj -Pattern '<Version>([^<]+)</Version>' | Select-Object -First 1
        if ($m) { $Version = $m.Matches[0].Groups[1].Value }
    }
    if (-not $Version) { $Version = "1.0.0" }
}

$building = Get-DeepSeekBuildingRoot -RepoRoot $RepoRoot
$outDir = Join-Path $building "artifacts\release"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$zipName = "DeepSeek-Desktop-$Version-win-x64.zip"
$zipPath = Join-Path $outDir $zipName
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

$staging = Join-Path $env:TEMP "deepseek-release-staging"
if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
New-Item -ItemType Directory -Force -Path $staging | Out-Null

robocopy $PublishDir $staging /E /NFL /NDL /NJH /NJS /NC /NS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy staging failed" }

$disclaimer = Join-Path $RepoRoot "DISCLAIMER.md"
if (Test-Path $disclaimer) {
    Copy-Item -Force $disclaimer (Join-Path $staging "DISCLAIMER.md")
}

Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath -Force
Remove-Item -Recurse -Force $staging

Write-Host "package-release: $zipPath"
exit 0
