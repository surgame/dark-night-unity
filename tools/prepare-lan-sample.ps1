param([string]$FrameworkPath = 'D:\Developer\YYGC')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$commit = '10b8f0ef6a5ed965ebd473dbcbe4a0dd795379c4'
$checkout = Join-Path $root '.deps/YYGC'
if (!(Test-Path $checkout)) {
    git clone --no-hardlinks --no-checkout $FrameworkPath $checkout
    if ($LASTEXITCODE) { throw 'YYGC clone failed' }
    git -C $checkout checkout --detach $commit
    if ($LASTEXITCODE) { throw 'YYGC checkout failed' }
}
if ((git -C $checkout rev-parse HEAD) -ne $commit) { throw 'Isolated YYGC commit mismatch' }
$registryPatch = Join-Path $PSScriptRoot 'lan-framework-patch/ExcludeSampleFromGlobalRegistry.patch'
$expectedDiff = (Get-Content -LiteralPath $registryPatch -Raw).Replace("`r`n", "`n").TrimEnd()
$actualDiff = (git -C $checkout diff HEAD --binary) -join "`n"
if ($LASTEXITCODE) { throw 'Unable to inspect isolated YYGC changes' }
if ($actualDiff -and $actualDiff.TrimEnd() -ne $expectedDiff) {
    throw 'Isolated YYGC has unexpected tracked changes; preserve and inspect them first'
}
$allowed = @('Runtime/NetworkCommands/SampleAssemblyAccess.cs', 'Runtime/NetworkCommands/SampleAssemblyAccess.cs.meta')
foreach ($untracked in (git -C $checkout ls-files --others --exclude-standard)) {
    if ($untracked -notin $allowed) { throw "Unexpected file in isolated YYGC: $untracked" }
}
if (!$actualDiff) {
    git -C $checkout apply --check $registryPatch
    if ($LASTEXITCODE) { throw 'Sample registry exclusion patch does not match locked YYGC' }
    git -C $checkout apply $registryPatch
    if ($LASTEXITCODE) { throw 'Unable to apply sample registry exclusion patch' }
}
foreach ($name in @('SampleAssemblyAccess.cs', 'SampleAssemblyAccess.cs.meta')) {
    $source = Join-Path $PSScriptRoot "lan-framework-patch/$name"
    $destination = Join-Path $checkout "Runtime/NetworkCommands/$name"
    if ((Test-Path $destination) -and (Get-FileHash $source).Hash -ne (Get-FileHash $destination).Hash) {
        throw "Locally edited patch: $destination"
    }
    Copy-Item -LiteralPath $source -Destination $destination
}
Write-Output "YYGC sample dependency ready: $commit"
