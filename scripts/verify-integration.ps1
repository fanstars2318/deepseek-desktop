param(
    [string]$PublishDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $PublishDir) { $PublishDir = Join-Path $root "publish" }

function Assert-File($path, $label) {
    if (-not (Test-Path $path)) { throw "missing $label : $path" }
    Write-Host "  OK $label"
}

Write-Host "verify-integration: publish dir $PublishDir"
Assert-File (Join-Path $PublishDir "DeepSeek.exe") "main exe"
Assert-File (Join-Path $PublishDir "Assets\inject\bridge.js") "inject bridge"
Assert-File (Join-Path $PublishDir "Assets\inject\overlay.js") "inject overlay"
Assert-File (Join-Path $PublishDir "Assets\tools\deepseek.exe") "TUI dispatcher"

$tuiRuntime = Join-Path $PublishDir "Assets\tools\deepseek-tui.exe"
Assert-File $tuiRuntime "TUI runtime"
$runtimeSize = (Get-Item $tuiRuntime).Length
if ($runtimeSize -lt 30000000) {
    throw "TUI runtime too small ($runtimeSize bytes), likely corrupt download"
}
try {
    $ver = & $tuiRuntime --version 2>&1 | Select-Object -First 1
    if (-not $ver) { throw "deepseek-tui --version returned empty" }
    Write-Host "  OK TUI runtime version: $ver"
} catch {
    throw "TUI runtime --version failed: $_"
}

$submoduleRoot = Join-Path $root "third-party\DeepSeek-TUI"
if (Test-Path (Join-Path $submoduleRoot "Cargo.toml")) {
    Write-Host "  OK DeepSeek-TUI submodule present"
} else {
    Write-Warning "third-party/DeepSeek-TUI not initialized (git submodule update --init)"
}

Assert-File (Join-Path $PublishDir "Assets\chat2api\index.html") "Chat2API console UI"

function Assert-TextFileContains {
    param([string]$Path, [string]$Pattern, [string]$Label)
    if (-not (Test-Path $Path)) { throw "missing $Label : $Path" }
    $text = [System.IO.File]::ReadAllText($Path, [System.Text.UTF8Encoding]::new($false))
    if ($text -notlike "*$Pattern*") { throw "$Label missing text: $Pattern" }
    Write-Host "  OK $Label"
}

