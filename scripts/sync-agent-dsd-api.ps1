param(
    [string]$Root = ""
)

$ErrorActionPreference = "Stop"
if (-not $Root) { $Root = Split-Path $PSScriptRoot -Parent }
$src = Join-Path $Root "Assets\dsd-api"
$dest = Join-Path $Root "Assets\agent\dsd-api"

if (-not (Test-Path $src)) {
    throw "DSD API assets missing: $src (run scripts/build-dsd-api-ui.ps1)"
}

if (Test-Path $dest) {
    Remove-Item -Recurse -Force $dest
}
Copy-Item -Recurse -Force $src $dest
Write-Host "Synced DSD API UI to $dest (same-origin Agent embed)"
