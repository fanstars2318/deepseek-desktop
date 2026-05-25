# Returns $true when Qt 6 MSVC WebEngine kit + CMake are available for build-qt.ps1
param([string]$Root = "")

if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }

function Find-QtKitRoot {
    $roots = @()
    if ($env:CMAKE_PREFIX_PATH) {
        $roots += ($env:CMAKE_PREFIX_PATH -split ';' | Where-Object { $_ })
    }
    $roots += @(
        "C:\Qt\6.9.0\msvc2022_64",
        "C:\Qt\6.8.3\msvc2022_64",
        "C:\Qt\6.8.0\msvc2022_64",
        "C:\Qt\6.7.3\msvc2022_64",
        "C:\Qt\6.6.3\msvc2022_64"
    )
    foreach ($r in $roots) {
        if (Test-Path (Join-Path $r "bin\windeployqt.exe")) { return $r }
    }
    if (Test-Path "C:\Qt") {
        $hit = Get-ChildItem "C:\Qt" -Directory -ErrorAction SilentlyContinue |
            ForEach-Object { Get-ChildItem $_.FullName -Directory -Filter "msvc*" -ErrorAction SilentlyContinue } |
            Where-Object { Test-Path (Join-Path $_.FullName "bin\windeployqt.exe") } |
            Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    return $null
}

function Find-CmakeExe {
    param([string]$QtKitRoot)
    $candidates = @(
        (Get-Command cmake -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
        "C:\Program Files\CMake\bin\cmake.exe",
        "C:\Program Files (x86)\CMake\bin\cmake.exe"
    )
    if ($QtKitRoot) {
        $qtTools = Split-Path (Split-Path $QtKitRoot -Parent) -Parent
        $candidates += @(
            (Join-Path $qtTools "Tools\CMake_64\bin\cmake.exe"),
            (Join-Path $qtTools "Tools\Ninja\ninja.exe")
        )
        $candidates += Get-ChildItem (Join-Path $qtTools "Tools") -Recurse -Filter "cmake.exe" -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
    }
    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) { return $c }
    }
    return $null
}

$kit = Find-QtKitRoot
$cmake = Find-CmakeExe -QtKitRoot $kit
[pscustomobject]@{
    Available = [bool]($kit -and $cmake)
    QtKitRoot = $kit
    CmakeExe  = $cmake
}
