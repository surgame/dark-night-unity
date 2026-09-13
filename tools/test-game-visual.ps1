param([int]$Port = 28270, [int]$Width = 1280, [int]$Height = 800, [string]$PlayerPath = '', [switch]$BatchMode)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$player = if ($PlayerPath) { [IO.Path]::GetFullPath($PlayerPath) } else { Join-Path $repo 'artifacts/migration/player-mono/DarkNights.exe' }
$run = Join-Path $repo ('artifacts/migration/visual-' + $Width + 'x' + $Height + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$saves = Join-Path $run 'saves'
New-Item -ItemType Directory -Path $run | Out-Null
$processes = @{}
$checks = [ordered]@{}
$failure = $null
$ClientPort = $Port
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
    $arguments = @('-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0', '-logFile', ('"' + (Join-Path $run "$Role.log") + '"'),
        '--dn-role', $Role, '--dn-port', $(if ($Role -eq 'host') { $Port } else { $ClientPort }), '--dn-save-dir', ('"' + $saves + '"'),
        '--dn-report', ('"' + (Join-Path $run "$Role.json") + '"'), '--dn-commands', ('"' + (Join-Path $run "$Role.commands") + '"'))
    if ($BatchMode) { $arguments = @('-batchmode') + $arguments }
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
function Capture([string]$Name, [Nullable[float]]$CameraX = $null) {
    if ($null -ne $CameraX) {
        $positionFile='framing-'+$Name+'.png'
        Send 'host' @{operation='capture';file=$positionFile;x=$CameraX}
        $null=Wait-Report 'host' {param($r) Test-Path -LiteralPath (Join-Path $run $positionFile)}
        # Focus 立即移动相机；背景视差和环境在下一次 Present 更新后才与镜头一致。
        Start-Sleep -Milliseconds 300
    }
    $file=$Name+'.png'
    Send 'host' @{operation='capture';file=$file}
    $null=Wait-Report 'host' {param($r) Test-Path -LiteralPath (Join-Path $run $file)}
}
try {
    Start-Player 'host'
    $null=Wait-Report 'host' {param($r) $r.ready -and $r.entityViews -eq 17}
    $null=Receipt 'host' @{operation='SetPaused';value=1}
    Start-Sleep -Seconds 8
    Capture 'day' 255
    $null=Receipt 'host' @{operation='StartNight'}
    Start-Sleep -Seconds 8
    Capture 'night' 730
    Send 'host' @{operation='ui';panel='Chrome';key='Menu'}
    $null=Wait-Report 'host' {param($r) $r.uiPage -eq 'PauseMenu'}
    Capture 'pause'
    Send 'host' @{operation='ui';panel='PauseMenu';key='Help'}
    $null=Wait-Report 'host' {param($r) $r.uiPage -eq 'Help'}
    Capture 'help'
    Send 'host' @{operation='ui';panel='Help';key='Back'}
    $null=Wait-Report 'host' {param($r) $r.uiPage -eq 'PauseMenu'}
    Send 'host' @{operation='ui';panel='PauseMenu';key='MainMenu'}
    $null=Wait-Report 'host' {param($r) $r.uiPage -eq 'MainMenu' -and !$r.ready}
    Capture 'menu'
    foreach($name in @('day','night','pause','help','menu')) {
        $path=Join-Path $run ($name+'.png')
        $bytes=[IO.File]::ReadAllBytes($path)
        $w=[Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes,16))
        $h=[Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes,20))
        Check ($name+'_has_requested_dimensions') ($w -eq $Width -and $h -eq $Height -and $bytes.Length -gt 10000)
    }
    $log=Get-Content (Join-Path $run 'host.log') -Raw
    Check 'no_runtime_exception' ($log -notmatch '(?im)(Exception:|Shader error|\[AppStartup\].*(failed|cancelled))')
}
catch {$failure=$_.Exception.ToString()}
finally {
    foreach($p in $processes.Values) {$p.Refresh();if(!$p.HasExited){Stop-Process -Id $p.Id;$p.WaitForExit()}}
    [ordered]@{passed=(!$failure);checks=$checks;error=$failure;width=$Width;height=$Height;artifacts=$run;
      scope='Real Mono offscreen screenshots; image content requires separate visual review';renderedBatchMode=[bool]$BatchMode;
      gameCodeSha256=(Get-FileHash (Join-Path (Split-Path $player -Parent) 'DarkNights_Data/Managed/DarkNights.Entry.dll')).Hash} |
      ConvertTo-Json -Depth 8 | Set-Content (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Visual: passed=$(!$failure) checks=$($checks.Count); $run/result.json"
}
if($failure){throw $failure}
