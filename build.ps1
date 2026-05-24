param(
    [switch]$UseLocalTui,
    [switch]$BuildTuiFromSource,
    [switch]$LegacyWpf,
    [switch]$WinUi,
    [string]$TuiSourcePath = "",
    [string]$DeployDir = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$out = Join-Path $root "publish"
if (-not $TuiSourcePath) {
    $TuiSourcePath = Join-Path $root "third-party\DeepSeek-TUI"
}

# 清空 publish，避免旧版 WPF (net10) 的 DeepSeek.dll / runtimeconfig 与 WinUI 混用导致无法启动
if (Test-Path $out) {
    Remove-Item -Recurse -Force $out
}

Push-Location $root
# 默认使用已验证可运行的 WPF 壳；WinUI 需本机 Windows App Runtime 正常，可用 -WinUi 尝试
$useWpf = $LegacyWpf -or (-not $WinUi)
if ($useWpf) {
    if (Test-Path (Join-Path $root "scripts\build-chat2api-ui.ps1")) {
        & (Join-Path $root "scripts\build-chat2api-ui.ps1")
    }
    dotnet publish DeepSeekBrowser.csproj -c Release -r win-x64 --self-contained false -o $out "-p:UseAppHost=true"
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

# Ensure main assembly is present in publish output
if ($useWpf) {
    $builtDll = Join-Path $root "bin\Release\net10.0-windows\win-x64\DeepSeek.dll"
    if ((Test-Path $builtDll) -and -not (Test-Path (Join-Path $out "DeepSeek.dll"))) {
        Copy-Item -Force $builtDll $out
        Write-Host "Copied DeepSeek.dll from build output"
    }
}
else {
    $builtDll = Join-Path $root "DeepSeek.Desktop\bin\Release\net9.0-windows10.0.19041.0\win-x64\DeepSeek.Desktop.dll"
    if ((Test-Path $builtDll) -and -not (Test-Path (Join-Path $out "DeepSeek.Desktop.dll"))) {
        Copy-Item -Force $builtDll $out
        Write-Host "Copied DeepSeek.Desktop.dll from build output"
    }
}

$exePath = Join-Path $out $exeName
if (-not (Test-Path $exePath)) {
    throw "publish 失败：未找到 $exeName"
}

# WinUI 发布目录不应包含旧 WPF 主程序集
$staleWpf = Join-Path $out "DeepSeek.dll"
if ((Test-Path $staleWpf) -and -not $useWpf) {
    Remove-Item -Force $staleWpf, (Join-Path $out "DeepSeek.pdb") -ErrorAction SilentlyContinue
}

if ($useWpf -and -not (Test-Path (Join-Path $out "DeepSeek.dll"))) {
    throw "publish 缺少 DeepSeek.dll，请重新运行 build.ps1"
}

$required = @(
    (Join-Path $out "Assets\inject\overlay.css"),
    (Join-Path $out "Assets\inject\bridge.js")
)
if (-not $useWpf) {
    $required += (Join-Path $out "DeepSeek.Desktop.runtimeconfig.json")
}
foreach ($p in $required) {
    if (-not (Test-Path $p)) { throw "publish 缺少资源: $p" }
}

# DeepSeek-TUI：优先 npm 本地包，其次 -UseLocalTui 源码编译，最后 GitHub Release
$toolsOut = Join-Path $out "Assets\tools"
New-Item -ItemType Directory -Force -Path $toolsOut | Out-Null
$dispatcher = Join-Path $toolsOut "deepseek.exe"
$tuiRuntime = Join-Path $toolsOut "deepseek-tui.exe"
$npmDir = Join-Path $env:APPDATA "npm\node_modules\deepseek-tui\bin\downloads"
$minDispatcher = 10000000
$minRuntime = 30000000

function Test-TuiBinaryPair {
    param([string]$Dir)
    $d = Join-Path $Dir "deepseek.exe"
    $r = Join-Path $Dir "deepseek-tui.exe"
  if (-not (Test-Path $d) -or -not (Test-Path $r)) { return $false }
  if ((Get-Item $d).Length -lt $minDispatcher -or (Get-Item $r).Length -lt $minRuntime) { return $false }
  try {
    $v1 = & $d --version 2>$null
    $v2 = & $r --version 2>$null
    return ($LASTEXITCODE -eq 0 -or $v1) -and ($v2)
  } catch { return $false }
}

function Copy-TuiBinaryPair {
    param([string]$FromDir, [string]$Label)
    Copy-Item -Force (Join-Path $FromDir "deepseek.exe") $dispatcher
    Copy-Item -Force (Join-Path $FromDir "deepseek-tui.exe") $tuiRuntime
    $ver = (& $dispatcher --version 2>$null | Select-Object -First 1)
    if (-not $ver) { $ver = "0.8.39" }
    Set-Content -Path (Join-Path $toolsOut "version.txt") -Value "$ver ($Label)" -Encoding utf8
    Write-Host "Bundled DeepSeek-TUI from $Label"
}

$tuiReady = $false
$buildFromSource = $UseLocalTui -or $BuildTuiFromSource

if (Test-TuiBinaryPair $npmDir) {
    Copy-TuiBinaryPair $npmDir "npm"
    $tuiReady = $true
}
elseif ($buildFromSource) {
    & (Join-Path $root "scripts\ensure-rust.ps1")
    & (Join-Path $root "scripts\build-deepseek-tui.ps1") -TuiSourcePath $TuiSourcePath -ToolsOut $toolsOut
    if (Test-TuiBinaryPair $toolsOut) { $tuiReady = $true }
}
else {
    $tag = "v0.8.39"
    $base = "https://github.com/Hmbown/DeepSeek-TUI/releases/download/$tag"
    function Download-TuiBinary {
        param([string]$Url, [string]$Dest, [long]$MinBytes)
        $tmp = "$Dest.download"
        if (Test-Path $tmp) { Remove-Item -Force $tmp -ErrorAction SilentlyContinue }
        Write-Host "Downloading $(Split-Path $Dest -Leaf) ..."
        curl.exe -fL --connect-timeout 20 --max-time 600 $Url -o $tmp
        if ($LASTEXITCODE -ne 0) { throw "Download failed: $Url" }
        if ((Get-Item $tmp).Length -lt $MinBytes) { throw "Download incomplete: $(Split-Path $Dest -Leaf)" }
        Move-Item -Force $tmp $Dest
    }
    try {
        Download-TuiBinary "$base/deepseek-windows-x64.exe" $dispatcher $minDispatcher
        Download-TuiBinary "$base/deepseek-tui-windows-x64.exe" $tuiRuntime $minRuntime
        if (Test-TuiBinaryPair $toolsOut) {
            Copy-TuiBinaryPair $toolsOut "GitHub $tag"
            $tuiReady = $true
        }
    }
    catch {
        Write-Host "WARN: DeepSeek-TUI GitHub download failed: $_"
    }
}

if (-not $tuiReady -and (Test-TuiBinaryPair $npmDir)) {
    Copy-TuiBinaryPair $npmDir "npm fallback"
    $tuiReady = $true
}

if (-not $tuiReady) {
    Write-Host "WARN: DeepSeek-TUI binaries missing. Run: npm install -g deepseek-tui"
    Write-Host "       Or: build.ps1 -BuildTuiFromSource (requires Rust 1.88+ and submodule)"
    Write-Host "       Submodule: git submodule update --init third-party/DeepSeek-TUI"
}

Write-Host "Running unit tests..."
Push-Location $root
dotnet test DeepSeek.Core.Tests\DeepSeek.Core.Tests.csproj -c Release
& (Join-Path $root "scripts\verify-integration.ps1") -PublishDir $out
Pop-Location

$desktop = [Environment]::GetFolderPath("Desktop")
$installFolder = "DeepSeek_desktop"
if ($DeployDir) {
    $target = [System.IO.Path]::GetFullPath($DeployDir)
} else {
    $target = Join-Path $desktop $installFolder
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

Write-Host "Build OK: $(Join-Path $target $exeName)"
if ($useWpf) {
    Write-Host "Tip: WPF build (stable). Use build.ps1 -WinUi for WinUI 3 experimental shell."
} else {
    Write-Host "Tip: WinUI build requires .NET 9 + Windows App SDK 2.x runtime."
}
