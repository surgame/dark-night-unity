param([int]$Port = 28240, [int]$ClientPort = 0, [int]$DurationSeconds = 30, [string]$PlayerPath = '',
    [switch]$CompactReports, [ValidateSet('none','host','client1','client2','client3')][string]$ForegroundRole = 'none')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$player = if ($PlayerPath) { [IO.Path]::GetFullPath($PlayerPath) } else { Join-Path $repo 'artifacts/migration/player-mono/DarkNights.exe' }
$run = Join-Path $repo ('artifacts/migration/pressure-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$saves = Join-Path $run 'saves'
New-Item -ItemType Directory -Path $run | Out-Null
$processes = @{}
. (Join-Path $PSScriptRoot 'player-memory-sampling.ps1')
$checks = [ordered]@{}
$failure = $null
$publicationWindow = $null
$measurementStartUtc = $null
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
    Sample-PlayerMemory $processes
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
    $arguments = @('-screen-width', '1280', '-screen-height', '800', '-screen-fullscreen', '0', '-logFile', ('"' + (Join-Path $run "$Role.log") + '"'),
        '--dn-role', $Role, '--dn-port', $(if ($Role -eq 'host') { $Port } else { $ClientPort }), '--dn-save-dir', ('"' + $saves + '"'),
        '--dn-report', ('"' + (Join-Path $run "$Role.json") + '"'), '--dn-commands', ('"' + (Join-Path $run "$Role.commands") + '"'))
    $arguments += '--dn-metrics'
    if ($Role -eq 'host') { $arguments += '--dn-projection-pressure' }
    $style = if ($Role -eq $ForegroundRole) { 'Normal' } else { 'Hidden' }
    $processes[$Role] = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle $style -PassThru
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
$relay = $null
$ClientPort = $Port + 1
try {
    $relayReport = Join-Path $run 'udp.json'
    $arguments = @(('"' + (Join-Path $PSScriptRoot 'lan-netem.py') + '"'), '--listen', $ClientPort, '--target', $Port,
        '--loss', '0', '--delay', '0', '--jitter', '0', '--report', ('"' + $relayReport + '"'))
    $relay = Start-Process -FilePath 'python' -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Start-Player 'host'
    $hostBaseline = Wait-Report 'host' { param($r) $r.ready -and $r.entityViews -eq 17 -and $r.arrowViews -eq 1024 }
    Check 'host_retains_one_authoritative_object_per_real_entity' ($hostBaseline.frame.World.Identities.Count -eq 256)
    $null = Receipt 'host' @{operation='SetPaused';value=1}
    foreach($role in @('client1','client2','client3')) {
        Start-Player $role
        $r = Wait-Report $role { param($r) $r.ready -and $r.entityViews -eq 256 -and $r.arrowViews -eq 1024 }
        Check ($role+'_full_limit_received_and_rendered') ($r.frame.World.Identities.Count -eq 256 -and $r.frame.World.Projectiles.Count -eq 1024)
    }
    foreach($role in $processes.Keys) {
        $before = Wait-Report $role {param($r) $r.ready}
        $expectedCommands = $before.commandsConsumed + $(if ($CompactReports) { 2 } else { 1 })
        if ($CompactReports) { Send $role @{operation='report-detail';value=0} }
        Send $role @{operation='metrics-reset'}
        $null = Wait-Report $role {param($r) $r.commandsConsumed -ge $expectedCommands -and
            (!$CompactReports -or $r.reportDetail -eq 'summary')}
    }
    $first = Wait-Report 'host' {param($r) $r.readyCount -eq 4}
    $started=[DateTime]::UtcNow
    $measurementStartUtc=$started.ToString('O')
    $deadline=$started.AddSeconds($DurationSeconds)
    Write-Output "Measuring four-process limit for $DurationSeconds seconds; payload=$($first.serverPayloadBytes) bytes."
    do {
        foreach($role in $processes.Keys) { $null=Wait-Report $role {param($r) $r.ready} }
        Start-Sleep -Seconds 1
    } while([DateTime]::UtcNow -lt $deadline)
    # 报告由 Player 原子替换，重试瞬时文件访问失败，避免把空读当作发布停止。
    $last=Wait-Report 'host' {param($r) $r.publication -gt $first.publication}
    $publicationWindow=[ordered]@{first=$first.publication;last=$last.publication;
        firstTick=$first.serverTick;lastTick=$last.serverTick;
        readyCount=$last.readyCount;requiredDelta=$DurationSeconds * 5;
        elapsedSeconds=([DateTime]::UtcNow-$started).TotalSeconds}
    $publishing=($last.readyCount -eq 4 -and $last.publication -gt $first.publication + $DurationSeconds * 5)
    foreach($role in $processes.Keys) { Send $role @{operation='metrics';file="$role-metrics.json"} }
    Start-Sleep -Seconds 3
    $metrics=[ordered]@{}
    Sample-PlayerMemory $processes -Force
    foreach($role in $processes.Keys) {
        $m=Get-Content (Join-Path $run "$role-metrics.json") -Raw | ConvertFrom-Json
        $metrics[$role]=$m
        Check ($role+'_metrics_captured') ($m.frameMilliseconds.samples -gt 30 -and $m.entityCount -eq 256 -and $m.projectileCount -eq 1024)
        if ($role -eq $ForegroundRole) {
            Check ($role+'_os_foreground_observation_covers_window') ($m.foregroundFrameMilliseconds.samples -ge $m.frameMilliseconds.samples * .95)
        }
        Check ($role+'_windows_working_set_recorded') (@(Get-PlayerMemorySamples $role).Count -ge 3)
        $log=Get-Content -LiteralPath (Join-Path $run "$role.log") -Raw
        Check ($role+'_no_runtime_exception') ($log -notmatch '(?im)(Exception:|Shader error|\[AppStartup\].*(failed|cancelled))')
    }
    [IO.File]::WriteAllText($relayReport+'.stop','')
    $null=$relay.WaitForExit(5000)
    $udp=Get-Content $relayReport -Raw | ConvertFrom-Json
    Check 'actual_udp_payload_measured' ($udp.server_to_client_bytes -gt 1000000)
    Check 'full_projection_keeps_publishing_with_four_ready_peers' $publishing
}
catch { $failure=$_.Exception.ToString() }
finally {
    foreach($p in $processes.Values) { $p.Refresh(); if(!$p.HasExited) {Stop-Process -Id $p.Id; $p.WaitForExit()} }
    if($relay) { $relay.Refresh(); if(!$relay.HasExited) {Stop-Process -Id $relay.Id; $relay.WaitForExit()} }
    [ordered]@{passed=(!$failure);checks=$checks;error=$failure;scope='Synthetic supported projection ceiling, four rendered Mono processes, real UDP relay';
      publicationWindow=$publicationWindow;measurementStartUtc=$measurementStartUtc;compactReports=[bool]$CompactReports;foregroundRole=$ForegroundRole;
      metrics=$metrics;osMemorySamples=$script:playerMemorySamples.ToArray();udp=$udp;artifacts=$run;gameCodeSha256=(Get-FileHash (Join-Path (Split-Path $player -Parent) 'DarkNights_Data/Managed/DarkNights.Entry.dll')).Hash} |
      ConvertTo-Json -Depth 12 | Set-Content (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Pressure: passed=$(!$failure) checks=$($checks.Count); $run/result.json"
}
if($failure) {throw $failure}
