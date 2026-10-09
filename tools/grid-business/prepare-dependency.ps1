param(
    [string]$FrameworkRepository = "D:/Developer/YYGC"
)
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
$target = [IO.Path]::GetFullPath((Join-Path $root ".deps/YYGC-grid-business"))
$overlay = Join-Path $root "tools/debug-hub/prepare-overlay.ps1"
$base = "fee18645c997ed7529c4592917de6c412033c84e"
$patch = Join-Path $PSScriptRoot "yygc.patch"
$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot "dependency.lock.json") -Raw | ConvertFrom-Json
if ($lock.base_commit -ne $base -or (Get-FileHash -LiteralPath $patch -Algorithm SHA256).Hash.ToLowerInvariant() -ne $lock.patch_sha256) {
    throw "依赖锁或源码补丁摘要不匹配，拒绝修改检出。"
}
if (!$target.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "依赖目标不在本工作区内。"
}
if (!(Test-Path -LiteralPath $target)) {
    & git -C $FrameworkRepository worktree add --detach $target $base
    if ($LASTEXITCODE -ne 0) { throw "无法准备锁定框架检出。原仓库不会被切换或清理。" }
}
$head = (& git -C $target rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $head -ne $base) { throw "已有依赖的基线不同，拒绝覆盖。" }
& git -C $target apply --reverse --check $patch 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "网格业务补丁已经应用；未编译、构建或启动 Unity。"
    if (Test-Path -LiteralPath $overlay) { & $overlay -DependencyPath $target }
    exit 0
}
$status = & git -C $target status --porcelain
if ($LASTEXITCODE -ne 0 -or $status) { throw "依赖包含不匹配的本地修改，拒绝重置或覆盖。" }
& git -C $target apply $patch
if ($LASTEXITCODE -ne 0) { throw "应用依赖补丁失败，保留现场。" }
Write-Host "网格业务依赖已准备：$base + yygc.patch。未编译、构建或启动 Unity。"
if (Test-Path -LiteralPath $overlay) { & $overlay -DependencyPath $target }
