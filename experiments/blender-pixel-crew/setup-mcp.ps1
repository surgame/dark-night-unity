param(
    [string]$BlenderPath = 'D:\Program Files\Blender\blender.exe',
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
$expectedHash = 'F43469C8518C7021E0060E32CFE52E3BEB126B0F62FBAE7293106642A3EBDA89'
$uv = (Get-Command uv -ErrorAction Stop).Source
$codex = (Get-Command codex -ErrorAction Stop).Source
$toolRoot = (& $uv tool dir).Trim()
$toolBin = (& $uv tool dir --bin).Trim()
$runtime = Join-Path $toolRoot 'blender-mcp\Scripts\python.exe'
$server = Join-Path $toolBin 'blender-mcp.exe'
$installed = ''
if (Test-Path -LiteralPath $runtime) {
    $installed = (& $runtime -c "from importlib.metadata import version; print(version('blender-mcp'))").Trim()
}
if ($installed -ne '1.9.1') {
    if ($CheckOnly) { throw '缺少 blender-mcp 1.9.1。去掉 -CheckOnly 安装。' }
    $installArgs = @('tool', 'install', '--python', (Get-Command python).Source,
        '--constraints', (Join-Path $PSScriptRoot 'mcp-constraints.txt'), 'blender-mcp==1.9.1')
    if ($installed) { $installArgs += '--force' }
    & $uv @installArgs
    if ($LASTEXITCODE -ne 0) { throw 'MCP 包安装失败。' }
}
$versionText = (& $BlenderPath --version | Select-Object -First 1)
if ($versionText -notmatch 'Blender (\d+\.\d+)\.') { throw '无法读取 Blender 版本。' }
$blenderConfig = Join-Path $env:APPDATA ('Blender Foundation\Blender\' + $Matches[1])
$addonDirectory = Join-Path $blenderConfig 'scripts\addons'
$addonPath = Join-Path $addonDirectory 'blender_mcp.py'
$addonMatches = (Test-Path -LiteralPath $addonPath) -and
    ((Get-FileHash -LiteralPath $addonPath -Algorithm SHA256).Hash -eq $expectedHash)
if (!$addonMatches) {
    if ($CheckOnly) { throw 'Blender 插件缺失或哈希与锁定版本不同。' }
    $previousDestination = $env:BLENDERMCP_ADDONS_DIR
    try {
        $env:BLENDERMCP_ADDONS_DIR = $addonDirectory
        & $server install-addon
        if ($LASTEXITCODE -ne 0) { throw '插件复制失败。' }
    }
    finally { $env:BLENDERMCP_ADDONS_DIR = $previousDestination }
    if ((Get-FileHash -LiteralPath $addonPath).Hash -ne $expectedHash) {
        throw '插件 SHA-256 校验失败。'
    }
}
$codexDirectory = if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path $env:USERPROFILE '.codex' }
$configPath = Join-Path $codexDirectory 'config.toml'
$query = "import tomllib,json,sys; d=tomllib.load(open(sys.argv[1],'rb')).get('mcp_servers',{}).get('blender',{}); print(json.dumps({'command':d.get('command'),'host':d.get('env',{}).get('BLENDER_HOST'),'port':d.get('env',{}).get('BLENDER_PORT'),'telemetry':d.get('env',{}).get('DISABLE_TELEMETRY')}))"
$existing = (& $runtime -c $query $configPath) | ConvertFrom-Json
$matchesConfig = $existing.command -eq $server -and $existing.host -eq '127.0.0.1' -and
    $existing.port -eq '9876' -and $existing.telemetry -eq 'true'
if ($CheckOnly -and !$matchesConfig) { throw 'Codex 中的 blender 登记与本试验配置不同。' }
if (!$CheckOnly) {
    $backup = Join-Path $env:LOCALAPPDATA ('DarkNights\BlenderMCP\backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    Copy-Item -LiteralPath $configPath -Destination (Join-Path $backup 'codex-config.toml')
    $preferences = Join-Path $blenderConfig 'config\userpref.blend'
    if (Test-Path -LiteralPath $preferences) {
        Copy-Item -LiteralPath $preferences -Destination (Join-Path $backup 'userpref.blend')
    }
    if ($existing.command -and !$matchesConfig) {
        throw '已有不同的 blender MCP 登记；已保留配置，未替换。'
    }
    if (!$matchesConfig) {
        & $codex mcp add blender --env DISABLE_TELEMETRY=true --env BLENDER_HOST=127.0.0.1 --env BLENDER_PORT=9876 -- $server
        if ($LASTEXITCODE -ne 0) { throw 'Codex MCP 登记失败。' }
    }
    & $BlenderPath --background --python-exit-code 1 --python (Join-Path $PSScriptRoot 'scripts\enable_mcp.py')
    if ($LASTEXITCODE -ne 0) { throw 'Blender 插件启用失败。' }
}
[pscustomobject]@{
    Status='passed'; Package='blender-mcp==1.9.1'; Blender=$versionText
    AddonSha256=$expectedHash; Server=$server; Host='127.0.0.1'; Port=9876
    Telemetry=$false; CheckOnly=[bool]$CheckOnly
} | ConvertTo-Json -Compress
