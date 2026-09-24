param(
    [Parameter(Mandatory)][string]$PlayerPath,
    [string]$ClientPlayerPath = '',
    [switch]$UseCompatibilityHash,
    [switch]$VerifyDebugBridge,
    [int]$Port = 28820,
    [int]$ClientPort = 0,
    [string]$Seed = 'terrain-loop-acceptance',
    [ValidateRange(7, 20)][int]$SaveVersion = 7
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$player = [IO.Path]::GetFullPath($PlayerPath)
if (!(Test-Path -LiteralPath $player)) { throw 'Build the formal Mono Player first.' }
$clientPlayer = if ($ClientPlayerPath) { [IO.Path]::GetFullPath($ClientPlayerPath) } else { $player }
if (!(Test-Path -LiteralPath $clientPlayer)) { throw 'Build the client Mono Player first.' }
if (!$ClientPort) { $ClientPort = $Port }
$run = Join-Path $repo ('artifacts/map-fix/terrain-loop-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$saves = Join-Path $run 'saves'
New-Item -ItemType Directory -Path $run -Force | Out-Null
$processes = @{}
$checks = [ordered]@{}
$failure = $null
$debugToken = [Guid]::NewGuid().ToString('N')
$debugPorts = @{host=31821; client=31822; late=31823}

function Read-DebugBridge([string]$Role, [string]$Secret, [switch]$Compare) {
    $tcp = [Net.Sockets.TcpClient]::new()
    try {
        $tcp.Connect('127.0.0.1', $debugPorts[$Role])
        $tcp.ReceiveTimeout = 3000; $tcp.SendTimeout = 3000
        $stream = $tcp.GetStream()
        $request = [Text.Encoding]::ASCII.GetBytes($Secret + $(if ($Compare) { ' compare' } else { '' }) + "`n")
        $stream.Write($request, 0, $request.Length)
        $reader = [IO.StreamReader]::new($stream)
        return ($reader.ReadLine() | ConvertFrom-Json)
    } finally { $tcp.Dispose() }
}

function Read-Report([string]$Role) {
    try {
        $stream = [IO.FileStream]::new((Join-Path $run "$Role.json"), [IO.FileMode]::Open, [IO.FileAccess]::Read,
            [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
        $reader = [IO.StreamReader]::new($stream)
        try { return $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    } catch { return $null }
}
function Wait-Report([string]$Role, [scriptblock]$Condition, [string]$Description = '') {
    $end = [DateTime]::UtcNow.AddSeconds(75)
    do {
        $value = Read-Report $Role
        if ($value -and $value.error) { throw "$Role reported: $($value.error)" }
        if ($value -and (& $Condition $value)) { return $value }
        if ($processes.ContainsKey($Role)) {
            $processes[$Role].Refresh()
            if ($processes[$Role].HasExited) { throw "$Role exited unexpectedly; see $run" }
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $end)
    throw "Timeout waiting for $Role : $Description"
}
function Start-Player([string]$Role) {
    $report = Join-Path $run "$Role.json"
    Remove-Item -LiteralPath $report -Force -ErrorAction SilentlyContinue
    [IO.File]::WriteAllText((Join-Path $run "$Role.commands"), '')
    $endpoint = if ($Role -eq 'host') { $Port } else { $ClientPort }
    $arguments = @('-batchmode', '-nographics', '-logFile', ('"' + (Join-Path $run "$Role.log") + '"'),
        '--dn-role', $Role, '--dn-port', $endpoint, '--dn-save-dir', ('"' + $saves + '"'),
        '--dn-map-seed', $Seed, '--dn-report', ('"' + $report + '"'),
        '--dn-commands', ('"' + (Join-Path $run "$Role.commands") + '"'))
    if ($UseCompatibilityHash) { $arguments += '--dn-map-compat-hash' }
    if ($VerifyDebugBridge) { $arguments += @('--ard-map-debug-port', $debugPorts[$Role], '--ard-map-debug-token', $debugToken) }
    $rolePlayer = if ($Role -eq 'host') { $player } else { $clientPlayer }
    $processes[$Role] = Start-Process -FilePath $rolePlayer -ArgumentList $arguments -WindowStyle Hidden -PassThru
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
function Consume([string]$Role, [hashtable]$Command) {
    $before = Wait-Report $Role { param($r) $true }
    Send $Role $Command
    return Wait-Report $Role { param($r) $r.commandsConsumed -gt $before.commandsConsumed } $Command.operation
}
function Receipt([string]$Role, [hashtable]$Command) {
    $before = Wait-Report $Role { param($r) $r.ready }
    $epoch = $before.frame.Epoch
    $connection = @($before.feedback | Where-Object ReadyReply | Select-Object -Last 1)[0].ConnectionGeneration
    $last = @($before.feedback | Where-Object { !$_.ReadyReply -and $_.Epoch -eq $epoch -and
        $_.ConnectionGeneration -eq $connection } | Sort-Object Sequence | Select-Object -Last 1)
    $sequence = if ($last.Count) { $last[0].Sequence } else { 0 }
    Send $Role $Command
    $after = Wait-Report $Role { param($r) @($r.feedback | Where-Object { !$_.ReadyReply -and $_.Epoch -eq $epoch -and
        $_.ConnectionGeneration -eq $connection -and $_.Sequence -gt $sequence }).Count -gt 0 } $Command.operation
    return @($after.feedback | Where-Object { !$_.ReadyReply -and $_.Epoch -eq $epoch -and
        $_.ConnectionGeneration -eq $connection -and $_.Sequence -gt $sequence } | Sort-Object Sequence)[-1]
}
function Terrain-Receipt([string]$Role, [int]$U, [int]$V, [string]$RequestId) {
    $before = Wait-Report $Role { param($r) $r.ready }
    $count = @($before.terrainFeedback | Where-Object RequestId -eq $RequestId).Count
    Send $Role @{operation='terrain';u=$U;v=$V;requestId=$RequestId}
    $after = Wait-Report $Role { param($r) @($r.terrainFeedback | Where-Object RequestId -eq $RequestId).Count -gt $count } $RequestId
    return @($after.terrainFeedback | Where-Object RequestId -eq $RequestId)[-1]
}
function Check([string]$Name, [bool]$Ok) { $checks[$Name] = $Ok; if (!$Ok) { throw "Failed: $Name" } }
function Map-Hash($Report) {
    if ($UseCompatibilityHash -and $Report.terrain.compatibilitySha256) { return $Report.terrain.compatibilitySha256 }
    return $Report.terrain.sha256
}
function Actor($Report, [int]$Slot) { return @($Report.frame.World.Actors | Where-Object ControllerSlot -eq $Slot)[0] }
function Find-Cell([byte[]]$Cells, [byte[]]$Protection, [byte[]]$Soft, [scriptblock]$Predicate) {
    for ($y = 0; $y -lt 192; $y++) { for ($x = 0; $x -lt 320; $x++) {
        $i = $y * 320 + $x
        if (& $Predicate $Cells[$i] $Protection[$i] $Soft[$i] $x $y) { return @{x=$x;y=$y;i=$i} }
    } }
    throw 'Required terrain fixture cell was not found.'
}
function Place-Actor($Actor, $Cell, [int]$Item) {
    $Actor.x = ($Cell.x + 0.5) * 16
    $Actor.height = 640 - ($Cell.y + 0.5) * 16
    $Actor.vertical_speed = 0; $Actor.support_platform = -1; $Actor.ignored_platform = 0
    $Actor.selected_item = $Item; $Actor.explosive_charges = 3
}

try {
    Start-Player 'host'
    $h = Wait-Report 'host' { param($r) $r.ready -and $r.terrain.visible } 'host ready'
    Start-Player 'client'
    $c = Wait-Report 'client' { param($r) $r.ready -and $r.terrain.visible } 'client ready'
    if ($VerifyDebugBridge) {
        $deadline = [DateTime]::UtcNow.AddSeconds(20)
        do {
            $serverDebug = Read-DebugBridge 'host' $debugToken -Compare
            $clientDebug = Read-DebugBridge 'client' $debugToken -Compare
            $projection = @($serverDebug.sessions.peerProjections | Where-Object {
                $_.status -eq 'Ready' -and $_.canonical -eq $clientDebug.sessions[0].replicaCanonical -and
                $_.streamCommit -eq $clientDebug.sessions[0].streamCommit -and
                $_.session -eq $clientDebug.sessions[0].session -and
                $_.generation -eq $clientDebug.sessions[0].generation })
            if ($projection.Count) { break }
            Start-Sleep -Milliseconds 300
        } while ([DateTime]::UtcNow -lt $deadline)
        Check 'debug_bridge_authorized_projection_matches_client' ($projection.Count -gt 0)
        Check 'debug_bridge_wrong_token_denied' ((Read-DebugBridge 'host' ('0' * 32)).status -eq 'denied')
        $debugEvidence = @{server=$serverDebug;client=$clientDebug;matchedPeer=$projection[0].peer;wrongTokenDenied=$true}
    }
    $h = Wait-Report 'host' { param($r) @($r.frame.World.Actors | Where-Object ControllerSlot -eq 0).Count -eq 1 -and
        @($r.frame.World.Actors | Where-Object ControllerSlot -eq 1).Count -eq 1 } 'host sees both controlled heroes'
    $c = Wait-Report 'client' { param($r) @($r.frame.World.Actors | Where-Object ControllerSlot -eq 0).Count -eq 1 -and
        @($r.frame.World.Actors | Where-Object ControllerSlot -eq 1).Count -eq 1 } 'client sees both controlled heroes'
    Check 'initial_host_client_map_equal' ((Map-Hash $h) -eq (Map-Hash $c))
    if ($SaveVersion -ge 10) {
        Check 'expedition_depart_for_terrain_authorization' ((Receipt 'host' @{operation='Expedition';kind='depart'}).Code -eq 'Applied')
        $h = Wait-Report 'host' { param($r) $r.frame.World.Expedition.Phase -eq 1 } 'active expedition'
        $c = Wait-Report 'client' { param($r) $r.frame.World.Expedition.Phase -eq 1 } 'client active expedition'
    }
    Check 'pause_for_fixture' ((Receipt 'host' @{operation='SetPaused';value=1}).Code -eq 'Applied')
    $null = Receipt 'host' @{operation='Save';value=0}
    $savePath = Join-Path $saves "v$SaveVersion/slot-00.dnsave.json"
    $h = Wait-Report 'host' { param($r) !$r.storageBusy -and (Test-Path -LiteralPath $savePath) } 'initial current-format save'
    $fixture = [IO.File]::ReadAllText($savePath) | ConvertFrom-Json
    $cells = [Convert]::FromBase64String($fixture.world.terrain.materials)
    $protection = [Convert]::FromBase64String($fixture.world.terrain.protection)
    $soft = [Convert]::FromBase64String($fixture.world.terrain.soft_rock)
    $softCell = Find-Cell $cells $protection $soft { param($m,$p,$s,$x,$y) $s -eq 1 -and $p -eq 0 -and $m -notin 0,8 }
    $normalNearSoft = Find-Cell $cells $protection $soft { param($m,$p,$s,$x,$y)
        $p -eq 0 -and $s -eq 0 -and $m -in 1,2,3,7 -and [Math]::Abs($x-$softCell.x) -le 3 -and [Math]::Abs($y-$softCell.y) -le 3 }
    $blastCell = Find-Cell $cells $protection $soft { param($m,$p,$s,$x,$y)
        $p -eq 0 -and $m -in 1,2,3,7 -and ($x % 32) -gt 3 -and ($x % 32) -lt 28 -and ($y % 32) -gt 3 -and ($y % 32) -lt 28 }
    $scatterCell = if ($SaveVersion -le 7) {
        Find-Cell $cells $protection $soft { param($m,$p,$s,$x,$y) $p -eq 0 -and $m -in 4,5,6 }
    } else { $null }
    $protectedCell = Find-Cell $cells $protection $soft { param($m,$p,$s,$x,$y) $p -eq 1 -and $m -ne 0 }
    $bedrockCell = Find-Cell $cells $protection $soft { param($m,$p,$s,$x,$y) $m -eq 8 }
    $hostId = (Actor $h 0).Id; $clientId = (Actor $c 1).Id
    Place-Actor (@($fixture.world.actors | Where-Object id -eq $clientId)[0]) $softCell 1
    Place-Actor (@($fixture.world.actors | Where-Object id -eq $hostId)[0]) $blastCell 2
    $fixture.world.paused = $false
    [IO.File]::WriteAllText($savePath, ($fixture | ConvertTo-Json -Depth 64), [Text.UTF8Encoding]::new($false))
    $oldEpoch = $h.epoch
    $null = Consume 'host' @{operation='BeginLoad';value=0}
    $h = Wait-Report 'host' { param($r) $r.ready -and $r.epoch -gt $oldEpoch -and $r.terrain.visible } 'fixture loaded'
    $c = Wait-Report 'client' { param($r) $r.ready -and $r.epoch -eq $h.epoch -and $r.terrain.visible } 'client fixture loaded'
    $beforeDigest = Map-Hash $h
    $softReply = Terrain-Receipt 'client' $softCell.x (-$softCell.y) 'client-soft-rock'
    Check 'client_hand_mines_soft_rock' $softReply.Accepted
    $normalReply = Terrain-Receipt 'client' $normalNearSoft.x (-$normalNearSoft.y) 'client-normal-wall'
    Check 'normal_wall_rejects_hand_mining' (!$normalReply.Accepted)
    $protectedReply = Terrain-Receipt 'host' $protectedCell.x (-$protectedCell.y) 'host-protected'
    $bedrockReply = Terrain-Receipt 'host' $bedrockCell.x (-$bedrockCell.y) 'host-bedrock'
    Check 'explosive_rejects_protected_and_bedrock' (!$protectedReply.Accepted -and !$bedrockReply.Accepted)
    $blastReply = Terrain-Receipt 'host' $blastCell.x (-$blastCell.y) 'host-direct-blast'
    Check 'bomb_slot_rejects_direct_cell_explosion' (!$blastReply.Accepted)
    $h = Wait-Report 'host' { param($r) (Map-Hash $r) -eq (Map-Hash (Read-Report 'client')) -and !$r.terrainPresentation.refreshing }
    $beforeBlast = Map-Hash $h
    $hero = Actor $h 0
    $throw = @{operation='input-raw';actor=$hero.Id;lease=$hero.ControlLease;sequence=1000000;
        observedTick=$h.frame.ServerTick;epoch=$h.epoch;selectionRevision=$hero.SelectionRevision;
        aimAngle=-90;usePressed=$true;useReleased=$true}
    $null = Consume 'host' $throw
    $null = Consume 'host' $throw
    $h = Wait-Report 'host' { param($r) (Actor $r 0).ExplosiveCharges -eq 2 } 'one projectile consumes one charge'
    Check 'throw_consumes_one_charge_for_duplicate_input' ((Actor $h 0).ExplosiveCharges -eq 2)
    $h = Wait-Report 'host' { param($r) (Map-Hash $r) -ne $beforeBlast -and !$r.terrainPresentation.refreshing } 'projectile fuse changes terrain'
    $c = Wait-Report 'client' { param($r) (Map-Hash $r) -eq (Map-Hash $h) -and !$r.terrainPresentation.refreshing } 'client blast refresh'
    Check 'projectile_fuse_changes_terrain_at_actual_location' ((Map-Hash $h) -ne $beforeBlast)
    Check 'explosive_inventory_decrements_once' ((Actor $h 0).ExplosiveCharges -eq 2)
    Check 'changed_chunks_only_refresh_local_regions' ($h.terrainPresentation.changedChunks -ge 1 -and
        $h.terrainPresentation.changedChunks -le 4 -and $h.terrainPresentation.refreshRegions -ge 1 -and
        $h.terrainPresentation.refreshRegions -le 4)
    Check 'host_client_final_map_equal_after_blast' ((Map-Hash $h) -eq (Map-Hash $c))

    if ($SaveVersion -ge 10) {
        $null = Receipt 'host' @{operation='Save';value=3}
        $save3 = Join-Path $saves "v$SaveVersion/slot-03.dnsave.json"
        $null = Wait-Report 'host' { param($r) !$r.storageBusy -and (Test-Path -LiteralPath $save3) } 'current-format final save'
        $finalDigest = Map-Hash $h
        Start-Player 'late'
        $late = Wait-Report 'late' { param($r) $r.ready -and $r.terrain.visible } 'late join edited map'
        Check 'late_join_matches_edited_map' ((Map-Hash $late) -eq $finalDigest)
        Stop-Player 'late'; Stop-Player 'client'
        Start-Player 'client'
        $reconnected = Wait-Report 'client' { param($r) $r.ready -and $r.terrain.visible } 'reconnected map'
        Check 'reconnect_matches_edited_map' ((Map-Hash $reconnected) -eq $finalDigest)
        Stop-Player 'client'; Stop-Player 'host'
        Start-Player 'host'
        $restarted = Wait-Report 'host' { param($r) $r.ready -and $r.terrain.visible } 'restarted host'
        $restartEpoch = $restarted.epoch
        $null = Consume 'host' @{operation='BeginLoad';value=3}
        $restarted = Wait-Report 'host' { param($r) $r.ready -and $r.epoch -gt $restartEpoch -and (Map-Hash $r) -eq $finalDigest } 'restored edited map'
        Check 'restart_restores_edited_map' ((Map-Hash $restarted) -eq $finalDigest)
        return
    }

    $null = Receipt 'host' @{operation='Save';value=1}
    $save1 = Join-Path $saves "v$SaveVersion/slot-01.dnsave.json"
    $null = Wait-Report 'host' { param($r) !$r.storageBusy -and (Test-Path -LiteralPath $save1) } 'post-blast save'
    $fixture = [IO.File]::ReadAllText($save1) | ConvertFrom-Json
    Place-Actor (@($fixture.world.actors | Where-Object id -eq $clientId)[0]) $scatterCell 1
    [IO.File]::WriteAllText($save1, ($fixture | ConvertTo-Json -Depth 64), [Text.UTF8Encoding]::new($false))
    $oldEpoch = $h.epoch
    $null = Consume 'host' @{operation='BeginLoad';value=1}
    $h = Wait-Report 'host' { param($r) $r.ready -and $r.epoch -gt $oldEpoch -and $r.terrain.visible } 'scatter fixture loaded'
    $c = Wait-Report 'client' { param($r) $r.ready -and $r.epoch -eq $h.epoch -and $r.terrain.visible }
    $scatterReply = Terrain-Receipt 'client' $scatterCell.x (-$scatterCell.y) 'client-scattered-ore'
    Check 'client_hand_mines_scattered_ore' $scatterReply.Accepted
    $h = Wait-Report 'host' { param($r) (Map-Hash $r) -eq (Map-Hash (Read-Report 'client')) -and !$r.terrainPresentation.refreshing }

    $mineDeposits = @($h.frame.World.Worksites | Where-Object { $_.IsMineralDeposit -and $_.RoomKind -eq 'mine' })
    $scatteredCount = @($cells | Where-Object { $_ -in 4,5,6 }).Count
    $mineCapacity = ($mineDeposits | Measure-Object -Property Capacity -Sum).Sum
    Check 'mine_room_capacity_exceeds_scattered_ore' ($mineDeposits.Count -eq 6 -and $mineCapacity -gt $scatteredCount)
    $deposit = @($mineDeposits | Where-Object {
        $gridX = [int]([Math]::Round($_.X / 16 - 0.5)); $gridY = [int]$_.Y
        $gridY -lt 191 -and $cells[(($gridY + 1) * 320 + $gridX)] -notin 0,8
    })[0]
    Check 'hand_mine_deposit_has_floor_support' ($null -ne $deposit)
    $beforeDeposit = $deposit.Amount
    $null = Receipt 'host' @{operation='Save';value=2}
    $save2 = Join-Path $saves "v$SaveVersion/slot-02.dnsave.json"
    $null = Wait-Report 'host' { param($r) !$r.storageBusy -and (Test-Path -LiteralPath $save2) } 'deposit fixture save'
    $fixture = [IO.File]::ReadAllText($save2) | ConvertFrom-Json
    $clientActor = @($fixture.world.actors | Where-Object id -eq $clientId)[0]
    $clientActor.x = $deposit.X; $clientActor.height = 640 - ($deposit.Y + 1) * 16
    $clientActor.vertical_speed = 0; $clientActor.support_platform = 0; $clientActor.selected_item = 1
    $fixture.world.paused = $false
    [IO.File]::WriteAllText($save2, ($fixture | ConvertTo-Json -Depth 64), [Text.UTF8Encoding]::new($false))
    $oldEpoch = $h.epoch
    $null = Consume 'host' @{operation='BeginLoad';value=2}
    $h = Wait-Report 'host' { param($r) $r.ready -and $r.epoch -gt $oldEpoch -and $r.terrain.visible }
    $c = Wait-Report 'client' { param($r) $r.ready -and $r.epoch -eq $h.epoch }
    $clientActor = Actor $c 1
    $use = Receipt 'client' @{operation='UseHeroItem';actors=@($clientActor.Id);target=$deposit.Id;kind='pickaxe';
        value=$clientActor.SelectionRevision;lease=$clientActor.ControlLease}
    Check 'mineral_deposit_is_hand_mined' ($use.Code -eq 'Applied')
    $h = Wait-Report 'host' { param($r) @($r.frame.World.Worksites | Where-Object Id -eq $deposit.Id)[0].Amount -eq $beforeDeposit - 1 }
    Check 'deposit_remaining_is_authoritative' (@($h.frame.World.Worksites | Where-Object Id -eq $deposit.Id)[0].Amount -eq $beforeDeposit - 1)
    $null = Receipt 'host' @{operation='SetPaused';value=1}
    $null = Receipt 'host' @{operation='Save';value=3}
    $save3 = Join-Path $saves "v$SaveVersion/slot-03.dnsave.json"
    $null = Wait-Report 'host' { param($r) !$r.storageBusy -and (Test-Path -LiteralPath $save3) } 'final save'
    $finalDigest = Map-Hash $h

    Start-Player 'late'
    $late = Wait-Report 'late' { param($r) $r.ready -and $r.terrain.visible } 'late join final map'
    Check 'late_join_matches_final_map' ((Map-Hash $late) -eq $finalDigest)
    Stop-Player 'late'; Stop-Player 'client'
    Start-Player 'client'
    $reconnected = Wait-Report 'client' { param($r) $r.ready -and $r.terrain.visible } 'client reconnect final map'
    Check 'reconnect_matches_final_map' ((Map-Hash $reconnected) -eq $finalDigest)
    Stop-Player 'client'; Stop-Player 'host'
    Start-Player 'host'
    $restarted = Wait-Report 'host' { param($r) $r.ready -and $r.terrain.visible } 'restarted host initial map'
    $restartEpoch = $restarted.epoch
    $null = Consume 'host' @{operation='BeginLoad';value=3}
    $restarted = Wait-Report 'host' { param($r) $r.ready -and $r.epoch -gt $restartEpoch -and (Map-Hash $r) -eq $finalDigest } 'disk save after process restart'
    Check 'player_restart_restores_exact_final_map' ((Map-Hash $restarted) -eq $finalDigest)
    Check 'player_restart_restores_deposit_remaining' (@($restarted.frame.World.Worksites | Where-Object Id -eq $deposit.Id)[0].Amount -eq $beforeDeposit - 1)
}
catch { $failure = $_.Exception.ToString() }
finally {
    foreach ($role in @($processes.Keys)) { Stop-Player $role }
    [ordered]@{passed=(!$failure);checks=$checks;error=$failure;artifacts=$run;player=$player;clientPlayer=$clientPlayer;
        network=@{port=$Port;clientPort=$ClientPort;seed=$Seed;compatibilityHash=[bool]$UseCompatibilityHash;
            debugBridge=[bool]$VerifyDebugBridge};debugEvidence=$debugEvidence} | ConvertTo-Json -Depth 12 |
        Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Terrain loop: passed=$(!$failure); checks=$($checks.Count); $run/result.json"
}
if ($failure) { throw $failure }
