param(
    [string]$DeployDir = "",
    [int]$StartupWaitSec = 20,
    [int]$ShutdownWaitSec = 8
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $DeployDir) {
    $DeployDir = Join-Path ([Environment]::GetFolderPath("Desktop")) "DeepSeek_desktop"
}
$DeployDir = [System.IO.Path]::GetFullPath($DeployDir)

$exe = Join-Path $DeployDir "DeepSeek.exe"
$toolsDir = Join-Path $DeployDir "Assets\tools"
$toolsPrefix = [System.IO.Path]::GetFullPath($toolsDir).TrimEnd('\') + '\'

if (-not (Test-Path $exe)) {
    throw "DeepSeek.exe not found: $exe"
}

function Get-DeployTuiProcesses {
    Get-Process -ErrorAction SilentlyContinue | Where-Object {
        try {
            if ($_.HasExited) { return $false }
            $path = $_.MainModule.FileName
            if (-not $path) { return $false }
            $full = [System.IO.Path]::GetFullPath($path)
            if (-not $full.StartsWith($toolsPrefix, [StringComparison]::OrdinalIgnoreCase)) { return $false }
            $name = [System.IO.Path]::GetFileName($full)
            return $name -eq "deepseek.exe" -or $name -eq "deepseek-tui.exe"
        } catch {
            return $false
        }
    }
}

function Stop-DeployAppInstances {
    Get-Process -Name "DeepSeek" -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            $path = $_.MainModule.FileName
            if ($path -and ([System.IO.Path]::GetFullPath($path).StartsWith($DeployDir, [StringComparison]::OrdinalIgnoreCase))) {
                Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
            }
        } catch { }
    }
}

Write-Host "verify-runtime-shutdown: deploy dir $DeployDir"

Stop-DeployAppInstances
Get-DeployTuiProcesses | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
Start-Sleep -Seconds 2

Write-Host "Starting DeepSeek.exe (graceful shutdown verify mode) ..."
$env:DEEPSEEK_DESKTOP_VERIFY_SHUTDOWN = "1"
$app = Start-Process -FilePath $exe -WorkingDirectory $DeployDir -PassThru
Remove-Item Env:DEEPSEEK_DESKTOP_VERIFY_SHUTDOWN -ErrorAction SilentlyContinue
try {
    $deadline = (Get-Date).AddSeconds($StartupWaitSec)
    $tuiSeen = $false
    while ((Get-Date) -lt $deadline) {
        if ($app.HasExited) { break }
        Start-Sleep -Seconds 2
        $tui = @(Get-DeployTuiProcesses)
        if ($tui.Count -gt 0) {
            Write-Host "  OK TUI processes running: $($tui.Count)"
            $tuiSeen = $true
        }
        if ($app.HasExited -and $tuiSeen) { break }
    }
    if (-not $tuiSeen) {
        throw "No deepseek.exe / deepseek-tui.exe from $toolsDir within ${StartupWaitSec}s"
    }

    $exitDeadline = (Get-Date).AddSeconds($StartupWaitSec)
    while (-not $app.HasExited -and (Get-Date) -lt $exitDeadline) {
        Start-Sleep -Seconds 1
    }
    if (-not $app.HasExited) {
        throw "DeepSeek.exe did not exit gracefully within ${StartupWaitSec}s"
    }
    Write-Host "  OK DeepSeek.exe exited (code $($app.ExitCode))"

    Start-Sleep -Seconds $ShutdownWaitSec
    $leftover = @(Get-DeployTuiProcesses)
    if ($leftover.Count -gt 0) {
        $detail = ($leftover | ForEach-Object {
            try { $_.MainModule.FileName } catch { $_.ProcessName }
        }) -join ", "
        throw "TUI processes still running after exit: $detail"
    }

    Write-Host "verify-runtime-shutdown: PASS"
}
finally {
    if (-not $app.HasExited) {
        Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
    }
    Get-DeployTuiProcesses | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
}
