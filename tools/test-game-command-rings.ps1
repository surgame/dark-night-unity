param([Parameter(Mandatory)][string]$PlayerPath, [int]$Port = 28410, [int]$ClientPort = 0, [switch]$Capture)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$player = [IO.Path]::GetFullPath($PlayerPath)
if (!(Test-Path -LiteralPath $player)) { throw 'Build the corrected Mono Player first.' }
if (!$ClientPort) { $ClientPort = $Port }
$run = Join-Path $repo ('artifacts/local-command-rings/session-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $run | Out-Null
$saves = Join-Path $run 'saves'
$processes = @{}
$checks = [ordered]@{}
$failure = $null
function Read-Report([string]$Role) {
    $path = Join-Path $run "$Role.json"
    if (!(Test-Path -LiteralPath $path)) { return $null }
    $stream = [IO.FileStream]::new($path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
        [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
    $reader = [IO.StreamReader]::new($stream)
    try { return $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
}
function Wait-Report([string]$Role, [scriptblock]$Predicate) {
    $deadline = [DateTime]::UtcNow.AddSeconds(40)
    do {
        $report = Read-Report $Role
        if ($report -and $report.error) { throw "$Role reported: $($report.error)" }
        if ($report -and (& $Predicate $report)) { return $report }
        $processes[$Role].Refresh()
        if ($processes[$Role].HasExited) { throw "$Role exited; see $run/$Role.log" }
        Start-Sleep -Milliseconds 50
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timeout for $Role condition: $Predicate"
}
function Start-Player([string]$Role) {
    [IO.File]::WriteAllText((Join-Path $run "$Role.commands"), '')
    $connectPort = if ($Role -eq 'host') { $Port } else { $ClientPort }
    $arguments = @('-batchmode', '-screen-width', '1280', '-screen-height', '800', '-screen-fullscreen', '0',
        '-logFile', ('"' + (Join-Path $run "$Role.log") + '"'), '--dn-role', $Role, '--dn-port', $connectPort,
        '--dn-save-dir', ('"' + $saves + '"'),
        '--dn-report', ('"' + (Join-Path $run "$Role.json") + '"'), '--dn-commands', ('"' + (Join-Path $run "$Role.commands") + '"'))
    if (!$Capture) { $arguments += '-nographics' }
    $processes[$Role] = Start-Process -FilePath $player -ArgumentList $arguments -PassThru -WindowStyle Hidden
}
function Send([string]$Role, [hashtable[]]$Commands) {
    $before = Wait-Report $Role { param($r) $true }
    $required = $before.commandsConsumed + $Commands.Count
    $lines = @($Commands | ForEach-Object { $_ | ConvertTo-Json -Compress }) -join "`n"
    [IO.File]::AppendAllText((Join-Path $run "$Role.commands"), $lines + "`n")
    return Wait-Report $Role { param($r) $r.commandsConsumed -ge $required }
}
function Receipt([string]$Role, [hashtable]$Command) {
    $before = Wait-Report $Role { param($r) $true }
    $last = @($before.feedback | Where-Object { !$_.ReadyReply } | Sort-Object Sequence | Select-Object -Last 1)
    $sequence = if ($last.Count) { $last[0].Sequence } else { 0 }
    $null = Send $Role @($Command)
    $report = Wait-Report $Role { param($r) @($r.feedback | Where-Object { !$_.ReadyReply -and $_.Sequence -gt $sequence }).Count -gt 0 }
    return @($report.feedback | Where-Object { !$_.ReadyReply -and $_.Sequence -gt $sequence } | Sort-Object Sequence)[-1]
}
function Check([string]$Name, [bool]$Passed) {
    $checks[$Name] = $Passed
    if (!$Passed) { throw "Failed: $Name" }
}
function Click([string]$Role, [int]$Actor, [string]$Screenshot) {
    $commands = @(@{ operation='input-orders'; actor=$Actor; x=510 })
    if ($Capture) { $commands += @{ operation='capture'; file=$Screenshot; x=510 } }
    return Send $Role $commands
}
try {
    Start-Player 'host'
    $h = Wait-Report 'host' { param($r) $r.ready -and $r.uiPage -eq '' -and $r.entityViews -eq 17 }
    Check 'host_prewarmed_local_pool_is_idle' ($h.commandRingInstances -eq 8 -and $h.commandRings -eq 0)
    Check 'host_pause_applies' ((Receipt 'host' @{operation='SetPaused';value=1}).Code -eq 'Applied')
    Start-Player 'client'
    $c = Wait-Report 'client' { param($r) $r.ready -and $r.uiPage -eq '' -and $r.frame.Paused -and $r.entityViews -eq 17 }
    Check 'client_prewarmed_local_pool_is_idle' ($c.commandRingInstances -eq 8 -and $c.commandRings -eq 0)
    $worker = @($c.frame.World.Actors | Where-Object Kind -eq 'worker')[0].Id
    Check 'business_order_still_applies' ((Receipt 'client' @{operation='IssueOrders';actors=@($worker);x=500}).Code -eq 'Applied')
    $h = Wait-Report 'host' { param($r) $r.frame.World.Actors[0].Activity -eq 'Move' }
    $c = Read-Report 'client'
    Check 'business_path_does_not_create_circles_on_either_peer' ($h.commandRingShows -eq 0 -and $c.commandRingShows -eq 0)

    $c = Click 'client' $worker 'guest-own-click.png'
    Check 'guest_input_immediately_shows_one_local_circle' ($c.commandRings -eq 1 -and $c.commandRingShows -eq 1)
    $h = Wait-Report 'host' { param($r) $r.frame.ServerTick -gt $c.frame.ServerTick + 6 }
    Check 'host_never_presents_guest_circle' ($h.commandRingShows -eq 0)
    if ($Capture) { $null = Send 'host' @(@{operation='capture';file='host-after-guest-click.png';x=510}) }
    $c = Wait-Report 'client' { param($r) $r.commandRings -eq 0 }
    Check 'guest_circle_expires_while_paused_without_replay' ($c.frame.Paused -and $c.commandRingShows -eq 1)
    Check 'projection_contains_no_command_circle' (@($c.frame.Events | Where-Object { $_.Cue.Kind -eq 'command' }).Count -eq 0)

    $h = Click 'host' $worker 'host-own-click.png'
    Check 'host_input_immediately_shows_one_local_circle' ($h.commandRings -eq 1 -and $h.commandRingShows -eq 1)
    $c = Wait-Report 'client' { param($r) $r.frame.ServerTick -gt $h.frame.ServerTick + 6 }
    Check 'guest_never_presents_host_circle' ($c.commandRingShows -eq 1)
    if ($Capture) { $null = Send 'client' @(@{operation='capture';file='guest-after-host-click.png';x=510}) }

    $burst = @(1..16 | ForEach-Object { @{operation='input-orders';actor=$worker;x=(520 + $_)} })
    $c = Send 'client' $burst
    Check 'burst_is_bounded_and_reuses_eight_instances' ($c.commandRings -eq 8 -and $c.commandRingShows -eq 17 -and $c.commandRingInstances -eq 8)
    $null = Wait-Report 'client' { param($r) $r.commandRings -eq 0 }
    Check 'host_only_policy_applies' ((Receipt 'host' @{operation='SetControlMode';value=1}).Code -eq 'Applied')
    $null = Wait-Report 'client' { param($r) $r.frame.HostOnly }
    $c = Click 'client' $worker 'guest-denied-click.png'
    Check 'known_permission_denial_does_not_show_circle' ($c.commandRingShows -eq 17 -and $c.commandRings -eq 0)
    $null = Receipt 'host' @{operation='SetControlMode';value=0}
    $null = Wait-Report 'client' { param($r) !$r.frame.HostOnly }

    $oldEpoch = (Read-Report 'host').frame.Epoch
    $null = Send 'host' @(@{operation='input-orders';actor=$worker;x=570}, @{operation='Restart'})
    $h = Wait-Report 'host' { param($r) $r.ready -and $r.frame.Epoch -gt $oldEpoch -and $r.commandRings -eq 0 }
    $c = Wait-Report 'client' { param($r) $r.ready -and $r.frame.Epoch -eq $h.frame.Epoch -and $r.commandRings -eq 0 }
    Check 'new_epoch_clears_circles_and_retains_pool' ($h.commandRingInstances -eq 8 -and $c.commandRingInstances -eq 8)
    $null = Receipt 'host' @{operation='SetPaused';value=1}
    $null = Wait-Report 'client' { param($r) $r.frame.Paused }
    $null = Send 'client' @(@{operation='input-orders';actor=$worker;x=580}, @{operation='disconnect'})
    $c = Wait-Report 'client' { param($r) !$r.ready -and $null -eq $r.frame -and $r.commandRings -eq 0 }
    Check 'disconnect_clears_active_circle' ($c.commandRingShows -eq 18 -and $c.commandRingInstances -eq 8)
    $null = Send 'client' @(@{operation='connect'})
    $c = Wait-Report 'client' { param($r) $r.ready -and $r.entityViews -eq 17 }
    Check 'reconnect_does_not_replay_or_recreate_circles' ($c.commandRingShows -eq 18 -and $c.commandRings -eq 0 -and $c.commandRingInstances -eq 8)
    $h = Read-Report 'host'
    Check 'host_does_not_replay_after_epoch_or_guest_reconnect' ($h.commandRingShows -eq 2 -and $h.commandRings -eq 0)
    foreach ($role in @('host','client')) {
        $log = Get-Content -LiteralPath (Join-Path $run "$role.log") -Raw
        Check ($role + '_creates_only_eight_native_command_objects') ([regex]::Matches($log, "Successfully created LOCAL 'Command'").Count -eq 8)
        Check ($role + '_no_runtime_exception') ($log -notmatch '(?im)(Exception:|Shader error|\[AppStartup\].*(failed|cancelled))')
    }
}
catch { $failure = $_.Exception.ToString() }
finally {
    $result = [ordered]@{passed=(!$failure);checks=$checks;error=$failure;player=$player;
        playerSha256=(Get-FileHash -LiteralPath $player -Algorithm SHA256).Hash;artifacts=$run;
        port=$Port;clientPort=$ClientPort;saveDirectory=$saves;renderedBatchMode=[bool]$Capture;processes=@($processes.Values | ForEach-Object Id);
        scope='Independent Mono Host/client, local input route, pooled native circles, permissions, epoch and reconnect; no foreground performance acceptance'}
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    foreach ($process in $processes.Values) {
        $process.Refresh()
        if (!$process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit() }
    }
    Write-Output "Local command rings: passed=$($result.passed) checks=$($checks.Count); $run/result.json"
    if ($failure) { Write-Output $failure }
}
if ($failure) { exit 1 }
