# Agent 验收：单元测试 + Token 优化专项
param(
    [switch]$TokenOnly
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

try {
    Write-Host "== Agent acceptance: dotnet test ==" -ForegroundColor Cyan
    if ($TokenOnly) {
        dotnet test DeepSeek.Core.Tests --filter "FullyQualifiedName~HarnessTokenOptimizationTests|FullyQualifiedName~HarnessToolFilterTests" --verbosity minimal
    } else {
        dotnet test DeepSeek.Core.Tests --verbosity minimal
    }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "OK: all acceptance checks passed." -ForegroundColor Green
}
finally {
    Pop-Location
}
