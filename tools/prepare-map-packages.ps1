param([string]$FrameworkPath = 'D:/Developer/YYGC')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$lockPath = Join-Path $PSScriptRoot 'map-framework-patch/source-lock-map-state.json'
$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
$short = $lock.commit.Substring(0, 7)
$destination = Join-Path $root ".deps/AnyRules-map-state-$short"
$archive = Join-Path $root ".deps/anyrules-map-state-$short.zip"
$marker = Join-Path $destination 'source-commit.txt'

if (-not (Test-Path -LiteralPath $destination)) {
    if (Test-Path -LiteralPath $archive) { throw "Preserve existing archive for inspection: $archive" }
    git -C $FrameworkPath archive --format=zip --output=$archive $lock.commit `
        AnyRuleD~/Packages/com.tsgame.anyrules `
        AnyRuleD~/Packages/com.tsgame.anyrules.yygc `
        AnyRuleD~/Packages/com.tsgame.anyrules.networking `
        AnyRuleD~/Packages/com.tsgame.anyrules.networking.fishnet
    if ($LASTEXITCODE) { throw "Cannot archive locked AnyRuleD source: $($lock.commit)" }
    New-Item -ItemType Directory -Path $destination | Out-Null
    Expand-Archive -LiteralPath $archive -DestinationPath $destination
    Set-Content -LiteralPath $marker -Value $lock.commit -Encoding utf8
}
if (-not (Test-Path -LiteralPath $marker) -or (Get-Content -LiteralPath $marker -Raw).Trim() -ne $lock.commit) {
    throw "Existing AnyRuleD extraction has a different source marker: $destination"
}
$packageRoot = Join-Path $destination 'AnyRuleD~/Packages'
$mismatches = New-Object System.Collections.Generic.List[string]
foreach ($entry in $lock.files) {
    $file = Join-Path $packageRoot $entry.path
    if (-not (Test-Path -LiteralPath $file)) { $mismatches.Add($entry.path); continue }
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $entry.sha256) { $mismatches.Add($entry.path) }
}
if ($mismatches.Count -ne 0) {
    throw "AnyRuleD locked source differs in $($mismatches.Count) file(s): $($mismatches -join ', ')"
}
Write-Output "AnyRuleD map-state packages ready: $($lock.commit); $($lock.files.Count) files verified"
