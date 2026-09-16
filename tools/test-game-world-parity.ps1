param(
    [Parameter(Mandatory = $true)][string]$PlayerPath,
    [Parameter(Mandatory = $true)][string]$FixtureDirectory,
    [int]$Port = 28430
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$player = [IO.Path]::GetFullPath($PlayerPath)
$fixtures = [IO.Path]::GetFullPath($FixtureDirectory)
$run = Join-Path $repo ('artifacts/m5-world/player-parity-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$saves = Join-Path $run 'saves'
$processes = @{}
$checks = [ordered]@{}
$failure = $null
$reference = Get-Content -LiteralPath (Join-Path $fixtures 'result.json') -Raw | ConvertFrom-Json
if (!$reference.passed -or $reference.colorSpace -ne 'Linear') { throw 'A completed Linear Editor capture is required.' }
New-Item -ItemType Directory -Path (Join-Path $saves 'v2') | Out-Null
Copy-Item -LiteralPath (Join-Path $fixtures 'camp.v2.json') -Destination (Join-Path $saves 'v2/slot-00.dnsave.json')
Copy-Item -LiteralPath (Join-Path $fixtures 'night.v2.json') -Destination (Join-Path $saves 'v2/slot-01.dnsave.json')

function Read-Report([string]$Role) {
    try {
        $stream = [IO.FileStream]::new((Join-Path $run "$Role.json"), [IO.FileMode]::Open, [IO.FileAccess]::Read,
            [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
        $reader = [IO.StreamReader]::new($stream)
        try { return $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    } catch { return $null }
}
function Wait-Report([string]$Role, [scriptblock]$Condition) {
    $until = [DateTime]::UtcNow.AddSeconds(30)
    do {
        $value = Read-Report $Role
        if ($value -and $value.error) { throw "$Role reported: $($value.error)" }
        if ($value -and (& $Condition $value)) { return $value }
        $processes[$Role].Refresh()
        if ($processes[$Role].HasExited) { throw "$Role exited unexpectedly." }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $until)
    throw "Timeout waiting for $Role"
}
function Start-Player([string]$Role, [int]$Width, [int]$Height) {
    [IO.File]::WriteAllText((Join-Path $run "$Role.commands"), '')
    $arguments = @('-batchmode', '-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0',
        '-logFile', ('"' + (Join-Path $run "$Role.log") + '"'), '--dn-camp-mode', '--dn-role', $Role, '--dn-port', $Port,
        '--dn-save-dir', ('"' + $saves + '"'), '--dn-report', ('"' + (Join-Path $run "$Role.json") + '"'),
        '--dn-commands', ('"' + (Join-Path $run "$Role.commands") + '"'))
    $processes[$Role] = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
}
function Send([string]$Role, [hashtable]$Command) {
    [IO.File]::AppendAllText((Join-Path $run "$Role.commands"), ($Command | ConvertTo-Json -Depth 8 -Compress) + "`n")
}
function Check([string]$Name, [bool]$Ok) {
    $checks[$Name] = $Ok
    if (!$Ok) { throw "Failed: $Name" }
}
function Load-Fixture([int]$Slot, [int]$Entities) {
    $before = Wait-Report 'host' { param($r) $r.ready -and !$r.storageBusy }
    $epoch = $before.frame.Epoch + 1
    Send 'host' @{ operation = 'BeginLoad'; value = $Slot }
    foreach ($role in @('host', 'client1')) {
        $value = Wait-Report $role { param($r) $r.ready -and $r.frame.Epoch -eq $epoch -and $r.entityViews -eq $Entities }
        Check ($role + '_epoch_' + $epoch + '_frozen') ($value.frame.Paused -and $value.frame.Elapsed -eq 0)
    }
}
function Sample([string]$Role, [string]$Name, [float]$X, [float]$Zoom, [float]$Night, [int]$Selected = 0, [int]$Effects = 0, [array]$Cues = @()) {
    $command = @{ operation = 'capture-sample'; file = "$Name.png"; x = $X; zoom = $Zoom; night = $Night;
        time = 2; selected = $Selected; effects = $Effects }
    if ($Cues.Count) { $command.cues = $Cues }
    Send $Role $command
}
function Verify-Capture([string]$Role, [string]$Name, [string]$ReferenceName, [int]$Entities, [int]$Effects) {
    $path = Join-Path $run "$Name.png"
    $null = Wait-Report $Role { param($r) Test-Path -LiteralPath ($path + '.json') }
    $meta = Get-Content -LiteralPath ($path + '.json') -Raw | ConvertFrom-Json
    $width = if ($Role -eq 'host') { 1280 } else { 1600 }
    $height = if ($Role -eq 'host') { 800 } else { 900 }
    $bytes = [IO.File]::ReadAllBytes($path)
    $w = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, 16))
    $h = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, 20))
    Check ($Name + '_dimensions') ($w -eq $width -and $h -eq $height -and $bytes.Length -gt 10000)
    Check ($Name + '_linear_background') ($meta.colorSpace -eq 'Linear' -and $meta.batchmode)
    Check ($Name + '_entities_and_effects') ($meta.entities -eq $Entities -and $meta.effects -eq $Effects)
    $expected = @($reference.captures | Where-Object name -eq $ReferenceName)[0]
    Check ($Name + '_fixed_clock_and_camera') ($meta.time -eq 2 -and [math]::Abs($meta.night - $expected.night) -lt .0001 -and
        [math]::Abs($meta.zoom - $expected.zoom) -lt .0001 -and [math]::Abs($meta.cameraX - $expected.cameraX) -lt .0001)
    Check ($Name + '_selection') (($meta.selected -join ',') -eq ($expected.selected -join ','))
    if ($Entities -gt 0) {
        Check ($Name + '_same_frozen_world') ($meta.frame.Paused -and $meta.frame.Elapsed -eq 0 -and
            (($meta.frame.World | ConvertTo-Json -Depth 30 -Compress) -eq ($expected.frame.World | ConvertTo-Json -Depth 30 -Compress)))
    }
}

try {
    Start-Player 'host' 1280 800
    $null = Wait-Report 'host' { param($r) $r.ready -and $r.entityViews -eq 17 }
    Start-Player 'client1' 1600 900
    $null = Wait-Report 'client1' { param($r) $r.ready -and $r.entityViews -eq 17 }
    Load-Fixture 0 17
    Sample 'host' 'camp' 255 2.8 .16
    Verify-Capture 'host' 'camp' 'camp' 17 0
    Load-Fixture 1 20
    Sample 'host' 'night' 730 2.8 1 17
    Verify-Capture 'host' 'night' 'night' 20 0
    $cues = @(
        @{ Kind = 'corpse'; X = 744; Y = 320; ContentId = 'archer'; Face = -1 },
        @{ Kind = 'corpse'; X = 720; Y = 320; ContentId = 'ghoul'; Face = 1 },
        @{ Kind = 'rubble'; X = 590; Y = 320; ContentId = 'house' },
        @{ Kind = 'resource'; X = 640; Y = 297; Text = '+3'; ContentId = 'wood' },
        @{ Kind = 'damage'; X = 680; Y = 297; Text = '5'; Enemy = $true },
        @{ Kind = 'command'; X = 760; Y = 320 }
    )
    Sample 'host' 'remnants' 730 2.8 1 17 6 $cues
    # 指令圈属于各端输入；来宾画面对照显式触发自己的本地圈，不从 Host 投影取得。
    Sample 'client1' 'client-remnants' 730 2.8 1 17 6 @(@{ Kind = 'command'; X = 760; Y = 320 })
    Verify-Capture 'host' 'remnants' 'remnants' 20 6
    Verify-Capture 'client1' 'client-remnants' 'remnants' 20 6
    Load-Fixture 0 17
    Sample 'host' 'zoom_out' 550 1.8 .16
    Sample 'client1' 'wide' 550 1.8 .16
    Verify-Capture 'host' 'zoom_out' 'zoom_out' 17 0
    Verify-Capture 'client1' 'wide' 'wide' 17 0
    Send 'client1' @{ operation = 'disconnect' }
    $null = Wait-Report 'client1' { param($r) !$r.ready -and $r.entityViews -eq 0 }
    Send 'host' @{ operation = 'disconnect' }
    $null = Wait-Report 'host' { param($r) !$r.ready -and $r.entityViews -eq 0 -and $r.uiPage -eq 'MainMenu' }
    Sample 'host' 'menu' 255 2.8 .16
    Verify-Capture 'host' 'menu' 'menu' 0 0
    foreach ($role in @('host', 'client1')) {
        $log = Get-Content -LiteralPath (Join-Path $run "$role.log") -Raw
        Check ($role + '_no_runtime_exception') ($log -notmatch '(?im)(Exception:|Shader error|\[AppStartup\].*(failed|cancelled))')
    }
}
catch { $failure = $_.Exception.ToString() }
finally {
    foreach ($process in $processes.Values) {
        $process.Refresh()
        if (!$process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit() }
    }
    [ordered]@{ passed = (!$failure); checks = $checks; error = $failure; artifacts = $run;
        fixtures = $fixtures; colorSpace = 'Linear'; foregroundPerformanceAcceptance = $false;
        scope = 'Two real Mono processes, seven fixed-time captures; visual review is separate from command/metadata checks';
        gameCodeSha256 = (Get-FileHash -LiteralPath (Join-Path (Split-Path $player -Parent) 'DarkNights_Data/Managed/DarkNights.Entry.dll')).Hash } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "World parity: passed=$(!$failure) checks=$($checks.Count); $run/result.json"
}
if ($failure) { throw $failure }
