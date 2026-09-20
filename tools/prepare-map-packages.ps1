param([string]$FrameworkPath = 'D:/Developer/YYGC')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$commit = 'aa450a7168d5800b0899e216ed484892e44fb40a'
$destination = Join-Path $root '.deps/AnyRules-locked-aa450a7'
$marker = Join-Path $destination 'source-commit.txt'
if (Test-Path $destination) {
    if (!(Test-Path $marker) -or (Get-Content $marker -Raw).Trim() -ne $commit) {
        throw 'Existing AnyRules directory is not the locked extraction; preserve it for inspection.'
    }
} else {
    $archive = Join-Path $root '.deps/anyrules-locked-aa450a7.zip'
    if (Test-Path $archive) { throw 'Archive already exists; preserve it for inspection.' }
    git -C $FrameworkPath archive --format=zip --output=$archive $commit AnyRuleD~/Packages/com.tsgame.anyrules AnyRuleD~/Packages/com.tsgame.anyrules.yygc AnyRuleD~/Packages/com.tsgame.anyrules.yygc.fishnet
    if ($LASTEXITCODE) { throw 'Unable to archive locked AnyRules source.' }
    New-Item -ItemType Directory -Path $destination | Out-Null
    Expand-Archive -LiteralPath $archive -DestinationPath $destination
    Set-Content -LiteralPath $marker -Value $commit -Encoding utf8
    Write-Output "AnyRules prepared: $commit; archive retained for stage cleanup: $archive"
}
$lock = Get-Content (Join-Path $PSScriptRoot 'map-framework-patch/source-lock.json') -Raw | ConvertFrom-Json
$packageRoot = Join-Path $destination 'AnyRuleD~/Packages'
$locked = $true
foreach ($entry in $lock.files) {
    $file = Join-Path $packageRoot $entry.path
    if (!(Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) {
        $locked = $false
        break
    }
}
if ($locked) {
    Write-Output "AnyRules locked source and map network patches ready: $commit; $($lock.files.Count) files verified"
    return
}
Push-Location $root
try {
    foreach ($name in @('IdleMapPublication','RealtimeMapRetry','PlayableMapChunkBudget','TerrainEditCommandPayload')) {
        $patch = Join-Path $PSScriptRoot "map-framework-patch/$name.patch"
        git apply --directory=.deps/AnyRules-locked-aa450a7 --reverse --check --ignore-space-change $patch 2>$null
        if ($LASTEXITCODE -ne 0) {
            git apply --directory=.deps/AnyRules-locked-aa450a7 --check --ignore-space-change $patch
            if ($LASTEXITCODE) { throw 'AnyRules patch does not match locked source; preserve local changes.' }
            git apply --directory=.deps/AnyRules-locked-aa450a7 --ignore-space-change $patch
            if ($LASTEXITCODE) { throw "AnyRules $name patch failed." }
        }
    }
} finally { Pop-Location }
$encoding = [Text.UTF8Encoding]::new($false)
foreach ($relative in @('Protocol/MapInterestService.cs','Runtime/FishNetMapTransport.cs','Protocol/ProtocolLimits.cs')) {
    $patched = Join-Path $destination "AnyRuleD~/Packages/com.tsgame.anyrules.yygc.fishnet/$relative"
    $original = [IO.File]::ReadAllText($patched)
    $contents = $original.Replace("`r`n", "`n")
    if ($original -cne $contents) { [IO.File]::WriteAllText($patched, $contents, $encoding) }
}
$lock = Get-Content (Join-Path $PSScriptRoot 'map-framework-patch/source-lock.json') -Raw | ConvertFrom-Json
$packageRoot = Join-Path $destination 'AnyRuleD~/Packages'
foreach ($entry in $lock.files) {
    $file = Join-Path $packageRoot $entry.path
    if (!(Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) {
        throw "AnyRules source differs from lock; preserve and inspect: $file"
    }
}
Write-Output "AnyRules locked source and map network patches ready: $commit; $($lock.files.Count) files verified"
