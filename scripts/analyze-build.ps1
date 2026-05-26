# Emits analyzer summary for CI / local governance (phase 0).
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

$logDir = Join-Path $root "artifacts\analyzer"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logFile = Join-Path $logDir "build-$Configuration.log"

Write-Host "Building with analyzers -> $logFile"
dotnet build DeepSeekBrowser.csproj -c $Configuration --no-restore 2>&1 | Tee-Object -FilePath $logFile
$exit = $LASTEXITCODE
if ($exit -ne 0) {
    Pop-Location
    exit $exit
}

$warnCount = (Select-String -Path $logFile -Pattern "warning (IDE|CA|RCS)" -AllMatches).Count
Write-Host "Analyzer warnings (IDE/CA/RCS): $warnCount"
Pop-Location
exit 0
