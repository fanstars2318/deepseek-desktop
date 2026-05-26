# Legacy entry — forwards to agent embed sync for dsd-api.
param([string]$Root = "")
& (Join-Path $PSScriptRoot "sync-agent-dsd-api.ps1") @PSBoundParameters
