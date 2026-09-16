param([ValidateSet('mono', 'il2cpp')][string]$Backend = 'mono', [int]$Port = 27991, [string]$PlayerPath = '')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$player = if ($PlayerPath) { [IO.Path]::GetFullPath($PlayerPath) } else { Join-Path $repo "artifacts/migration/player-$Backend/DarkNights.exe" }
$gameCode = Join-Path (Split-Path $player -Parent) $(if ($Backend -eq 'mono') { 'DarkNights_Data/Managed/DarkNights.Entry.dll' } else { 'GameAssembly.dll' })
if (!(Test-Path -LiteralPath $player)) { throw "Build the formal $Backend Player first." }
$run = Join-Path $repo ('artifacts/migration/session-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $run | Out-Null
$processes = @{}
$checks = [ordered]@{}
function Read-Report([string]$Role) {
    $path = Join-Path $run "$Role.json"
    if (Test-Path -LiteralPath $path) {
        try {
            $stream = [IO.FileStream]::new($path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
                [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
            $reader = [IO.StreamReader]::new($stream)
            try { return $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        } catch { return $null }
    }
    return $null
}
function Wait-Report([string]$Role, [scriptblock]$Predicate, [string]$Description) {
    $deadline = [DateTime]::UtcNow.AddSeconds(40)
    do {
        $report = Read-Report $Role
        if ($report -and $report.error) { throw "$Role reported: $($report.error)" }
        if ($report -and (& $Predicate $report)) { return $report }
        $processes[$Role].Refresh()
        if ($processes[$Role].HasExited) { throw "$Role exited; see $run/$Role.log" }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timeout: $Description; see $run/$Role.log"
}
function Start-Player([string]$Role) {
    $report = Join-Path $run "$Role.json"
    $commands = Join-Path $run "$Role.commands"
    [IO.File]::WriteAllText($commands, '')
    $arguments = @('-batchmode', '-nographics', '-logFile', ('"' + (Join-Path $run "$Role.log") + '"'),
        '--dn-camp-mode', '--dn-role', $Role, '--dn-port', $Port, '--dn-report', ('"' + $report + '"'), '--dn-commands', ('"' + $commands + '"'))
    $processes[$Role] = Start-Process -FilePath $player -ArgumentList $arguments -PassThru -WindowStyle Hidden
}
function Send-Operation([string]$Role, [hashtable]$Command) {
    $before = Wait-Report $Role { param($r) $true } 'Stable pre-command report'
    $last = @($before.feedback | Where-Object { !$_.ReadyReply } | Sort-Object Sequence | Select-Object -Last 1)
    $lastSequence = if ($last.Count) { $last[0].Sequence } else { 0 }
    $requiredConsumed = $before.commandsConsumed + 1
    [IO.File]::AppendAllText((Join-Path $run "$Role.commands"), ($Command | ConvertTo-Json -Compress) + "`n")
    $report = Wait-Report $Role { param($r) $r.commandsConsumed -ge $requiredConsumed -and @($r.feedback | Where-Object { !$_.ReadyReply -and $_.Sequence -gt $lastSequence }).Count -gt 0 } $Command.operation
    return @($report.feedback | Where-Object { !$_.ReadyReply -and $_.Sequence -gt $lastSequence } | Sort-Object Sequence)[-1]
}
function Check([string]$Name, [bool]$Passed) {
    $checks[$Name] = $Passed
    if (!$Passed) { throw "Failed: $Name" }
}
try {
    Start-Player 'host'
    $hostReport = Wait-Report 'host' { param($r) $r.ready } 'Host Ready'
    Check 'host_ready_with_all_initial_entities' ($hostReport.slot -eq 0 -and $hostReport.frame.World.Actors.Count -eq 7)
    Start-Player 'client'
    $clientReport = Wait-Report 'client' { param($r) $r.ready } 'Client Ready'
    Check 'independent_client_receives_full_late_baseline' ($clientReport.slot -eq 1 -and $clientReport.frame.World.Buildings.Count -eq 4 -and $clientReport.frame.World.Worksites.Count -eq 6)
    $null = Wait-Report 'host' { param($r) $r.uiPage -eq '' -and $r.entityViews -eq 17 } 'Formal HUD and views'
    $null = Wait-Report 'client' { param($r) $r.uiPage -eq '' -and $r.entityViews -eq 17 } 'Client HUD and views'
    $receipt = Send-Operation 'host' @{ operation = 'ui'; panel = 'Chrome'; key = 'Pause' }
    Check 'host_pause_applied' ($receipt.Code -eq 'Applied')
    $receipt = Send-Operation 'host' @{ operation = 'ui'; panel = 'Chrome'; key = 'Recruit' }
    Check 'generated_recruit_button_routes_once' ($receipt.Code -eq 'Applied')
    $null = Wait-Report 'client' { param($r) $r.frame.World.Camp.Population -eq 8 -and $r.frame.World.Camp.Stock.Food -eq 48 } 'One authoritative recruit payment'
    $clientReport = Wait-Report 'client' { param($r) $r.frame.Paused } 'Shared pause'
    $wood = $clientReport.frame.World.Camp.Stock.Wood
    $receipt = Send-Operation 'client' @{ operation = 'PlaceBuilding'; kind = 'house'; x = 184 }
    Check 'guest_build_paid_once' ($receipt.Code -eq 'Applied' -and $receipt.EntityId -gt 0)
    $clientReport = Wait-Report 'client' { param($r) $r.frame.World.Buildings.Count -eq 5 } 'Built house projection'
    $hostReport = Wait-Report 'host' { param($r) $r.frame.World.Buildings.Count -eq 5 } 'Host built house projection'
    Check 'host_and_client_share_single_payment' ($clientReport.frame.World.Camp.Stock.Wood -lt $wood -and $clientReport.frame.World.Camp.Stock.Wood -eq $hostReport.frame.World.Camp.Stock.Wood)
    $receipt = Send-Operation 'client' @{ operation = 'SetSpeed'; value = 2 }
    Check 'guest_cannot_change_time' ($receipt.Code -eq 'PermissionDenied')
    $receipt = Send-Operation 'host' @{ operation = 'SetControlMode'; value = 1 }
    Check 'host_only_enabled' ($receipt.Code -eq 'Applied')
    $null = Wait-Report 'client' { param($r) $r.frame.HostOnly } 'Control policy projection'
    $receipt = Send-Operation 'client' @{ operation = 'Recruit' }
    Check 'host_only_rejects_guest_camp_mutation' ($receipt.Code -eq 'PermissionDenied')
    $null = Send-Operation 'host' @{ operation = 'SetControlMode'; value = 0 }
    $null = Wait-Report 'client' { param($r) !$r.frame.HostOnly } 'SharedCamp restored'
    $actor = @($clientReport.frame.World.Actors | Where-Object Kind -eq 'worker')[1].Id
    $receipt = Send-Operation 'client' @{ operation = 'IssueOrders'; actors = @($actor); x = 700 }
    Check 'guest_explicit_move_accepted' ($receipt.Code -eq 'Applied')
    $null = Send-Operation 'host' @{ operation = 'SetPaused'; value = 0 }
    $clientReport = Wait-Report 'client' { param($r) @($r.frame.World.Actors | Where-Object { $_.Id -eq $actor -and $_.Walking }).Count -eq 1 } 'Moving actor'
    Check 'client_observes_authoritative_motion' ($clientReport.frame.Elapsed -gt 0)
    $null = Send-Operation 'host' @{ operation = 'SetPaused'; value = 1 }
    $null = Wait-Report 'client' { param($r) $r.frame.Paused } 'Stable comparison'
    Start-Sleep -Milliseconds 500
    $hostReport = Read-Report 'host'
    $clientReport = Read-Report 'client'
    Check 'complete_world_converges_while_paused' (($hostReport.frame.World | ConvertTo-Json -Depth 15 -Compress) -eq ($clientReport.frame.World | ConvertTo-Json -Depth 15 -Compress))
    $runtimeErrors = '(?im)^(?:[A-Za-z_][\w]*\.)*[A-Za-z_][\w]*Exception:|^Font texture|\[AppStartup\].*(failed|cancelled)'
    $checks['no_player_failure'] = !$hostReport.error -and !$clientReport.error -and
        (Get-Content (Join-Path $run 'host.log') -Raw) -notmatch $runtimeErrors -and
        (Get-Content (Join-Path $run 'client.log') -Raw) -notmatch $runtimeErrors
}
catch {
    $checks['completed_without_error'] = $false
    $failure = $_.ToString()
    Write-Output $failure
}
finally {
    $result = [ordered]@{ passed = @($checks.Values | Where-Object { !$_ }).Count -eq 0; backend = $Backend;
        player = $player; playerSha256 = (Get-FileHash -LiteralPath $player -Algorithm SHA256).Hash;
        gameCodeSha256 = (Get-FileHash -LiteralPath $gameCode -Algorithm SHA256).Hash;
        scope = 'Formal two-process network, instantiated native views and generated UGUI pause/recruit bindings; no pixel or weak-network acceptance';
        processes = @($processes.Values | ForEach-Object Id); checks = $checks; failure = $failure; artifacts = $run }
    $result | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    foreach ($process in $processes.Values) {
        $process.Refresh()
        if (!$process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit() }
    }
    Write-Output "Formal session: passed=$($result.passed) checks=$($checks.Count); $run/result.json"
}
if (!$result.passed) { exit 1 }
