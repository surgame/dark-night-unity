$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$checkout = Join-Path $root '.deps/FishNet'
$commit = 'de19b5d66459f60400ffd0edc443c4da173a01e7'
if (!(Test-Path -LiteralPath $checkout)) {
    git clone --quiet --depth 1 --branch 4.7.2 --filter=blob:none https://github.com/FirstGearGames/FishNet.git $checkout
    if ($LASTEXITCODE) { throw 'FishNet clone failed.' }
}
if ((git -C $checkout rev-parse HEAD) -ne $commit) { throw 'Isolated FishNet does not match locked commit.' }
$patch = Join-Path $PSScriptRoot 'fishnet-patch/ResetClientSplitOnDisconnect.patch'
$expected = (Get-Content -LiteralPath $patch -Raw).Replace("`r`n", "`n").TrimEnd()
$actual = (git -C $checkout diff HEAD --binary) -join "`n"
if ($LASTEXITCODE) { throw 'Cannot inspect isolated FishNet.' }
if ($actual -and $actual.TrimEnd() -ne $expected) { throw 'Preserving unexpected local FishNet changes.' }
$untracked = @(git -C $checkout ls-files --others --exclude-standard)
if ($untracked.Count) { throw 'Preserving unexpected untracked FishNet files.' }
if (!$actual) {
    git -C $checkout apply --check $patch
    if ($LASTEXITCODE) { throw 'FishNet patch does not match the locked baseline.' }
    git -C $checkout apply $patch
    if ($LASTEXITCODE) { throw 'Unable to apply FishNet lifecycle patch.' }
}
Write-Output "FishNet dependency ready: $commit + ResetClientSplitOnDisconnect"
