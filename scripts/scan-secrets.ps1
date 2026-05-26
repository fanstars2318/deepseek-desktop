# 扫描仓库中疑似硬编码密钥（发布前门禁）
param(
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"
if (-not $RepoRoot) { $RepoRoot = Split-Path -Parent $PSScriptRoot }
$RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)

$patterns = @(
    @{ Name = "aws-key"; Pattern = 'AKIA[0-9A-Z]{16}' },
    @{ Name = "bearer-token"; Pattern = 'Bearer\s+[A-Za-z0-9\-._~+/]+=*' },
    @{ Name = "apikey-assign"; Pattern = '(?i)(api[_-]?key|secret|token)\s*[:=]\s*["''][^"'']{12,}["'']' },
    @{ Name = "private-key"; Pattern = 'BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY' }
)

$excludeDirs = @(
    '\.git\', '\bin\', '\obj\', '\publish\', '\node_modules\',
    'Assets\dsd-api\', 'Assets\agent\dsd-api\', 'scripts\legacy\'
)

$hits = @()
Get-ChildItem -Path $RepoRoot -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object {
        $p = $_.FullName
        foreach ($ex in $excludeDirs) {
            if ($p -match [regex]::Escape($ex)) { return $false }
        }
        $_.Extension -match '\.(cs|js|ts|json|ps1|md|xaml|env|yaml|yml|toml)$'
    } |
    ForEach-Object {
        foreach ($rule in $patterns) {
            $m = Select-String -Path $_.FullName -Pattern $rule.Pattern -AllMatches -ErrorAction SilentlyContinue
            if ($m) {
                foreach ($line in $m) {
                    $text = $line.Line.Trim()
                    if ($text -match 'example|placeholder|YOUR_|fake|test-only|sample|///|Bearer\s*/|Bearer\s+Token') { continue }
                    if ($text -notmatch 'Bearer\s+[A-Za-z0-9\-._~+/]{20,}') { continue }
                    $hits += [pscustomobject]@{
                        Rule = $rule.Name
                        File = $line.Path
                        Line = $line.LineNumber
                    }
                }
            }
        }
    }

if ($hits.Count -gt 0) {
    $hits | Format-Table -AutoSize
    throw "scan-secrets: $($hits.Count) potential secret(s) — review before release."
}

Write-Host "scan-secrets: PASS (no matches)"
exit 0
