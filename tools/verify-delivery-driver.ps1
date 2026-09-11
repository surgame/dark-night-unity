$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$run = Join-Path $repo ('artifacts/migration/delivery-driver-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $run | Out-Null
$checks = [ordered]@{}
$failure = $null

function Case([string]$Name, [string]$StartAt, [string]$PressureScript) {
    $fixture = Join-Path $run $Name
    $fixtureTools = Join-Path $fixture 'tools'
    $managed = Join-Path $fixture 'artifacts/migration/player-mono/DarkNights_Data/Managed'
    New-Item -ItemType Directory -Path $fixtureTools,$managed | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'test-game-delivery.ps1') -Destination $fixtureTools
    # These inert text fixtures test process orchestration only; no real Player is launched.
    Set-Content -LiteralPath (Join-Path $managed 'DarkNights.Entry.dll') -Value 'inert driver fixture' -Encoding utf8
    Set-Content -LiteralPath (Join-Path $fixtureTools 'test-game-startup.ps1') -Encoding utf8 -Value @'
param([string]$Backend)
if ($Backend -ne 'mono') {exit 17}
Write-Output 'fixture startup passed'
exit 0
'@
    Set-Content -LiteralPath (Join-Path $fixtureTools 'test-game-session.ps1') -Value 'exit 23' -Encoding utf8
    Set-Content -LiteralPath (Join-Path $fixtureTools 'test-game-pressure.ps1') -Value $PressureScript -Encoding utf8
    & pwsh -NoProfile -File (Join-Path $fixtureTools 'test-game-delivery.ps1') -StartAt $StartAt *> (Join-Path $fixture 'driver.log')
    $exitCode = $LASTEXITCODE
    $resultPath = @(Get-ChildItem (Join-Path $fixture 'artifacts/migration') -Directory -Filter 'delivery-*')
    if ($resultPath.Count -ne 1) {throw 'Driver must create exactly one result directory.'}
    return @{code=$exitCode;result=(Get-Content (Join-Path $resultPath[0].FullName 'result.json') -Raw | ConvertFrom-Json)}
}

function Check([string]$Name, [bool]$Ok) {
    $checks[$Name]=$Ok
    if (!$Ok) {throw "Failed: $Name"}
}

try {
    $failed = Case 'nonzero-exit' 'startup' 'exit 0'
    Check 'nonzero_child_exit_stops_delivery' ($failed.code -ne 0 -and !$failed.result.passed -and $failed.result.error -match 'code 23')
    Check 'only_completed_predecessor_is_recorded' ((@($failed.result.steps.PSObject.Properties.Name) -join ',') -eq 'startup')
    $resume = Case 'resume' 'pressure' 'exit 0'
    Check 'resume_runs_requested_suffix_only' ($resume.code -eq 0 -and $resume.result.passed -and (@($resume.result.steps.PSObject.Properties.Name) -join ',') -eq 'pressure')
    Check 'resume_does_not_claim_full_matrix' (!$resume.result.fullMatrixRun -and $resume.result.startAt -eq 'pressure')
    $changed = Case 'artifact-change' 'pressure' @'
$fixture = Split-Path $PSScriptRoot -Parent
Set-Content -LiteralPath (Join-Path $fixture 'artifacts/migration/player-mono/DarkNights_Data/Managed/DarkNights.Entry.dll') -Value 'changed fixture' -Encoding utf8
exit 0
'@
    Check 'changed_artifact_rejects_acceptance' ($changed.code -ne 0 -and !$changed.result.passed -and $changed.result.error -match 'Player code changed')
}
catch {$failure=$_.Exception.ToString()}
finally {
    [ordered]@{passed=(!$failure);checks=$checks;error=$failure;scope='Inert fixtures for delivery orchestration; not game or Player acceptance';artifacts=$run} |
        ConvertTo-Json -Depth 6 | Set-Content (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Delivery driver: passed=$(!$failure) checks=$($checks.Count); $run/result.json"
}
if ($failure) {throw $failure}
