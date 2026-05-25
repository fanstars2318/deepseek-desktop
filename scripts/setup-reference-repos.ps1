# Map local DSD reference sources (*-main zip extracts). Optional shallow clone when missing.
param(
    [string]$TargetRoot = "C:\Users\xiaow\Desktop\DSD",
    [switch]$TryClone,
    [int]$MaxRetries = 2
)

$ErrorActionPreference = "Continue"

# Local folder name -> optional git URL for -TryClone
$repos = [ordered]@{
    "mem0-main"                        = "https://github.com/mem0ai/mem0.git"
    "langfuse-main"                    = "https://github.com/langfuse/langfuse.git"
    "opik-main"                        = "https://github.com/comet-ml/opik.git"
    "langchain-master"                 = "https://github.com/langchain-ai/langchain.git"
    "autogen-main"                     = "https://github.com/microsoft/autogen.git"
    "MetaGPT-main"                     = "https://github.com/FoundationAgents/MetaGPT.git"
    "camel-master"                     = "https://github.com/camel-ai/camel.git"
    "antigravity-awesome-skills-main"  = "https://github.com/sickn33/antigravity-awesome-skills.git"
    "awesome-claude-skills-master"     = "https://github.com/ComposioHQ/awesome-claude-skills.git"
    "AutoGPT-master"                   = "https://github.com/Significant-Gravitas/AutoGPT.git"
    "awesome-llm-apps-main"            = "https://github.com/Shubhamsaboo/awesome-llm-apps.git"
    "deepcode-cli-main"                = "https://github.com/lessweb/deepcode-cli.git"
    "DeepSeek-TUI-main"                = "https://github.com/Hmbown/CodeWhale.git"
}

if (-not (Test-Path $TargetRoot)) {
    New-Item -ItemType Directory -Path $TargetRoot -Force | Out-Null
}

$present = @()
$missing = @()

foreach ($entry in $repos.GetEnumerator()) {
    $dest = Join-Path $TargetRoot $entry.Key
    if (Test-Path $dest) {
        $present += $entry.Key
        Write-Host "[ok] $($entry.Key)"
        continue
    }
    $missing += $entry.Key
    if (-not $TryClone) { continue }

    $ok = $false
    for ($i = 1; $i -le $MaxRetries; $i++) {
        Write-Host "[clone] $($entry.Key) (attempt $i)..."
        git clone --depth 1 $entry.Value $dest 2>&1 | Out-Host
        if ($LASTEXITCODE -eq 0 -and (Test-Path $dest)) { $ok = $true; $present += $entry.Key; break }
        if (Test-Path $dest) { Remove-Item -Recurse -Force $dest -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
    }
}

Write-Host ""
Write-Host "Present ($($present.Count)): $($present -join ', ')"
if ($missing.Count -and -not $TryClone) {
    Write-Host "Missing ($($missing.Count)): $($missing -join ', ') — extract zips or re-run with -TryClone"
}
if ($TryClone -and $missing.Count) {
    $still = $missing | Where-Object { -not (Test-Path (Join-Path $TargetRoot $_)) }
    if ($still.Count) {
        Write-Host "Still missing: $($still -join ', ')"
        exit 1
    }
}
