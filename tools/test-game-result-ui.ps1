param([Parameter(Mandatory = $true)][string]$PlayerPath, [int]$Port = 28410)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$player = [IO.Path]::GetFullPath($PlayerPath)
$run = Join-Path $repo ('artifacts/migration/result-ui-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$saves = Join-Path $run 'saves'
$processes = @{}
$checks = [ordered]@{}
$failure = $null
New-Item -ItemType Directory -Path $run | Out-Null

function Read-Report([string]$Role) {
    try {
        $stream = [IO.FileStream]::new((Join-Path $run "$Role.json"), [IO.FileMode]::Open, [IO.FileAccess]::Read,
            [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
        $reader = [IO.StreamReader]::new($stream)
        try { return $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    } catch { return $null }
}
function Wait-Report([string]$Role, [scriptblock]$Condition) {
    $end = [DateTime]::UtcNow.AddSeconds(30)
    do {
        $value = Read-Report $Role
        if ($value -and $value.error) { throw "$Role reported: $($value.error)" }
        if ($value -and (& $Condition $value)) { return $value }
        $processes[$Role].Refresh()
        if ($processes[$Role].HasExited) { throw "$Role exited unexpectedly." }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $end)
    throw "Timeout waiting for $Role"
}
function Start-Player([string]$Role) {
    $width = if ($Role -eq 'host') { 1280 } else { 1600 }
    $height = if ($Role -eq 'host') { 800 } else { 900 }
    [IO.File]::WriteAllText((Join-Path $run "$Role.commands"), '')
    $arguments = @('-batchmode', '-screen-width', $width, '-screen-height', $height, '-screen-fullscreen', '0',
        '-logFile', ('"' + (Join-Path $run "$Role.log") + '"'), '--dn-camp-mode', '--dn-role', $Role, '--dn-port', $Port,
        '--dn-save-dir', ('"' + $saves + '"'), '--dn-report', ('"' + (Join-Path $run "$Role.json") + '"'),
        '--dn-commands', ('"' + (Join-Path $run "$Role.commands") + '"'))
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
function Check([string]$Name, [bool]$Ok) {
    $checks[$Name] = $Ok
    if (!$Ok) { throw "Failed: $Name" }
}
function Capture([string]$Role, [string]$State) {
    $file = "$Role-$State.png"
    Send $Role @{operation='capture';file=$file}
    $null = Wait-Report $Role { param($r) Test-Path -LiteralPath (Join-Path $run $file) }
    $bytes = [IO.File]::ReadAllBytes((Join-Path $run $file))
    $width = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes,16))
    $height = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes,20))
    Check ($Role + '_' + $State + '_capture_dimensions') (
        ($Role -eq 'host' -and $width -eq 1280 -and $height -eq 800) -or
        ($Role -eq 'client1' -and $width -eq 1600 -and $height -eq 900))
}
function Stable-AfterRestart([string]$Role, [int]$ExpectedEpoch, [string]$State) {
    $null = Wait-Report $Role { param($r) $r.ready -and $r.frame.Epoch -eq $ExpectedEpoch -and $r.frame.World.Camp.Mode -eq 'Playing' }
    # 等待后续多个报告，防止刚收到新世界的一帧偶然掩盖旧模态重新显示。
    Start-Sleep -Milliseconds 750
    $after = Wait-Report $Role { param($r) $r.ready -and $r.frame.Epoch -ge $ExpectedEpoch }
    $after | ConvertTo-Json -Depth 24 | Set-Content -LiteralPath (Join-Path $run "$Role-after-$State-restart.json") -Encoding utf8
    Check ($Role + '_' + $State + '_restart_once') ($after.frame.Epoch -eq $ExpectedEpoch -and $after.entityViews -eq 17)
    Check ($Role + '_' + $State + '_restart_closes_result') ($after.uiPage -eq '')
}

try {
    Start-Player 'host'
    $null = Wait-Report 'host' { param($r) $r.ready -and $r.entityViews -eq 17 }
    $null = Receipt 'host' @{operation='SetPaused';value=1}
    $null = Receipt 'host' @{operation='Save';value=0}
    $initialPath = Join-Path $saves 'v3/slot-00.dnsave.json'
    $null = Wait-Report 'host' { param($r) !$r.storageBusy -and (Test-Path -LiteralPath $initialPath) }
    $initial = Get-Content -LiteralPath $initialPath -Raw | ConvertFrom-Json
    # 合成终局只用于 UI／会话生命周期；不改规则夹具，不作为三夜或胜负计算证据。
    foreach ($case in @(@{slot=1;mode='Won'}, @{slot=2;mode='Lost'})) {
        $fixture = $initial | ConvertTo-Json -Depth 24 | ConvertFrom-Json
        $fixture.world.mode = $case.mode
        if ($case.mode -eq 'Lost') {
            # Lost 的存档合同要求酒馆已被摧毁，同时移除对应身份，保留其余初始状态。
            $tavern = @($fixture.world.buildings | Where-Object kind -eq 'tavern')
            if ($tavern.Count -ne 1) { throw 'Initial fixture must contain exactly one tavern.' }
            $fixture.world.buildings = @($fixture.world.buildings | Where-Object id -ne $tavern[0].id)
            $fixture.world.identities = @($fixture.world.identities | Where-Object id -ne $tavern[0].id)
        }
        $fixture | ConvertTo-Json -Depth 24 -Compress | Set-Content -LiteralPath (Join-Path $saves ('v3/slot-{0:D2}.dnsave.json' -f $case.slot)) -Encoding utf8
    }
    Send 'host' @{operation='BeginLoad';value=1}
    $won = Wait-Report 'host' { param($r) $r.ready -and $r.frame.Epoch -eq 2 -and $r.uiPage -eq 'Result' }
    Check 'host_loaded_won_fixture' ($won.frame.World.Camp.Mode -eq 'Won')
    Start-Player 'client1'
    $guest = Wait-Report 'client1' { param($r) $r.ready -and $r.uiPage -eq 'Result' }
    Check 'late_guest_sees_same_won_fixture' ($guest.frame.Epoch -eq 2 -and $guest.frame.World.Camp.Mode -eq 'Won')
    Capture 'host' 'won'
    Capture 'client1' 'won'
    Send 'host' @{operation='ui';panel='Result';key='NewGame'}
    foreach ($role in @('host','client1')) { Stable-AfterRestart $role 3 'won' }
    Send 'host' @{operation='BeginLoad';value=2}
    foreach ($role in @('host','client1')) {
        $lost = Wait-Report $role { param($r) $r.ready -and $r.frame.Epoch -eq 4 -and $r.uiPage -eq 'Result' }
        Check ($role + '_loaded_lost_fixture') ($lost.frame.World.Camp.Mode -eq 'Lost')
        Capture $role 'lost'
    }
    Send 'host' @{operation='ui';panel='Result';key='NewGame'}
    foreach ($role in @('host','client1')) { Stable-AfterRestart $role 5 'lost' }
    Send 'host' @{operation='BeginLoad';value=1}
    $null = Wait-Report 'client1' { param($r) $r.ready -and $r.frame.Epoch -eq 6 -and $r.uiPage -eq 'Result' }
    Send 'client1' @{operation='ui';panel='Result';key='MainMenu'}
    # 菜单在命令执行帧立即切换；展示缓存由下一个 Update 清空，等待完整退出状态。
    $guest = Wait-Report 'client1' { param($r) !$r.ready -and $r.uiPage -eq 'MainMenu' -and $r.entityViews -eq 0 }
    $guest | ConvertTo-Json -Depth 24 | Set-Content -LiteralPath (Join-Path $run 'client1-result-exit.json') -Encoding utf8
    Check 'guest_result_exit_clears_live_views' ($guest.entityViews -eq 0)
    $hostAfterGuest = Wait-Report 'host' { param($r) $r.ready -and $r.frame.PlayerCount -eq 1 }
    Check 'guest_exit_preserves_host_result' ($hostAfterGuest.uiPage -eq 'Result' -and $hostAfterGuest.frame.Epoch -eq 6)
    Send 'host' @{operation='ui';panel='Result';key='MainMenu'}
    $hostMenu = Wait-Report 'host' { param($r) !$r.ready -and $r.uiPage -eq 'MainMenu' -and $r.entityViews -eq 0 }
    $hostMenu | ConvertTo-Json -Depth 24 | Set-Content -LiteralPath (Join-Path $run 'host-result-exit.json') -Encoding utf8
    Check 'host_result_exit_clears_live_views' ($hostMenu.entityViews -eq 0)
    foreach ($role in $processes.Keys) {
        $log = Get-Content -LiteralPath (Join-Path $run "$role.log") -Raw
        Check ($role + '_no_runtime_exception') ($log -notmatch '(?im)(Exception:|Shader error|\[AppStartup\].*(failed|cancelled))')
    }
}
catch { $failure = $_.Exception.ToString() }
finally {
    foreach ($process in $processes.Values) { $process.Refresh(); if (!$process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit() } }
    [ordered]@{passed=(!$failure);checks=$checks;error=$failure;artifacts=$run;renderedBatchMode=$true;
        scope='Two real Mono processes; synthetic v3 terminal fixtures test Result UI rendering, restart and exit only; no gameplay-win or foreground-performance claim';
        player=$player;gameCodeSha256=(Get-FileHash -LiteralPath (Join-Path (Split-Path $player -Parent) 'DarkNights_Data/Managed/DarkNights.Entry.dll')).Hash} |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Result UI: passed=$(!$failure) checks=$($checks.Count); $run/result.json"
}
if ($failure) { throw $failure }
