param([int]$Port = 28750, [string]$Python = 'python')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$run = Join-Path $root ('artifacts/terrain/weak-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $run | Out-Null
$report = Join-Path $run 'relay.json'
$arguments = @(('"' + (Join-Path $PSScriptRoot 'lan-netem.py') + '"'), '--listen', ($Port + 1),
    '--target', $Port, '--loss', '0.05', '--delay', '0.1', '--jitter', '0.025', '--report', ('"' + $report + '"'))
$relay = Start-Process -FilePath $Python -ArgumentList $arguments -WindowStyle Hidden -PassThru
try {
    Start-Sleep -Milliseconds 500
    if ($relay.HasExited) { throw 'Terrain UDP relay failed to start.' }
    & (Join-Path $PSScriptRoot 'test-terrain-network.ps1') -Port $Port -ClientPort ($Port + 1)
} finally {
    [IO.File]::WriteAllText($report + '.stop', '')
    if (!$relay.WaitForExit(5000)) { Stop-Process -Id $relay.Id }
}
$metrics = Get-Content $report -Raw | ConvertFrom-Json
if ($metrics.dropped -eq 0 -or $metrics.reordered -eq 0) { throw 'Loss and reordering were not exercised.' }
Write-Output ("PASS weak network: dropped={0}; reordered={1}; {2}" -f $metrics.dropped, $metrics.reordered, $report)
