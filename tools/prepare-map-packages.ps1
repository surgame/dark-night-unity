param([string]$FrameworkPath = 'D:/Developer/YYGC')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'map-framework-patch/source-lock-map-state.json') -Raw | ConvertFrom-Json
if ($lock.format_version -ne 2 -or $lock.commit -notmatch '^[0-9a-f]{40}$') { throw 'Unsupported map source lock; use the prepare script from the same game commit.' }
$packages = @('com.tsgame.anyrules', 'com.tsgame.anyrules.yygc', 'com.tsgame.anyrules.networking', 'com.tsgame.anyrules.networking.fishnet')
$commit = $lock.commit
& git -C $FrameworkPath cat-file -e "$($commit)^{commit}"
if ($LASTEXITCODE) { throw "Locked YYGC commit is missing. Fetch origin fix-20260925-terrain-refresh-review in $FrameworkPath; no checkout/reset is required." }
$expected = @{}
foreach ($package in $packages) {
    $prefix = "AnyRuleD~/Packages/$package"
    $actual = (& git -C $FrameworkPath rev-parse "$($commit):$prefix").Trim()
    if ($LASTEXITCODE -or $actual -ne $lock.package_trees.$package) { throw "Package tree does not match lock: $package" }
    $rows = & git -C $FrameworkPath -c core.quotepath=false ls-tree -r $commit -- $prefix
    if ($LASTEXITCODE) { throw "Cannot enumerate locked package: $package" }
    foreach ($row in $rows) {
        if ($row -notmatch '^100(?:644|755) blob ([0-9a-f]{40})\t(.+)$') { throw "Unsupported package entry: $row" }
        $expected[$Matches[2]] = $Matches[1]
    }
}
if ($expected.Count -eq 0) { throw 'The locked package inventory is empty.' }
$short = $commit.Substring(0, 7)
$destination = Join-Path $root ".deps/AnyRules-map-state-$short"
$marker = Join-Path $destination 'source-commit.txt'
if (!(Test-Path -LiteralPath $destination)) {
    $archive = Join-Path $root ".deps/anyrules-map-state-$short.zip"
    if (Test-Path -LiteralPath $archive) { throw "Preserve existing archive for inspection: $archive" }
    New-Item -ItemType Directory -Force -Path (Join-Path $root '.deps') | Out-Null
    $paths = @($packages | ForEach-Object { "AnyRuleD~/Packages/$_" })
    & git -C $FrameworkPath archive --format=zip "--output=$archive" $commit @paths
    if ($LASTEXITCODE) { throw 'Cannot export the locked AnyRuleD packages.' }
    Expand-Archive -LiteralPath $archive -DestinationPath $destination
    [IO.File]::WriteAllText($marker, $commit + "`n", [Text.UTF8Encoding]::new($false))
}
if (!(Test-Path -LiteralPath $marker) -or (Get-Content -LiteralPath $marker -Raw).Trim() -ne $commit) {
    throw "Existing package extraction has a different source marker: $destination"
}
$audit = [System.Collections.Generic.List[object]]::new()
foreach ($relative in ($expected.Keys | Sort-Object)) {
    $file = Join-Path $destination $relative
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing locked file: $relative" }
    $blob = (& git -C $FrameworkPath hash-object --no-filters -- $file).Trim()
    if ($LASTEXITCODE -or $blob -ne $expected[$relative]) { throw "Locked source bytes differ: $relative" }
    $audit.Add([ordered]@{ path = $relative; sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() })
}
$prefixLength = $destination.TrimEnd([char[]]'\/').Length + 1
foreach ($package in $packages) {
    $packageRoot = Join-Path $destination "AnyRuleD~/Packages/$package"
    foreach ($file in (Get-ChildItem -LiteralPath $packageRoot -File -Recurse -Force)) {
        $relative = $file.FullName.Substring($prefixLength).Replace('\', '/')
        if (!$expected.ContainsKey($relative)) {
            throw "Unexpected package file: $relative. Preserve generated Unity metadata; commit/import it in the framework source and update the lock rather than overwriting the extraction."
        }
    }
}
foreach ($name in @('manifest.json', 'packages-lock.json')) {
    $metadata = Get-Content -LiteralPath (Join-Path $root "Game/Packages/$name") -Raw | ConvertFrom-Json
    foreach ($package in $packages) {
        $wanted = "file:../../.deps/AnyRules-map-state-$short/AnyRuleD~/Packages/$package"
        $value = $metadata.dependencies.$package
        if ($name -eq 'packages-lock.json') { $value = $value.version }
        if ($value -ne $wanted) { throw "$name does not reference the locked source for $package" }
    }
}
$report = [ordered]@{ commit = $commit; verified_files = $expected.Count; files = $audit }
[IO.File]::WriteAllText((Join-Path $destination 'source-audit-sha256.json'), ($report | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Output "AnyRuleD packages ready: $commit; $($expected.Count) raw files verified. GameCore/YYGC-unified and FishNet pins were not changed."
