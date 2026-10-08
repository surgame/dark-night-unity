param([string]$DependencyPath)
$ErrorActionPreference = "Stop"
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
$expected = [IO.Path]::GetFullPath((Join-Path $root ".deps/YYGC-grid-business"))
$target = if ($DependencyPath) { [IO.Path]::GetFullPath($DependencyPath) } else { $expected }
if (![string]::Equals($target, $expected, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Debug Hub 补丁仅能应用到本检出的锁定依赖，拒绝其他路径。"
}
$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot "dependency.lock.json") -Raw | ConvertFrom-Json
$patch = Join-Path $PSScriptRoot "yygc-debug-hub.patch"
if ((Get-FileHash -LiteralPath $patch -Algorithm SHA256).Hash.ToLowerInvariant() -ne $lock.patch_sha256) {
    throw "Debug Hub 补丁摘要不匹配。"
}
$basePatch = Join-Path $root "tools/grid-business/yygc.patch"
if ((Get-FileHash -LiteralPath $basePatch -Algorithm SHA256).Hash.ToLowerInvariant() -ne $lock.base_patch_sha256) {
    throw "前置网格业务补丁与 Debug Hub 锁不匹配。"
}
$head = (& git -C $target rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $head -ne $lock.base_commit) { throw "Debug Hub 框架基线不匹配。" }
& git -C $target apply --reverse --check $basePatch 2>$null
if ($LASTEXITCODE -ne 0) { throw "前置网格业务补丁未完整应用。" }
& git -C $target apply --reverse --check $patch 2>$null
if ($LASTEXITCODE -eq 0) { Write-Host "Debug Hub 补丁已应用；未启动 Unity。"; return }
& git -C $target apply --check $patch
if ($LASTEXITCODE -ne 0) { throw "Debug Hub 文件存在不匹配的修改，保留现场并拒绝覆盖。" }
& git -C $target apply $patch
if ($LASTEXITCODE -ne 0) { throw "Debug Hub 补丁应用失败，保留现场。" }
Write-Host "Debug Hub 补丁已准备；未启动 Unity、编译或测试。"
