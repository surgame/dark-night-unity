param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$ArchiveRoot
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$archives = [IO.Path]::GetFullPath($ArchiveRoot)
$lockPath = Join-Path $project 'Packages/packages-lock.json'
$locked = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
$manifest = Get-Content -LiteralPath (Join-Path $project 'Packages/manifest.json') -Raw | ConvertFrom-Json
$packages = @($locked.dependencies.PSObject.Properties | Where-Object { $_.Value.source -eq 'git' })
$report = [ordered]@{
    project = $project; originalLockSha256 = (Get-FileHash -LiteralPath $lockPath).Hash
    source = 'Public GitHub archives at exact packages-lock commits; temporary embedded packages'
    packages = @()
}

# This prepares only a disposable clean build project. Existing package directories are never overwritten.
foreach ($entry in $packages) {
    if (Test-Path -LiteralPath (Join-Path $project ('Packages/' + $entry.Name))) {
        throw "Preserving existing embedded package: $($entry.Name)"
    }
    if ($entry.Value.hash -notmatch '^[a-f0-9]{40}$' -or
        $manifest.dependencies.($entry.Name) -ne $entry.Value.version) {
        throw "Git dependency does not have a matching full commit lock: $($entry.Name)"
    }
}
New-Item -ItemType Directory -Path $archives -Force | Out-Null
foreach ($entry in $packages) {
    $uri = [Uri]$entry.Value.version
    if ($uri.Scheme -ne 'https' -or $uri.Host -ne 'github.com' -or $uri.Query -notlike '?path=*') {
        throw "Unsupported locked package URL: $($entry.Name)"
    }
    $repository = $uri.AbsolutePath.Trim('/').Replace('.git', '')
    $relative = [Uri]::UnescapeDataString($uri.Query.Substring(6)).Trim('/')
    $archive = Join-Path $archives ($entry.Name + '-' + $entry.Value.hash + '.zip')
    $extracted = Join-Path $archives ($entry.Name + '-' + $entry.Value.hash)
    if (Test-Path -LiteralPath $extracted) { throw "Preserving prior extraction: $extracted" }
    if (!(Test-Path -LiteralPath $archive)) {
        Invoke-WebRequest -Uri ("https://api.github.com/repos/$repository/zipball/" + $entry.Value.hash) -OutFile $archive -TimeoutSec 60
    }
    Expand-Archive -LiteralPath $archive -DestinationPath $extracted
    $roots = @(Get-ChildItem -LiteralPath $extracted -Directory)
    if ($roots.Count -ne 1) { throw "Unexpected archive layout: $archive" }
    $source = [IO.Path]::GetFullPath((Join-Path $roots[0].FullName $relative))
    if (!$source.StartsWith($roots[0].FullName + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Locked package path must remain inside its repository.'
    }
    $metadata = Get-Content -LiteralPath (Join-Path $source 'package.json') -Raw | ConvertFrom-Json
    if ($metadata.name -ne $entry.Name) { throw "Package identity mismatch: $source" }
    $target = Join-Path $project ('Packages/' + $entry.Name)
    Copy-Item -LiteralPath $source -Destination $target -Recurse
    $report.packages += [ordered]@{
        name = $entry.Name; commit = $entry.Value.hash; version = $metadata.version
        archiveSha256 = (Get-FileHash -LiteralPath $archive).Hash; target = $target
        files = @(Get-ChildItem -LiteralPath $target -File -Recurse).Count
    }
    Write-Output "Prepared locked package: $($entry.Name) at $($entry.Value.hash)"
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $archives 'prepared-packages.json') -Encoding utf8
