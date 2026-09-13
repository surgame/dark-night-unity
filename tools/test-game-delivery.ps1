param(
    [ValidateSet('startup','session','concurrency','active-load','recovery','network-matrix','visual-1280','visual-1600','battle','campaign','pressure')]
    [string]$StartAt = 'startup',
    [string]$PlayerPath = ''
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$run = Join-Path $repo ('artifacts/migration/delivery-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $run | Out-Null
$steps = @(
    @{name='startup'; script='test-game-startup.ps1'; args=@{Backend='mono'}},
    @{name='session'; script='test-game-session.ps1'; args=@{}},
    @{name='concurrency'; script='test-game-concurrency.ps1'; args=@{}},
    @{name='active-load'; script='test-game-active-load.ps1'; args=@{}},
    @{name='recovery'; script='test-game-recovery.ps1'; args=@{}},
    @{name='network-matrix'; script='test-game-network-matrix.ps1'; args=@{}},
    @{name='visual-1280'; script='test-game-visual.ps1'; args=@{Width=1280;Height=800}},
    @{name='visual-1600'; script='test-game-visual.ps1'; args=@{Width=1600;Height=900}},
    @{name='battle'; script='test-game-battle.ps1'; args=@{}},
    @{name='campaign'; script='test-game-campaign.ps1'; args=@{}},
    @{name='pressure'; script='test-game-pressure.ps1'; args=@{}}
)
$results = [ordered]@{}
$failure = $null
$reached = $false
$player = if ($PlayerPath) { [IO.Path]::GetFullPath($PlayerPath) } else { Join-Path $repo 'artifacts/migration/player-mono/DarkNights.exe' }
$entryDll = Join-Path (Split-Path $player -Parent) 'DarkNights_Data/Managed/DarkNights.Entry.dll'
$artifactHash = $null
try {
    $artifactHash = (Get-FileHash -LiteralPath $entryDll).Hash
    foreach ($step in $steps) {
        if ($step.name -eq $StartAt) {$reached = $true}
        if (!$reached) {continue}
        Write-Output "Delivery step: $($step.name)"
        $arguments = @('-PlayerPath', $player)
        foreach ($entry in $step.args.GetEnumerator()) {$arguments += @("-$($entry.Key)", [string]$entry.Value)}
        & pwsh -NoProfile -File (Join-Path $PSScriptRoot $step.script) @arguments 2>&1 | Tee-Object -FilePath (Join-Path $run ($step.name+'.log'))
        if ($LASTEXITCODE -ne 0) {throw "Step $($step.name) exited with code $LASTEXITCODE."}
        if ((Get-FileHash -LiteralPath $entryDll).Hash -ne $artifactHash) {throw 'Player code changed during acceptance.'}
        $results[$step.name] = 'passed'
    }
}
catch {$failure=$_.Exception.ToString()}
finally {
    [ordered]@{passed=(!$failure);steps=$results;error=$failure;startAt=$StartAt;fullMatrixRun=($StartAt -eq 'startup');
      player=$player; entryDllSha256=$artifactHash;
      scope='Serial local Mono acceptance; visual review, IL2CPP and two-machine LAN are separate';artifacts=$run} |
      ConvertTo-Json -Depth 6 | Set-Content (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Delivery: passed=$(!$failure); $run/result.json"
}
if ($failure) {throw $failure}
