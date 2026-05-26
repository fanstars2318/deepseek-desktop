# Install Qt 6.8.3 MSVC WebEngine kit for DeepSeek Desktop (DD) Hybrid shell.
# Requires: Python + pip (aqtinstall), ~3GB download.
param(
    [string]$QtVersion = "6.8.3",
    [string]$InstallRoot = "C:\Qt"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command pip -ErrorAction SilentlyContinue)) {
    throw "pip not found. Install Python 3 and run: pip install aqtinstall"
}

pip install aqtinstall --quiet

$modules = "qtwebengine", "qtwebchannel", "qtpositioning"
Write-Host "Installing Qt $QtVersion win64_msvc2022_64 modules: $($modules -join ', ') ..."
aqt install-qt windows desktop $QtVersion win64_msvc2022_64 -m $modules -O $InstallRoot

Write-Host "Installing Qt CMake tools ..."
aqt install-tool windows desktop tools_cmake -O $InstallRoot

$kit = Join-Path $InstallRoot "$QtVersion\msvc2022_64"
$env:CMAKE_PREFIX_PATH = $kit
Write-Host ""
Write-Host "Done. Set permanently (User):"
Write-Host "  [Environment]::SetEnvironmentVariable('CMAKE_PREFIX_PATH', '$kit', 'User')"
Write-Host "Then rebuild: .\build.ps1 -Qt"
