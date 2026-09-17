param([string]$PlayerPath = '', [string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (!$PlayerPath) { $PlayerPath = Join-Path $repo 'artifacts/terrain-debug/player-mono/TerrainDebug.exe' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo 'artifacts/terrain-debug/player-smoke' }
$player = [IO.Path]::GetFullPath($PlayerPath)
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $output | Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class TerrainDebugWindowCapture {
    [StructLayout(LayoutKind.Sequential)] public struct Bounds { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr window, out Bounds bounds);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr window, IntPtr target, uint flags);
    private delegate bool Visit(IntPtr window, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Visit visit, IntPtr state);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    public static IntPtr Find(int process) {
        IntPtr result = IntPtr.Zero;
        EnumWindows((window, state) => {
            uint owner; GetWindowThreadProcessId(window, out owner);
            Bounds bounds;
            if (owner == process && GetClientRect(window, out bounds) && bounds.Right > 100 && bounds.Bottom > 100) {
                result = window; return false;
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
'@
$process = $null
$failure = $null
$checks = [ordered]@{}
try {
    $log = Join-Path $output 'player.log'
    $arguments = @('-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
        '-logFile', ('"' + $log + '"'))
    $process = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 12
    $process.Refresh()
    $checks.processAlive = !$process.HasExited
    if ($process.HasExited) { throw 'Debug Player exited before inspection.' }
    $window = [TerrainDebugWindowCapture]::Find($process.Id)
    $checks.nativeWindow = $window -ne [IntPtr]::Zero
    if (!$checks.nativeWindow) { throw 'Debug Player did not create its native window.' }
    $bounds = [TerrainDebugWindowCapture+Bounds]::new()
    $null = [TerrainDebugWindowCapture]::GetClientRect($window, [ref]$bounds)
    $bitmap = [Drawing.Bitmap]::new($bounds.Right, $bounds.Bottom)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $dc = $graphics.GetHdc()
    try { $checks.capture = [TerrainDebugWindowCapture]::PrintWindow($window, $dc, 3) }
    finally { $graphics.ReleaseHdc($dc); $graphics.Dispose() }
    try { $bitmap.Save((Join-Path $output 'player.png'), [Drawing.Imaging.ImageFormat]::Png) }
    finally { $bitmap.Dispose() }
    $content = Get-Content -LiteralPath $log -Raw
    $checks.noRuntimeErrors = $content -notmatch '(?im)(Exception:|Shader error|Unable to load font face|NullReferenceException|MissingReferenceException)'
    if (@($checks.Values | Where-Object { !$_ }).Count) { throw 'Player smoke checks failed.' }
}
catch { $failure = $_.Exception.ToString() }
finally {
    if ($process) { $process.Refresh(); if (!$process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit() } }
    [ordered]@{ passed = !$failure; checks = $checks; error = $failure; player = $player;
        scope = 'Independent Mono startup and captured native client area; image content requires visual review.' } |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'result.json') -Encoding utf8
}
if ($failure) { throw $failure }
Get-Content (Join-Path $output 'result.json')
