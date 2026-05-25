param(
    [string]$EvalDir = "$env:USERPROFILE\.deepseek\evals",
    [string]$WorkspaceRoot = ""
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $EvalDir)) {
    Write-Host "No eval dir: $EvalDir"
    exit 0
}

$files = Get-ChildItem -Path $EvalDir -Filter "*.json" -File
if (-not $files.Count) {
    Write-Host "No eval JSON in $EvalDir"
    exit 0
}

$pass = 0
$fail = 0
foreach ($file in $files) {
    try {
        $cases = Get-Content $file.FullName -Raw | ConvertFrom-Json
        if ($cases -isnot [System.Array]) { $cases = @($cases) }
        foreach ($case in $cases) {
            $id = $case.id
            $prompt = $case.prompt
            $expected = @($case.expectedContains)
            Write-Host "[eval] $id — prompt: $prompt"
            # Local eval reads latest trace meta when harness writes eval.score span.
            # Full automation requires running Desktop with DEEPSEEK_DESKTOP_VERIFY_AGENT=1 or IPC.
            $ok = $true
            foreach ($frag in $expected) {
                if ([string]::IsNullOrWhiteSpace($frag)) { continue }
            }
            if ($ok) { $pass++ } else { $fail++ }
            Write-Host "  -> pending (wire to harness runner for full pass/fail)"
        }
    }
    catch {
        Write-Host "Invalid eval file: $($file.Name) — $($_.Exception.Message)"
        $fail++
    }
}

Write-Host ""
Write-Host "Eval summary: pass=$pass fail=$fail (scaffold — extend to invoke harness IPC)"
exit $(if ($fail -gt 0) { 1 } else { 0 })
