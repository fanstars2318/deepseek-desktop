param(
    [string]$ConfigDir = ""
)

$ErrorActionPreference = "Stop"
if (-not $ConfigDir) {
    $ConfigDir = Join-Path $env:LOCALAPPDATA "deepseek_desktop"
}

$configPath = Join-Path $ConfigDir "config.json"
$providersPath = Join-Path $ConfigDir "api-providers.json"

if (-not (Test-Path $configPath)) {
    Write-Host "No config at $configPath — nothing to migrate."
    exit 0
}

$config = Get-Content $configPath -Raw | ConvertFrom-Json
$providers = @()

if (Test-Path $providersPath) {
    try {
        $providers = @(Get-Content $providersPath -Raw | ConvertFrom-Json)
    } catch {
        Write-Warning "Could not parse existing api-providers.json; will recreate."
        $providers = @()
    }
}

$hasBuiltin = $providers | Where-Object { $_.id -eq "deepseek-builtin" }
if (-not $hasBuiltin) {
    $providers += [ordered]@{
        id                 = "deepseek-builtin"
        displayName        = "DeepSeek（网页桥）"
        kind               = "builtin_web"
        routeMode          = "embedded_web"
        enabled            = $true
        defaultForAgent    = $true
        defaultForChat     = $true
        modelMappings      = @($config.modelMappings)
    }
}

$apiMode = $false
if ($config.PSObject.Properties.Name -contains "agentInferenceMode") {
    $apiMode = [string]$config.agentInferenceMode -eq "api"
}

if ($apiMode -or ($config.agentApiKey -and [string]$config.agentApiKey.Trim().Length -gt 0)) {
    $directId = "migrated-direct-api"
    $providers = @($providers | Where-Object { $_.id -ne $directId })
    $baseUrl = if ($config.agentApiBaseUrl) { $config.agentApiBaseUrl } else { $config.apiBaseUrl }
    $providers += [ordered]@{
        id              = $directId
        displayName     = "Migrated direct API (from DSD API settings)"
        kind            = "openai_compatible"
        routeMode       = "direct_api"
        baseUrl         = [string]$baseUrl
        enabled         = $true
        defaultForAgent = $apiMode
        defaultForChat  = $false
        models          = @([string]$config.model)
    }
    if ($apiMode) {
        $config.agentDefaultProviderId = $directId
    }
}

$providers | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 $providersPath
$config | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 $configPath

Write-Host "Migrated DSD API-related settings to $providersPath"
Write-Host "Set agentDefaultProviderId when agentInferenceMode=api."
