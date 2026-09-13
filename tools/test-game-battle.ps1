param([int]$Port = 27993, [string]$PlayerPath = '')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$player = if ($PlayerPath) { [IO.Path]::GetFullPath($PlayerPath) } else { Join-Path $repo 'artifacts/migration/player-mono/DarkNights.exe' }
$run = Join-Path $repo ('artifacts/migration/battle-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
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
    }
    catch { return $null }
}
function Wait-Report([string]$Role, [scriptblock]$Condition, [int]$Seconds = 45) {
    $end = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        $value = Read-Report $Role
        if ($value -and $value.error) { throw "$Role reported: $($value.error)" }
        if ($value -and (& $Condition $value)) { return $value }
        $processes[$Role].Refresh()
        if ($processes[$Role].HasExited) { throw "$Role exited unexpectedly." }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $end)
    throw "Timeout waiting for $Role; see $run"
}
function Start-Player([string]$Role) {
    [IO.File]::WriteAllText((Join-Path $run "$Role.commands"), '')
    $arguments = @('-screen-width', '1280', '-screen-height', '800', '-screen-fullscreen', '0',
        '-logFile', ('"' + (Join-Path $run "$Role.log") + '"'), '--dn-role', $Role, '--dn-port', $Port,
        '--dn-report', ('"' + (Join-Path $run "$Role.json") + '"'), '--dn-commands', ('"' + (Join-Path $run "$Role.commands") + '"'))
    $processes[$Role] = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
}
function Send([string]$Role, [hashtable]$Command) {
    [IO.File]::AppendAllText((Join-Path $run "$Role.commands"), ($Command | ConvertTo-Json -Compress) + "`n")
}
function Check([string]$Name, [bool]$Ok) {
    $checks[$Name] = $Ok
    if (!$Ok) { throw "Failed: $Name" }
}
try {
    Start-Player 'host'
    $initial = Wait-Report 'host' { param($r) $r.ready -and $r.entityViews -eq 17 }
    Check 'real_host_starts_native_scene' ($initial.slot -eq 0)
    Send 'host' @{ operation = 'SetSpeed'; value = 2 }
    Send 'host' @{ operation = 'StartNight' }
    $battle = Wait-Report 'host' { param($r) $r.frame.World.Camp.NextSpawn -ge 2 -and $r.peakArrowViews -gt 0 -and $r.peakEffectViews -gt 0 } 90
    Check 'real_night_creates_arrows_and_effects' ($battle.frame.World.Camp.WavePhase -eq 'Night')
    Start-Player 'client'
    $guest = Wait-Report 'client' { param($r) $r.ready -and $r.frame.PlayerCount -eq 2 -and $r.entityViews -gt 17 }
    Check 'late_join_gets_running_battle_and_native_entities' ($guest.slot -eq 1 -and $guest.frame.World.Camp.NextSpawn -ge 2)
    $guest = Wait-Report 'client' { param($r) $r.peakArrowViews -gt 0 -and $r.peakEffectViews -gt 0 -and @($r.frame.Remnants).Count -gt 0 } 60
    Check 'late_join_receives_real_projectiles_effects_and_remnants' ($guest.frame.World.Camp.Kills -gt 0)
    Send 'host' @{ operation = 'SetPaused'; value = 1 }
    $paused = Wait-Report 'host' { param($r) $r.frame.Paused }
    $guest = Wait-Report 'client' { param($r) $r.frame.Paused -and $r.frame.Elapsed -eq $paused.frame.Elapsed }
    Check 'paused_battle_converges' ($guest.frame.World.Camp.Kills -eq $paused.frame.World.Camp.Kills)
    Send 'host' @{ operation = 'capture'; file = 'host-battle.png'; x = 780 }
    Send 'client' @{ operation = 'capture'; file = 'client-battle.png'; x = 780 }
    Start-Sleep -Seconds 2
    Check 'both_rendered_screenshots_exist' ((Test-Path (Join-Path $run 'host-battle.png')) -and (Test-Path (Join-Path $run 'client-battle.png')))
    Send 'host' @{ operation = 'disconnect' }
    $guest = Wait-Report 'client' { param($r) !$r.ready -and $null -eq $r.frame -and $r.entityViews -eq 0 -and $r.effectViews -eq 0 -and $r.arrowViews -eq 0 }
    Check 'host_exit_clears_guest_world_and_transient_views' ($guest.uiPage -eq 'MainMenu')
    foreach ($role in @('host', 'client')) {
        $log = Get-Content -LiteralPath (Join-Path $run "$role.log") -Raw
        Check ($role + '_runtime_has_no_exception_or_shader_error') ($log -notmatch '(?im)(Exception:|Shader error|\[AppStartup\].*(failed|cancelled)|font does not have a material)')
    }
}
catch { $failure = $_.Exception.ToString() }
finally {
    foreach ($process in $processes.Values) {
        $process.Refresh()
        if (!$process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit() }
    }
    $result = [ordered]@{
        passed = !$failure; scope = 'Mono graphical Host and independent late-joining client during real battle'
        player = $player; gameCodeSha256 = (Get-FileHash -LiteralPath (Join-Path (Split-Path $player -Parent) 'DarkNights_Data/Managed/DarkNights.Entry.dll')).Hash
        checks = $checks; error = $failure; artifacts = $run
    }
    $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Game battle: passed=$(!$failure) checks=$($checks.Count); $run/result.json"
}
if ($failure) { throw $failure }
