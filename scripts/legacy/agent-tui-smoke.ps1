# Agent TUI 冒烟测试：启动桌面端并自动发送 HELLO，断言正常 AI 回复
param(
    [string]$ExePath = "",
    [int]$TimeoutSec = 360
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$helloScript = Join-Path $PSScriptRoot "agent-hello-test.ps1"

if (-not (Test-Path $helloScript)) {
    throw "missing agent-hello-test.ps1"
}

Write-Host "agent-tui-smoke: delegating to agent-hello-test.ps1"
& $helloScript -ExePath $ExePath -TimeoutSec $TimeoutSec
exit $LASTEXITCODE
