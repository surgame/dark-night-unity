<#
.SYNOPSIS
只读采集 YYGC 与 Dark Nights 的评估基线，报告仅写入当前 Unity 仓库。
.DESCRIPTION
记录 Git 状态、源码物理行数、关键文件哈希、生成器和素材完整性。
不启动引擎、不执行输入仓库脚本、不改变输入内容；常规输出不覆盖冻结证据。
#>
[CmdletBinding()]
param(
    [string]$FrameworkPath = 'D:\Developer\YYGC',
    [string]$GamePath = (Join-Path $PSScriptRoot '..\..\projects'),
    [string]$OutputPath = 'artifacts/assessment-current.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$frameworkRoot = (Resolve-Path -LiteralPath $FrameworkPath).Path
$gameRoot = (Resolve-Path -LiteralPath $GamePath).Path
$reportPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputPath))
if (-not $reportPath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw '评估报告必须写入当前 Unity 仓库。'
}

function Invoke-RepositoryGit([string]$Root, [string[]]$Arguments) {
    $result = @(& git -C $Root -c core.quotepath=false @Arguments)
    if ($LASTEXITCODE -ne 0) { throw "Git failed: $Root" }
    return $result
}

function Get-RepositoryState([string]$Root) {
    return [ordered]@{
        path = $Root
        head = (Invoke-RepositoryGit $Root @('rev-parse', 'HEAD')) -join ''
        status = @(Invoke-RepositoryGit $Root @('status', '--porcelain=v1', '-uall'))
        staged_numstat = @(Invoke-RepositoryGit $Root @('diff', '--cached', '--numstat'))
    }
}

