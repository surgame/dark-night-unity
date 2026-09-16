param([Parameter(Mandatory)][string]$PlayerPath, [int]$Port = 28610, [switch]$CommandRings)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$run = Join-Path $repo ('artifacts/hero-input/weak-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $run | Out-Null
$relayReport = Join-Path $run 'relay.json'
$relay = $null
$failure = $null
$transport = $null
$case = $null
try {
    $relayArgs = @(('"' + (Join-Path $PSScriptRoot 'lan-netem.py') + '"'), '--listen', ($Port + 1), '--target', $Port,
        '--loss', '0.05', '--delay', '0.1', '--jitter', '0.025', '--report', ('"' + $relayReport + '"'))
    $relay = Start-Process -FilePath 'python' -ArgumentList $relayArgs -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 500
    if ($relay.HasExited) { throw 'UDP relay did not start.' }
    $scriptName = if ($CommandRings) { 'test-game-command-rings.ps1' } else { 'test-game-hero.ps1' }
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot $scriptName) -PlayerPath $PlayerPath -Port $Port -ClientPort ($Port + 1) 2>&1 |
        Tee-Object -FilePath (Join-Path $run 'case.log')
    $exitCode = $LASTEXITCODE
    $resultLine = Get-Content -LiteralPath (Join-Path $run 'case.log') | Where-Object { $_ -match '[/\\]result\.json' } | Select-Object -First 1
    if ($resultLine -notmatch '([A-Za-z]:[\\/].+[/\\]result\.json)') { throw 'Case result path was not reported.' }
    $case = Get-Content -LiteralPath $Matches[1] -Raw | ConvertFrom-Json
    [IO.File]::WriteAllText($relayReport + '.stop', '')
    if (!$relay.WaitForExit(5000)) { throw 'Relay did not finish its report.' }
    $transport = Get-Content -LiteralPath $relayReport -Raw | ConvertFrom-Json
    if ($exitCode -or !$case.passed) { throw "Weak-network case failed: $scriptName" }
    if ($transport.dropped -le 0 -or $transport.reordered -le 0) { throw 'Loss and reordering were not exercised.' }
}
catch { $failure = $_.Exception.ToString() }
finally {
    if ($relay) { $relay.Refresh(); if (!$relay.HasExited) { Stop-Process -Id $relay.Id; $relay.WaitForExit() } }
    [ordered]@{passed=(!$failure); scope='Same Mono code; independent Host/client; nominal RTT 200 ms, loss 5%, one-way jitter 25 ms';
        commandRings=[bool]$CommandRings; case=$case; transport=$transport; error=$failure; artifacts=$run} |
        ConvertTo-Json -Depth 16 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Hero network: passed=$(!$failure); $run/result.json"
}
if ($failure) { throw $failure }
