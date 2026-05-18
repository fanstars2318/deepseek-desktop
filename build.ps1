$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$out = Join-Path $root "publish"

Push-Location $root
dotnet publish -c Release -r win-x64 --self-contained false -o $out
Pop-Location

$desktop = [Environment]::GetFolderPath("Desktop")
$target = Join-Path $desktop "DeepSeek-Edge"
if (Test-Path $target) { Remove-Item -Recurse -Force $target }
Copy-Item -Recurse $out $target

$exe = Join-Path $target "DeepSeek.exe"
$Wsh = New-Object -ComObject WScript.Shell
$lnk = Join-Path $desktop "DeepSeek-Edge.lnk"
$sc = $Wsh.CreateShortcut($lnk)
$sc.TargetPath = $exe
$sc.WorkingDirectory = $target
$ico = Join-Path $target "Assets\deepseek.ico"
if (Test-Path $ico) { $sc.IconLocation = "$ico,0" } else { $sc.IconLocation = "$exe,0" }
$sc.Description = "DeepSeek Browser"
$sc.Save()
Write-Host "Build OK: $exe"
