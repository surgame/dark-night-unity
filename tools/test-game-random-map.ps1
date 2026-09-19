param([Parameter(Mandatory)][string]$PlayerPath, [int]$Port = 28600, [int]$ClientPort = 0, [switch]$Capture,
    [int]$Width = 1280, [int]$Height = 800)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$player = [IO.Path]::GetFullPath($PlayerPath)
if (!(Test-Path -LiteralPath $player)) { throw 'Build the formal Mono Player first.' }
if (!$ClientPort) { $ClientPort = $Port }
$run = Join-Path $repo ('artifacts/random-map/network-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$saves = Join-Path $run 'saves'
New-Item -ItemType Directory -Path $run | Out-Null
$processes = @{}
$checks = [ordered]@{}
$failure = $null
function Read-Report([string]$Role) {
    try {
        $stream = [IO.FileStream]::new((Join-Path $run "$Role.json"), [IO.FileMode]::Open, [IO.FileAccess]::Read,
            [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
        $reader = [IO.StreamReader]::new($stream)
        try { return $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    } catch { return $null }
}
function Wait-Report([string]$Role, [scriptblock]$Condition, [string]$Description = '') {
    $end = [DateTime]::UtcNow.AddSeconds(45)
    do {
        $value = Read-Report $Role
        if ($value -and $value.error) { throw "$Role reported: $($value.error)" }
        if ($value -and (& $Condition $value)) { return $value }
        $processes[$Role].Refresh()
        if ($processes[$Role].HasExited) { throw "$Role exited unexpectedly; see $run" }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $end)
    throw "Timeout waiting for $Role : $Description"
}
function Start-Player([string]$Role) {
    [IO.File]::WriteAllText((Join-Path $run "$Role.commands"), '')
    $arguments = @('-batchmode', '-screen-width', $Width, '-screen-height', $Height,
        '-logFile', ('"' + (Join-Path $run "$Role.log") + '"'), '--dn-role', $Role,
        '--dn-port', $(if ($Role -eq 'host') { $Port } else { $ClientPort }), '--dn-save-dir', ('"' + $saves + '"'),
        '--dn-report', ('"' + (Join-Path $run "$Role.json") + '"'), '--dn-commands', ('"' + (Join-Path $run "$Role.commands") + '"'))

    $processes[$Role] = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
}
function Send([string]$Role, [hashtable]$Command) {
    [IO.File]::AppendAllText((Join-Path $run "$Role.commands"), ($Command | ConvertTo-Json -Compress) + "`n")
}
function Consume([string]$Role, [hashtable]$Command) {
    $before = Wait-Report $Role { param($r) $true }
    Send $Role $Command
    return Wait-Report $Role { param($r) $r.commandsConsumed -gt $before.commandsConsumed } $Command.operation
}
function Receipt([string]$Role, [hashtable]$Command) {
    $before = Wait-Report $Role { param($r) $r.ready }
    $epoch = $before.frame.Epoch
    $connection = @($before.feedback | Where-Object ReadyReply | Select-Object -Last 1)[0].ConnectionGeneration
    $last = @($before.feedback | Where-Object { !$_.ReadyReply -and $_.Epoch -eq $epoch -and $_.ConnectionGeneration -eq $connection } | Sort-Object Sequence | Select-Object -Last 1)
    $sequence = if ($last.Count) { $last[0].Sequence } else { 0 }
    Send $Role $Command
    $after = Wait-Report $Role { param($r) @($r.feedback | Where-Object { !$_.ReadyReply -and $_.Epoch -eq $epoch -and $_.ConnectionGeneration -eq $connection -and $_.Sequence -gt $sequence }).Count -gt 0 } $Command.operation
    return @($after.feedback | Where-Object { !$_.ReadyReply -and $_.Epoch -eq $epoch -and $_.ConnectionGeneration -eq $connection -and $_.Sequence -gt $sequence } | Sort-Object Sequence)[-1]
}
function Actor($Report, [int]$Id) { return @($Report.frame.World.Actors | Where-Object Id -eq $Id)[0] }
function Ticks([int]$Count = 60) {
    $start = (Wait-Report 'host' { param($r) $r.ready }).frame.ServerTick
    return Wait-Report 'host' { param($r) $r.frame.ServerTick -ge $start + $Count } 'server tick boundary'
}
function Check([string]$Name, [bool]$Ok) { $checks[$Name] = $Ok; if (!$Ok) { throw "Failed: $Name" } }
try {
    Start-Player 'host'
    $h = Wait-Report 'host' { param($r) $r.ready -and $r.terrain.visible } 'host map and hero Ready'
    Check 'host_map_ready_before_controls' ($h.terrain.dataReady -and $h.terrain.sha256.Length -eq 64)
    Check 'cold_generation_under_two_seconds' ($h.terrain.generationMs -lt 2000)
    $digest = $h.terrain.sha256
    $hostActor = @($h.frame.World.Actors | Where-Object ControllerSlot -eq 0)[0].Id
    Start-Player 'client'
    $c = Wait-Report 'client' { param($r) $r.ready -and $r.terrain.visible } 'independent client map and hero Ready'
    Check 'all_61440_cells_match_independent_client' ($c.terrain.sha256 -eq $digest -and $c.terrain.seed -eq $h.terrain.seed)
    Check 'two_distinct_default_heroes' (@($c.frame.World.Actors | Where-Object ControllerSlot -ge 0).Count -eq 2)
    $null = Consume 'host' @{operation='capture';file='host-random-map.png'}
    $null = Consume 'client' @{operation='capture';file='client-random-map.png'}
    $lease = (Actor $h $hostActor).ControlLease
    $x = (Actor $h $hostActor).X
    $null = Consume 'host' @{operation='input-hold';actor=$hostActor;lease=$lease;horizontal=1;jumpPressed=$true}
    $h = Wait-Report 'host' {param($r) (Actor $r $hostActor).Height -gt 1} 'jump from authoritative terrain'
    Check 'jump_uses_terrain_support' ((Actor $h $hostActor).SupportPlatform -eq -1)
    $null = Ticks 90
    $null = Consume 'host' @{operation='input-stop'}
    $h = Wait-Report 'host' {param($r) (Actor $r $hostActor).SupportPlatform -eq 0} 'land on terrain'
    Check 'hero_moves_and_lands_on_map' ((Actor $h $hostActor).X -gt $x -and (Actor $h $hostActor).Height -eq 0)
    Check 'pause_applied' ((Receipt 'host' @{operation='SetPaused';value=1}).Code -eq 'Applied')
    $h = Wait-Report 'host' {param($r) $r.frame.Paused}
    $c = Wait-Report 'client' {param($r) $r.frame.Paused}
    $bytes = $h.terrain.sentBytes
    Start-Sleep -Seconds 3
    $h = Wait-Report 'host' {param($r) $r.ready}
    Check 'static_map_sends_no_more_bytes' ($h.terrain.sentBytes -eq $bytes)
    Start-Player 'late'
    $late = Wait-Report 'late' {param($r) $r.ready -and $r.terrain.visible} 'late join while paused'
    Check 'late_join_full_map_while_paused' ($late.terrain.sha256 -eq $digest -and $late.frame.Paused)
    $null = Receipt 'host' @{operation='Save';value=0}
    $h = Wait-Report 'host' {param($r) !$r.storageBusy -and (Test-Path -LiteralPath (Join-Path $saves 'v6/slot-00.dnsave.json'))} 'save v6 map'
    Check 'save_contains_final_map' (Test-Path -LiteralPath (Join-Path $saves 'v6/slot-00.dnsave.json'))
    $oldEpoch = $h.epoch
    $null = Consume 'host' @{operation='BeginLoad';value=0}
    $h = Wait-Report 'host' {param($r) $r.ready -and $r.epoch -gt $oldEpoch -and $r.terrain.epoch -eq $r.epoch} 'load new epoch'
    $c = Wait-Report 'client' {param($r) $r.ready -and $r.epoch -eq $h.epoch -and $r.terrain.visible} 'client loaded map'
    Check 'save_load_restores_map_and_resynchronizes' ($h.terrain.sha256 -eq $digest -and $c.terrain.sha256 -eq $digest)
    $null = Consume 'client' @{operation='disconnect'}
    $null = Wait-Report 'client' {param($r) !$r.ready}
    $null = Consume 'client' @{operation='connect'}
    $c = Wait-Report 'client' {param($r) $r.ready -and $r.terrain.visible} 'reconnect map'
    Check 'reconnect_restores_complete_map' ($c.terrain.sha256 -eq $digest)
    $oldEpoch = $h.epoch
    $null = Consume 'host' @{operation='Restart'}
    $h = Wait-Report 'host' {param($r) $r.ready -and $r.epoch -gt $oldEpoch -and $r.terrain.visible} 'restart'
    $c = Wait-Report 'client' {param($r) $r.ready -and $r.epoch -eq $h.epoch -and $r.terrain.visible}
    Check 'restart_keeps_selected_map' ($h.terrain.sha256 -eq $digest -and $c.terrain.sha256 -eq $digest)
    $null = Consume 'host' @{operation='capture';file='host-restarted.png'}
    $null = Consume 'host' @{operation='disconnect'}
    $null = Wait-Report 'host' {param($r) !$r.ready -and $r.uiPage -eq 'MainMenu'} 'return to menu'
    $null = Consume 'host' @{operation='ui';panel='MainMenu';key='Map'}
    Start-Sleep -Seconds 1
    $null = Consume 'host' @{operation='capture';file='menu-selected-map.png'}
    $generated = @(Select-String -LiteralPath (Join-Path $run 'host.log') -Pattern 'DARK_NIGHTS_MAP_GENERATED seed=')
    Check 'menu_map_button_generates_new_seed' ($generated.Count -eq 2)
    $null = Consume 'host' @{operation='ui';panel='MainMenu';key='NewGame'}
    $h = Wait-Report 'host' {param($r) $r.ready -and $r.terrain.visible} 'selected map new game'
    Check 'selected_new_map_is_used_in_game' ($h.terrain.sha256 -ne $digest -and $h.terrain.seed -ne $c.terrain.seed)
    Check 'new_game_reuses_prepared_map' (@(Select-String -LiteralPath (Join-Path $run 'host.log') -Pattern 'DARK_NIGHTS_MAP_GENERATED seed=').Count -eq 2)
    # A derived test save places the hero above real generated terrain beyond the protected camp.
    # This is isolated in this run's save directory; no frozen fixture or user save is edited.
    $hero = @($h.frame.World.Actors | Where-Object ControllerSlot -eq 0)[0].Id
    $null = Consume 'host' @{operation='Save';value=1}
    $fixturePath = Join-Path $saves 'v6/slot-01.dnsave.json'
    $null = Wait-Report 'host' {param($r) !$r.storageBusy -and (Test-Path -LiteralPath $fixturePath)} 'exploration fixture save'
    $fixture = [IO.File]::ReadAllText($fixturePath) | ConvertFrom-Json
    $cells = [Convert]::FromBase64String($fixture.world.terrain.materials)
    $column = 160; $row = 0
    while ($cells[$row * 320 + $column] -eq 0) { $row++ }
    $surface = 640 - $row * 16
    $actor = @($fixture.world.actors | Where-Object id -eq $hero)[0]
    $actor.x = $column * 16; $actor.height = $surface + 24
    $actor.support_platform = -1; $actor.vertical_speed = 0; $fixture.world.paused = $false
    [IO.File]::WriteAllText($fixturePath, ($fixture | ConvertTo-Json -Depth 64), [Text.UTF8Encoding]::new($false))
    $oldEpoch = $h.epoch
    $null = Consume 'host' @{operation='BeginLoad';value=1}
    $h = Wait-Report 'host' {param($r) $r.ready -and $r.epoch -gt $oldEpoch -and (Actor $r $hero).SupportPlatform -eq 0} 'land outside camp'
    Check 'hero_lands_on_generated_surface_beyond_camp' ((Actor $h $hero).Height -eq $surface -and (Actor $h $hero).X -eq $column * 16)
    Start-Sleep -Seconds 1
    $null = Consume 'host' @{operation='capture';file='generated-terrain-outside-camp.png'}
    # A bounded empty shaft above the last bedrock row verifies the lowest legal landing height.
    for ($y = 185; $y -lt 191; $y++) { for ($x = 158; $x -le 162; $x++) { $cells[$y * 320 + $x] = 0 } }
    $protection = [Convert]::FromBase64String($fixture.world.terrain.protection)
    for ($y = 185; $y -lt 191; $y++) { for ($x = 158; $x -le 162; $x++) { $protection[$y * 320 + $x] = 0 } }
    $fixture.world.terrain.materials = [Convert]::ToBase64String($cells)
    $fixture.world.terrain.protection = [Convert]::ToBase64String($protection)
    $actor.height = -2380; $actor.support_platform = -1
    [IO.File]::WriteAllText($fixturePath, ($fixture | ConvertTo-Json -Depth 64), [Text.UTF8Encoding]::new($false))
    $oldEpoch = $h.epoch
    $null = Consume 'host' @{operation='BeginLoad';value=1}
    $h = Wait-Report 'host' {param($r) $r.ready -and $r.epoch -gt $oldEpoch -and (Actor $r $hero).SupportPlatform -eq 0} 'bedrock floor landing'
    Check 'lowest_bedrock_landing_has_no_floating_gap' ((Actor $h $hero).Height -eq -2416)
}
catch { $failure = $_.Exception.ToString() }
finally {
    foreach ($process in $processes.Values) { $process.Refresh(); if (!$process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit() } }
    [ordered]@{passed=(!$failure);checks=$checks;error=$failure;artifacts=$run;player=$player;generationMs=$h.terrain.generationMs} |
        ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Random map network: passed=$(!$failure); checks=$($checks.Count); $run/result.json"
}
if ($failure) { throw $failure }
