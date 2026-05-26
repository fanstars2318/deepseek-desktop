param(
    [string]$Root = "",
    [string]$OutDir = "",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }
. (Join-Path $Root "scripts\Get-PublishDir.ps1")
if (-not $OutDir) { $OutDir = Get-DeepSeekPublishDir -RepoRoot $Root }

function Find-QtBinDir {
    if ($env:CMAKE_PREFIX_PATH) {
        foreach ($p in ($env:CMAKE_PREFIX_PATH -split ';')) {
            $windeploy = Join-Path $p "bin\windeployqt.exe"
            if (Test-Path $windeploy) { return (Join-Path $p "bin") }
        }
    }
    $candidates = @(
        "C:\Qt\6.8.0\msvc2022_64",
        "C:\Qt\6.7.3\msvc2022_64",
        "C:\Qt\6.6.3\msvc2022_64"
    )
    foreach ($base in $candidates) {
        if (Test-Path (Join-Path $base "bin\windeployqt.exe")) { return (Join-Path $base "bin") }
    }
    return $null
}

function Copy-QWebChannelJs {
    param([string]$InjectDir)
    $dest = Join-Path $InjectDir "qwebchannel.js"
    if (Test-Path $dest) { return }
    $search = @(
        (Join-Path $env:CMAKE_PREFIX_PATH "share\qt6\resources\webchannel\qwebchannel.js"),
        "C:\Qt\6.8.0\msvc2022_64\share\qt6\resources\webchannel\qwebchannel.js",
        "C:\Qt\6.7.3\msvc2022_64\share\qt6\resources\webchannel\qwebchannel.js"
    )
    foreach ($src in $search) {
        if ($src -and (Test-Path $src)) {
            Copy-Item -Force $src $dest
            Write-Host "Copied qwebchannel.js from $src"
            return
        }
    }
    Write-Warning "qwebchannel.js not found; Qt WebChannel bootstrap may fail until Qt share path is on CMAKE_PREFIX_PATH"
}

$qtBin = Find-QtBinDir
if (-not $qtBin) {
    throw "Qt 6 MSVC kit not found. Install Qt 6.6+ WebEngine (aqtinstall or Qt Online Installer) and set CMAKE_PREFIX_PATH."
}

$qtRoot = Split-Path $qtBin -Parent
$cmakeExe = $null
if (Get-Command cmake -ErrorAction SilentlyContinue) {
    $cmakeExe = (Get-Command cmake).Source
}
if (-not $cmakeExe) {
    $qtToolsRoot = Split-Path (Split-Path $qtRoot -Parent) -Parent
    $bundled = Get-ChildItem (Join-Path $qtToolsRoot "Tools") -Recurse -Filter "cmake.exe" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
    if ($bundled) { $cmakeExe = $bundled }
}
if (-not $cmakeExe -and (Test-Path "C:\Program Files\CMake\bin\cmake.exe")) {
    $cmakeExe = "C:\Program Files\CMake\bin\cmake.exe"
}
if (-not $cmakeExe) { throw "cmake not found (install CMake or Qt Tools CMake_64)" }

$ninjaExe = Join-Path (Split-Path $qtRoot -Parent) "Tools\Ninja\ninja.exe"
if (-not (Test-Path $ninjaExe)) {
    $ninjaExe = Get-Command ninja -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
}
$useNinja = $ninjaExe -and (Test-Path $ninjaExe)
Write-Host "Qt kit: $qtRoot"
Write-Host "CMake: $cmakeExe"

$buildDir = Join-Path $Root "DeepSeek.Qt\build"
if (Test-Path $buildDir) {
    try { Remove-Item -Recurse -Force $buildDir -ErrorAction Stop }
    catch { Write-Host "WARN: reusing existing CMake build dir" }
}
New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

$injectDir = Join-Path $Root "Assets\inject"
Copy-QWebChannelJs -InjectDir $injectDir

Push-Location $buildDir
if ($useNinja) {
    & $cmakeExe .. -G "Ninja" -DCMAKE_BUILD_TYPE=$Configuration -DCMAKE_PREFIX_PATH=$qtRoot
}
else {
    & $cmakeExe .. -DCMAKE_BUILD_TYPE=$Configuration -DCMAKE_PREFIX_PATH=$qtRoot
}
if ($LASTEXITCODE -ne 0) { throw "cmake configure failed (exit $LASTEXITCODE)" }
& $cmakeExe --build . --config $Configuration
if ($LASTEXITCODE -ne 0) { throw "cmake build failed (exit $LASTEXITCODE)" }
Pop-Location

$builtExe = Join-Path $buildDir "DeepSeek.Qt.exe"
if (-not (Test-Path $builtExe)) {
    $builtExe = Join-Path $buildDir "Release\DeepSeek.Qt.exe"
}
if (-not (Test-Path $builtExe)) { throw "DeepSeek.Qt.exe not found after build" }

Copy-Item -Force $builtExe (Join-Path $OutDir "DeepSeek.Qt.exe")

$windeploy = Join-Path $qtBin "windeployqt.exe"
& $windeploy --no-compiler-runtime --webenginewidgets (Join-Path $OutDir "DeepSeek.Qt.exe")
if ($LASTEXITCODE -ne 0) { throw "windeployqt failed (exit $LASTEXITCODE)" }

Write-Host "Qt shell: $(Join-Path $OutDir 'DeepSeek.Qt.exe')"
