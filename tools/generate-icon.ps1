$root = Split-Path $PSScriptRoot -Parent
$src = Join-Path $root "Assets\logo.png"
$out = Join-Path $root "Assets\deepseek.ico"
$tool = Join-Path $PSScriptRoot "IconGen"

dotnet run --project $tool -- $src $out
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
