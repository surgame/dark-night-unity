param(
    [string]$BlenderPath = 'D:\Program Files\Blender\blender.exe',
    [string]$Model = (Join-Path $PSScriptRoot 'model\pixel_crew.blend')
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $BlenderPath)) { throw "Blender 不存在：$BlenderPath" }
$modelPath = (Resolve-Path -LiteralPath $Model).Path
$logRoot = Join-Path $PSScriptRoot 'local'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$listener = Get-NetTCPConnection -LocalPort 9876 -State Listen -ErrorAction SilentlyContinue
if ($listener) {
    throw '9876 已有 Blender MCP 实例。请在现有窗口打开模型，避免启动互相竞争的实例。'
}
$arguments = @('"' + $modelPath + '"', '--python',
    '"' + (Join-Path $PSScriptRoot 'scripts\start_mcp.py') + '"')
$previousTelemetry = $env:DISABLE_TELEMETRY
try {
    $env:DISABLE_TELEMETRY = 'true'
    $startOptions = @{
        FilePath = $BlenderPath
        ArgumentList = $arguments
        WorkingDirectory = $PSScriptRoot
        WindowStyle = 'Normal'
        PassThru = $true
        RedirectStandardOutput = Join-Path $logRoot "blender-$stamp.log"
        RedirectStandardError = Join-Path $logRoot "blender-$stamp-error.log"
    }
    $process = Start-Process @startOptions
    [pscustomobject]@{ Status='started'; ProcessId=$process.Id; Model=$modelPath; McpPort=9876 } |
        ConvertTo-Json -Compress
}
finally {
    $env:DISABLE_TELEMETRY = $previousTelemetry
}
