param(
    [string]$DsdRoot = "C:\Users\xiaow\Desktop\DSD",
    [switch]$SkipClone
)

$ErrorActionPreference = "Stop"
$configPath = Join-Path $env:USERPROFILE ".deepseek\config.json"

# Prefer local *-main zip extracts; fall back to shallow clone names.
$candidates = @(
    @{ Label = "antigravity"; Main = "antigravity-awesome-skills-main"; Clone = "antigravity-awesome-skills"; Url = "https://github.com/sickn33/antigravity-awesome-skills.git" },
    @{ Label = "awesome-claude"; Main = "awesome-claude-skills-master"; Clone = "awesome-claude-skills"; Url = "https://github.com/ComposioHQ/awesome-claude-skills.git" }
)

function Resolve-Root($entry) {
    $main = Join-Path $DsdRoot $entry.Main
    if (Test-Path $main) { return $main }
    $clone = Join-Path $DsdRoot $entry.Clone
    if (Test-Path $clone) { return $clone }
    return $null
}

if (-not $SkipClone) {
    Set-Location $DsdRoot
    foreach ($entry in $candidates) {
        if (Resolve-Root $entry) { continue }
        Write-Host "[clone] $($entry.Label)..."
        git clone --depth 1 $entry.Url (Join-Path $DsdRoot $entry.Clone) 2>&1 | Out-Host
    }
}

$roots = @()
foreach ($entry in $candidates) {
    $path = Resolve-Root $entry
    if ($path) {
        $roots += $path
        Write-Host "[ok] $($entry.Label) -> $path"
    } else {
        Write-Warning "[missing] $($entry.Label) under $DsdRoot"
    }
}

if (-not $roots.Count) {
    Write-Host "No skill roots found. Extract zips to *-main folders or run without -SkipClone."
    exit 1
}

if (Test-Path $configPath) {
    try {
        $cfg = Get-Content $configPath -Raw | ConvertFrom-Json
        if (-not $cfg.PSObject.Properties["AgentSkillExtraRoots"]) {
            $cfg | Add-Member -NotePropertyName AgentSkillExtraRoots -NotePropertyValue @()
        }
        $cfg.AgentSkillExtraRoots = @($roots)
        $cfg | ConvertTo-Json -Depth 8 | Set-Content $configPath -Encoding UTF8
        Write-Host "Updated $configPath AgentSkillExtraRoots"
    }
    catch {
        Write-Warning "Could not patch config.json: $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "Roots (also default in AppConfig for new installs):"
$roots | ForEach-Object { Write-Host "  $_" }
Write-Host ""
Write-Host "Next: open Agent -> /skills reindex"
