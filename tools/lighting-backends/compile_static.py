"""Finite lighting checks using read-only Local compiler inputs; never starts Unity."""
import argparse
import ctypes
import importlib.util
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import time

sys.dont_write_bytecode = True
parser = argparse.ArgumentParser()
parser.add_argument('--local', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--editor-pid', type=int, required=True)
parser.add_argument('--variant', choices=['editor', 'development', 'release'], default='editor')
parser.add_argument('--reuse-from', type=Path)
parser.add_argument('--architecture-scope', choices=['full', 'affected'], default='affected')
parser.add_argument('--skip-profile', action='store_true')
parser.add_argument('--skip-architecture', action='store_true')
args = parser.parse_args()
repo = Path(__file__).resolve().parents[2]
local = args.local.resolve()
output = args.output.resolve()
if not output.is_relative_to(local / 'artifacts') or output.exists():
    raise SystemExit('Use a fresh Local artifacts directory')
output.mkdir(parents=True)
spec = importlib.util.spec_from_file_location('dn_memory_shapes', local / 'tools/ground-baseline/watch_memory.py')
memory_types = importlib.util.module_from_spec(spec)
spec.loader.exec_module(memory_types)
psapi = ctypes.WinDLL('psapi', use_last_error=True)
kernel = ctypes.WinDLL('kernel32', use_last_error=True)
kernel.OpenProcess.restype = ctypes.c_void_p
kernel.OpenProcess.argtypes = [ctypes.c_ulong, ctypes.c_int, ctypes.c_ulong]
psapi.GetPerformanceInfo.argtypes = [ctypes.POINTER(memory_types.Performance), ctypes.c_ulong]
psapi.GetProcessMemoryInfo.argtypes = [ctypes.c_void_p, ctypes.POINTER(memory_types.ProcessMemory), ctypes.c_ulong]
kernel.CloseHandle.argtypes = [ctypes.c_void_p]
handle = kernel.OpenProcess(0x410, False, args.editor_pid)
if not handle:
    raise SystemExit('Editor PID no longer exists')
report = {'variant': args.variant, 'scope': 'static C#; no Unity import, Play, shaders or Player', 'checks': [], 'memory': []}
report['sources'] = {str(path.relative_to(repo)): hashlib.sha256(path.read_bytes()).hexdigest()
                     for path in (repo / 'Game/Assets/DarkNights/Scripts').rglob('*.cs')}


def sample():
    performance = memory_types.Performance()
    editor = memory_types.ProcessMemory()
    if not psapi.GetPerformanceInfo(ctypes.byref(performance), ctypes.sizeof(performance)) or not psapi.GetProcessMemoryInfo(handle, ctypes.byref(editor), ctypes.sizeof(editor)):
        raise RuntimeError('Memory gate unavailable')
    gib = 1024 ** 3
    value = {'commit_gib': (performance.limit - performance.commit) * performance.page_size / gib,
             'available_gib': performance.available * performance.page_size / gib, 'editor_private_gib': editor.private / gib}
    report['memory'].append(value)
    return value


def save():
    (output / 'result.json').write_text(json.dumps(report, indent=2), encoding='utf-8')


def run(command, name, reserve=.3):
    before = sample()
    if before['commit_gib'] < 4 + reserve or before['available_gib'] < 6 or before['editor_private_gib'] > 8:
        report['stopped'] = 'memory gate before ' + name
        save()
        raise SystemExit(2)
    with (output / (name + '.log')).open('w', encoding='utf-8') as log:
        process = subprocess.Popen(command, cwd=local / 'Game', stdout=log, stderr=subprocess.STDOUT)
        while process.poll() is None:
            state = sample()
            if state['commit_gib'] < 4 or state['available_gib'] < 6 or state['editor_private_gib'] > 8:
                process.terminate()
                process.wait(timeout=10)
                report['stopped'] = 'memory gate during ' + name
                save()
                raise SystemExit(2)
            time.sleep(.25)
    text = (output / (name + '.log')).read_text(encoding='utf-8')
    entry = {'name': name, 'exit_code': process.returncode, 'errors': text.count('error CS'), 'warnings': text.count('warning CS')}
    report['checks'].append(entry)
    save()
    print(name, process.returncode, entry['errors'], entry['warnings'], flush=True)
    if process.returncode:
        print('\n'.join(line for line in text.splitlines() if 'error ' in line)[:6000], flush=True)
        raise SystemExit(process.returncode)


try:
    sdk = subprocess.check_output(['dotnet', '--version'], text=True).strip()
    csc = Path('C:/Program Files/dotnet/sdk') / sdk / 'Roslyn/bincore/csc.dll'
    pure = output / 'LightingProfileProbe.dll'
    refs = Path('C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/8.0.16/ref/net8.0')
    pure_lines = ['-target:exe', '-langversion:9.0', '-out:"' + str(pure) + '"']
    pure_lines += ['-r:"' + str(path) + '"' for path in refs.glob('*.dll')]
    pure_lines += ['"' + str(path) + '"' for path in [repo / 'tools/lighting-backends/ProfileProbe.cs',
        repo / 'Game/Assets/DarkNights/Scripts/Core/Config/LightEmissionRules.cs',
        repo / 'Game/Assets/DarkNights/Scripts/Core/Logic/Lighting/LightBeamProfile.cs']]
    pure_rsp = output / 'profile.rsp'
    pure_rsp.write_text('\n'.join(pure_lines), encoding='utf-8')
    if not args.reuse_from and not args.skip_profile: run(['dotnet', str(csc), '@' + str(pure_rsp)], 'profile-compile', .2)
    pure.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
        'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': '8.0.16'}}}), encoding='utf-8')
    if not args.reuse_from and not args.skip_profile: run(['dotnet', str(pure)], 'profile-run', .1)
    names = ['DarkNights.' + name for name in ['Core', 'Runtime', 'View', 'Entry']]
    if args.variant == 'editor': names += ['DarkNights.Editor', 'DarkNights.Tests']
    for name in names:
        if args.reuse_from and name in ['DarkNights.Core', 'DarkNights.Runtime']:
            import shutil
            for suffix in ['.dll', '.ref.dll']:
                shutil.copyfile(args.reuse_from / (name + suffix), output / (name + suffix))
            report['checks'].append({'name': name, 'reused_from': str(args.reuse_from), 'exit_code': 0})
            continue
        original = local / 'Game/Library/Bee/artifacts/1900b0aE.dag' / (name + '.rsp')
        lines = []
        for line in original.read_text(encoding='utf-8-sig').splitlines():
            if line.startswith('"') and line.endswith('.cs"'): continue
            if line.startswith('-define:') and args.variant != 'editor':
                flag = line.split(':', 1)[1]
                if flag.startswith('UNITY_EDITOR') or flag == 'UNITY_INCLUDE_TESTS': continue
            if line.startswith(('-out:', '-refout:')):
                suffix = '.ref.dll' if line.startswith('-refout:') else '.dll'
                line = line.split(':', 1)[0] + ':"' + str(output / (name + suffix)) + '"'
            for prefix in ('-r:', '-analyzer:', '/additionalfile:'):
                if not line.startswith(prefix): continue
                path = Path(line[len(prefix):].strip('"'))
                if not path.is_absolute(): path = local / 'Game' / path
                assembly = path.stem.removesuffix('.ref')
                if prefix == '-r:' and assembly in names: path = output / path.name
                line = prefix + '"' + str(path.resolve()) + '"'
            if 'generatedfilesout:' in line or line.startswith(('-doc:', '/doc:')):
                raise RuntimeError('Unexpected compiler output path')
            lines.append(line)
        if name in ['DarkNights.View', 'DarkNights.Editor', 'DarkNights.Tests']:
            lines.append('-r:"' + str(local / 'Game/Library/ScriptAssemblies/Unity.RenderPipelines.Universal.2D.Runtime.dll') + '"')
        if args.variant == 'development': lines.append('-define:DEVELOPMENT_BUILD')
        lines += ['"' + str(path) + '"' for path in sorted((repo / 'Game/Assets/DarkNights/Scripts' / name.split('.')[-1]).rglob('*.cs'))]
        rsp = output / (name + '.rsp')
        rsp.write_text('\n'.join(lines), encoding='utf-8')
        run(['dotnet', str(csc), '@' + str(rsp)], name)
    guard = local / 'tools/ArchitectureGuard/bin/Debug/net8.0/ArchitectureGuard.dll'
    if args.skip_architecture:
        report['passed'] = True
        save()
        raise SystemExit(0)
    guard_root = repo
    if args.architecture_scope == 'affected':
        import shutil
        changed = subprocess.check_output(['git', 'diff', '--name-only', 'HEAD', '--', 'Game/Assets/DarkNights/Scripts'], cwd=repo, text=True).splitlines()
        changed += subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard', 'Game/Assets/DarkNights/Scripts'], cwd=repo, text=True).splitlines()
        guard_root = output / 'architecture-input'
        for relative in sorted(set(changed)):
            if not relative.endswith(('.cs', '.asmdef')): continue
            target = guard_root / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(repo / relative, target)
        report['architecture_scope'] = 'Affected handwritten C# and asmdefs; unchanged source not rechecked'
    run(['dotnet', str(guard), str(guard_root), str(output / 'architecture.json')], 'architecture',
        1.0 if args.architecture_scope == 'full' else .3)
    report['passed'] = True
    save()
finally:
    kernel.CloseHandle(handle)
