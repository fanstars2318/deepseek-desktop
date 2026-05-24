# 确保 Rust 1.88+ 可用（DeepSeek-TUI rust-version = 1.88）
param(
    [int]$MinVersionMajor = 1,
    [int]$MinVersionMinor = 88
)

$ErrorActionPreference = "Stop"

function Get-RustcVersion {
    $rustc = Get-Command rustc -ErrorAction SilentlyContinue
    if (-not $rustc) { return $null }
    try {
        $out = & rustc --version 2>&1 | Out-String
        if ($out -match 'rustc\s+(\d+)\.(\d+)') {
            return [PSCustomObject]@{ Major = [int]$Matches[1]; Minor = [int]$Matches[2]; Raw = $out.Trim() }
        }
    } catch { }
    return $null
}

function Ensure-RustupStable {
    $rustup = Get-Command rustup -ErrorAction SilentlyContinue
    if (-not $rustup) {
        throw "未找到 rustup。请安装 Rust: https://rustup.rs"
    }
    Write-Host "ensure-rust: installing/updating stable toolchain..."
    & rustup toolchain install stable
    if ($LASTEXITCODE -ne 0) { throw "rustup toolchain install stable 失败" }
    & rustup default stable
    if ($LASTEXITCODE -ne 0) { throw "rustup default stable 失败" }
}

$ver = Get-RustcVersion
$needInstall = $false
if ($null -eq $ver) {
    Write-Host "ensure-rust: rustc not found"
    $needInstall = $true
}
elseif ($ver.Major -lt $MinVersionMajor -or ($ver.Major -eq $MinVersionMajor -and $ver.Minor -lt $MinVersionMinor)) {
    Write-Host "ensure-rust: rustc $($ver.Major).$($ver.Minor) < required $MinVersionMajor.$MinVersionMinor"
    $needInstall = $true
}

if ($needInstall) {
    Ensure-RustupStable
    $ver = Get-RustcVersion
    if ($null -eq $ver) { throw "ensure-rust: rustc still unavailable after rustup" }
}

if ($ver.Major -lt $MinVersionMajor -or ($ver.Major -eq $MinVersionMajor -and $ver.Minor -lt $MinVersionMinor)) {
    throw "ensure-rust: rustc $($ver.Major).$($ver.Minor) still below $MinVersionMajor.$MinVersionMinor"
}

$cargo = Get-Command cargo -ErrorAction SilentlyContinue
if (-not $cargo) { throw "ensure-rust: cargo not found" }

Write-Host "ensure-rust: OK ($($ver.Raw))"
return $true