function Get-FileEvidence([string]$Root, [string[]]$Paths) {
    foreach ($relative in $Paths) {
        $fullPath = Join-Path $Root $relative
        $item = Get-Item -LiteralPath $fullPath
        [ordered]@{
            path = $relative.Replace('\', '/')
            bytes = $item.Length
            sha256 = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
}

function Get-CodeInventory([string]$Root, [string[]]$Folders, [int]$ModuleDepth) {
    Push-Location -LiteralPath $Root
    try {
        $files = @(& rg --files @Folders -g '*.cs' -g '!**/SourceGenerators~/**')
        if ($LASTEXITCODE -ne 0) { throw "Source scan failed: $Root" }
    } finally { Pop-Location }
    $rows = @(foreach ($relative in $files) {
        $normalized = $relative.Replace('\', '/')
        $lines = [IO.File]::ReadAllLines((Join-Path $Root $relative))
        [pscustomobject]@{
            path = $normalized
            lines = $lines.Count
            module = ($normalized -split '/')[$ModuleDepth]
        }
    })
    return [ordered]@{
        counting = 'Physical .cs lines including comments and blanks; excludes Editor/SourceGenerators~. Folder totals are not Player assembly totals.'
        files = $rows.Count
        lines = [int]($rows | Measure-Object lines -Sum).Sum
        over_300 = @($rows | Where-Object lines -gt 300).Count
        modules = @($rows | Group-Object module | Sort-Object Name | ForEach-Object {
            [ordered]@{ name = $_.Name; files = $_.Count; lines = [int]($_.Group | Measure-Object lines -Sum).Sum }
        })
        longest = @($rows | Sort-Object lines -Descending | Select-Object -First 12 path, lines)
    }
}

$frameworkFiles = @(
    'package.json', 'GameCore.Runtime.asmdef', 'Editor/GameCore.Editor.asmdef', 'Tests/GameCore.Tests.asmdef',
    'Runtime/Objects/NetworkStates/StateSynchronizer.cs', 'Runtime/Objects/NetworkStates/StatefulBehaviour.cs',
    'Runtime/NetworkCommands/NetworkCommandGateway.cs', 'Runtime/NetworkCommands/NetworkCommandProcessor.cs',
    'Runtime/NetworkCommands/NetworkCommandSender.cs', 'Runtime/NetworkCommands/INetworkCommand.cs',
    'Runtime/Objects/Runner/ObjectInstance.cs', 'Runtime/Objects/Runner/ObjectInstanceFactory.cs',
    'Runtime/Objects/Runner/LocalObjectInstanceInitializer.cs', 'Runtime/Objects/Runner/DI/SessionScope.cs',
    'Runtime/Objects/Views/ObjectView.cs', 'Runtime/Objects/Definition/ObjectDefinition.cs',
    'Runtime/Middlewares/GenericTypeSerializer/GenericTypeSerializer.cs', 'Runtime/Utils/IsExternalInit.cs',
    'Runtime/AppStartup/AppStartup.cs', 'Runtime/UI/UGUI/UGUIManager.cs', 'Runtime/YYPlugins/YYArchive/YYArchiveService.cs'
)
$gameFiles = @(
    'DarkNights.csproj', 'data/balance.json', 'data/levels/pinewatch.json', 'assets/manifest.json',
    'scenes/levels/Pinewatch.tscn', 'src/Simulation/GameSession.cs', 'src/Simulation/State/InteractionState.cs',
    'src/Simulation/Commands/UnitOrders.cs', 'src/Simulation/Commands/ConstructionService.cs',
    'src/Simulation/Commands/TrainingService.cs', 'src/Content/JsonSettings.cs',
    'tests/fixtures/pinewatch-layout-v1.json', 'tests/fixtures/legacy-v1.json',
    'tests/fixtures/legacy-v1-after-20s.json', 'artifacts/architecture-validation.json',
    'artifacts/gameplay-validation.json', 'docs/VALIDATION.md'
)
$manifest = Get-Content -LiteralPath (Join-Path $gameRoot 'assets/manifest.json') -Raw | ConvertFrom-Json
$assetFailures = @(foreach ($entry in $manifest.files) {
    $path = Join-Path $gameRoot $entry.path
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { $entry.path; continue }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $entry.sha256) { $entry.path }
})
Push-Location -LiteralPath $frameworkRoot
try {
    $dllPaths = @(& rg --files Runtime Editor -g '*.dll' -g '!**/SourceGenerators~/**' | Sort-Object)
    if ($LASTEXITCODE -ne 0) { throw 'Generator DLL scan failed.' }
} finally { Pop-Location }
$dllMetaPaths = @($dllPaths | ForEach-Object { $_ + '.meta' })
$unityExecutable = 'D:\Program Files\Unity\Unity 6000.2.13f1\Editor\Unity.exe'
$unityObserved = if (Test-Path -LiteralPath $unityExecutable) {
    [ordered]@{ path = $unityExecutable; version = (Get-Item -LiteralPath $unityExecutable).VersionInfo.ProductVersion }
} else { $null }
$report = [ordered]@{
    schema_version = 1
    captured_at = [DateTimeOffset]::Now.ToString('o')
    scope = 'Read-only static assessment. Existing Godot test reports are historical evidence, not tests rerun by this script.'
    framework = Get-RepositoryState $frameworkRoot
    framework_package = Get-Content -LiteralPath (Join-Path $frameworkRoot 'package.json') -Raw | ConvertFrom-Json
    framework_code = Get-CodeInventory $frameworkRoot @('Runtime', 'Editor', 'Tests') 0
    framework_evidence = @(Get-FileEvidence $frameworkRoot $frameworkFiles)
    bundled_dlls = @(Get-FileEvidence $frameworkRoot $dllPaths)
    generator_import_settings = @(Get-FileEvidence $frameworkRoot $dllMetaPaths)
    game = Get-RepositoryState $gameRoot
    game_code = Get-CodeInventory $gameRoot @('src') 1
    game_evidence = @(Get-FileEvidence $gameRoot $gameFiles)
    original_assets = [ordered]@{
        files = @($manifest.files).Count
        bytes = [long]($manifest.files | Measure-Object bytes -Sum).Sum
        sprite_groups = @($manifest.sprites.PSObject.Properties).Count
        sounds = @($manifest.sounds.PSObject.Properties).Count
        sha256_verified = ($assetFailures.Count -eq 0)
        failures = $assetFailures
    }
    unity_observed = $unityObserved
    unity_inventory_is_exhaustive = $false
    unity_editor_import_run = $false
    unity_player_build_run = $false
    multiplayer_runtime_test_run = $false
}
$outputDirectory = Split-Path -Parent $reportPath
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$serializedReport = ($report | ConvertTo-Json -Depth 12).Replace("`r`n", "`n")
[IO.File]::WriteAllText($reportPath, $serializedReport + "`n", [Text.UTF8Encoding]::new($false))
Write-Output "Assessment written: $reportPath"
Write-Output "Original assets verified: $($report.original_assets.sha256_verified) ($($report.original_assets.files) files)"
