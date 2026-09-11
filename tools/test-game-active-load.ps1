param([int]$Port = 28260, [int]$ClientPort = 0, [int]$MinimumUptimeSeconds = 35)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$player = Join-Path $repo 'artifacts/migration/player-mono/DarkNights.exe'
$run = Join-Path $repo ('artifacts/migration/active-load-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
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
    $null=Wait-Report 'host' {param($r) $r.ready}
    Send 'host' @{operation='pause-on-projectile'}
    $null=Receipt 'host' @{operation='StartNight'}
    $r=Wait-Report 'host' {param($r) $r.frame.Paused -and $r.frame.World.Projectiles.Count -gt 0}
    $null=Receipt 'host' @{operation='SetSpeed';value=2}
    Check 'real_inflight_arrow_frozen' ($r.frame.Paused -and $r.frame.World.Projectiles.Count -gt 0)
    $workers=@($r.frame.World.Actors | Where-Object Kind -eq 'worker')
    $null=Receipt 'host' @{operation='PlaceBuilding';kind='house';x=184;actors=@($workers[0].Id)}
    $null=Receipt 'host' @{operation='TrainActors';kind='archer';actors=@($workers[1].Id)}
    Start-Player 'client1'
    $guest=Wait-Report 'client1' {param($r) $r.ready -and $r.frame.Paused -and $r.arrowViews -gt 0}
    Check 'late_join_sees_construction_training_and_inflight_arrow' ($guest.frame.World.Buildings.Count -eq 5 -and @($guest.frame.World.Actors | Where-Object Activity -eq 'TrainingMove').Count -eq 1)
    $saved=Read-Report 'host'
    $null=Receipt 'host' @{operation='Save';value=0}
    $null=Wait-Report 'host' {param($r) !$r.storageBusy -and $r.storageStatus -like '*已保存*'}
    $null=Receipt 'host' @{operation='SetPaused';value=0}
    $null=Wait-Report 'host' {param($r) $r.frame.Elapsed -gt $saved.frame.Elapsed + 3}
    Send 'host' @{operation='BeginLoad';value=0}
    foreach($role in @('host','client1')) {
        $restored=Wait-Report $role {param($r) $r.ready -and $r.frame.Epoch -eq 2 -and $r.frame.Paused -and $r.arrowViews -gt 0}
        $expected=$saved.frame.World | ConvertTo-Json -Depth 20 -Compress | ConvertFrom-Json
        $actual=$restored.frame.World | ConvertTo-Json -Depth 20 -Compress | ConvertFrom-Json
        foreach($world in @($expected,$actual)) {foreach($arrow in $world.Projectiles) {$arrow.PSObject.Properties.Remove('Id')}}
        Check ($role+'_restores_every_gameplay_field') (($expected | ConvertTo-Json -Depth 20 -Compress) -eq ($actual | ConvertTo-Json -Depth 20 -Compress))
        Check ($role+'_restores_simulation_time_and_speed') ($restored.frame.Elapsed -eq $saved.frame.Elapsed -and $restored.frame.Speed -eq 2)
    }
    $null=Receipt 'host' @{operation='SetPaused';value=0}
    $r=Wait-Report 'host' {param($r) $r.frame.Elapsed -gt $saved.frame.Elapsed + 3}
    Check 'loaded_projectile_and_tasks_resume' ($r.ready -and $r.frame.Epoch -eq 2)
    foreach($role in $processes.Keys) {
        $log=Get-Content (Join-Path $run "$role.log") -Raw
        Check ($role+'_no_runtime_exception') ($log -notmatch '(?im)(Exception:|Shader error|\[AppStartup\].*(failed|cancelled))')
    }
}
catch {$failure=$_.Exception.ToString()}
finally {
    foreach($p in $processes.Values) {$p.Refresh();if(!$p.HasExited){Stop-Process -Id $p.Id;$p.WaitForExit()}}
    [ordered]@{passed=(!$failure);checks=$checks;error=$failure;artifacts=$run;
      gameCodeSha256=(Get-FileHash (Join-Path (Split-Path $player -Parent) 'DarkNights_Data/Managed/DarkNights.Entry.dll')).Hash} |
      ConvertTo-Json -Depth 8 | Set-Content (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Active load: passed=$(!$failure) checks=$($checks.Count); $run/result.json"
}
if($failure){throw $failure}
