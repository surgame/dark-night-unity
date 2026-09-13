param([string]$FrameworkPath = 'D:\Developer\YYGC')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$commit = 'ddce2ffdf422c8c9cb8e872fb5f20053cdedcda6'
$checkout = Join-Path $root '.deps/YYGC-unified'
if (!(Test-Path $checkout)) {
    git clone --no-hardlinks --no-checkout $FrameworkPath $checkout
    if ($LASTEXITCODE) { throw 'YYGC clone failed' }
    git -C $checkout checkout --detach $commit
    if ($LASTEXITCODE) { throw 'YYGC checkout failed' }
}
if ((git -C $checkout rev-parse HEAD) -ne $commit) { throw 'Isolated YYGC commit mismatch' }
$registryPatch = Join-Path $PSScriptRoot 'lan-framework-patch/ExcludeSampleFromGlobalRegistry.patch'
$expectedDiff = (Get-Content -LiteralPath $registryPatch -Raw).Replace("`r`n", "`n").TrimEnd()
$startupPatch = Join-Path $PSScriptRoot 'lan-framework-patch/ExcludeSampleFromStartupValidation.patch'
$startupDiff = (Get-Content -LiteralPath $startupPatch -Raw).Replace("`r`n", "`n").TrimEnd()
$combinedDiff = $expectedDiff + "`n" + $startupDiff
$uiPatch = Join-Path $PSScriptRoot 'lan-framework-patch/FixUguiRootUnityNull.patch'
$uiDiff = (Get-Content -LiteralPath $uiPatch -Raw).Replace("`r`n", "`n").TrimEnd()
$completeDiff = $combinedDiff + "`n" + $uiDiff
$previousCompleteDiff = $completeDiff
$singletonPatch = Join-Path $PSScriptRoot 'lan-framework-patch/RestoreSingletonOnPooledReentry.patch'
$singletonDiff = (Get-Content -LiteralPath $singletonPatch -Raw).Replace("`r`n", "`n").TrimEnd()
$completeDiff = $combinedDiff + "`n" + $singletonDiff + "`n" + $uiDiff
$actualDiff = (git -C $checkout diff HEAD --binary) -join "`n"
if ($LASTEXITCODE) { throw 'Unable to inspect isolated YYGC changes' }
if ($actualDiff -and $actualDiff.TrimEnd() -ne $expectedDiff -and $actualDiff.TrimEnd() -ne $combinedDiff -and $actualDiff.TrimEnd() -ne $previousCompleteDiff -and $actualDiff.TrimEnd() -ne $completeDiff) {
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
if (!$actualDiff -or $actualDiff.TrimEnd() -eq $expectedDiff) {
    git -C $checkout apply --check $startupPatch
    if ($LASTEXITCODE) { throw 'Startup validation patch does not match locked YYGC' }
    git -C $checkout apply $startupPatch
    if ($LASTEXITCODE) { throw 'Unable to apply sample startup validation exclusion patch' }
}
if (!$actualDiff -or ($actualDiff.TrimEnd() -ne $completeDiff -and $actualDiff.TrimEnd() -ne $previousCompleteDiff)) {
    git -C $checkout apply --check $uiPatch
    if ($LASTEXITCODE) { throw 'UGUI root null patch does not match locked YYGC' }
    git -C $checkout apply $uiPatch
    if ($LASTEXITCODE) { throw 'Unable to apply UGUI root null patch' }
}
if (!$actualDiff -or $actualDiff.TrimEnd() -ne $completeDiff) {
    git -C $checkout apply --check $singletonPatch
    if ($LASTEXITCODE) { throw 'Singleton reentry patch does not match locked YYGC' }
    git -C $checkout apply $singletonPatch
    if ($LASTEXITCODE) { throw 'Unable to apply singleton reentry patch' }
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
