# Delete fanstars2318/deepseek-edge on GitHub (requires delete_repo scope).
$ErrorActionPreference = "Stop"
$gh = "C:\Program Files\GitHub CLI\gh.exe"
if (-not (Test-Path $gh)) { throw "Install GitHub CLI: winget install GitHub.cli" }

Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue

Write-Host "Step 1: Authorize gh with delete_repo (browser will open) ..."
& $gh auth login -h github.com -p https -s delete_repo -w

Write-Host "Step 2: Deleting fanstars2318/deepseek-edge ..."
& $gh repo delete fanstars2318/deepseek-edge --yes

Write-Host "Done. Verifying ..."
try {
    & $gh repo view fanstars2318/deepseek-edge 2>$null
    throw "Repository still exists."
} catch {
    Write-Host "Repository deleted successfully."
}
