param(
    [switch]$UseLocalTui,
    [switch]$BuildTuiFromSource,
    [switch]$LegacyWpf,
    [switch]$WinUi,
    [switch]$Qt,
    [switch]$NoAutoQt,
    [string]$TuiSourcePath = "",
    [switch]$DeployToDesktop,
    [string]$DeployDir = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
. (Join-Path $root "scripts\Get-PublishDir.ps1")
$out = Get-DeepSeekPublishDir -RepoRoot $root
if (-not $TuiSourcePath) {
    $TuiSourcePath = Join-Path $root "third-party\DeepSeek-TUI"
}

function Publish-RuntimeLauncher {
    param([string]$Root, [string]$OutDir)
    $launcherProj = Join-Path $Root "DeepSeek.Launcher\DeepSeek.Launcher.csproj"
    if (-not (Test-Path $launcherProj)) {
        throw "缺少 DeepSeek.Launcher 项目，无法生成运行库检测启动器"
    }
    $launcherTemp = Join-Path $env:TEMP ("deepseek-launcher-" + [Guid]::NewGuid().ToString("n"))
    New-Item -ItemType Directory -Force -Path $launcherTemp | Out-Null
    try {
        dotnet publish $launcherProj -c Release -r win-x64 --self-contained true -o $launcherTemp `
            "-p:PublishSingleFile=true" "-p:IncludeNativeLibrariesForSelfExtract=true"
        if ($LASTEXITCODE -ne 0) { throw "DeepSeek.Launcher publish failed (exit $LASTEXITCODE)" }
        $built = Join-Path $launcherTemp "DeepSeek.Launcher.exe"
        if (-not (Test-Path $built)) { throw "DeepSeek.Launcher.exe not found after publish" }
        Copy-Item -Force $built (Join-Path $OutDir "DeepSeek.exe")
        Write-Host "Runtime launcher: DeepSeek.exe -> checks prerequisites, starts DeepSeek.App.exe"
    }
    finally {
        Remove-Item -Recurse -Force $launcherTemp -ErrorAction SilentlyContinue
    }
}

# 清空 publish，避免旧版 WPF (net10) 的 DeepSeek.dll / runtimeconfig 与 WinUI 混用导致无法启动
if (Test-Path $out) {
    Get-Process -Name "DeepSeek","DeepSeek.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    try {
        Remove-Item -Recurse -Force $out -ErrorAction Stop
    }
    catch {
        Write-Host "WARN: publish 目录被占用，尝试 robocopy 清空..."
        $empty = Join-Path $env:TEMP "deepseek-publish-empty"
        New-Item -ItemType Directory -Force -Path $empty | Out-Null
        robocopy $empty $out /MIR /NFL /NDL /NJH /NJS /NC /NS | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "无法清空 publish: $_" }
    }
}

Push-Location $root

# Auto -Qt when DD Qt toolchain is installed (-NoAutoQt or -LegacyWpf forces WPF)
if (-not $NoAutoQt -and -not $PSBoundParameters.ContainsKey("Qt") -and -not $WinUi -and -not $LegacyWpf) {
    $qtProbe = & (Join-Path $root "scripts\Test-DdQtToolchain.ps1") -Root $root
    if ($qtProbe.Available) {
        $Qt = $true
        Write-Host "Auto: Qt toolchain detected ($($qtProbe.QtKitRoot)) -> building DD Qt hybrid shell"
    }
}

# 默认使用已验证可运行的 WPF 壳；WinUI 需本机 Windows App Runtime 正常，可用 -WinUi 尝试；-Qt 使用 Qt6 WebEngine 主壳
$useWpf = $LegacyWpf -or ((-not $WinUi) -and (-not $Qt))
if ($Qt) {
    if (Test-Path (Join-Path $root "scripts\build-chat2api-ui.ps1")) {
        & (Join-Path $root "scripts\build-chat2api-ui.ps1")
    }
    if (Test-Path (Join-Path $root "scripts\sync-agent-chat2api.ps1")) {
        & (Join-Path $root "scripts\sync-agent-chat2api.ps1") -Root $root
    }
    dotnet publish DeepSeekBrowser.csproj -c Release -r win-x64 --self-contained false -o $out "-p:UseAppHost=false"
    if ($LASTEXITCODE -ne 0) { throw "DeepSeekBrowser library publish failed (exit $LASTEXITCODE)" }
    dotnet publish DeepSeek.DdBridge\DeepSeek.DdBridge.csproj -c Release -r win-x64 --self-contained false -o $out
    if ($LASTEXITCODE -ne 0) { throw "DeepSeek.DdBridge publish failed (exit $LASTEXITCODE)" }

    & (Join-Path $root "scripts\verify-dd-ipc.ps1") -PublishDir $out

    $qtProbe = & (Join-Path $root "scripts\Test-DdQtToolchain.ps1") -Root $root
    if ($qtProbe.Available) {
        & (Join-Path $root "scripts\build-qt.ps1") -Root $root -OutDir $out
        Publish-RuntimeLauncher -Root $root -OutDir $out
        $exeName = "DeepSeek.Qt.exe"
    }
    else {
        Write-Warning "Qt MSVC kit not installed — published Bridge + Assets only. Install Qt 6.6+ WebEngine then re-run build.ps1 -Qt"
        Publish-RuntimeLauncher -Root $root -OutDir $out
        $exeName = "DeepSeek.exe"
    }
}
elseif ($useWpf) {
    if (Test-Path (Join-Path $root "scripts\build-chat2api-ui.ps1")) {
        & (Join-Path $root "scripts\build-chat2api-ui.ps1")
    }
    if (Test-Path (Join-Path $root "scripts\sync-agent-chat2api.ps1")) {
        & (Join-Path $root "scripts\sync-agent-chat2api.ps1") -Root $root
    }
    dotnet publish DeepSeekBrowser.csproj -c Release -r win-x64 --self-contained false -o $out "-p:UseAppHost=true"
    $mainExe = Join-Path $out "DeepSeek.exe"
    if (-not (Test-Path $mainExe)) { throw "publish failed: DeepSeek.exe (app host) not found" }
    Rename-Item -Force $mainExe (Join-Path $out "DeepSeek.App.exe")
    Publish-RuntimeLauncher -Root $root -OutDir $out
    dotnet publish DeepSeek.DdBridge\DeepSeek.DdBridge.csproj -c Release -r win-x64 --self-contained false -o $out
    if ($LASTEXITCODE -ne 0) { throw "DeepSeek.DdBridge publish failed (exit $LASTEXITCODE)" }
    & (Join-Path $root "scripts\verify-dd-ipc.ps1") -PublishDir $out
    $exeName = "DeepSeek.exe"
}
else {
    dotnet publish DeepSeek.Desktop\DeepSeek.Desktop.csproj -c Release -r win-x64 --self-contained false -o $out
    # 启动别名：必须同时复制 runtimeconfig / deps，否则会误读旧版 WPF 配置
    Copy-Item -Force (Join-Path $out "DeepSeek.Desktop.exe") (Join-Path $out "DeepSeek.exe")
    Copy-Item -Force (Join-Path $out "DeepSeek.Desktop.runtimeconfig.json") (Join-Path $out "DeepSeek.runtimeconfig.json")
    Copy-Item -Force (Join-Path $out "DeepSeek.Desktop.deps.json") (Join-Path $out "DeepSeek.deps.json")
    $exeName = "DeepSeek.exe"
}
Pop-Location

# Publish output only under publish/ (never bin/)
$exePath = Join-Path $out $exeName
if (-not (Test-Path $exePath)) {
    throw "publish failed: $exeName not found under $out"
}

$legacyBin = Join-Path $root 'bin'
if (Test-Path $legacyBin) {
    try {
        Remove-Item -Recurse -Force $legacyBin -ErrorAction Stop
        Write-Host 'Removed legacy bin/ (canonical output: publish/)'
    }
    catch {
        Write-Host "WARN: could not remove bin/ (in use?): $legacyBin"
    }
}

# WinUI 发布目录不应包含旧 WPF 主程序集
$staleWpf = Join-Path $out "DeepSeek.dll"
if ((Test-Path $staleWpf) -and -not $useWpf -and -not $Qt) {
    Remove-Item -Force $staleWpf, (Join-Path $out "DeepSeek.pdb") -ErrorAction SilentlyContinue
}

if (($useWpf -or $Qt) -and -not (Test-Path (Join-Path $out "DeepSeek.dll"))) {
    throw "publish 缺少 DeepSeek.dll，请重新运行 build.ps1"
}
if ($useWpf -and -not (Test-Path (Join-Path $out "DeepSeek.App.exe"))) {
    throw "publish 缺少 DeepSeek.App.exe（主程序），请重新运行 build.ps1"
}
if (($useWpf -or $Qt) -and -not (Test-Path (Join-Path $out "DeepSeek.Bridge.exe"))) {
    throw "publish 缺少 DeepSeek.Bridge.exe，请重新运行 build.ps1"
}

$required = @(
    (Join-Path $out "Assets\inject\overlay.css"),
    (Join-Path $out "Assets\inject\bridge.js")
)
if ($useWpf -or $Qt) {
    $required += @(
        (Join-Path $out "DeepSeek.Bridge.exe"),
        (Join-Path $out "Assets\inject\dd-webview-shim.js"),
        (Join-Path $out "Assets\inject\qwebchannel.js")
    )
}
if ($Qt) {
    if (Test-Path (Join-Path $out "DeepSeek.Qt.exe")) {
        $required += (Join-Path $out "DeepSeek.Qt.exe")
    }
}
if (-not $useWpf -and -not $Qt) {
    $required += (Join-Path $out "DeepSeek.Desktop.runtimeconfig.json")
}
foreach ($p in $required) {
    if (-not (Test-Path $p)) { throw "publish 缺少资源: $p" }
}

# Agent 使用进程内 C# Harness，不再打包 deepseek-tui.exe
$toolsOut = Join-Path $out "Assets\tools"
New-Item -ItemType Directory -Force -Path $toolsOut | Out-Null
Write-Host "Agent engine: native C# Harness (no DeepSeek-TUI binary required)"

Write-Host "Running unit tests..."
Push-Location $root
dotnet test DeepSeek.Core.Tests\DeepSeek.Core.Tests.csproj -c Release
& (Join-Path $root "scripts\verify-integration.ps1") -PublishDir $out -Qt:($Qt -and (Test-Path (Join-Path $out "DeepSeek.Qt.exe")))
& (Join-Path $root "scripts\agent-harness-smoke.ps1") -PublishDir $out
Pop-Location

Write-Host "Build OK: $(Join-Path $out $exeName)"
Write-Host "Publish directory: $out"

$shouldDeploy = $DeployToDesktop -or -not [string]::IsNullOrWhiteSpace($DeployDir)
if (-not $shouldDeploy) {
    Write-Host "Run: .\publish\DeepSeek.exe"
    if ($Qt) {
        Write-Host "Tip: Qt hybrid build. Launcher starts DeepSeek.Qt.exe; backend DeepSeek.Bridge.exe (WebView2 API bridge)."
    }
    elseif ($useWpf) {
        Write-Host "Tip: WPF build. DeepSeek.exe checks and installs .NET / WebView2 runtimes if missing."
    } else {
        Write-Host "Tip: WinUI build requires .NET 9 + Windows App SDK 2.x runtime."
    }
    return
}

$desktop = [Environment]::GetFolderPath("Desktop")
$installFolder = "DeepSeek_desktop"
if ($DeployDir) {
    $target = [System.IO.Path]::GetFullPath($DeployDir)
} elseif ($DeployToDesktop) {
    $target = Join-Path $desktop $installFolder
} else {
    return
}
$skipCopy = $false

# 完整替换部署目录，删除残留的 WPF 文件
if (Test-Path $target) {
    try { Remove-Item -Recurse -Force $target -ErrorAction Stop }
    catch {
        Write-Host "目标目录被占用，使用 robocopy 镜像同步..."
        New-Item -ItemType Directory -Force -Path (Join-Path $target ".sync") | Out-Null
        robocopy $out $target /MIR /NFL /NDL /NJH /NJS /NC /NS | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy 部署失败 (exit $LASTEXITCODE)" }
        # 跳过 robocopy 成功分支的 Copy-Item
        $skipCopy = $true
    }
}
if (-not $skipCopy) {
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -Path (Join-Path $out '*') -Destination $target -Recurse -Force
}

$Wsh = New-Object -ComObject WScript.Shell
$lnk = Join-Path $desktop "$installFolder.lnk"
$sc = $Wsh.CreateShortcut($lnk)
$sc.TargetPath = Join-Path $target $exeName
$sc.WorkingDirectory = $target
$ico = Join-Path $target "Assets\AppIcon.ico"
if (-not (Test-Path $ico)) { $ico = Join-Path $target "Assets\deepseek.ico" }
if (Test-Path $ico) { $sc.IconLocation = "$ico,0" }
$sc.Description = "DeepSeek Desktop"
$sc.Save()

Write-Host "Deployed copy: $(Join-Path $target $exeName)"
Write-Host "Canonical publish: $out"
if ($useWpf) {
    Write-Host "Tip: WPF build (stable). Use build.ps1 -WinUi for WinUI 3 experimental shell."
} else {
    Write-Host "Tip: WinUI build requires .NET 9 + Windows App SDK 2.x runtime."
}
