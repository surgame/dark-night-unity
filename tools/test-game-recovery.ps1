param([int]$Port = 27997, [int]$ClientPort = 0, [int]$MinimumUptimeSeconds = 35, [string]$PlayerPath = '')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$player = if ($PlayerPath) { [IO.Path]::GetFullPath($PlayerPath) } else { Join-Path $repo 'artifacts/migration/player-mono/DarkNights.exe' }
$run = Join-Path $repo ('artifacts/migration/recovery-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$saves = Join-Path $run 'saves'
$versionedSaves = Join-Path $saves 'v3'
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
        '--dn-camp-mode', '--dn-role', $Role, '--dn-port', $(if ($Role -eq 'host') { $Port } else { $ClientPort }), '--dn-save-dir', ('"' + $saves + '"'),
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
    $null = Wait-Report 'host' { param($r) $r.ready -and $r.uiPage -eq '' }
    $null = Receipt 'host' @{ operation='SetPaused'; value=1 }
    foreach ($role in @('client1','client2','client3')) {
        Start-Player $role
        $null = Wait-Report $role { param($r) $r.ready }
    }
    $before = Wait-Report 'host' { param($r) $r.frame.ReadyCount -eq 4 }
    $world = $before.frame.World | ConvertTo-Json -Depth 20 -Compress
    Send 'host' @{ operation='ui'; panel='Chrome'; key='Menu' }
    $null = Wait-Report 'host' { param($r) $r.uiPage -eq 'PauseMenu' }
    $receipt = Receipt 'host' @{ operation='ui'; panel='PauseMenu'; key='Save' }
    $null = Wait-Report 'host' { param($r) !$r.storageBusy -and $r.storageStatus -like '*已保存*' }
    Check 'native_save_button_commits_isolated_slot' ($receipt.Code -eq 'Applied' -and (Test-Path (Join-Path $versionedSaves 'slot-00.dnsave.json')))
    $savedText = Get-Content (Join-Path $versionedSaves 'slot-00.dnsave.json') -Raw
    Check 'new_save_is_v3_with_complete_identities' (($savedText | ConvertFrom-Json).format_version -eq 3 -and
        ($savedText | ConvertFrom-Json).world.identities.Count -eq 17)
    $receipt = Receipt 'client1' @{ operation='Save'; value=1 }
    Check 'guest_cannot_write_host_files' ($receipt.Code -eq 'PermissionDenied' -and !(Test-Path (Join-Path $versionedSaves 'slot-01.dnsave.json')))
    $null = Receipt 'client1' @{ operation='Recruit' }
    $null = Receipt 'host' @{ operation='SetControlMode'; value=1 }
    Write-Output 'Four players saved; testing load after original 30-second Ready timeout.'
    $null = Wait-Report 'host' { param($r) $r.frame.ServerTick -gt $MinimumUptimeSeconds * 60 }
    Send 'host' @{ operation='ui'; panel='PauseMenu'; key='Load' }
    foreach ($role in @('host','client1','client2','client3')) {
        $restored = Wait-Report $role { param($r) $r.ready -and $r.frame.Epoch -eq 2 -and $r.frame.ReadyCount -eq 4 }
        Check ($role + '_restores_saved_world_and_new_ready_deadline') (($restored.frame.World | ConvertTo-Json -Depth 20 -Compress) -eq $world -and $restored.frame.HostOnly -and $restored.frame.Paused)
    }
    [IO.File]::WriteAllText((Join-Path $versionedSaves 'slot-01.dnsave.json'),
        ($savedText -replace '"identity_sha256":"[0-9a-f]{64}"', '"identity_sha256":"invalid"'))
    $null = Receipt 'host' @{ operation='BeginLoad'; value=1 }
    $bad = Wait-Report 'host' { param($r) !$r.storageBusy -and $r.storageStatus -like '*失败*' }
    Check 'corrupt_save_preserves_world_and_epoch' ($bad.frame.Epoch -eq 2 -and ($bad.frame.World | ConvertTo-Json -Depth 20 -Compress) -eq $world)
    [IO.File]::WriteAllText((Join-Path $versionedSaves 'slot-01.dnsave.json'), ($savedText -replace '"format_version":3', '"format_version":1'))
    $null = Receipt 'host' @{ operation='BeginLoad'; value=1 }
    $oldVersion = Wait-Report 'host' { param($r) !$r.storageBusy -and $r.storageStatus -eq '不支持的存档版本。' }
    Check 'old_version_has_explicit_refusal_and_keeps_world' ($oldVersion.frame.Epoch -eq 2 -and
        ($oldVersion.frame.World | ConvertTo-Json -Depth 20 -Compress) -eq $world)
    $old = Wait-Report 'client2' { param($r) $r.ready }
    $generation = @($old.feedback | Where-Object ReadyReply | Select-Object -Last 1)[0].ConnectionGeneration
    Send 'client2' @{ operation='disconnect' }
    $null = Wait-Report 'client2' { param($r) !$r.ready -and $null -eq $r.frame }
    $null = Wait-Report 'host' { param($r) $r.frame.PlayerCount -eq 3 }
    Send 'client2' @{ operation='connect' }
    $rejoined = Wait-Report 'client2' { param($r) $r.ready -and $r.frame.ReadyCount -eq 4 }
    $newGeneration = @($rejoined.feedback | Where-Object ReadyReply | Select-Object -Last 1)[0].ConnectionGeneration
    Check 'reconnect_recovers_original_slot_with_new_generation' ($rejoined.slot -eq 2 -and $newGeneration -gt $generation)
    Check 'reconnect_receives_current_world_and_policy' ($rejoined.frame.HostOnly -and ($rejoined.frame.World | ConvertTo-Json -Depth 20 -Compress) -eq $world)
    Send 'host' @{ operation='Restart' }
    foreach ($role in @('host','client1','client2','client3')) {
        $fresh = Wait-Report $role { param($r) $r.ready -and $r.frame.Epoch -eq 3 -and $r.frame.ReadyCount -eq 4 }
        Check ($role + '_restarts_in_same_room') ($fresh.frame.HostOnly -and $fresh.frame.World.Camp.Population -eq 7 -and $fresh.frame.World.Camp.Kills -eq 0)
    }
    Send 'host' @{ operation='disconnect' }
    foreach ($role in @('host','client1','client2','client3')) {
        $gone = Wait-Report $role { param($r) $null -eq $r.frame -and $r.entityViews -eq 0 -and $r.uiPage -eq 'MainMenu' }
        Check ($role + '_cleans_world_after_host_exit') (!$gone.ready -and $gone.effectViews -eq 0)
    }
    Send 'host' @{ operation='ui'; panel='MainMenu'; key='Continue' }
    $continued = Wait-Report 'host' { param($r) $r.ready -and $r.frame.Epoch -eq 2 }
    Check 'main_menu_continue_loads_selected_saved_world' (($continued.frame.World | ConvertTo-Json -Depth 20 -Compress) -eq $world)
    foreach ($role in $processes.Keys) {
        $log = Get-Content -LiteralPath (Join-Path $run "$role.log") -Raw
        Check ($role + '_no_runtime_exception') ($log -notmatch '(?im)(Exception:|Shader error|\[AppStartup\].*(failed|cancelled))')
    }
}
catch { $failure = $_.Exception.ToString() }
finally {
    foreach ($process in $processes.Values) { $process.Refresh(); if (!$process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit() } }
    $result = [ordered]@{ passed = !$failure; scope='Four Mono processes; native save/load/continue, corrupt file, epoch, credential recovery, restart and host exit'
        checks=$checks; error=$failure; artifacts=$run
        gameCodeSha256=(Get-FileHash -LiteralPath (Join-Path (Split-Path $player -Parent) 'DarkNights_Data/Managed/DarkNights.Entry.dll')).Hash }
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Game recovery: passed=$(!$failure) checks=$($checks.Count); $run/result.json"
}
if ($failure) { throw $failure }
