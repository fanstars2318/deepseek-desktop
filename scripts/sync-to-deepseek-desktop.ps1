# 将 WPF 主线路源码同步到独立工作区 deepseek_desktop（瘦身 + 工程文档）
param(
    [string]$SourceRoot = "",
    [string]$DestRoot = "C:\Users\xiaow\Desktop\DSD\deepseek_desktop",
    [switch]$InitGit,
    [switch]$RunBuild
)

$ErrorActionPreference = "Stop"
if (-not $SourceRoot) {
    $SourceRoot = Split-Path -Parent $PSScriptRoot
}
$SourceRoot = [System.IO.Path]::GetFullPath($SourceRoot)
$DestRoot = [System.IO.Path]::GetFullPath($DestRoot)
$templates = Join-Path $SourceRoot "scripts\templates"

Write-Host "Source: $SourceRoot"
Write-Host "Dest:   $DestRoot"

New-Item -ItemType Directory -Force -Path $DestRoot | Out-Null

# 镜像同步（排除实验子项目与产物）
$robocopyArgs = @(
    $SourceRoot, $DestRoot,
    "/MIR", "/NFL", "/NDL", "/NJH", "/NJS", "/NC", "/NS",
    "/XD", "DeepSeek.Qt", "DeepSeek.Desktop", "DeepSeek.DdBridge", "DeepSeek.Launcher",
    "third-party", "bin", "obj", "publish", "publish-dd-test", "artifacts", "DDbuilding", "DDpublish",
    ".git", ".vs", ".cursor", ".serena", "DeepSeek.Core\bin", "DeepSeek.Core\obj",
    "DeepSeek.Core.Tests\bin", "DeepSeek.Core.Tests\obj"
)
& robocopy @robocopyArgs | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed (exit $LASTEXITCODE)" }

# 生成物不纳入工作区
$agentChat2api = Join-Path $DestRoot "Assets\agent\chat2api"
if (Test-Path $agentChat2api) {
    Remove-Item -Recurse -Force $agentChat2api
    Write-Host "Removed generated Assets\agent\chat2api"
}

# 工作区专用文件
Copy-Item -Force (Join-Path $templates "build-wpf-publish.ps1") (Join-Path $DestRoot "build.ps1")
Copy-Item -Force (Join-Path $templates "README-deepseek-desktop.md") (Join-Path $DestRoot "README.md")
Copy-Item -Force (Join-Path $templates "CONTRIBUTING.md") (Join-Path $DestRoot "CONTRIBUTING.md")
Copy-Item -Force (Join-Path $SourceRoot "scripts\audit-supply-chain.ps1") (Join-Path $DestRoot "scripts\audit-supply-chain.ps1")
Copy-Item -Force (Join-Path $SourceRoot "scripts\sync-to-deepseek-desktop.ps1") (Join-Path $DestRoot "scripts\sync-to-deepseek-desktop.ps1")

New-Item -ItemType Directory -Force -Path (Join-Path $DestRoot "docs") | Out-Null
Copy-Item -Force (Join-Path $templates "docs\ENGINEERING_REVIEW.md") (Join-Path $DestRoot "docs\ENGINEERING_REVIEW.md")
Copy-Item -Force (Join-Path $templates "docs\PERIPHERAL_AUDIT.md") (Join-Path $DestRoot "docs\PERIPHERAL_AUDIT.md")

$ghWorkflow = Join-Path $DestRoot ".github\workflows"
New-Item -ItemType Directory -Force -Path $ghWorkflow | Out-Null
Copy-Item -Force (Join-Path $templates ".github\workflows\ci.yml") (Join-Path $ghWorkflow "ci.yml")

Copy-Item -Force (Join-Path $SourceRoot "scripts\Get-DsdPaths.ps1") (Join-Path $DestRoot "scripts\Get-DsdPaths.ps1")
Copy-Item -Force (Join-Path $SourceRoot "scripts\Get-PublishDir.ps1") (Join-Path $DestRoot "scripts\Get-PublishDir.ps1")
if (Test-Path (Join-Path $SourceRoot "Directory.Build.props")) {
    Copy-Item -Force (Join-Path $SourceRoot "Directory.Build.props") (Join-Path $DestRoot "Directory.Build.props")
}

$gitignoreExtra = @"

# deepseek_desktop: 勿将仓库内 bin/obj/publish/artifacts 提交进 Git
"@
$giPath = Join-Path $DestRoot ".gitignore"
if (Test-Path $giPath) {
    $gi = Get-Content $giPath -Raw
    if ($gi -notmatch "deepseek_desktop") {
        Add-Content -Path $giPath -Value $gitignoreExtra
    }
}

# 解决方案文件便于 IDE 打开
Push-Location $DestRoot
if (-not (Test-Path "DeepSeek.sln")) {
    dotnet new sln -n DeepSeek --force 2>$null
    dotnet sln add DeepSeekBrowser.csproj 2>$null
    dotnet sln add DeepSeek.Core\DeepSeek.Core.csproj 2>$null
    dotnet sln add DeepSeek.Core.Tests\DeepSeek.Core.Tests.csproj 2>$null
}
Pop-Location

Write-Host "Sync complete: $DestRoot"
Write-Host "Next: cd '$DestRoot'; .\build.ps1"

if ($InitGit -and -not (Test-Path (Join-Path $DestRoot ".git"))) {
    Push-Location $DestRoot
    git init
    git add -A
    Pop-Location
    Write-Host "Initialized git repository."
}

if ($RunBuild) {
    Push-Location $DestRoot
    & .\build.ps1
    Pop-Location
}
