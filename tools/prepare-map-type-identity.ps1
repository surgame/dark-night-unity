param([string]$CheckoutPath = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$checkout = if ($CheckoutPath) { [IO.Path]::GetFullPath($CheckoutPath) } else { Join-Path $root '.deps/YYGC-unified' }
$relative = 'Runtime/Objects/NetworkStates/DefinitionNetworkProfile.cs'
$source = Join-Path $checkout $relative
$patch = Join-Path $PSScriptRoot 'lan-framework-patch/MapTypeWireIdentityAlias.patch'
$baseCommit = '12b253c6bdd262feb860ab905b9e56e940ec9c40'
$baseHash = '819DE7B8F81A4FE4F638D9A7015160043058082B36C7A428E00D0E31C0967007'
$patchedHash = '0E22C5BC3878F98B93FD9F2A1B68661E4E6A3426B64B61A5F6F1F08E13F6E843'
$patchHash = '7F0F59C0E25DECC7DC84788441460514F856D66B69441AD4B18358E9A1D0D63B'
if (!(Test-Path -LiteralPath $source) -or !(Test-Path -LiteralPath $patch)) { throw 'Locked YYGC dependency or patch is missing.' }
if ((git -C $checkout rev-parse HEAD).Trim() -ne $baseCommit) { throw 'YYGC dependency base commit changed.' }
if ((Get-FileHash -LiteralPath $patch -Algorithm SHA256).Hash -ne $patchHash) { throw 'YYGC wire-identity patch changed.' }
$current = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
if ($current -eq $patchedHash) { Write-Output 'YYGC wire identity ready: already applied'; return }
if ($current -ne $baseHash) { throw 'YYGC profile contains unknown changes; preserve and inspect it.' }
git -C $checkout apply --check $patch
if ($LASTEXITCODE) { throw 'YYGC wire-identity patch does not match locked source.' }
git -C $checkout apply $patch
if ($LASTEXITCODE -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $patchedHash) {
    throw 'YYGC wire-identity patch was not installed exactly.'
}
Write-Output 'YYGC wire identity ready: source 1501025, one locked patch'
