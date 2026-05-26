param(
    [string]$PublishDir = "",
    [int]$TimeoutSec = 150
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "Get-PublishDir.ps1")
if (-not $PublishDir) {
    $PublishDir = Get-DeepSeekPublishDir -RepoRoot $root
}
$PublishDir = [System.IO.Path]::GetFullPath($PublishDir)

$exe = Join-Path $PublishDir "DeepSeek.exe"
$traceLog = Join-Path $env:LOCALAPPDATA "deepseek_desktop\logs\work-mode-trace.log"
$uiLog = Join-Path $env:LOCALAPPDATA "deepseek_desktop\logs\desktop-ui-trace.log"

if (-not (Test-Path $exe)) {
    throw "DeepSeek.exe not found: $exe (run .\build.ps1 first)"
}

function Get-PublishDeepSeekProcesses {
    Get-Process -Name "DeepSeek" -ErrorAction SilentlyContinue | Where-Object {
        try {
            if ($_.HasExited) { return $false }
            $path = $_.MainModule.FileName
            return $path -and [IO.Path]::GetFullPath($path).StartsWith($PublishDir, [StringComparison]::OrdinalIgnoreCase)
        } catch { return $false }
    }
}

function Stop-PublishAppInstances {
    Get-PublishDeepSeekProcesses | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
}

Write-Host "verify-desktop-smoothness: $PublishDir"

Stop-PublishAppInstances
if (Test-Path $traceLog) { Remove-Item -Force $traceLog }
if (Test-Path $uiLog) { Remove-Item -Force $uiLog }

$env:DEEPSEEK_DESKTOP_VERIFY_WORKMODE = "1"
$env:DEEPSEEK_DESKTOP_VERIFY_SMOOTHNESS = "1"
$env:DEEPSEEK_DESKTOP_VERIFY_SMOOTHNESS_UI = "1"
$app = Start-Process -FilePath $exe -WorkingDirectory $PublishDir -PassThru
Remove-Item Env:\DEEPSEEK_DESKTOP_VERIFY_WORKMODE -ErrorAction SilentlyContinue
Remove-Item Env:\DEEPSEEK_DESKTOP_VERIFY_SMOOTHNESS -ErrorAction SilentlyContinue
Remove-Item Env:\DEEPSEEK_DESKTOP_VERIFY_SMOOTHNESS_UI -ErrorAction SilentlyContinue

try {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $sawSmoothPass = $false
    $sawSmoothFail = $false
    $sawExit = $false

    while ((Get-Date) -lt $deadline) {
        if ($app.HasExited) { $sawExit = $true; break }
        if (Test-Path $traceLog) {
            $text = Get-Content $traceLog -Raw -ErrorAction SilentlyContinue
            if ($text -match "SmoothnessSelfTest: PASS") { $sawSmoothPass = $true }
            if ($text -match "SmoothnessSelfTest: FAIL") { $sawSmoothFail = $true }
            if ($text -match "WorkModeUiVerify: exiting") { $sawExit = $true; break }
        }
        Start-Sleep -Seconds 1
    }

    if (-not $app.HasExited) {
        Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }

    if ($sawSmoothFail) { throw "SmoothnessSelfTest reported FAIL (see $traceLog)" }
    if (-not $sawSmoothPass) { throw "SmoothnessSelfTest missing PASS (see $traceLog)" }

    if (Test-Path $uiLog) {
        $uiText = Get-Content $uiLog -Raw
        $burstScheduled = ([regex]::Matches($uiText, "inject_burst_scheduled")).Count
        $loadingShows = ([regex]::Matches($uiText, "loading_overlay_show")).Count
        if ($burstScheduled -gt 15) {
            throw "too many inject_burst_scheduled ($burstScheduled) in $uiLog"
        }
        if ($loadingShows -gt 6) {
            throw "too many loading_overlay_show ($loadingShows) in $uiLog"
        }
        Write-Host "  OK inject_burst_scheduled=$burstScheduled loading_overlay_show=$loadingShows"
    } else {
        Write-Warning "desktop-ui-trace.log not found (non-fatal)"
    }

    if ($app.ExitCode -gt 0) {
        throw "DeepSeek.exe exit code $($app.ExitCode)"
    }

    Write-Host "verify-desktop-smoothness: PASS"
}
finally {
    Stop-PublishAppInstances
}
