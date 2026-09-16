param([int]$Port = 28740, [int]$ClientPort = 0)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$player = Join-Path $root 'artifacts/terrain/player-mono-r4/TerrainTest.exe'
$run = Join-Path $root ('artifacts/terrain/network-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $run | Out-Null
$processes = @{}
$reports = @{}
if ($ClientPort -eq 0) { $ClientPort = $Port }
try {
    foreach ($role in @('host','client')) {
        $endpoint = if ($role -eq 'client') { $ClientPort } else { $Port }
        $arguments = '-batchmode -nographics --dn-terrain-role {1} --dn-terrain-port {2} --dn-terrain-report "{0}/{1}.json" -logFile "{0}/{1}.log"' -f $run,$role,$endpoint
        $processes[$role] = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
        if ($role -eq 'host') { Start-Sleep -Seconds 2 }
    }
    $deadline = (Get-Date).AddSeconds(95)
    do {
        foreach ($role in @('host','client')) {
            if ($processes[$role].HasExited) { throw "$role exited unexpectedly." }
            $path = Join-Path $run "$role.json"
            if (Test-Path $path) {
                try { $reports[$role] = Get-Content $path -Raw | ConvertFrom-Json } catch { continue }
                if ($reports[$role].Failure) { throw $reports[$role].Failure }
            }
        }
        if ($reports.host.Success -and $reports.client.Success) { break }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    if (!$reports.host.Success -or !$reports.client.Success) { throw 'Terrain network acceptance timed out.' }
    # Wait for the final commit / reconnect traffic to settle, with a bounded deadline.
    $stableDeadline = (Get-Date).AddSeconds(15)
    $stableSince = Get-Date
    $settled = Get-Content (Join-Path $run 'host.json') -Raw | ConvertFrom-Json
    do {
        Start-Sleep -Milliseconds 500
        try { $sample = Get-Content (Join-Path $run 'host.json') -Raw | ConvertFrom-Json } catch { continue }
        if ($sample.Scans -ne $settled.Scans -or $sample.Bytes -ne $settled.Bytes) { $stableSince = Get-Date }
        $settled = $sample
        if (((Get-Date) - $stableSince).TotalSeconds -ge 3) { break }
    } while ((Get-Date) -lt $stableDeadline)
    if (((Get-Date) - $stableSince).TotalSeconds -lt 3) { throw 'Terrain stream did not settle within 15 seconds.' }
    $settled | ConvertTo-Json | Set-Content (Join-Path $run 'idle-baseline.json') -Encoding utf8
    $baseline = $settled.Scans
    Start-Sleep -Seconds 3
    $final = Get-Content (Join-Path $run 'host.json') -Raw | ConvertFrom-Json
    $final | ConvertTo-Json | Set-Content (Join-Path $run 'idle-final.json') -Encoding utf8
    if ($final.Scans -ne $baseline) { throw 'Static map caused new publication scans.' }
    if ($final.Bytes -ne $settled.Bytes) { throw 'Static map sent new terrain bytes.' }
    $result = @{ success=$true; checks=@('real-host-client','authenticated-YYGC-command','duplicate-rejected','illegal-target-rejected','two-single-commits','client-reconnect','current-map-after-reconnect','host-single-execution','idle-no-publication-scan','idle-no-terrain-bytes'); host=$final; client=$reports.client; idleStart=$settled; idleSeconds=3 }
    $result | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $run 'result.json') -Encoding utf8
    Write-Output ("PASS {0} checks; {1}" -f $result.checks.Count, $run)
} catch {
    @{success=$false; failure=$_.Exception.ToString(); reports=$reports} | ConvertTo-Json -Depth 8 |
        Set-Content (Join-Path $run 'result.json') -Encoding utf8
    throw
} finally {
    foreach ($role in $processes.Keys) { New-Item -ItemType File -Force (Join-Path $run "$role.json.stop") | Out-Null }
    foreach ($process in $processes.Values) {
        if (!$process.HasExited -and !$process.WaitForExit(5000)) { Stop-Process -Id $process.Id }
    }
}
