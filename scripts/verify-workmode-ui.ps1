param(
    [string]$PublishDir = "",
    [int]$TimeoutSec = 120
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

Write-Host "verify-workmode-ui: $PublishDir"

Stop-PublishAppInstances
if (Test-Path $traceLog) { Remove-Item -Force $traceLog }

$env:DEEPSEEK_DESKTOP_VERIFY_WORKMODE = "1"
$env:DEEPSEEK_DESKTOP_VERIFY_WORKMODE_UI = "1"
$app = Start-Process -FilePath $exe -WorkingDirectory $PublishDir -PassThru
Remove-Item Env:\DEEPSEEK_DESKTOP_VERIFY_WORKMODE -ErrorAction SilentlyContinue
Remove-Item Env:\DEEPSEEK_DESKTOP_VERIFY_WORKMODE_UI -ErrorAction SilentlyContinue

try {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $sawChat = $false
    $sawAgent = $false
    $sawFloaterPass = $false
    $sawFloaterFail = $false

    while ((Get-Date) -lt $deadline) {
        if ($app.HasExited) { break }
        if (Test-Path $traceLog) {
            $text = Get-Content $traceLog -Raw -ErrorAction SilentlyContinue
            if ($text -match "ShowChat:") { $sawChat = $true }
            if ($text -match "ShowAgent:") { $sawAgent = $true }
            if ($text -match "SelfTest: floater PASS") { $sawFloaterPass = $true }
            if ($text -match "SelfTest: floater FAIL") { $sawFloaterFail = $true }
            if ($text -match "WorkModeUiVerify: exiting") { break }
        }
        Start-Sleep -Seconds 1
    }

    if (-not $app.HasExited) {
        Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }

    if (-not $sawChat) { throw "work-mode trace missing ShowChat" }
    if (-not $sawAgent) { throw "work-mode trace missing ShowAgent" }
    if ($sawFloaterFail) { throw "SelfTest reported floater FAIL (see $traceLog)" }
    if (-not $sawFloaterPass) { throw "SelfTest missing floater PASS (see $traceLog)" }

    if (Test-Path $traceLog) {
        $traceText = Get-Content $traceLog -Raw
        $applyCount = ([regex]::Matches($traceText, "ApplyWorkMode start")).Count
        $noOpCount = ([regex]::Matches($traceText, "ApplyWorkMode no-op")).Count
        $toggleMsgs = ([regex]::Matches($traceText, '"type":"toggleWorkMode"')).Count
        if ($applyCount -gt 6) {
            throw "too many ApplyWorkMode start ($applyCount) — possible double-toggle (see $traceLog)"
        }
        if ($toggleMsgs -gt 8) {
            throw "too many toggleWorkMode messages ($toggleMsgs) (see $traceLog)"
        }
        Write-Host "  OK ApplyWorkMode count=$applyCount no-op=$noOpCount toggleMsgs=$toggleMsgs"
    }

    Write-Host "  OK ShowChat / ShowAgent in trace"
    Write-Host "  OK chat mode floater visible (SelfTest: floater PASS)"
    if ($app.ExitCode -gt 0) {
        throw "DeepSeek.exe exit code $($app.ExitCode)"
    }
    Write-Host "verify-workmode-ui: PASS"
}
finally {
    Stop-PublishAppInstances
}
