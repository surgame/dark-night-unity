param([int]$Port = 28220, [int]$ClientPort = 0, [int]$MinimumUptimeSeconds = 35, [string]$PlayerPath = '')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$player = if ($PlayerPath) { [IO.Path]::GetFullPath($PlayerPath) } else { Join-Path $repo 'artifacts/migration/player-mono/DarkNights.exe' }
$run = Join-Path $repo ('artifacts/migration/concurrency-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$saves = Join-Path $run 'saves'
New-Item -ItemType Directory -Path $run | Out-Null
$processes = @{}
$checks = [ordered]@{}
$failure = $null
if ($ClientPort -eq 0) { $ClientPort = $Port }
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
        if ($processes[$Role].HasExited) { throw "$Role exited unexpectedly." }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $end)
    throw "Timeout waiting for $Role"
}
function Start-Player([string]$Role) {
    [IO.File]::WriteAllText((Join-Path $run "$Role.commands"), '')
    $arguments = @('-batchmode', '-nographics', '-logFile', ('"' + (Join-Path $run "$Role.log") + '"'),
        '--dn-role', $Role, '--dn-port', $(if ($Role -eq 'host') { $Port } else { $ClientPort }), '--dn-save-dir', ('"' + $saves + '"'),
        '--dn-report', ('"' + (Join-Path $run "$Role.json") + '"'), '--dn-commands', ('"' + (Join-Path $run "$Role.commands") + '"'))
    $processes[$Role] = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
}
function Send([string]$Role, [hashtable]$Command) {
    [IO.File]::AppendAllText((Join-Path $run "$Role.commands"), ($Command | ConvertTo-Json -Compress) + "`n")
}
function Receipt([string]$Role, [hashtable]$Command) {
    $before = Wait-Report $Role { param($r) $r.ready }
    $last = @($before.feedback | Where-Object { !$_.ReadyReply } | Sort-Object Sequence | Select-Object -Last 1)
    $sequence = if ($last.Count) { $last[0].Sequence } else { 0 }
    Send $Role $Command
    $after = Wait-Report $Role { param($r) @($r.feedback | Where-Object { !$_.ReadyReply -and $_.Sequence -gt $sequence }).Count -gt 0 }
    return @($after.feedback | Where-Object { !$_.ReadyReply -and $_.Sequence -gt $sequence } | Sort-Object Sequence)[-1]
}
function Check([string]$Name, [bool]$Ok) { $checks[$Name] = $Ok; if (!$Ok) { throw "Failed: $Name" } }
try {
    Start-Player 'host'
    $null = Wait-Report 'host' { param($r) $r.ready }
    $null = Receipt 'host' @{ operation='SetPaused'; value=1 }
    foreach ($role in @('client1','client2','client3')) { Start-Player $role; $null = Wait-Report $role { param($r) $r.ready } }
    $initial = Wait-Report 'host' { param($r) $r.frame.ReadyCount -eq 4 }
    $workers = @($initial.frame.World.Actors | Where-Object Kind -eq 'worker')
    $requests = @{}
    $positions = @(184,708,756)
    for ($i=1; $i -le 3; $i++) {
        $role = "client$i"
        $requests[$role] = @{ operation='raw'; intent='PlaceBuilding'; sequence=100; kind='tower'; x=$positions[$i-1] }
        Send $role $requests[$role]
    }
    $receipts = @{}
    foreach ($role in @('client1','client2','client3')) {
        $r = Wait-Report $role { param($r) @($r.feedback | Where-Object { !$_.ReadyReply -and $_.Sequence -eq 100 }).Count -gt 0 }
        $receipts[$role] = @($r.feedback | Where-Object { !$_.ReadyReply -and $_.Sequence -eq 100 })[-1]
    }
    Check 'concurrent_three_purchases_only_two_can_pay' (@($receipts.Values | Where-Object Code -eq 'Applied').Count -eq 2 -and @($receipts.Values | Where-Object Code -eq 'NoEffect').Count -eq 1)
    $paid = Wait-Report 'host' { param($r) $r.frame.World.Buildings.Count -eq 6 }
    Check 'concurrent_payment_is_atomic' ($paid.frame.World.Camp.Stock.Wood -eq 10 -and $paid.frame.World.Camp.Stock.Stone -eq 10)
    $winner = @($receipts.Keys | Where-Object { $receipts[$_].Code -eq 'Applied' })[0]
    $before = Read-Report $winner
    $count = @($before.feedback | Where-Object { !$_.ReadyReply -and $_.Sequence -eq 100 }).Count
    Send $winner $requests[$winner]
    Send $winner $requests[$winner]
    $replayed = Wait-Report $winner { param($r) @($r.feedback | Where-Object { !$_.ReadyReply -and $_.Sequence -eq 100 }).Count -ge $count + 2 }
    Check 'actual_network_resend_returns_original_entity_without_payment' (@($replayed.feedback | Where-Object { $_.Sequence -eq 100 -and $_.EntityId -ne $receipts[$winner].EntityId }).Count -eq 0 -and $replayed.frame.World.Camp.Stock.Wood -eq 10)
    Send $winner @{operation='raw'; intent='Recruit'; sequence=100}
    $null = Wait-Report $winner { param($r) @($r.feedback | Where-Object Code -eq 'SequenceConflict').Count -gt 0 }
    Check 'sequence_reuse_with_changed_intent_rejected' $true
    Send 'client1' @{operation='raw'; intent='Repair'; sequence=101; target=999999}
    $null = Wait-Report 'client1' { param($r) @($r.feedback | Where-Object { $_.Sequence -eq 101 -and $_.Code -eq 'InvalidRequest' }).Count -gt 0 }
    Check 'illegal_entity_target_rejected' $true
    $null = Receipt 'host' @{operation='SetControlMode';value=1}
    $null = Wait-Report 'client2' { param($r) $r.frame.HostOnly }
    Send 'client2' @{operation='raw';intent='Recruit';sequence=101;policy=0}
    $null = Wait-Report 'client2' { param($r) @($r.feedback | Where-Object Code -eq 'PolicyChanged').Count -gt 0 }
    Check 'old_policy_request_rejected_after_switch' $true
    $denied = Receipt 'client3' @{operation='TrainActors';actors=@($workers[4].Id);kind='spearman'}
    Check 'host_only_blocks_guest_training' ($denied.Code -eq 'PermissionDenied')
    $null = Receipt 'host' @{operation='SetControlMode';value=0}
    $null = Wait-Report 'client1' { param($r) !$r.frame.HostOnly }
    $null = Wait-Report 'client2' { param($r) !$r.frame.HostOnly }
    $site = @($paid.frame.World.Worksites | Where-Object Kind -eq 'stone')[0]
    Send 'client1' @{operation='raw';intent='IssueOrders';sequence=102;actors=@($workers[3].Id);target=$site.Id;x=$site.X}
    Send 'client2' @{operation='raw';intent='IssueOrders';sequence=102;actors=@($workers[4].Id);target=$site.Id;x=$site.X}
    $claims = @()
    foreach($role in @('client1','client2')) {
        $r=Wait-Report $role {param($r) @($r.feedback | Where-Object { !$_.ReadyReply -and $_.Sequence -eq 102 }).Count -gt 0}
        $claims += @($r.feedback | Where-Object { !$_.ReadyReply -and $_.Sequence -eq 102 })[-1]
    }
    Check 'shared_worksite_claim_has_one_winner' (@($claims | Where-Object Code -eq 'Applied').Count -eq 1 -and @($claims | Where-Object Code -eq 'NoEffect').Count -eq 1)
    Send 'host' @{operation='Restart'}
    $null=Wait-Report 'client1' {param($r) $r.ready -and $r.frame.Epoch -eq 2}
    Send 'client1' @{operation='raw';intent='Recruit';sequence=200;epoch=1}
    $stale=Wait-Report 'client1' {param($r) @($r.feedback | Where-Object Code -eq 'EpochChanged').Count -gt 0}
    Check 'old_world_request_cannot_mutate_restarted_camp' ($stale.frame.World.Camp.Population -eq 7)
    foreach($role in $processes.Keys) {
        $log=Get-Content -LiteralPath (Join-Path $run "$role.log") -Raw
        Check ($role+'_no_runtime_exception') ($log -notmatch '(?im)(Exception:|Shader error|\[AppStartup\].*(failed|cancelled))')
    }
}
catch { $failure=$_.Exception.ToString() }
finally {
    foreach($p in $processes.Values) { $p.Refresh(); if(!$p.HasExited) {Stop-Process -Id $p.Id; $p.WaitForExit()} }
    [ordered]@{passed=(!$failure);checks=$checks;error=$failure;scope='Four real processes; simultaneous purchases and worksite, replay, illegal target, stale policy/epoch';artifacts=$run;
      gameCodeSha256=(Get-FileHash (Join-Path (Split-Path $player -Parent) 'DarkNights_Data/Managed/DarkNights.Entry.dll')).Hash} | ConvertTo-Json -Depth 7 | Set-Content (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Concurrency: passed=$(!$failure) checks=$($checks.Count); $run/result.json"
}
if($failure) {throw $failure}
