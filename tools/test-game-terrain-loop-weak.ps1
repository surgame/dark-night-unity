param([Parameter(Mandatory)][string]$PlayerPath, [int]$Port = 28840,
    [ValidateRange(7, 20)][int]$SaveVersion = 7, [string]$Python = 'python')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$run = Join-Path $repo ('artifacts/map-fix/terrain-loop-weak-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$report = Join-Path $run 'relay.json'
$arguments = @(('"' + (Join-Path $PSScriptRoot 'lan-netem.py') + '"'), '--listen', ($Port + 1), '--target', $Port,
    '--loss', '0.05', '--delay', '0.1', '--jitter', '0.025', '--report', ('"' + $report + '"'))
$relay = Start-Process -FilePath $Python -ArgumentList $arguments -WindowStyle Hidden -PassThru
try {
    Start-Sleep -Milliseconds 500
    if ($relay.HasExited) { throw 'Terrain UDP relay failed to start.' }
    & (Join-Path $PSScriptRoot 'test-game-terrain-loop.ps1') -PlayerPath $PlayerPath -Port $Port -ClientPort ($Port + 1) -SaveVersion $SaveVersion
} finally {
    [IO.File]::WriteAllText($report + '.stop', '')
    if (!$relay.WaitForExit(5000)) { Stop-Process -Id $relay.Id }
}
$metrics = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
if ($metrics.dropped -eq 0 -or $metrics.reordered -eq 0) { throw 'Loss and reordering were not exercised.' }
Write-Output ("PASS 200ms RTT + 5% loss + 25ms jitter: dropped={0}; reordered={1}; {2}" -f
    $metrics.dropped, $metrics.reordered, $report)
