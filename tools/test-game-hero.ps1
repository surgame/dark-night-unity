param([Parameter(Mandatory)][string]$PlayerPath, [int]$Port = 28600, [int]$ClientPort = 0, [switch]$Capture,
    [int]$Width = 1280, [int]$Height = 800)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$player = [IO.Path]::GetFullPath($PlayerPath)
if (!(Test-Path -LiteralPath $player)) { throw 'Build the formal Mono Player first.' }
if (!$ClientPort) { $ClientPort = $Port }
$run = Join-Path $repo ('artifacts/hero-input/network-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$saves = Join-Path $run 'saves'
New-Item -ItemType Directory -Path $run | Out-Null
$processes = @{}
$checks = [ordered]@{}
$flight = [Collections.Generic.List[object]]::new()
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
    if (!$Capture) { $arguments = @('-nographics') + $arguments }
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
    $h = Wait-Report 'host' { param($r) $r.ready -and @($r.frame.World.Actors | Where-Object ControllerSlot -eq 0).Count -eq 1 } 'default hero'
    $hostReadyActorIds = @($h.frame.World.Actors | ForEach-Object Id)
    Start-Player 'client'
    $c = Wait-Report 'client' { param($r) $r.ready -and @($r.frame.World.Actors | Where-Object ControllerSlot -eq 1).Count -eq 1 } 'guest hero'
    $hostActor = @($h.frame.World.Actors | Where-Object ControllerSlot -eq 0)[0].Id
    $guestActor = @($c.frame.World.Actors | Where-Object ControllerSlot -eq 1)[0].Id
    Check 'default_mode_claims_distinct_heroes' ($hostActor -ne $guestActor)
    Check 'guest_ready_spawns_a_new_villager' ($guestActor -notin $hostReadyActorIds -and $c.frame.World.Actors.Count -eq $hostReadyActorIds.Count + 1)
    if ($Capture) {
        $null = Consume 'host' @{operation='capture';file='hero-default.png'}
        $null = Consume 'host' @{operation='ui';panel='Chrome';key='Help'}
        $null = Wait-Report 'host' { param($r) $r.uiPage -eq 'Help' }
        $null = Consume 'host' @{operation='capture';file='hero-help.png'}
        $null = Consume 'host' @{operation='ui';panel='Help';key='Back'}
        $null = Wait-Report 'host' { param($r) $r.uiPage -eq '' }
        Check 'native_hero_and_current_controls_guide_captured' ((Test-Path -LiteralPath (Join-Path $run 'hero-default.png')) -and
            (Test-Path -LiteralPath (Join-Path $run 'hero-help.png')))
    }
    $null = Consume 'host' @{operation='hero-mode';value=0}
    $null = Consume 'client' @{operation='hero-mode';value=0}
    $h = Wait-Report 'host' { param($r) @($r.frame.World.Actors | Where-Object { $_.ControllerSlot -ge 0 }).Count -eq 0 }
    Check 'host_can_release_preserved_camp_backend' ((Actor $h $hostActor).ControllerSlot -eq -1)
    Check 'guest_can_release_preserved_camp_backend' ((Actor $h $guestActor).ControllerSlot -eq -1)
    Check 'camp_mode_has_no_orphaned_owner' $true
    Check 'host_claims_one_actor' ((Receipt 'host' @{operation='ClaimHero';actors=@($hostActor)}).Code -eq 'Applied')
    Check 'contested_actor_rejected' ((Receipt 'client' @{operation='ClaimHero';actors=@($hostActor)}).Code -eq 'NoEffect')
    Check 'guest_claims_another_actor' ((Receipt 'client' @{operation='ClaimHero';actors=@($guestActor)}).Code -eq 'Applied')
    Check 'one_actor_per_connection' ((Receipt 'client' @{operation='ClaimHero';actors=@(17)}).Code -eq 'NoEffect')
    $h = Wait-Report 'host' { param($r) (Actor $r $hostActor).ControllerSlot -eq 0 -and (Actor $r $guestActor).ControllerSlot -eq 1 }
    $hostLease = (Actor $h $hostActor).ControlLease; $guestLease = (Actor $h $guestActor).ControlLease
    $startX = (Actor $h $hostActor).X
    $null = Consume 'client' @{operation='input-raw';actor=$hostActor;lease=$hostLease;sequence=1000;horizontal=1}
    $h = Ticks 60
    Check 'foreign_connection_input_rejected' ((Actor $h $hostActor).X -eq $startX)
    Check 'camp_orders_cannot_override_hero' ((Receipt 'client' @{operation='IssueOrders';actors=@($hostActor);x=800}).Code -eq 'InvalidRequest')
    $null = Consume 'host' @{operation='input-hold';actor=$hostActor;lease=$hostLease;horizontal=1}
    $h = Wait-Report 'host' { param($r) (Actor $r $hostActor).X -ge $startX + 18 } 'continuous movement'
    Check 'continuous_input_advances_original_movement' ((Actor $h $hostActor).Walking)
    $null = Consume 'host' @{operation='input-stop'}
    $h = Ticks 90; $stopped = (Actor $h $hostActor).X; $h = Ticks 60
    Check 'missing_input_times_out_without_a_neutral_packet' ((Actor $h $hostActor).X -eq $stopped)

    $null = Consume 'client' @{operation='input';actor=$guestActor;lease=$guestLease;jumpPressed=$true}
    $null = Wait-Report 'host' { param($r) (Actor $r $guestActor).Height -gt 0 } 'short jump via remote Gateway'
    Check 'remote_short_jump_survives_released_held_state' $true
    $h = Wait-Report 'host' { param($r) (Actor $r $guestActor).SupportPlatform -gt 0 } 'landing on one-way platform'
    # 场景 Transform 换算到玩法像素存在 float 尾数；仍要求正确支撑与零垂直速度。
    Check 'jump_lands_on_authored_platform' ([Math]::Abs((Actor $h $guestActor).Height - 12) -lt 0.001 -and
        (Actor $h $guestActor).SupportPlatform -eq 1 -and (Actor $h $guestActor).VerticalSpeed -eq 0)
    $null = Consume 'client' @{operation='input';actor=$guestActor;lease=$guestLease;dropPressed=$true}
    $null = Wait-Report 'host' { param($r) (Actor $r $guestActor).Height -eq 0 -and (Actor $r $guestActor).SupportPlatform -eq 0 } 'S drops through support'
    Check 'drop_through_returns_to_ground' $true
    $null = Receipt 'client' @{operation='SelectHeroItem';actors=@($guestActor);lease=$guestLease;value=2}
    $c = Wait-Report 'client' { param($r) (Actor $r $guestActor).SelectedItem -eq 2 }
    $selection = (Actor $c $guestActor).SelectionRevision
    Check 'stale_item_selection_rejected' ((Receipt 'client' @{operation='UseHeroItem';actors=@($guestActor);lease=$guestLease;kind='jetpack';value=($selection-1)}).Code -eq 'NoEffect')
    $toggle = @{operation='raw';intent='UseHeroItem';actors=@($guestActor);lease=$guestLease;kind='jetpack';value=$selection;sequence=1000}
    Check 'selected_jetpack_equips' ((Receipt 'client' $toggle).Code -eq 'Applied')
    $null = Consume 'client' $toggle
    $h = Ticks 60
    Check 'duplicate_item_request_does_not_toggle_twice' ((Actor $h $guestActor).JetpackEquipped)
    $null = Consume 'client' @{operation='input-hold';actor=$guestActor;lease=$guestLease;jumpPressed=$true;jumpHeld=$true}
    $h = Wait-Report 'host' { param($r)
        $a = Actor $r $guestActor
        $flight.Add([ordered]@{tick=$r.frame.ServerTick;height=$a.Height;speed=$a.VerticalSpeed;fuel=$a.JetpackFuel;
            support=$a.SupportPlatform;lease=$a.ControlLease})
        return $a.Height -gt 90
    } 'jetpack flight above the toolbar'
    Check 'fuel_and_vertical_motion_are_authoritative' ((Actor $h $guestActor).JetpackFuel -lt 2 -and (Actor $h $guestActor).JetpackFuel -ge 0)
    $null = Receipt 'host' @{operation='SetPaused';value=1}
    $null = Consume 'client' @{operation='input-stop'}
    $h = Wait-Report 'host' { param($r) $r.frame.Paused }; $air = Actor $h $guestActor
    Check 'pause_invalidates_input_lease' ($air.ControlLease -ne $guestLease -and $air.Height -gt 0)
    if ($Capture) {
        $null = Consume 'host' @{operation='capture';file='hero-airborne.png';x=$air.X}
        Check 'actual_player_render_captured' (Test-Path -LiteralPath (Join-Path $run 'hero-airborne.png'))
    }
    $null = Receipt 'host' @{operation='Save';value=0}
    $null = Wait-Report 'host' { param($r) !$r.storageBusy -and $r.storageStatus -like '*已保存*' }
    $saved = Get-Content -LiteralPath (Join-Path $saves 'v3/slot-00.dnsave.json') -Raw | ConvertFrom-Json
    Check 'new_format_saves_airborne_state' ($saved.format_version -eq 3 -and @($saved.world.actors | Where-Object { $_.id -eq $guestActor })[0].height -eq $air.Height)
    # 恢复产品默认偏好；当前人物不变，新 epoch Ready 时由服务端重新分配。
    $null = Consume 'host' @{operation='hero-mode';value=1}
    $null = Consume 'client' @{operation='hero-mode';value=1}
    $oldEpoch = $h.frame.Epoch
    # 加载可先发布新 epoch，旧 epoch 的完成回执会按产品合同丢弃；以新世界 Ready 验证完成。
    $null = Consume 'host' @{operation='BeginLoad';value=0}
    $h = Wait-Report 'host' { param($r) $r.ready -and $r.frame.Epoch -gt $oldEpoch -and $r.frame.ReadyCount -eq 2 }
    $c = Wait-Report 'client' { param($r) $r.ready -and $r.frame.Epoch -eq $h.frame.Epoch }
    $airborneActor = $guestActor
    $restored = Actor $c $airborneActor
    Check 'airborne_recovery_preserves_motion_equipment_and_fuel' ($restored.Height -eq $air.Height -and $restored.VerticalSpeed -eq $air.VerticalSpeed -and $restored.JetpackFuel -eq $air.JetpackFuel -and $restored.JetpackEquipped)
    $hostActor = @($h.frame.World.Actors | Where-Object ControllerSlot -eq 0)[0].Id
    $guestActor = @($h.frame.World.Actors | Where-Object ControllerSlot -eq 1)[0].Id
    Check 'load_reassigns_each_ready_player' ($hostActor -gt 0 -and $guestActor -gt 0 -and $hostActor -ne $guestActor)
    $null = Receipt 'host' @{operation='SetPaused';value=0}
    $c = Wait-Report 'client' { param($r) !$r.frame.Paused -and (Actor $r $guestActor).ControllerSlot -eq 1 }
    $guestLease = (Actor $c $guestActor).ControlLease; $position = (Actor $c $guestActor).X
    $null = Consume 'client' @{operation='input-raw';actor=$guestActor;lease=$guestLease;epoch=$oldEpoch;sequence=2000;horizontal=1}
    $h = Ticks 60
    Check 'previous_epoch_input_rejected_after_load' ((Actor $h $guestActor).X -eq $position)
    foreach ($bad in @(
        @{operation='input-raw';actor=$guestActor;lease=$guestLease;sequence=2001;horizontal=2},
        @{operation='input-raw';actor=$guestActor;lease=$guestLease;sequence=2002;observedTick=0;horizontal=1},
        @{operation='input-raw';actor=$guestActor;lease=$guestLease;sequence=2003;protocol=7;horizontal=1})) { $null = Consume 'client' $bad }
    $h = Ticks 60
    Check 'invalid_direction_stale_tick_and_old_protocol_rejected' ((Actor $h $guestActor).X -eq $position)
    $null = Receipt 'host' @{operation='SetPaused';value=1}
    $null = Receipt 'host' @{operation='SetPaused';value=0}
    $null = Consume 'client' @{operation='input';actor=$guestActor;lease=$guestLease;horizontal=1}
    $h = Ticks 60
    Check 'pre_pause_lease_cannot_move_after_resume' ((Actor $h $guestActor).X -eq $position)
    $generation = @($c.feedback | Where-Object ReadyReply | Select-Object -Last 1)[0].ConnectionGeneration
    $null = Consume 'client' @{operation='disconnect'}
    $h = Wait-Report 'host' { param($r) $r.frame.PlayerCount -eq 1 }
    Check 'disconnect_releases_hero_to_automatic_control' ((Actor $h $guestActor).ControllerSlot -eq -1 -and !(Actor $h $guestActor).ManualControl)
    $releasedGuestActor = $guestActor; $actorCountBeforeReconnect = $h.frame.World.Actors.Count
    $null = Consume 'client' @{operation='connect'}
    $c = Wait-Report 'client' { param($r) $r.ready -and $r.frame.ReadyCount -eq 2 -and
        @($r.frame.World.Actors | Where-Object ControllerSlot -eq 1).Count -eq 1 }
    $guestActor = @($c.frame.World.Actors | Where-Object ControllerSlot -eq 1)[0].Id
    Check 'reconnect_recovers_slot_with_fresh_connection' ($c.slot -eq 1 -and @($c.feedback | Where-Object ReadyReply | Select-Object -Last 1)[0].ConnectionGeneration -gt $generation)
    Check 'reconnected_player_receives_new_default_villager' ($guestActor -ne $releasedGuestActor -and $c.frame.World.Actors.Count -eq $actorCountBeforeReconnect + 1)
    Check 'reconnect_does_not_reclaim_released_villager' ((Actor $c $releasedGuestActor).ControllerSlot -eq -1 -and !(Actor $c $releasedGuestActor).ManualControl)
    $null = Receipt 'host' @{operation='SetControlMode';value=1}
    $h = Wait-Report 'host' { param($r) $r.frame.HostOnly }
    Check 'host_only_revokes_guest_possession' ((Actor $h $guestActor).ControllerSlot -eq -1)
    # 先等来宾收到新策略，再检查权限拒绝；旧策略请求正确返回 PolicyChanged。
    $null = Wait-Report 'client' { param($r) $r.frame.HostOnly -and $r.frame.PolicyRevision -eq $h.frame.PolicyRevision }
    Check 'host_only_rejects_guest_claim' ((Receipt 'client' @{operation='ClaimHero';actors=@($guestActor)}).Code -eq 'PermissionDenied')
    $null = Receipt 'host' @{operation='SetControlMode';value=0}
    $c = Wait-Report 'client' { param($r) !$r.frame.HostOnly -and (Actor $r $guestActor).ControllerSlot -eq 1 }
    Check 'shared_policy_restores_default_hero' $true
    $null = Receipt 'host' @{operation='SetPaused';value=1}
    $h = Ticks 90; $c = Wait-Report 'client' { param($r) $r.frame.Paused -and $r.frame.Publication -ge $h.frame.Publication }
    $h = Read-Report 'host'
    Check 'complete_projection_converges_while_paused' (($h.frame.World | ConvertTo-Json -Depth 20 -Compress) -eq ($c.frame.World | ConvertTo-Json -Depth 20 -Compress))
    foreach ($role in $processes.Keys) {
        $log = Get-Content -LiteralPath (Join-Path $run "$role.log") -Raw
        Check ($role + '_no_runtime_exception') ($log -notmatch '(?im)(Exception:|Shader error|\[AppStartup\].*(failed|cancelled))')
    }
}
catch { $failure = $_.Exception.ToString() }
finally {
    foreach ($process in $processes.Values) { $process.Refresh(); if (!$process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit() } }
    $result = [ordered]@{ passed=(!$failure); checks=$checks; error=$failure; artifacts=$run; player=$player;
        capture=[bool]$Capture; width=$Width; height=$Height;
        scope='Two independent Mono processes; native hero mode, authoritative input, possession, platform, jetpack, recovery and policy. No foreground performance claim.';
        gameCodeSha256=(Get-FileHash -LiteralPath (Join-Path (Split-Path $player -Parent) 'DarkNights_Data/Managed/DarkNights.Entry.dll')).Hash }
    $result | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    $flight | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'jetpack-flight.json') -Encoding utf8
    Write-Output "Hero transport: passed=$(!$failure) checks=$($checks.Count); $run/result.json"
}
if ($failure) { throw $failure }
