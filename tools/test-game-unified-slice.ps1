param([int]$Port = 28320)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$player = Join-Path $repo 'artifacts/migration/player-mono/DarkNights.exe'
$run = Join-Path $repo ('artifacts/yygc-unified/u2/player-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
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
function Wait-Report([string]$Role, [scriptblock]$Condition) {
    $end = [DateTime]::UtcNow.AddSeconds(45)
    do {
        $value = Read-Report $Role
        if ($value -and $value.error) { throw "$Role reported: $($value.error)" }
        if ($value -and (& $Condition $value)) { return $value }
        $processes[$Role].Refresh()
        if ($processes[$Role].HasExited) { throw "$Role exited unexpectedly; see $run/$Role.log" }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $end)
    throw "Timeout waiting for $Role; see $run/$Role.log"
}
function Start-Player([string]$Role) {
    [IO.File]::WriteAllText((Join-Path $run "$Role.commands"), '')
    $arguments = @('-batchmode', '-nographics', '-logFile', ('"' + (Join-Path $run "$Role.log") + '"'),
        '--dn-unified-slice', '--dn-role', $Role, '--dn-port', $Port, '--dn-save-dir', ('"' + $saves + '"'),
        '--dn-report', ('"' + (Join-Path $run "$Role.json") + '"'),
        '--dn-commands', ('"' + (Join-Path $run "$Role.commands") + '"'))
    $processes[$Role] = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
}
function Send([string]$Role, [hashtable]$Command) {
    [IO.File]::AppendAllText((Join-Path $run "$Role.commands"), ($Command | ConvertTo-Json -Compress) + "`n")
}
function Receipt([string]$Role, [hashtable]$Command) {
    $before = Wait-Report $Role { param($r) $r.ready }
    $count = @($before.feedback).Count
    if ($count -ge 63) { throw 'Receipt history exceeded this bounded test.' }
    Send $Role $Command
    $after = Wait-Report $Role { param($r) @($r.feedback).Count -gt $count }
    return @($after.feedback)[-1]
}
function Check([string]$Name, [bool]$Ok) { $checks[$Name] = $Ok; if (!$Ok) { throw "Failed: $Name" } }
function World-Json($Frame) { return $Frame.World | ConvertTo-Json -Depth 25 -Compress }

try {
    if (!(Test-Path -LiteralPath $player)) { throw 'Build the U2 Mono Player first.' }
    Start-Player 'host'
    $r = Wait-Report 'host' { param($r) $r.ready -and $r.entityViews -eq 10 }
    Check 'single_host_has_complete_new_identity_frame' ($r.slot -eq 0 -and $r.frame.World.Identities.Count -eq 10)
    $null = Receipt 'host' @{operation='SetPaused';value=1}
    $r = Wait-Report 'host' { param($r) $r.frame.Paused }
    $workers = @($r.frame.World.Actors | Where-Object Kind -eq 'worker')
    $first = $workers[0].Id
    $receipt = Receipt 'host' @{operation='IssueOrders';actors=@($first);x=240}
    Check 'single_host_explicit_order_applied' ($receipt.Code -eq 'Applied')
    $null = Receipt 'host' @{operation='SetPaused';value=0}
    $r = Wait-Report 'host' { param($r) @($r.frame.World.Actors | Where-Object { $_.Id -eq $first -and [math]::Abs($_.X-240) -lt 0.8 }).Count -eq 1 }
    $null = Receipt 'host' @{operation='SetPaused';value=1}
    Start-Player 'client'
    $guest = Wait-Report 'client' { param($r) $r.ready -and $r.entityViews -eq 10 -and $r.frame.Paused }
    Check 'independent_client_joins_while_paused' ($guest.slot -eq 1 -and $guest.frame.World.Identities.Count -eq 10)
    $tree = @($guest.frame.World.Worksites | Where-Object Kind -eq 'wood')[0]
    $null = Receipt 'host' @{operation='IssueOrders';actors=@($workers[1].Id);target=$tree.Id;x=$tree.X}
    $null = Receipt 'client' @{operation='IssueOrders';actors=@($workers[2].Id);target=$tree.Id;x=$tree.X}
    $r = Wait-Report 'host' { param($r) @($r.frame.World.Worksites | Where-Object WorkerId -gt 0).Count -eq 2 }
    $occupied = @($r.frame.World.Worksites | Where-Object WorkerId -gt 0)
    Check 'shared_worksite_keeps_unique_owners_and_original_fallback' (($occupied.WorkerId | Select-Object -Unique).Count -eq 2)
    $receipt = Receipt 'host' @{operation='PlaceBuilding';kind='house';x=184;actors=@($first)}
    Check 'host_house_pays_once' ($receipt.Code -eq 'Applied')
    $construction = $receipt.EntityId
    $null = Receipt 'host' @{operation='SetSpeed';value=2}
    $null = Receipt 'host' @{operation='SetPaused';value=0}
    $null = Wait-Report 'host' { param($r) @($r.frame.World.Buildings | Where-Object { $_.Id -eq $construction -and $_.Progress -gt 0.1 -and $_.Progress -lt 0.9 }).Count -eq 1 }
    $null = Receipt 'host' @{operation='SetPaused';value=1}
    $saved = Wait-Report 'host' { param($r) $r.frame.Paused -and $r.entityViews -eq 11 }
    $savedWorld = World-Json $saved.frame
    $null = Wait-Report 'client' { param($r) $r.frame.Paused -and $r.entityViews -eq 11 -and (World-Json $r.frame) -eq $savedWorld }
    Check 'active_construction_and_double_speed_are_projected' ($saved.frame.Speed -eq 2 -and $saved.frame.World.Camp.Stock.Wood -eq 75)
    $null = Receipt 'host' @{operation='Save';value=0}
    $slot0 = Join-Path $saves 'v2/slot-00.dnsave.json'
    $null = Wait-Report 'host' { param($r) !$r.storageBusy -and (Test-Path -LiteralPath $slot0) }
    $document = Get-Content -LiteralPath $slot0 -Raw | ConvertFrom-Json
    Check 'new_save_uses_v2_and_explicit_definition_identities' ($document.format_version -eq 2 -and $document.world.identities.Count -eq 11)
    Start-Player 'late'
    $late = Wait-Report 'late' { param($r) $r.ready -and $r.frame.Paused -and $r.entityViews -eq 11 }
    Check 'late_join_matches_complete_live_world' ((World-Json $late.frame) -eq (World-Json $saved.frame))
    Send 'client' @{operation='disconnect'}
    $null = Wait-Report 'client' { param($r) !$r.ready }
    Send 'client' @{operation='connect'}
    $guest = Wait-Report 'client' { param($r) $r.ready -and $r.frame.Paused }
    Check 'reconnect_restores_slot_and_current_world' ($guest.slot -eq 1 -and (World-Json $guest.frame) -eq (World-Json $saved.frame))
    $null = Receipt 'host' @{operation='SetControlMode';value=1}
    $null = Wait-Report 'client' { param($r) $r.frame.HostOnly }
    $receipt = Receipt 'client' @{operation='PlaceBuilding';kind='house';x=620}
    Check 'host_only_blocks_build_and_automatic_assignment' ($receipt.Code -eq 'PermissionDenied')
    $receipt = Receipt 'client' @{operation='IssueOrders';actors=@($first);x=300}
    Check 'host_only_blocks_direct_order' ($receipt.Code -eq 'PermissionDenied')
    $receipt = Receipt 'client' @{operation='SetSpeed';value=1}
    Check 'guest_cannot_control_time' ($receipt.Code -eq 'PermissionDenied')
    $null = Receipt 'host' @{operation='SetPaused';value=0}
    $null = Wait-Report 'host' { param($r) $r.frame.Elapsed -gt $saved.frame.Elapsed + 2 }
    Send 'host' @{operation='BeginLoad';value=0}
    foreach ($role in @('host','client','late')) {
        $restored = Wait-Report $role { param($r) $r.ready -and $r.frame.Epoch -eq 2 -and $r.frame.Paused -and $r.entityViews -eq 11 }
        Check ($role+'_load_keeps_policy_time_and_population') ($restored.frame.HostOnly -and $restored.frame.Speed -eq 2 -and
            $restored.frame.Elapsed -eq $saved.frame.Elapsed -and $restored.frame.World.Camp.Population -eq $saved.frame.World.Camp.Population)
    }
    $null = Receipt 'host' @{operation='Save';value=1}
    $slot1 = Join-Path $saves 'v2/slot-01.dnsave.json'
    $null = Wait-Report 'host' { param($r) !$r.storageBusy -and (Test-Path -LiteralPath $slot1) }
    Check 'new_world_roundtrip_preserves_complete_save' ((Get-FileHash -LiteralPath $slot0).Hash -eq (Get-FileHash -LiteralPath $slot1).Hash)
    $broken = Get-Content -LiteralPath $slot1 -Raw | ConvertFrom-Json
    $broken.world.actors[0].target_id = 99999
    $broken | ConvertTo-Json -Depth 25 -Compress | Set-Content -LiteralPath $slot1 -Encoding utf8
    Send 'host' @{operation='BeginLoad';value=1}
    $r = Wait-Report 'host' { param($r) !$r.storageBusy -and $r.storageStatus -like '*失败*' }
    Check 'bad_save_preserves_live_epoch_and_pause' ($r.frame.Epoch -eq 2 -and $r.frame.Paused)
    $receipt = Receipt 'client' @{operation='raw';intent='IssueOrders';sequence=400;epoch=1;actors=@($first);x=300}
    Check 'old_epoch_request_is_rejected' ($receipt.Code -eq 'EpochChanged')
    $null = Receipt 'host' @{operation='SetControlMode';value=0}
    $null = Wait-Report 'client' { param($r) !$r.frame.HostOnly }
    foreach ($x in @(620,664)) {
        $receipt = Receipt 'host' @{operation='PlaceBuilding';kind='house';x=$x}
        Check ('setup_last_payment_'+$x) ($receipt.Code -eq 'Applied')
    }
    $null = Wait-Report 'host' { param($r) $r.frame.World.Camp.Stock.Wood -eq 25 }
    $hostRequest = @{operation='raw';intent='PlaceBuilding';sequence=600;kind='house';x=708}
    $guestRequest = @{operation='raw';intent='PlaceBuilding';sequence=600;kind='house';x=752}
    Send 'host' $hostRequest
    Send 'client' $guestRequest
    $a = Wait-Report 'host' { param($r) @($r.feedback | Where-Object Sequence -eq 600).Count -gt 0 }
    $b = Wait-Report 'client' { param($r) @($r.feedback | Where-Object Sequence -eq 600).Count -gt 0 }
    $codes = @(@($a.feedback | Where-Object Sequence -eq 600)[-1].Code, @($b.feedback | Where-Object Sequence -eq 600)[-1].Code)
    Check 'concurrent_last_payment_succeeds_exactly_once' (@($codes | Where-Object { $_ -eq 'Applied' }).Count -eq 1 -and
        @($codes | Where-Object { $_ -eq 'NoEffect' }).Count -eq 1)
    $r = Wait-Report 'host' { param($r) $r.frame.World.Camp.Stock.Wood -eq 0 -and $r.frame.World.Buildings.Count -eq 6 }
    $again = Receipt 'host' $hostRequest
    Check 'duplicate_request_returns_cached_result' ($again.Code -eq $codes[0])
    $hostRequest.x = 760
    $conflict = Receipt 'host' $hostRequest
    Check 'changed_resend_is_rejected' ($conflict.Code -eq 'SequenceConflict')
    $receipt = Receipt 'client' @{operation='IssueOrders';actors=@($first);target=99999;x=300}
    Check 'invalid_entity_request_is_rejected' ($receipt.Code -eq 'InvalidRequest')
    $r = Wait-Report 'host' { param($r) $r.frame.World.Camp.Stock.Wood -eq 0 }
    $finalWorld = World-Json $r.frame
    foreach ($role in @('client','late')) {
        $other = Wait-Report $role { param($r) $r.frame.World.Buildings.Count -eq 6 -and $r.entityViews -eq 14 -and (World-Json $r.frame) -eq $finalWorld }
        Check ($role+'_converges_without_duplicate_objects') ((World-Json $other.frame) -eq $finalWorld)
    }
}
catch { $failure = $_.Exception.ToString() }
finally {
    foreach ($role in @($processes.Keys)) {
        $lastReport = Read-Report $role
        if ($lastReport) { $lastReport | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $run ($role+'-final.json')) -Encoding utf8 }
        $processes[$role].Refresh()
        if (!$processes[$role].HasExited) {
            Send $role @{operation='quit'}
            if (!$processes[$role].WaitForExit(5000)) { Stop-Process -Id $processes[$role].Id -Force }
        }
    }
    $code = Join-Path (Split-Path $player -Parent) 'DarkNights_Data/Managed/DarkNights.Runtime.dll'
    [ordered]@{passed=(!$failure);checks=$checks;error=$failure;scope='U2 Mono slice; Host and two independent clients';
        runtimeSha256=$(if (Test-Path -LiteralPath $code) {(Get-FileHash -LiteralPath $code).Hash});artifacts=$run} |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Unified slice: passed=$(!$failure) checks=$($checks.Count); $run/result.json"
}
if ($failure) { throw $failure }
