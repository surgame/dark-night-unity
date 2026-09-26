param([string]$FrameworkPath = 'D:/Developer/YYGC')
$ErrorActionPreference = 'Stop'
$framework = (Resolve-Path -LiteralPath $FrameworkPath).Path
$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'source-lock.json') -Raw | ConvertFrom-Json
$patch = Join-Path $PSScriptRoot 'YYGCStartup.patch'
if ((Get-FileHash -LiteralPath $patch -Algorithm SHA256).Hash -ne $lock.patchSha256) {
    throw '启动补丁哈希不匹配，停止应用。'
}
$before = 0
$after = 0
foreach ($file in $lock.files) {
    $path = Join-Path $framework $file.path
    # Git 的源码锁按 LF 计算，允许正常的 Windows checkout CRLF，不接受源码差异。
    $source = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($source)))
    if ($hash -eq $file.beforeSha256) { $before++ }
    elseif ($hash -eq $file.afterSha256) { $after++ }
    else { throw "源码包含其他改动，保留并停止：$path" }
}
if ($after -eq $lock.files.Count) { Write-Output '启动修复已应用，无需重复写入。'; exit 0 }
if ($before -ne $lock.files.Count) { throw '检测到部分应用状态，保留现场并停止。' }
git -C $framework apply --check $patch
if ($LASTEXITCODE -ne 0) { throw '启动补丁上下文检查失败。' }
git -C $framework apply $patch
if ($LASTEXITCODE -ne 0) { throw '启动补丁应用失败。' }
Write-Output '已应用启动修复。请在唯一 Unity 验证通道统一导入，由正式生成器重建注册源码。'
