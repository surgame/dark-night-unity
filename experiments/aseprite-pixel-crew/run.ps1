param(
    [string]$AsepritePath = 'D:\Program Files (x86)\Steam\steamapps\common\Aseprite\Aseprite.exe',
    [string]$PythonPath = (Get-Command python -ErrorAction Stop).Source,
    [string]$Model = (Join-Path $PSScriptRoot 'model\worker.aseprite'),
    [string]$OutputPath,
    [switch]$Initialize
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $AsepritePath)) { throw "Aseprite 不存在：$AsepritePath" }
if (!$OutputPath) { $OutputPath=Join-Path $PSScriptRoot ('exports\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')) }
$outputRoot = [System.IO.Path]::GetFullPath($OutputPath)
if ((Test-Path -LiteralPath $outputRoot) -and (Get-ChildItem -LiteralPath $outputRoot -Force)) {
    throw "输出目录非空，保留已导出的内容：$outputRoot"
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$scripts = Join-Path $PSScriptRoot 'scripts'
$modelPath = [System.IO.Path]::GetFullPath($Model)

function Invoke-AsepriteBatch([string[]]$Arguments,[string]$Stage) {
    $quoted = @($Arguments | ForEach-Object {
        if ($_.Contains('"')) { throw '命令参数中不能包含双引号。' }
        '"' + $_ + '"'
    })
    $options = @{
        FilePath=$AsepritePath
        ArgumentList=$quoted
        WindowStyle='Hidden'
        Wait=$true
        PassThru=$true
        RedirectStandardOutput=Join-Path $outputRoot ($Stage+'.log')
        RedirectStandardError=Join-Path $outputRoot ($Stage+'-error.log')
    }
    $process = Start-Process @options
    if ($process.ExitCode -ne 0) { throw "$Stage 失败（$($process.ExitCode)）；见 $outputRoot 下的日志。" }
}

if ($Initialize) {
    $modelFolder=Split-Path -Parent $modelPath
    if ((Test-Path -LiteralPath $modelFolder) -and (Get-ChildItem -LiteralPath $modelFolder -Force)) {
        throw '初始化模型目标目录必须为空，以保护人工编辑。'
    }
    New-Item -ItemType Directory -Path $modelFolder -Force | Out-Null
    $initArgs=@('--batch','--script-param',('source_root='+$scripts),
        '--script-param',('output='+$modelPath))
    $initArgs+=@('--script',(Join-Path $scripts 'build_sprite.lua'))
    Invoke-AsepriteBatch $initArgs 'initialize'
}
if (!(Test-Path -LiteralPath $modelPath)) { throw "模型不存在：$modelPath" }
$before=(Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash
New-Item -ItemType Directory -Path (Join-Path $outputRoot 'frames') -Force | Out-Null
$exportArgs=@('--batch','--list-tags','--list-layers','--list-slices',
    $modelPath,'--sheet-type','rows','--sheet-columns','8',
    '--format','json-array',
    '--sheet',(Join-Path $outputRoot 'sheet.png'),'--data',(Join-Path $outputRoot 'sheet.json'),
    '--save-as',(Join-Path $outputRoot 'frames\worker00.png'))
Invoke-AsepriteBatch $exportArgs 'export'
$partArgs=@('--batch','--script-param',('input='+$modelPath),
    '--script-param',('output='+$outputRoot),
    '--script',(Join-Path $scripts 'export_parts.lua'))
Invoke-AsepriteBatch $partArgs 'parts'
$after=(Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash
if ($before -ne $after) { throw '导出意外修改了 Aseprite 源文件。' }
& $PythonPath (Join-Path $scripts 'package_preview.py') --input $outputRoot --model $modelPath
if ($LASTEXITCODE -ne 0) { throw "预览检查失败：$outputRoot\verification.json" }
[pscustomobject]@{Status='passed'; Model=$modelPath; Preview=(Join-Path $outputRoot 'index.html'); SourceSha256=$before} |
    ConvertTo-Json -Compress
