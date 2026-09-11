param([int]$Port = 27995)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$player = Join-Path $repo 'artifacts/migration/player-mono/DarkNights.exe'
$run = Join-Path $repo ('artifacts/migration/campaign-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $run | Out-Null
$processes = @{}
$checks = [ordered]@{}
$failure = $null
$balance = Get-Content -LiteralPath (Join-Path $repo 'Game/Assets/DarkNights/Res/Config/balance.json') -Raw | ConvertFrom-Json
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
    $arguments = @('-batchmode', '-screen-width', '1280', '-screen-height', '800', '-screen-fullscreen', '0',
        '-logFile', ('"' + (Join-Path $run "$Role.log") + '"'), '--dn-role', $Role, '--dn-port', $Port,
        '--dn-report', ('"' + (Join-Path $run "$Role.json") + '"'), '--dn-commands', ('"' + (Join-Path $run "$Role.commands") + '"'))
    $processes[$Role] = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
}
function Send([hashtable]$Command) {
    [IO.File]::AppendAllText((Join-Path $run 'host.commands'), ($Command | ConvertTo-Json -Compress) + "`n")
}
function Check([string]$Name, [bool]$Ok) {
    $checks[$Name] = $Ok
    if (!$Ok) { throw "Failed: $Name" }
}
try {
    Start-Player 'host'
    $initial = Wait-Report 'host' { param($r) $r.ready -and $r.entityViews -eq 17 }
    Send @{ operation = 'SetPaused'; value = 1 }
    $paused = Wait-Report 'host' { param($r) $r.frame.Paused }
    $workers = @($paused.frame.World.Actors | Where-Object Kind -eq 'worker')
    $production = @($workers[0].Id, $workers[1].Id, $workers[4].Id)
    Send @{ operation = 'PlaceBuilding'; actors = @($workers[0].Id); kind = 'tower'; x = 708 }
    Send @{ operation = 'PlaceBuilding'; actors = @($workers[1].Id); kind = 'tower'; x = 764 }
    Send @{ operation = 'TrainActors'; actors = @($workers[2].Id, $workers[3].Id); kind = 'archer' }
    $food = @($paused.frame.World.Worksites | Where-Object Kind -eq 'food')[0]
    Send @{ operation = 'IssueOrders'; actors = @($workers[4].Id); target = $food.Id; x = $food.X }
    $prepared = Wait-Report 'host' { param($r) $r.frame.World.Buildings.Count -eq 6 -and @($r.frame.World.Actors | Where-Object Activity -eq 'TrainingMove').Count -eq 2 }
    Check 'normal_starting_resources_fund_frozen_strategy' ($prepared.frame.World.Camp.Stock.Wood -eq 0 -and $prepared.frame.World.Camp.Stock.Stone -eq 10)
    Send @{ operation = 'SetSpeed'; value = 2 }
    Send @{ operation = 'SetPaused'; value = 0 }
    $end = [DateTime]::UtcNow.AddSeconds(440)
    $next = 0.0
    $joined = 0
    $lastPhase = ''
    do {
        $hostReport = Wait-Report 'host' { param($r) $r.ready }
        $frame = $hostReport.frame
        $world = $frame.World
        $camp = $world.Camp
        if ($camp.Mode -ne 'Playing') { break }
        if ($frame.Elapsed -ge $next) {
            $next = $frame.Elapsed + 1
            if ($frame.Elapsed -ge 40 -and $camp.Population -lt $camp.Capacity -and $camp.RecruitCooldown -le 0 -and $camp.Stock.Food -ge 25) {
                Send @{ operation = 'Recruit' }
            }
            $claimed = @{}
            foreach ($actor in $world.Actors) {
                if ($actor.Enemy -or $actor.Activity -ne 'Idle') { continue }
                if ($actor.Kind -eq 'worker') {
                    if ($production -contains $actor.Id) {
                        $site = @($world.Worksites | Where-Object { $_.Kind -eq 'wood' -and $_.WorkerId -eq 0 -and $_.Amount -ne 0 -and !$claimed.ContainsKey($_.Id) } | Select-Object -First 1)
                        if ($site.Count) {
                            Send @{ operation = 'IssueOrders'; actors = @($actor.Id); target = $site[0].Id; x = $site[0].X }
                            $claimed[$site[0].Id] = $true
                        }
                    } else { Send @{ operation = 'TrainActors'; actors = @($actor.Id); kind = 'spearman' } }
                } else {
                    $rally = if ($actor.Kind -eq 'archer') { 712 } else { 766 }
                    if ([Math]::Abs($actor.X - $rally) -gt 2) { Send @{ operation = 'IssueOrders'; actors = @($actor.Id); x = $rally } }
                }
            }
            foreach ($building in $world.Buildings) {
                $definition = $balance.buildings.($building.Kind)
                if ($building.Progress -ge 1 -and $building.Hp -lt $definition.hp - $balance.economy.repair_hp -and
                    $camp.Stock.Wood -ge $balance.economy.repair_cost.wood -and $camp.Stock.Gold -ge $balance.economy.repair_cost.gold) {
                    Send @{ operation = 'Repair'; target = $building.Id }
                }
            }
        }
        $phase = "$($camp.WaveIndex):$($camp.WavePhase)"
        if ($phase -ne $lastPhase) { Write-Output "Campaign phase=$phase elapsed=$([Math]::Round($frame.Elapsed)) players=$($frame.PlayerCount)"; $lastPhase = $phase }
        $joinNow = ($joined -eq 0 -and $frame.Elapsed -ge 8) -or ($joined -eq 1 -and $camp.WaveIndex -ge 1) -or ($joined -eq 2 -and $camp.WaveIndex -ge 1 -and $world.Projectiles.Count -gt 0)
        if ($joinNow) {
            $joined++
            $role = "client$joined"
            Start-Player $role
            $guest = Wait-Report $role { param($r) $r.ready -and $r.entityViews -gt 0 }
            Check ($role + '_late_join_ready') ($guest.slot -eq $joined)
        }
        Start-Sleep -Milliseconds 300
    } while ([DateTime]::UtcNow -lt $end)
    Check 'all_three_nights_win_with_34_real_kills' ($camp.Mode -eq 'Won' -and $camp.WaveIndex -eq 2 -and $camp.Kills -eq 34 -and $camp.EnemyCount -eq 0)
    Check 'four_process_session_ready_at_victory' ($joined -eq 3 -and $frame.PlayerCount -eq 4 -and $frame.ReadyCount -eq 4)
    foreach ($role in @('client1', 'client2', 'client3')) {
        $guest = Wait-Report $role { param($r) $r.frame.World.Camp.Mode -eq 'Won' -and $r.uiPage -eq 'Result' }
        Check ($role + '_sees_authoritative_victory') ($guest.frame.World.Camp.Kills -eq 34 -and $guest.frame.Elapsed -eq $frame.Elapsed)
    }
    Send @{ operation = 'capture'; file = 'victory.png'; x = 780 }
    Start-Sleep -Seconds 2
    foreach ($role in $processes.Keys) {
        $log = Get-Content -LiteralPath (Join-Path $run "$role.log") -Raw
        Check ($role + '_no_runtime_exception') ($log -notmatch '(?im)(Exception:|Shader error|\[AppStartup\].*(failed|cancelled))')
    }
}
catch { $failure = $_.Exception.ToString() }
finally {
    foreach ($process in $processes.Values) { $process.Refresh(); if (!$process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit() } }
    $result = [ordered]@{ passed = !$failure; scope = 'Four real Mono processes; normal-resource three-night strategy through SessionClient'
        checks = $checks; error = $failure; elapsed = $frame.Elapsed; artifacts = $run
        gameCodeSha256 = (Get-FileHash -LiteralPath (Join-Path (Split-Path $player -Parent) 'DarkNights_Data/Managed/DarkNights.Entry.dll')).Hash }
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Game campaign: passed=$(!$failure) checks=$($checks.Count); $run/result.json"
}
if ($failure) { throw $failure }
