param([int]$BasePort = 28100)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$run = Join-Path $repo ('artifacts/migration/network-matrix-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $run | Out-Null
$results = [Collections.Generic.List[object]]::new()
$index = 0
$failure = $null
try {
    foreach ($rtt in @(0,100,200)) {
        foreach ($loss in @(0.0,0.01,0.05)) {
            $port = $BasePort + $index * 2
            $relayReport = Join-Path $run "relay-$index.json"
            $arguments = @(('"' + (Join-Path $PSScriptRoot 'lan-netem.py') + '"'), '--listen', ($port + 1), '--target', $port,
                '--loss', $loss.ToString([Globalization.CultureInfo]::InvariantCulture),
                '--delay', ($rtt / 2000.0).ToString([Globalization.CultureInfo]::InvariantCulture), '--jitter', '0.025',
                '--report', ('"' + $relayReport + '"'))
            $relay = Start-Process -FilePath 'python' -ArgumentList $arguments -WindowStyle Hidden -PassThru
            try {
                Start-Sleep -Milliseconds 500
                if ($relay.HasExited) { throw 'UDP relay failed to start.' }
                Write-Output "Network matrix: nominal RTT=$rtt ms, jitter=25 ms each direction, loss=$($loss * 100)%"
                & (Join-Path $PSScriptRoot 'test-game-recovery.ps1') -Port $port -ClientPort ($port + 1) -MinimumUptimeSeconds 5
                $latest = Get-ChildItem (Join-Path $repo 'artifacts/migration') -Directory -Filter 'recovery-*' | Sort-Object Name -Descending | Select-Object -First 1
                $case = Get-Content -LiteralPath (Join-Path $latest.FullName 'result.json') -Raw | ConvertFrom-Json
                if (!$case.passed) { throw "Recovery matrix case failed: $($latest.FullName)" }
                [IO.File]::WriteAllText($relayReport + '.stop', '')
                if (!$relay.WaitForExit(5000)) { Stop-Process -Id $relay.Id }
                $transport = Get-Content -LiteralPath $relayReport -Raw | ConvertFrom-Json
                if ($loss -gt 0 -and $transport.dropped -eq 0) { throw 'Configured packet loss was not exercised.' }
                if ($transport.reordered -eq 0) { throw 'Configured jitter did not exercise packet reordering.' }
                $results.Add([ordered]@{ rttMs=$rtt; oneWayJitterMs=25; loss=$loss; passed=$true; case=$case; transport=$transport })
            }
            finally { $relay.Refresh(); if (!$relay.HasExited) { Stop-Process -Id $relay.Id; $relay.WaitForExit() } }
            $index++
        }
    }
}
catch { $failure = $_.Exception.ToString() }
finally {
    [ordered]@{ passed=(!$failure -and $results.Count -eq 9); scope='Same Mono artifact; four processes; real UDP loss, jitter and reordering';
        results=$results.ToArray(); error=$failure; artifacts=$run } | ConvertTo-Json -Depth 15 | Set-Content (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Network matrix: completed=$($results.Count)/9; $run/result.json"
}
if ($failure) { throw $failure }