$agentIndex = Join-Path $PublishDir "Assets\agent\index.html"
$agentApp = Join-Path $PublishDir "Assets\agent\agent-app.js"
$settingsEmbed = Join-Path $PublishDir "Assets\agent\settings-embed.js"
Assert-TextFileContains $agentIndex "btn-chat2api" "Agent settings menu (API 管理 button)"
Assert-TextFileContains $agentApp 'chat2api: "https://ds-chat2api.local/index.html"' "Agent embedded Chat2API URL"
Assert-TextFileContains $agentApp "openEmbeddedPanel(`"chat2api`")" "Agent embedded Chat2API open"
Assert-TextFileContains $settingsEmbed "isInAgentIframe" "Settings embed iframe bridge"
Assert-TextFileContains (Join-Path $PublishDir "Assets\chat2api\webview-preload.js") "isInAgentIframe" "Chat2API embed iframe bridge"
Assert-TextFileContains (Join-Path $PublishDir "Assets\chat2api\webview-preload.js") "disabledUpdateStatus" "Chat2API update check disabled"
$chat2apiBundle = Get-ChildItem (Join-Path $PublishDir "Assets\chat2api\assets") -Filter "index-*.js" -ErrorAction SilentlyContinue |
    Sort-Object Length -Descending |
    Select-Object -First 1
if ($chat2apiBundle) {
    $bundleText = [System.IO.File]::ReadAllText($chat2apiBundle.FullName, [System.Text.UTF8Encoding]::new($false))
    if ($bundleText -match 'titleKey: "nav\.about"|path: "/about", element:|import\("\./About-') {
        throw "Chat2API bundle must not include About page: $($chat2apiBundle.Name)"
    }
    Write-Host "  OK Chat2API About page removed from bundle"
    if ($bundleText -notmatch 'fallbackLng:\s*"zh-CN"') {
        throw "Chat2API bundle must default to zh-CN locale"
    }
    if ($bundleText -match '"en-US":\s*\{\s*translation:\s*enUS') {
        throw "Chat2API bundle must not ship en-US locale"
    }
    Write-Host "  OK Chat2API default locale zh-CN"
}
Assert-TextFileContains (Join-Path $PublishDir "Assets\chat2api\ds-i18n-zh.js") "fixStoredLocale" "Chat2API Chinese locale helper"
Assert-TextFileContains (Join-Path $PublishDir "Assets\chat2api\ds-ui-trim.js") "hideLanguageControls" "Chat2API language settings hidden"
Assert-TextFileContains (Join-Path $PublishDir "Assets\chat2api\ds-desktop-stack.js") "deepseekDesktop" "Chat2API desktop stack banner"
Assert-TextFileContains (Join-Path $PublishDir "Assets\chat2api\index.html") "ds-desktop-stack.js" "Chat2API index loads desktop stack UI"

$mainDll = Join-Path $PublishDir "DeepSeek.dll"
if (Test-Path $mainDll) {
    $mainCs = Join-Path (Split-Path $PSScriptRoot -Parent) "MainWindow.xaml.cs"
    $agentCs = Join-Path (Split-Path $PSScriptRoot -Parent) "Services\DesktopAgentHost.cs"
    foreach ($f in @($mainCs, $agentCs)) {
        if (Test-Path $f) {
            $src = [System.IO.File]::ReadAllText($f, [System.Text.UTF8Encoding]::new($false))
            if ($src -match 'ScheduleWarmup\s*\(') {
                throw "startup must not call ScheduleWarmup: $f"
            }
        }
    }
    Write-Host "  OK no startup API console warmup"
    $ilspycmd = Get-Command ilspycmd -ErrorAction SilentlyContinue
    if ($ilspycmd) {
        $agentHost = & ilspycmd -t DeepSeekBrowser.Services.DesktopAgentHost $mainDll 2>&1 | Out-String
        if ($agentHost -notmatch 'ShowEmbeddedPanelAsync') {
            throw "Agent host missing ShowEmbeddedPanelAsync for embedded API/settings"
        }
        if ($agentHost -notmatch 'HandleEmbeddedIpcInvokeAsync') {
            throw "Agent host missing HandleEmbeddedIpcInvokeAsync for embedded Chat2API"
        }
        if ($agentHost -notmatch 'SyncChat2ApiStackAsync') {
            throw "Agent host missing SyncChat2ApiStackAsync for desktop/TUI linkage"
        }
        if ($agentHost -notmatch 'OpenTuiConfigFile') {
            throw "Agent host missing OpenTuiConfigFile for TUI config from Chat2API"
        }
        if ($agentHost -notmatch 'EnsureAgentScopedListening') {
            throw "RunAgentAsync path missing EnsureAgentScopedListening"
        }
        Write-Host "  OK embedded panel (API + settings in Agent page)"
        Write-Host "  OK Agent scoped LLM bridge"
    }
}

$cfgPath = Join-Path $env:LOCALAPPDATA "deepseek_desktop\config.json"
if (-not (Test-Path $cfgPath)) {
    $legacyCfg = Join-Path $env:LOCALAPPDATA "DeepSeekEdge\config.json"
    if (Test-Path $legacyCfg) { $cfgPath = $legacyCfg }
}
if (Test-Path $cfgPath) {
    $cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
    if ($cfg.LocalApiPort -eq 5111) {
        Write-Warning "config still has LocalApiPort=5111 (legacy); consider resetting to 0"
    }
}

# Ensure no listener on legacy 5111 when app is not running
$legacy = Get-NetTCPConnection -LocalPort 5111 -State Listen -ErrorAction SilentlyContinue
if ($legacy) {
    Write-Warning "port 5111 is listening (legacy proxy may still be running from an old build)"
} else {
    Write-Host "  OK port 5111 not listening"
}

Write-Host "verify-integration: PASS"
