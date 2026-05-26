# DSD 工作区路径：发布 ../DDpublish，中间产物 ../DDbuilding/deepseek_desktop（可用环境变量覆盖）。
function Get-DeepSeekDsdRoot {
    param([string]$RepoRoot = "")
    if (-not $RepoRoot) { $RepoRoot = Split-Path -Parent $PSScriptRoot }
    return [System.IO.Path]::GetFullPath((Join-Path $RepoRoot ".."))
}

function Get-DeepSeekBuildingRoot {
    param([string]$RepoRoot = "")
    if ($env:DEEPSEEK_BUILD_DIR) {
        return [System.IO.Path]::GetFullPath($env:DEEPSEEK_BUILD_DIR)
    }
    $dsd = Get-DeepSeekDsdRoot -RepoRoot $RepoRoot
    return [System.IO.Path]::GetFullPath((Join-Path $dsd "DDbuilding\deepseek_desktop"))
}

function Get-DeepSeekPublishDir {
    param([string]$RepoRoot = "")
    if ($env:DEEPSEEK_PUBLISH_DIR) {
        return [System.IO.Path]::GetFullPath($env:DEEPSEEK_PUBLISH_DIR)
    }
    $dsd = Get-DeepSeekDsdRoot -RepoRoot $RepoRoot
    return [System.IO.Path]::GetFullPath((Join-Path $dsd "DDpublish"))
}

function Get-DeepSeekPublishExe {
    param([string]$RepoRoot = "", [string]$ExeName = "DeepSeek.exe")
    return Join-Path (Get-DeepSeekPublishDir -RepoRoot $RepoRoot) $ExeName
}
