param([switch]$SkipNetworkMatrix)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$run = Join-Path $repo ('artifacts/migration/delivery-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $run | Out-Null
$steps = @(
    @{name='startup'; script='test-game-startup.ps1'; args=@{Backend='mono'}},
    @{name='session'; script='test-game-session.ps1'; args=@{}},
    @{name='concurrency'; script='test-game-concurrency.ps1'; args=@{}},
    @{name='active-load'; script='test-game-active-load.ps1'; args=@{}},
    @{name='network-matrix'; script='test-game-network-matrix.ps1'; args=@{}},
    @{name='visual-1280'; script='test-game-visual.ps1'; args=@{Width=1280;Height=800}},
    @{name='visual-1600'; script='test-game-visual.ps1'; args=@{Width=1600;Height=900}},
    @{name='battle'; script='test-game-battle.ps1'; args=@{}},
    @{name='campaign'; script='test-game-campaign.ps1'; args=@{}},
    @{name='pressure'; script='test-game-pressure.ps1'; args=@{}}
)
$results = [ordered]@{}
$failure = $null
try {
    foreach ($step in $steps) {
        if ($step.name -eq 'network-matrix' -and $SkipNetworkMatrix) {continue}
        Write-Output "Delivery step: $($step.name)"
        $arguments = $step.args
        & (Join-Path $PSScriptRoot $step.script) @arguments 2>&1 | Tee-Object -FilePath (Join-Path $run ($step.name+'.log'))
        $results[$step.name] = 'passed'
    }
}
catch {$failure=$_.Exception.ToString()}
finally {
    [ordered]@{passed=(!$failure);steps=$results;error=$failure;networkMatrixSkipped=[bool]$SkipNetworkMatrix;
      scope='Serial local Mono acceptance; visual review, IL2CPP and two-machine LAN are separate';artifacts=$run} |
      ConvertTo-Json -Depth 6 | Set-Content (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Delivery: passed=$(!$failure); $run/result.json"
}
if ($failure) {throw $failure}
