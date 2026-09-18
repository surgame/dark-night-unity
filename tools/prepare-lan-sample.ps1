param(
    [string]$FrameworkPath = 'D:\Developer\YYGC',
    [string]$Repository = 'https://github.com/surgame/YYGC.git',
    [string]$CheckoutPath = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$commit = '12b253c6bdd262feb860ab905b9e56e940ec9c40'
$baseCommit = '0c7cec00b7a7f9cec0287bb56d0af9fc45c9d143'
$networkPatchSha256 = '2D86D92A3EBE1B4DD37650C206B00D969F93850C3648D56293E4DC01927AB17E'
$checkout = if ([string]::IsNullOrWhiteSpace($CheckoutPath)) { Join-Path $root '.deps/YYGC-unified' } else { [IO.Path]::GetFullPath($CheckoutPath) }
$networkPatch = Join-Path $PSScriptRoot 'lan-framework-patch/NetworkCommandInterfaceGenerator.patch'
$fromRemoteBase = $false
if (!(Test-Path $checkout)) {
    $source = if (Test-Path -LiteralPath $FrameworkPath) { $FrameworkPath } else { $Repository }
    if ([string]::IsNullOrWhiteSpace($source)) { throw 'YYGC source is unavailable; provide -FrameworkPath or -Repository.' }
    git clone --no-hardlinks --no-checkout $source $checkout
    if ($LASTEXITCODE) { throw 'YYGC clone failed' }
    git -C $checkout checkout --detach $baseCommit
    if ($LASTEXITCODE) { throw 'YYGC checkout failed' }
    $fromRemoteBase = $true
}
$head = (git -C $checkout rev-parse HEAD).Trim()
if ($head -ne $commit -and $head -ne $baseCommit) { throw 'Isolated YYGC commit mismatch' }
if ($head -eq $baseCommit) { $fromRemoteBase = $true }
$registryPatch = Join-Path $PSScriptRoot 'lan-framework-patch/ExcludeSampleFromGlobalRegistry.patch'
$expectedDiff = [IO.File]::ReadAllText($registryPatch).Replace("`r`n", "`n").TrimEnd()
$startupPatch = Join-Path $PSScriptRoot 'lan-framework-patch/ExcludeSampleFromStartupValidation.patch'
$startupDiff = [IO.File]::ReadAllText($startupPatch).Replace("`r`n", "`n").TrimEnd()
$combinedDiff = $expectedDiff + "`n" + $startupDiff
$uiPatch = Join-Path $PSScriptRoot 'lan-framework-patch/FixUguiRootUnityNull.patch'
$uiDiff = [IO.File]::ReadAllText($uiPatch).Replace("`r`n", "`n").TrimEnd()
$actualNetworkPatchSha256 = (Get-FileHash -LiteralPath $networkPatch -Algorithm SHA256).Hash
if ($actualNetworkPatchSha256 -ne $networkPatchSha256) { throw 'Network command patch SHA-256 mismatch' }
$completeDiff = $combinedDiff + "`n" + $uiDiff
$previousCompleteDiff = $completeDiff
$singletonPatch = Join-Path $PSScriptRoot 'lan-framework-patch/RestoreSingletonOnPooledReentry.patch'
$singletonDiff = [IO.File]::ReadAllText($singletonPatch).Replace("`r`n", "`n").TrimEnd()
$completeDiff = $combinedDiff + "`n" + $singletonDiff + "`n" + $uiDiff
$allowed = @('Runtime/NetworkCommands/SampleAssemblyAccess.cs', 'Runtime/NetworkCommands/SampleAssemblyAccess.cs.meta')
foreach ($untracked in (git -C $checkout ls-files --others --exclude-standard)) {
    if ($untracked -notin $allowed) { throw "Unexpected file in isolated YYGC: $untracked" }
}
if ($fromRemoteBase) {
    $baseDiff = (git -C $checkout diff HEAD --binary) -join "`n"
    if ($LASTEXITCODE) { throw 'Unable to inspect remote-base YYGC changes' }
    if ($baseDiff.TrimEnd().Length -ne 0) { throw 'Remote-base YYGC has unexpected tracked changes; preserve and inspect them first' }
    foreach ($patch in @($networkPatch, $registryPatch, $startupPatch, $singletonPatch, $uiPatch)) {
        $previousErrorAction = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        git -C $checkout apply --reverse --check $patch 2>$null
        $reverseStatus = $LASTEXITCODE
        $ErrorActionPreference = $previousErrorAction
        if ($reverseStatus -eq 0) { continue }
        git -C $checkout apply --check $patch
        if ($LASTEXITCODE) { throw "YYGC base patch does not match locked source: $patch" }
        git -C $checkout apply $patch
        if ($LASTEXITCODE) { throw "Unable to apply YYGC base patch: $patch" }
    }
    $expectedTracked = @(
        'Editor/NetworkCommands/NetworkCommandInterfaceGenerator.cs',
        'Editor/Objects/NetworkStates/StateDataRegistryUpdater.cs',
        'Runtime/NetworkCommands/NetworkCommandStartupModule.cs',
        'Runtime/Objects/NetworkStates/StateDataTypeStartupModule.cs',
        'Runtime/Objects/Singletons/SingletonBehaviours.cs',
        'Runtime/UI/UGUI/UGUIRuntimeStartupModule.cs')
    $changedTracked = @(git -C $checkout diff HEAD --name-only)
    if ($LASTEXITCODE) { throw 'Unable to inspect remote-base YYGC changes' }
    if ($changedTracked.Count -ne $expectedTracked.Count -or
        ($changedTracked | Where-Object { $_ -notin $expectedTracked }).Count -ne 0) {
        throw 'Remote-base YYGC patch set contains unexpected tracked changes'
    }
} else {
    $actualDiff = (git -C $checkout diff HEAD --binary) -join "`n"
    if ($LASTEXITCODE) { throw 'Unable to inspect isolated YYGC changes' }
    if ($actualDiff -and $actualDiff.TrimEnd() -ne $expectedDiff.TrimEnd() -and $actualDiff.TrimEnd() -ne $combinedDiff.TrimEnd() -and $actualDiff.TrimEnd() -ne $previousCompleteDiff.TrimEnd() -and $actualDiff.TrimEnd() -ne $completeDiff.TrimEnd()) {
        throw 'Isolated YYGC has unexpected tracked changes; preserve and inspect them first'
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
}
foreach ($name in @('SampleAssemblyAccess.cs', 'SampleAssemblyAccess.cs.meta')) {
    $source = Join-Path $PSScriptRoot "lan-framework-patch/$name"
    $destination = Join-Path $checkout "Runtime/NetworkCommands/$name"
    if ((Test-Path $destination) -and (Get-FileHash $source).Hash -ne (Get-FileHash $destination).Hash) {
        throw "Locally edited patch: $destination"
    }
    Copy-Item -LiteralPath $source -Destination $destination
}
if ($fromRemoteBase) { Write-Output "YYGC sample dependency ready: remote base $baseCommit + locked patches (equivalent target $commit)" }
else { Write-Output "YYGC sample dependency ready: $commit" }
