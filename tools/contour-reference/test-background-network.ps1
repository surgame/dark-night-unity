param([int]$Port = 28960, [switch]$Weak, [switch]$SmokeOnly)
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$player = Join-Path $repo 'artifacts/contour/player-mono-r3/DarkNights.exe'
$run = Join-Path $repo ('artifacts/contour/background-network-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + $(if ($Weak) {'-weak'} else {'-normal'}))
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
        '-logFile', ('"' + (Join-Path $run "$Role.log") + '"'), '--dn-contour-static', '--dn-role', $Role,
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
    $h = Wait-Report 'host' {param($r) $r.ready -and $r.terrain.visible} 'host Ready'
    $initialReference = $h.terrain.backgroundHash
    Start-Player 'client'
    $c = Wait-Report 'client' {param($r) $r.ready -and $r.terrain.visible} 'early client Ready'
    Check 'initial_reference_equal' ($initialReference -eq $c.terrain.backgroundHash -and $initialReference.Length -eq 64)
    $null = Receipt 'host' @{operation='Expedition';kind='depart'}
    $null = Receipt 'host' @{operation='SetPaused';value=1}
    $null = Receipt 'host' @{operation='Save';value=0}
    $save = Join-Path $saves 'v9/slot-00.dnsave.json'
    $h = Wait-Report 'host' {param($r) !$r.storageBusy -and (Test-Path -LiteralPath $save)} 'fixture save'
    $fixture = Get-Content -LiteralPath $save -Raw | ConvertFrom-Json
    $cells = [Convert]::FromBase64String($fixture.world.terrain.materials)
    $protection = [Convert]::FromBase64String($fixture.world.terrain.protection)
    $chosen = $null
    for ($y=45; $y -lt 105 -and !$chosen; $y++) { for ($x=55; $x -lt 155; $x++) {
        $i=$y*320+$x
        if ($cells[$i] -in 1,2,3,7 -and $protection[$i] -eq 0 -and $cells[$i-320] -eq 0 -and $cells[$i-640] -eq 0 -and $cells[$i-960] -eq 0) {
            $chosen=@{x=$x;y=$y}; break
        }
    } }
    if (!$chosen) { throw 'No suitable blast fixture floor' }
    $heroId = (Actor $h 0).Id
    $a = @($fixture.world.actors | Where-Object id -eq $heroId)[0]
    # Explicit test fixture changes only hero position and equipment; the initial reference and terrain stay untouched.
    $a.x = $chosen.x * 16
    $a.height = 632 - ($chosen.y - 1) * 16
    $a.vertical_speed=0; $a.support_platform=-1; $a.ignored_platform=0; $a.selected_item=2; $a.explosive_charges=3
    $fixture.world.paused=$false
    [IO.File]::WriteAllText($save, ($fixture|ConvertTo-Json -Depth 80), [Text.UTF8Encoding]::new($false))
    $epoch=$h.epoch; Send 'host' @{operation='BeginLoad';value=0}
    $h=Wait-Report 'host' {param($r) $r.ready -and $r.epoch -gt $epoch -and $r.terrain.visible} 'fixture applied'
    $c=Wait-Report 'client' {param($r) $r.ready -and $r.epoch -eq $h.epoch -and $r.terrain.visible} 'early client fixture'
    Check 'fixture_keeps_initial_reference' ($h.terrain.backgroundHash -eq $initialReference)
    Start-Sleep -Seconds 3
    $h=Read-Report 'host'; $beforeMap=$h.terrain.sha256; $beforeBuilds=$h.terrainPresentation.backgroundBuilds; $beforeRock=$h.terrainPresentation.rockBuilds
    $hero=Actor $h 0
    $throw=@{operation='input-raw';actor=$hero.Id;lease=$hero.ControlLease;sequence=1000000;observedTick=$h.frame.ServerTick;
        epoch=$h.epoch;selectionRevision=$hero.SelectionRevision;aimAngle=-90;usePressed=$true;useReleased=$true}
    Send 'host' $throw; Send 'host' $throw
    $h=Wait-Report 'host' {param($r) $r.terrain.sha256 -ne $beforeMap -and !$r.terrainPresentation.refreshing} 'actual fuse destroys foreground'
    $c=Wait-Report 'client' {param($r) $r.terrain.sha256 -eq $h.terrain.sha256 -and !$r.terrainPresentation.refreshing} 'blast converges'
    Check 'bomb_is_idempotent' ((Actor $h 0).ExplosiveCharges -eq 2)
    Check 'blast_keeps_reference' ($h.terrain.backgroundHash -eq $initialReference -and $c.terrain.backgroundHash -eq $initialReference)
    Check 'blast_does_not_rebuild_visible_background' ($h.terrainPresentation.backgroundBuilds -eq $beforeBuilds)
    $h=Wait-Report 'host' {param($r) $r.terrainPresentation.rockBuilds -gt $beforeRock -and $r.terrain.visible} 'rock surface catches up'
    Check 'blast_rebakes_current_rock_surface' ($h.terrainPresentation.rockBuilds -gt $beforeRock)
    $null=Receipt 'host' @{operation='SetPaused';value=1}
    $mapHash=$h.terrain.sha256
    Start-Player 'late'
    $late=Wait-Report 'late' {param($r) $r.ready -and $r.terrain.visible} 'late after blast'
    Check 'late_receives_destroyed_map' ($late.terrain.sha256 -eq $mapHash)
    Check 'late_receives_original_reference' ($late.terrain.backgroundHash -eq $initialReference)
    Send 'client' @{operation='disconnect'}
    $null=Wait-Report 'client' {param($r) !$r.ready}
    Send 'client' @{operation='connect'}
    $c=Wait-Report 'client' {param($r) $r.ready -and $r.terrain.visible}
    Check 'reconnect_receives_both_sources' ($c.terrain.sha256 -eq $mapHash -and $c.terrain.backgroundHash -eq $initialReference)
    Send 'host' @{operation='capture';file='background-after-blast.png'}
    $null=Receipt 'host' @{operation='Save';value=1}
    $save1=Join-Path $saves 'v9/slot-01.dnsave.json'
    $null=Wait-Report 'host' {param($r) !$r.storageBusy -and (Test-Path -LiteralPath $save1)}
    foreach($role in @($processes.Keys)) {Stop-Player $role}
    Start-Player 'host'
    $h=Wait-Report 'host' {param($r) $r.ready}; $epoch=$h.epoch
    Send 'host' @{operation='BeginLoad';value=1}
    $h=Wait-Report 'host' {param($r) $r.ready -and $r.epoch -gt $epoch -and $r.terrain.visible}
    Check 'restart_restores_destroyed_map' ($h.terrain.sha256 -eq $mapHash)
    Check 'restart_restores_initial_background' ($h.terrain.backgroundHash -eq $initialReference)
    foreach($role in @($processes.Keys)) {Stop-Player $role}
    foreach($log in Get-ChildItem -LiteralPath $run -Filter '*.log') {
        $text=[IO.File]::ReadAllText($log.FullName)
        Check ($log.BaseName+'_no_runtime_exception') ($text -notmatch '(?im)(Exception:|InvalidKeyException|MissingMethodException|TypeLoadException)')
    }
} catch { $failure=$_.Exception.ToString() }
finally {
    foreach($role in @($processes.Keys)) {Stop-Player $role}
    if($relay) { [IO.File]::WriteAllText($relayPath+'.stop',''); if(!$relay.WaitForExit(5000)){Stop-Process -Id $relay.Id} }
    [ordered]@{passed=(!$failure);checks=$checks;error=$failure;player=$player;fixture='Hero position and bomb equipment only; real input, fuse and authority destruction';
        artifacts=$run;playerSha256=(Get-FileHash -LiteralPath $player).Hash}|ConvertTo-Json -Depth 12|Set-Content (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Background network: passed=$(!$failure) checks=$($checks.Count) report=$run/result.json"
}
if($failure){throw $failure}
