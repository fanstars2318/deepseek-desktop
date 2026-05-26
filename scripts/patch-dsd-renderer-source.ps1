param(
    [Parameter(Mandatory = $true)]
    [string]$RendererSource
)

$ErrorActionPreference = "Stop"
$rendererRoot = Join-Path $RendererSource "src\renderer"
if (-not (Test-Path $rendererRoot)) {
    throw "Renderer source not found: $rendererRoot"
}

$replacements = @(
    @('Chat2API XML', 'DeepSeek Desktop XML'),
    @('Chat2API', 'DeepSeek Desktop'),
    @('Chat2Api', 'DeepSeek Desktop'),
    @('chat2api-settings', 'dsd-desktop-api-settings'),
    @('chat2api-config', 'dsd-api-config')
)

$count = 0
$files = Get-ChildItem -Path $rendererRoot -Recurse -File -Include *.tsx, *.ts, *.json, *.html -ErrorAction SilentlyContinue
foreach ($file in $files) {
        $text = [IO.File]::ReadAllText($file.FullName)
        $orig = $text
        foreach ($pair in $replacements) {
            $text = $text.Replace($pair[0], $pair[1])
        }
        if ($text -ne $orig) {
            [IO.File]::WriteAllText($file.FullName, $text)
            $count++
        }
    }

Write-Host "  OK patched $count renderer source files for DeepSeek Desktop (DPDT) branding"
