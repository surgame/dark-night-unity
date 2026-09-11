param([ValidateSet('mono', 'il2cpp')][string]$Backend = 'mono')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$player = Join-Path $root "artifacts/migration/player-$Backend/DarkNights.exe"
if (!(Test-Path -LiteralPath $player)) { throw "Build the $Backend game host first: $player" }
$run = Join-Path $root ("artifacts/migration/run-{0}-{1}" -f (Get-Date -Format 'yyyyMMdd-HHmmss-fff'), $Backend)
New-Item -ItemType Directory -Path $run | Out-Null
$log = Join-Path $run 'player.log'
$process = Start-Process -FilePath $player -ArgumentList @('-batchmode', '-nographics', '-logFile', ('"' + $log + '"')) -PassThru -WindowStyle Hidden
$checks = [ordered]@{}
try {
    $deadline = (Get-Date).AddSeconds(45)
    $ready = $false
    do {
        Start-Sleep -Milliseconds 500
        $text = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Raw } else { '' }
        $ready = $text -match '\[AppStartup\] Startup completed\. Application is ready\.'
        $failed = $text -match '\[AppStartup\].*(failed|cancelled)'
        $process.Refresh()
    } while (!$ready -and !$failed -and !$process.HasExited -and (Get-Date) -lt $deadline)
    # 就绪后观察短暂稳定窗口，捕获同一启动流程尾部的失败或重复执行。
    if ($ready) { Start-Sleep -Seconds 2 }
    $verifiedLog = Join-Path $run 'verified.log'
    if (Test-Path -LiteralPath $log) { Copy-Item -LiteralPath $log -Destination $verifiedLog }
    else { Set-Content -LiteralPath $verifiedLog -Value '' }
    $text = Get-Content -LiteralPath $verifiedLog -Raw
    $process.Refresh()
    $checks['independent_player_remains_running'] = !$process.HasExited
    $checks['app_startup_ready_once'] = [regex]::Matches($text, '\[AppStartup\] Startup completed\. Application is ready\.').Count -eq 1
    $checks['frozen_content_loaded_once'] = [regex]::Matches($text, 'DARK_NIGHTS_CONTENT_READY level=pinewatch seed=90127 units=6 buildings=5 worksites=4 waves=3').Count -eq 1
    $checks['formal_object_content_loaded_once'] = [regex]::Matches($text, 'DARK_NIGHTS_FORMAL_CONTENT_READY definitions=23 commands=2 states=1 behaviours=7 identity=GuidFirst wire=GuidV2').Count -eq 1
    $checks['no_startup_or_runtime_exception'] = $text -notmatch '(?im)(Exception:|\[AppStartup\].*(failed|cancelled)|Unable to load|InvalidKeyException|Could not load|MissingMethodException|TypeLoadException)'
    $managed = Join-Path (Split-Path $player -Parent) 'DarkNights_Data/Managed'
    $checks['no_formal_editor_or_tests_in_player'] = !(Test-Path (Join-Path $managed 'DarkNights.Editor.dll')) -and !(Test-Path (Join-Path $managed 'DarkNights.Tests.dll'))
    $passed = @($checks.Values | Where-Object { !$_ }).Count -eq 0
    $result = [ordered]@{
        passed = $passed; backend = $Backend; processId = $process.Id
        scope = 'Bootstrap + YYGC AppStartup + Addressables + formal ObjectDefinition/Prefab/generated registry startup; no gameplay or multiplayer acceptance'
        player = $player; playerSha256 = (Get-FileHash -LiteralPath $player -Algorithm SHA256).Hash.ToLowerInvariant()
        log = $verifiedLog; logSha256 = (Get-FileHash -LiteralPath $verifiedLog -Algorithm SHA256).Hash.ToLowerInvariant()
        runtimeLog = $log
        checks = $checks
    }
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Game startup: backend=$Backend passed=$passed checks=$($checks.Count); $run/result.json"
    if (!$passed) { throw "Game startup verification failed. See $log" }
}
finally {
    $process.Refresh()
    if (!$process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit() }
}
