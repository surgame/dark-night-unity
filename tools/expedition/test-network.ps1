param([int]$Port = 28940, [switch]$Weak, [switch]$SmokeOnly)
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$player = Join-Path $repo 'artifacts/expedition/player-mono/DarkNights.exe'
$run = Join-Path $repo ('artifacts/expedition/network-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + $(if ($Weak) {'-weak'} else {'-normal'}))
$saves = Join-Path $run 'saves'
New-Item -ItemType Directory -Path $run -Force | Out-Null
$processes = @{}; $checks = [ordered]@{}; $failure = $null; $relay = $null
function Read-Report([string]$Role) {
    for ($retry = 0; $retry -lt 10; $retry++) {
        try { return [IO.File]::ReadAllText((Join-Path $run "$Role.json")) | ConvertFrom-Json } catch { Start-Sleep -Milliseconds 50 }
    }
    return $null
}
function Wait-Report([string]$Role, [scriptblock]$Condition, [string]$Description = '') {
    $end = [DateTime]::UtcNow.AddSeconds(60)
    do {
        $value = Read-Report $Role
        if ($value -and $value.error) { throw "$Role reported: $($value.error)" }
        if ($value -and (& $Condition $value)) { return $value }
        if ($processes.ContainsKey($Role)) {
            $processes[$Role].Refresh()
            if ($processes[$Role].HasExited) { throw "$Role exited unexpectedly" }
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $end)
    throw "Timeout waiting for $Role : $Description"
}
function Start-Player([string]$Role) {
    $report = Join-Path $run "$Role.json"
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
    [IO.File]::WriteAllText((Join-Path $run "$Role.commands"), '')
    $endpoint = if ($Role -eq 'host' -or !$Weak) { $Port } else { $Port + 1 }
    $arguments = @('-batchmode', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
        '-logFile', ('"' + (Join-Path $run "$Role.log") + '"'), '--dn-role', $Role,
        '--dn-port', $endpoint, '--dn-map-seed', 'EXPEDITION-QUICK-01', '--dn-save-dir', ('"' + $saves + '"'),
        '--dn-report', ('"' + $report + '"'), '--dn-commands', ('"' + (Join-Path $run "$Role.commands") + '"'))
    if ($Role -ne 'host') { $arguments += '-nographics' }
    $processes[$Role] = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
}
function Stop-Player([string]$Role) {
    if (!$processes.ContainsKey($Role)) { return }
    $processes[$Role].Refresh()
    if (!$processes[$Role].HasExited) { Stop-Process -Id $processes[$Role].Id; $processes[$Role].WaitForExit() }
    $processes.Remove($Role)
}
function Send([string]$Role, [hashtable]$Command) {
    [IO.File]::AppendAllText((Join-Path $run "$Role.commands"), ($Command | ConvertTo-Json -Compress) + "`n")
}
function Receipt([string]$Role, [hashtable]$Command) {
    $before = Wait-Report $Role {param($r) $r.ready}
    $epoch = $before.epoch
    $connection = @($before.feedback | Where-Object ReadyReply | Select-Object -Last 1)[0].ConnectionGeneration
    $last = @($before.feedback | Where-Object { !$_.ReadyReply -and $_.Epoch -eq $epoch -and $_.ConnectionGeneration -eq $connection } | Sort-Object Sequence | Select-Object -Last 1)
    $sequence = if ($last.Count) { $last[0].Sequence } else { 0 }
    Send $Role $Command
    $after = Wait-Report $Role {param($r) @($r.feedback | Where-Object { !$_.ReadyReply -and $_.Epoch -eq $epoch -and $_.ConnectionGeneration -eq $connection -and $_.Sequence -gt $sequence }).Count -gt 0} $Command.operation
    return @($after.feedback | Where-Object { !$_.ReadyReply -and $_.Epoch -eq $epoch -and $_.ConnectionGeneration -eq $connection -and $_.Sequence -gt $sequence } | Sort-Object Sequence)[-1]
}
function Check([string]$Name, [bool]$Ok) { $checks[$Name] = $Ok; if (!$Ok) { throw "Failed: $Name" } }
function Actor($r, [int]$Slot) { return @($r.frame.World.Actors | Where-Object ControllerSlot -eq $Slot)[0] }
function Crew($r, [int]$Slot) { return @($r.frame.World.Expedition.Crew | Where-Object OwnerSlot -eq $Slot)[0] }
try {
    if ($Weak) {
        $relayPath = Join-Path $run 'relay.json'
        $relay = Start-Process 'python' -WindowStyle Hidden -PassThru -ArgumentList @(
            ('"' + (Join-Path $repo 'tools/lan-netem.py') + '"'), '--listen', ($Port + 1), '--target', $Port,
            '--loss', '0.05', '--delay', '0.1', '--jitter', '0.025', '--duration', '600', '--report', ('"' + $relayPath + '"'))
    }
    Start-Player 'host'
    $h = Wait-Report 'host' {param($r) $r.ready -and $r.terrain.visible} 'formal cave Ready'
    Check 'formal_expediton_ready' ($h.frame.World.Expedition.Phase -eq 0)
    Start-Player 'client'; Start-Player 'guest'
    $c = Wait-Report 'client' {param($r) $r.ready -and $r.terrain.visible} 'client Ready'
    $null = Wait-Report 'guest' {param($r) $r.ready} 'third player Ready'
    Check 'host_client_same_map' ($h.terrain.sha256 -eq $c.terrain.sha256)
    Check 'guest_cannot_depart' ((Receipt 'client' @{operation='Expedition';kind='depart'}).Code -eq 'PermissionDenied')
    Check 'host_depart_once' ((Receipt 'host' @{operation='Expedition';kind='depart'}).Code -eq 'Applied')
    Start-Player 'late'
    $late = Wait-Report 'late' {param($r) $r.ready -and $r.terrain.visible} 'late fourth player'
    $h = Wait-Report 'host' {param($r) $r.readyCount -eq 4} 'four Ready'
    Check 'late_join_active_expedition' ($late.frame.World.Expedition.Phase -eq 1 -and $late.terrain.sha256 -eq $h.terrain.sha256)
    if (!$SmokeOnly) {
        $hero = Actor $h 0
        $deposit = @($h.frame.World.Worksites | Where-Object IsMineralDeposit | Sort-Object Id)[0]
        Send 'host' @{operation='input-hold';actor=$hero.Id;horizontal=1}
        $h = Wait-Report 'host' {param($r) (Actor $r 0).X -ge $deposit.X - 20} 'walk to starter ore'
        Send 'host' @{operation='input-hold';actor=$hero.Id;horizontal=0}
        $null = Receipt 'host' @{operation='SelectHeroItem';actors=@($hero.Id);value=1;lease=$hero.ControlLease}
        $null = Wait-Report 'host' {param($r) (Actor $r 0).SelectedItem -eq 1} 'pickaxe selection'
        for ($i=0; $i -lt 10; $i++) {
            $hero = Actor (Wait-Report 'host' {param($r) $r.ready}) 0
            $reply = Receipt 'host' @{operation='UseHeroItem';actors=@($hero.Id);target=$deposit.Id;kind='pickaxe';value=$hero.SelectionRevision;lease=$hero.ControlLease}
            Check "real_mining_$i" ($reply.Code -eq 'Applied')
            Start-Sleep -Milliseconds 550
        }
        $h = Wait-Report 'host' {param($r) (Crew $r 0).Iron -eq 10} 'ten actual mined ore'
        Check 'mine_does_not_credit_global_stock' ($h.frame.World.Camp.Stock.Iron -eq 0)
        Send 'host' @{operation='capture';file='cave-mining.png'}
        Send 'host' @{operation='input-hold';actor=$hero.Id;horizontal=-1}
        $end = [DateTime]::UtcNow.AddSeconds(30)
        do {
            Send 'host' @{operation='input';actor=$hero.Id;horizontal=-1;jumpPressed=$true}
            Start-Sleep -Milliseconds 800
            $h = Read-Report 'host'
        } while ((Actor $h 0).X -gt 650 -and [DateTime]::UtcNow -lt $end)
        Send 'host' @{operation='input-hold';actor=$hero.Id;horizontal=0}
        Check 'finite_fuel_return_to_ship' ((Actor $h 0).X -lt 655)
        $null = Wait-Report 'host' {param($r) [Math]::Abs((Actor $r 0).Height) -lt 20} 'land near ship'
        $null = Receipt 'host' @{operation='Expedition';kind='unload';actors=@($hero.Id);lease=$hero.ControlLease}
        $h = Wait-Report 'host' {param($r) (Crew $r 0).Iron -eq 0} 'actual unload'
        Check 'cargo_transferred_to_ship_once' (($h.frame.World.Expedition.Devices | Measure-Object Iron -Sum).Sum -eq 10)
    }
    $before = Crew (Wait-Report 'client' {param($r) $r.ready}) $c.slot
    Send 'client' @{operation='disconnect'}
    $null = Wait-Report 'client' {param($r) !$r.ready} 'disconnected'
    Send 'client' @{operation='connect'}
    $c = Wait-Report 'client' {param($r) $r.ready} 'reconnected'
    Check 'reconnect_same_cargo_owner' ((Crew $c $c.slot).Id -eq $before.Id)
    Check 'recall_applied' ((Receipt 'host' @{operation='Expedition';kind='recall'}).Code -eq 'Applied')
    foreach ($role in @('host','client','guest','late')) {
        $r = Wait-Report $role {param($v) $v.ready}
        $a = Actor $r $r.slot
        Check ($role + '_board') ((Receipt $role @{operation='Expedition';kind='board';actors=@($a.Id);lease=$a.ControlLease}).Code -eq 'Applied')
    }
    Check 'normal_launch_applied' ((Receipt 'host' @{operation='Expedition';kind='launch'}).Code -eq 'Applied')
    $h = Wait-Report 'host' {param($r) $r.frame.World.Expedition.Settled} 'atomic settlement'
    $amount = if ($SmokeOnly) {0} else {10}
    Check 'settled_stock_exactly_once' ($h.frame.World.Camp.Stock.Iron -eq $amount)
    Check 'automatic_save_exists' (Test-Path -LiteralPath (Join-Path $saves 'v8/slot-09.dnsave.json'))
    if (!$SmokeOnly) {
        Check 'robot_module_purchase' ((Receipt 'host' @{operation='Expedition';kind='robot'}).Code -eq 'Applied')
        $null = Receipt 'host' @{operation='Expedition';kind='depart'}
        $h = Wait-Report 'host' {param($r) @($r.frame.World.Expedition.Devices | Where-Object Stage -eq 3).Count -eq 5} 'robot deploys four devices'
        Check 'second_expedition_has_real_deployment' ($h.frame.World.Expedition.Run -eq 2)
        Send 'host' @{operation='capture';file='ship-deployed.png'}
    }
    $null = Receipt 'host' @{operation='SetPaused';value=1}
    $null = Receipt 'host' @{operation='Save';value=0}
    $null = Wait-Report 'host' {param($r) !$r.storageBusy -and (Test-Path -LiteralPath (Join-Path $saves 'v8/slot-00.dnsave.json'))} 'disk save'
    $digest = $h.terrain.sha256; $runNumber = $h.frame.World.Expedition.Run
    foreach ($role in @($processes.Keys)) { Stop-Player $role }
    Start-Player 'host'
    $h = Wait-Report 'host' {param($r) $r.ready} 'new process'
    $epoch = $h.epoch; Send 'host' @{operation='BeginLoad';value=0}
    $h = Wait-Report 'host' {param($r) $r.ready -and $r.epoch -gt $epoch} 'disk restore after process restart'
    Check 'restart_restores_final_map' ($h.terrain.sha256 -eq $digest)
    Check 'restart_restores_run_and_modules' ($h.frame.World.Expedition.Run -eq $runNumber -and ($SmokeOnly -or $h.frame.World.Expedition.RobotModule -eq 1))
    foreach ($role in @($processes.Keys)) { Stop-Player $role }
    foreach ($log in Get-ChildItem -LiteralPath $run -Filter '*.log') {
        $text = [IO.File]::ReadAllText($log.FullName)
        Check ($log.BaseName + '_no_runtime_exception') ($text -notmatch '(?im)(Exception:|InvalidKeyException|MissingMethodException|TypeLoadException)')
    }
} catch { $failure = $_.Exception.ToString() }
finally {
    foreach ($role in @($processes.Keys)) { Stop-Player $role }
    if ($relay) {
        [IO.File]::WriteAllText($relayPath + '.stop', '')
        if (!$relay.WaitForExit(5000)) { Stop-Process -Id $relay.Id }
    }
    [ordered]@{passed=(!$failure);checks=$checks;error=$failure;player=$player;weak=[bool]$Weak;smokeOnly=[bool]$SmokeOnly;
        artifacts=$run;playerSha256=(Get-FileHash -LiteralPath $player).Hash} | ConvertTo-Json -Depth 12 |
        Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Expedition network: passed=$(!$failure) checks=$($checks.Count) report=$run/result.json"
}
if ($failure) { throw $failure }
