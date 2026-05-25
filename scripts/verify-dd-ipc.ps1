param(
    [string]$PublishDir = "",
    [int]$TimeoutSec = 45
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "Get-PublishDir.ps1")
if (-not $PublishDir) { $PublishDir = Get-DeepSeekPublishDir -RepoRoot $root }

$bridgeExe = Join-Path $PublishDir "DeepSeek.Bridge.exe"
if (-not (Test-Path $bridgeExe)) {
    throw "missing DeepSeek.Bridge.exe — run: dotnet publish DeepSeek.DdBridge\DeepSeek.DdBridge.csproj -c Release -r win-x64 -o publish"
}

Write-Host "verify-dd-ipc: starting Bridge (--ipc-smoke, no WebView2)..."
$proc = Start-Process -FilePath $bridgeExe -ArgumentList "--ipc-smoke" -WorkingDirectory $PublishDir -PassThru -WindowStyle Hidden -Wait:$false

function Send-Line($writer, $obj) {
    $line = ($obj | ConvertTo-Json -Compress -Depth 8) + "`n"
    $writer.Write($line)
    $writer.Flush()
}

$pipeName = "dd-desktop-bridge"
$pipeDir = [System.IO.Pipes.PipeDirection]::InOut
$client = [System.IO.Pipes.NamedPipeClientStream]::new(".", $pipeName, $pipeDir)
$deadline = [datetime]::UtcNow.AddSeconds($TimeoutSec)
$connected = $false
while ([datetime]::UtcNow -lt $deadline -and -not $connected) {
    try {
        $client.Connect(2000)
        $connected = $true
    }
    catch {
        if ($proc.HasExited) { throw "Bridge exited early (code $($proc.ExitCode))" }
    }
}
if (-not $connected) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    throw "pipe connect timeout ($TimeoutSec s)"
}

$writer = New-Object System.IO.StreamWriter($client, [System.Text.UTF8Encoding]::new($false))
$writer.AutoFlush = $true
$reader = New-Object System.IO.StreamReader($client, [System.Text.UTF8Encoding]::new($false))

Send-Line $writer @{ channel = "control"; payload = @{ type = "ddReady" } }
Send-Line $writer @{ channel = "agent"; payload = @{ type = "nativeReady" } }
Send-Line $writer @{ channel = "agent"; payload = @{ type = "refreshLoginState" } }

$seenWorkMode = $false
$seenLogin = $false
$readDeadline = [datetime]::UtcNow.AddSeconds(25)
while ([datetime]::UtcNow -lt $readDeadline) {
    if ($client.CanRead) {
        $line = $reader.ReadLine()
        if ($line) {
            Write-Host "  <- $line"
            if ($line -match 'workModeState') { $seenWorkMode = $true }
            if ($line -match '"type"\s*:\s*"loginState"') { $seenLogin = $true }
            if ($seenWorkMode -and $seenLogin) { break }
        }
    }
    Start-Sleep -Milliseconds 200
    if ($proc.HasExited) { break }
}

$client.Close()
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Wait-Process -Id $proc.Id -ErrorAction SilentlyContinue

if (-not $seenWorkMode) { throw "verify-dd-ipc: no workModeState on agent channel (nativeReady echo)" }
if (-not $seenLogin) { throw "verify-dd-ipc: no loginState after refreshLoginState" }

Write-Host "verify-dd-ipc: PASS (workModeState + loginState)"
