param(
    [string]$BlenderPath = 'D:\Program Files\Blender\blender.exe',
    [string]$PythonPath = (Get-Command python -ErrorAction Stop).Source,
    [string]$Model = (Join-Path $PSScriptRoot 'model\pixel_crew.blend'),
    [string]$OutputPath,
    [string]$Presets = 'all',
    [string]$Actions = 'all',
    [string]$Directions = 'right,left',
    [string]$Sizes = '64',
    [string]$Frames = 'all',
    [string]$ComparisonSizes = '',
    [switch]$Initialize
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $BlenderPath)) { throw "Blender 不存在：$BlenderPath" }
if (!$OutputPath) { $OutputPath = Join-Path $PSScriptRoot ('renders\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')) }
$outputRoot = [System.IO.Path]::GetFullPath($OutputPath)
if ((Test-Path -LiteralPath $outputRoot) -and (Get-ChildItem -LiteralPath $outputRoot -Force)) {
    throw "目标非空，保留既有导出：$outputRoot"
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$scriptRoot = Join-Path $PSScriptRoot 'scripts'
$modelPath = [System.IO.Path]::GetFullPath($Model)
if ($Initialize) {
    $initArgs = @('--background', '--factory-startup', '--python-exit-code', '1',
        '--python', (Join-Path $scriptRoot 'build_scene.py'), '--', '--output', $modelPath)
    & $BlenderPath @initArgs *> (Join-Path $outputRoot 'initialize.log')
    if ($LASTEXITCODE -ne 0) { throw "初始化失败，见 $outputRoot\initialize.log" }
}
if (!(Test-Path -LiteralPath $modelPath)) { throw "模型不存在：$modelPath" }
$raw = Join-Path $outputRoot 'raw'
$pixels = Join-Path $outputRoot 'pixels'
$renderArgs = @('--background', $modelPath, '--python-exit-code', '1',
    '--python', (Join-Path $scriptRoot 'render_frames.py'), '--', '--output', $raw,
    '--presets', $Presets, '--actions', $Actions, '--directions', $Directions,
    '--sizes', $Sizes, '--frames', $Frames)
if ($ComparisonSizes) { $renderArgs += @('--comparison-sizes', $ComparisonSizes) }
& $BlenderPath @renderArgs *> (Join-Path $outputRoot 'render.log')
if ($LASTEXITCODE -ne 0) { throw "渲染失败，见 $outputRoot\render.log" }
& $PythonPath (Join-Path $scriptRoot 'pixel_finish.py') --input $raw --output $pixels
if ($LASTEXITCODE -ne 0) { throw "像素检查失败，见 $pixels\verification.json" }
& $PythonPath (Join-Path $scriptRoot 'gallery.py') --input $pixels
if ($LASTEXITCODE -ne 0) { throw "预览生成失败：$pixels" }
$report = Get-Content -LiteralPath (Join-Path $pixels 'verification.json') -Raw | ConvertFrom-Json
[pscustomobject]@{
    Status = $report.status
    Frames = $report.frame_count
    Clips = $report.clip_count
    Model = $modelPath
    Output = $outputRoot
    Preview = Join-Path $pixels 'index.html'
    ArtAcceptance = '未通过；仅作为技术管线样机'
} | ConvertTo-Json -Compress
