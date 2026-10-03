param()
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskSource = Join-Path $taskRoot '.deps/YYGC-grid-business/Tests/IdRegistry/ObjectDefinitionWorkshopNavigationTests.cs'
$taskTarget = Join-Path $taskRoot 'Game/Assets/DarkNights/Scripts/Tests/YYGCWorkshopNavigationProbe.cs'
if (!(Test-Path -LiteralPath $taskSource)) { throw '先准备锁定 YYGC 依赖，再生成导航验收镜像。' }
if ((Test-Path -LiteralPath $taskTarget) -and (Get-FileHash -LiteralPath $taskTarget).Hash -ne (Get-FileHash -LiteralPath $taskSource).Hash) {
    throw '验收镜像有不同的本地修改，拒绝覆盖。'
}
Copy-Item -LiteralPath $taskSource -Destination $taskTarget
Write-Output '导航回归镜像已准备。Unity 导入后，在现有 DarkNights.Tests 程序集中运行 GameCore.Tests.IdRegistry.ObjectDefinitionWorkshopNavigationTests。'
Write-Output '验证结束后将镜像及 .meta、framework-fixtures.txt 列出的临时夹具归档到 artifacts/待清理，不永久删除。'
