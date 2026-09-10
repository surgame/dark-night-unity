param([int]$Port = 17877, [switch]$WeakNetwork, [string]$Python = 'python')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$run = Join-Path $root ('artifacts/lan-sample/run-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $run | Out-Null
$player = Join-Path $root 'artifacts/lan-sample/player/LanCoop.exe'
$processes = @{}
$ids = @{}
$checks = [System.Collections.Generic.List[string]]::new()
$relay = $null
function Read-State($name) {
    $path = Join-Path $run "$name.json"
    if (!(Test-Path $path)) { return $null }
    try { $state = Get-Content -Raw $path | ConvertFrom-Json } catch { return $null }
    if ($state.error) { throw "$name Player error: $($state.error)" }
    return $state
}
function Wait-Check($label, [scriptblock]$predicate, [int]$seconds = 35) {
    $deadline = (Get-Date).AddSeconds($seconds)
    do {
        if ($relay -and $relay.HasExited) { throw 'UDP relay exited; see netem-error.log' }
        foreach ($processName in $processes.Keys) {
            if ($processes[$processName].HasExited) { throw "$processName exited before $label" }
            $null = Read-State $processName
        }
        if (& $predicate) { $checks.Add($label); Write-Output "PASS $label"; return }
        Start-Sleep -Milliseconds 100
    } while ((Get-Date) -lt $deadline)
    throw "Timeout: $label; reports: $run"
}
function Launch($name, $role) {
    $connectPort = if ($WeakNetwork -and $role -ne 'host') { $Port + 1 } else { $Port }
    $args = "-batchmode -nographics -sample-role $role -sample-port $connectPort -sample-report `"$run/$name.json`" -logFile `"$run/$name.log`""
    $processes[$name] = Start-Process $player -ArgumentList $args -WindowStyle Hidden -PassThru
    $ids[$name] = 0
}
function Input-Action($name, $action, [int]$entity = 1, [int]$fault = 0, [bool]$repeat = $false) {
    $ids[$name]++
    $path = Join-Path $run "$name.json.input"
    @{id=$ids[$name];action=$action;entity=$entity;fault=$fault;repeat=$repeat} |
        ConvertTo-Json | Set-Content -Path "$path.tmp" -Encoding utf8
    Move-Item -LiteralPath "$path.tmp" -Destination $path -Force
}
function Expect-Result($name, $action, $result, [int]$entity = 1, [int]$fault = 0, [bool]$repeat = $false) {
    $before = @((Read-State $name).results).Count
    Input-Action $name $action $entity $fault $repeat
    Wait-Check "$name $action -> $result" {
        $s = Read-State $name
        @($s.results).Count -gt $before -and $s.results[-1] -match ":${result}:"
    }
}
try {
    if ($WeakNetwork) {
        $relay = Start-Process $Python -ArgumentList "`"$PSScriptRoot/lan-netem.py`" --listen $($Port+1) --target $Port --report `"$run/netem.json`"" -WindowStyle Hidden -PassThru -RedirectStandardError "$run/netem-error.log"
        Start-Sleep -Milliseconds 300
    }
    Launch host host
    Wait-Check 'Host initial null -> projection -> Ready' { (Read-State host).ready }
    Launch client client
    Wait-Check 'Independent client initial snapshot while paused' { (Read-State client).ready -and (Read-State client).coins -eq 10 }
    Expect-Result client Buy InvalidEntity 999
    Expect-Result client Buy InvalidEntity 999 3
    Expect-Result client Pause HostOnly
    Expect-Result client Reset HostOnly
    Expect-Result client Buy ProtocolMismatch 1 1
    Expect-Result client Buy StaleEpoch 1 2
    Input-Action host Buy
    Input-Action client Buy
    Wait-Check 'Concurrent payment exactly once; Host agrees with client' {
        $h = Read-State host; $c = Read-State client
        $h.coins -eq 0 -and $c.coins -eq 0 -and $h.purchases -eq 1 -and $c.purchases -eq 1
    }
    Expect-Result host Buy DuplicateOrExpired 1 0 $true
    Input-Action host Claim
    Input-Action client Claim
    Wait-Check 'Shared worksite exclusive and converged' {
        $h = Read-State host; $c = Read-State client
        $h.occupant -gt 0 -and $h.occupant -eq $c.occupant -and
        @($h.results + $c.results | Where-Object { $_ -match ':Occupied:' }).Count -eq 1
    }
    Launch late client
    Launch fourth client
    Wait-Check 'Four independent Players; late join exact paused snapshot' {
        $h = Read-State host; $l = Read-State late; $f = Read-State fourth
        $l.ready -and $f.ready -and $l.coins -eq 0 -and $f.coins -eq 0 -and
        $l.occupant -eq $h.occupant -and $f.revision -eq $h.revision -and $l.ticks -eq 0
    }
    Input-Action client Disconnect
    Wait-Check 'Client disconnect clears Ready' { !(Read-State client).ready }
    Input-Action client Connect
    Wait-Check 'Reconnect new owned endpoint and fresh snapshot' { $s=Read-State client; $s.ready -and $s.joinCount -eq 2 -and $s.coins -eq 0 -and $s.senderCount -eq 1 }
    Expect-Result client Buy InsufficientFunds
    Expect-Result host Pause Accepted
    Wait-Check '60 Hz simulation advances on all clients' { (Read-State fourth).ticks -gt 30 }
    Expect-Result host Pause Accepted
    Wait-Check 'Pause converges without stopping network' { (Read-State fourth).paused -and (Read-State fourth).ticks -eq (Read-State host).ticks }
    Expect-Result late Buy InsufficientFunds
    # Reset can be followed by automatic Ready; assert world/epoch, not ordering of those two receipts.
    Input-Action host Reset
    Wait-Check 'Epoch reset, fresh Ready and atomic full state on four Players' {
        @('host','client','late','fourth' | Where-Object { $s=Read-State $_; !$s.ready -or $s.epoch -ne 2 -or $s.coins -ne 10 -or $s.purchases -ne 0 -or $s.occupant -ne -1 }).Count -eq 0
    }
    Expect-Result client Buy StaleEpoch 1 2
    Expect-Result host Buy Accepted
    Expect-Result host Buy DuplicateOrExpired 1 0 $true
    Wait-Check 'Host single execution after reset; all replicas converge' {
        @('host','client','late','fourth' | Where-Object { $s=Read-State $_; $s.coins -ne 0 -or $s.purchases -ne 1 }).Count -eq 0
    }
    Input-Action host Disconnect
    Wait-Check 'Host exit disconnects all peers and clears Ready' {
        @('client','late','fourth' | Where-Object { (Read-State $_).ready }).Count -eq 0
    }
    Input-Action host Connect
    Wait-Check 'Same-process Host restart creates one clean session' { $s=Read-State host; $s.ready -and $s.joinCount -eq 2 -and $s.epoch -eq 1 -and $s.coins -eq 10 -and $s.senderCount -eq 1 }
    Input-Action client Connect
    Wait-Check 'Client rejoins restarted Host without stale state or duplicate sender' { $s=Read-State client; $s.ready -and $s.joinCount -eq 3 -and $s.epoch -eq 1 -and $s.coins -eq 10 -and $s.senderCount -eq 1 }
    Expect-Result host Buy Accepted
    Wait-Check 'Restart retains exactly-once Host handling' { $s=Read-State client; $s.coins -eq 0 -and $s.purchases -eq 1 }
    $evidence = @{passed=$true;checks=$checks;utc=(Get-Date).ToUniversalTime().ToString('o');weakNetwork=[bool]$WeakNetwork;
        frameworkCommit='10b8f0ef6a5ed965ebd473dbcbe4a0dd795379c4';reports=@{}}
    foreach ($name in $processes.Keys) { $evidence.reports[$name] = Read-State $name }
    if ($WeakNetwork) {
        $netem = Get-Content -Raw "$run/netem.json" | ConvertFrom-Json
        if ($netem.dropped -eq 0 -or $netem.reordered -eq 0) { throw 'Network perturbation was not observed' }
        $evidence.netem = $netem
    }
    $evidence | ConvertTo-Json -Depth 10 | Set-Content "$run/result.json" -Encoding utf8
    Write-Output "EVIDENCE=$run/result.json"
} finally {
    foreach ($name in $processes.Keys) {
        if (!$processes[$name].HasExited) { Input-Action $name Quit }
    }
    if ($relay) { New-Item -ItemType File "$run/netem.json.stop" -Force | Out-Null }
    foreach ($process in $processes.Values) {
        if (!$process.WaitForExit(8000)) { Stop-Process -Id $process.Id }
    }
    if ($relay -and !$relay.WaitForExit(3000)) { Stop-Process -Id $relay.Id }
}
