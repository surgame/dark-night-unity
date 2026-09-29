param([string]$FrameworkPath = 'D:\Developer\YYGC')
$ErrorActionPreference = 'Stop'
$frameworkRoot = [IO.Path]::GetFullPath($FrameworkPath)
$source = Join-Path $frameworkRoot 'Runtime/Debugging/RuntimeDebugHub.cs'
$patch = Join-Path $PSScriptRoot 'F1Only.patch'
$expectedPatch = 'D7E4B52E873A4C48FC047B10E2AC0FD7D8C7B61F73A98624BA256F678B49FA7F'
if ((Get-FileHash -LiteralPath $patch -Algorithm SHA256).Hash -ne $expectedPatch) {
    throw 'F1Only.patch 内容与本批锁定不一致。'
}
$current = [IO.File]::ReadAllText($source).Replace("`r`n", "`n")
$original = (git -C $frameworkRoot show 'fee18645c997ed7529c4592917de6c412033c84e:Runtime/Debugging/RuntimeDebugHub.cs') -join "`n"
if ($LASTEXITCODE) { throw '找不到锁定的 YYGC 基线 fee1864。' }
$candidate = $original.Replace("if (keyboard != null &&`n                (keyboard.backquoteKey.wasPressedThisFrame || keyboard.f1Key.wasPressedThisFrame))", 'if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)')
$candidate = $candidate.Replace('Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.F1)', 'Input.GetKeyDown(KeyCode.F1)')
$candidate = $candidate.Replace('Runtime Debug Hub [ ` / F1 ]', 'Runtime Debug Hub [ F1 ]')
if ($current.TrimEnd() -eq $candidate.TrimEnd()) { Write-Output 'F1_ONLY_ALREADY_APPLIED'; return }
if ($current.TrimEnd() -ne $original.TrimEnd()) { throw 'YYGC 调试 Hub 存在未知修改，保留文件并停止。' }
git -C $frameworkRoot apply --check -- $patch
if ($LASTEXITCODE) { throw '补丁与当前 YYGC 文件不匹配。' }
git -C $frameworkRoot apply -- $patch
if ($LASTEXITCODE) { throw '应用 F1 补丁失败。' }
Write-Output 'F1_ONLY_APPLIED'
