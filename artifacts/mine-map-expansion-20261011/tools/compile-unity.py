"""以 Local 已导入的只读程序集核对薄 worktree；所有编译输出必须位于显式指定的 Local artifacts。"""
import argparse
import json
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--local', required=True, type=Path)
parser.add_argument('--output', required=True, type=Path)
args = parser.parse_args()
repo = Path(__file__).resolve().parents[3]
local = args.local.resolve()
out = args.output.resolve()
if out.exists() or not out.is_relative_to(local / 'artifacts'):
    raise SystemExit('Use a fresh Local artifacts output directory')
out.mkdir(parents=True)
sdk = subprocess.check_output(['dotnet', '--version'], text=True).strip()
csc = Path('C:/Program Files/dotnet/sdk') / sdk / 'Roslyn/bincore/csc.dll'
names = ['DarkNights.' + name for name in ['Core', 'Runtime', 'View', 'Entry', 'Editor', 'Tests']]
report = {'scope': 'Read-only Local C# API check; no Editor import, Play, shader or Player verdict', 'checks': []}
for name in names:
    sources = list((local / 'Game/Library/Bee/artifacts').glob('*/' + name + '.rsp'))
    if not sources:
        raise RuntimeError('Missing imported compiler input: ' + name)
    original = max(sources, key=lambda p: p.stat().st_mtime)
    lines = []
    for line in original.read_text(encoding='utf-8-sig').splitlines():
        if line.startswith('"') and line.endswith('.cs"'):
            continue
        if line.startswith(('-out:', '-refout:')):
            suffix = '.ref.dll' if line.startswith('-refout:') else '.dll'
            line = line.split(':', 1)[0] + ':"' + str(out / (name + suffix)) + '"'
        for prefix in ('-r:', '-analyzer:', '/additionalfile:'):
            if not line.startswith(prefix):
                continue
            candidate = Path(line[len(prefix):].strip('"'))
            if not candidate.is_absolute():
                candidate = local / 'Game' / candidate
            if prefix == '-r:' and candidate.stem.removesuffix('.ref') in names:
                candidate = out / candidate.name
            line = prefix + '"' + str(candidate.resolve()) + '"'
        if 'generatedfilesout:' in line or line.startswith(('-doc:', '/doc:')):
            raise RuntimeError('Unexpected compiler output path')
        lines.append(line)
    lines += ['"' + str(p) + '"' for p in sorted((repo / 'Game/Assets/DarkNights/Scripts' / name.split('.')[-1]).rglob('*.cs'))]
    rsp = out / (name + '.rsp')
    rsp.write_text('\n'.join(lines), encoding='utf-8')
    result = subprocess.run(['dotnet', str(csc), '@' + str(rsp)], cwd=local / 'Game',
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding='utf-8', errors='replace')
    (out / (name + '.log')).write_text(result.stdout, encoding='utf-8')
    errors = list(dict.fromkeys(line for line in result.stdout.splitlines() if 'error ' in line))
    report['checks'].append({'name': name, 'exitCode': result.returncode, 'errors': errors,
        'warnings': len([line for line in result.stdout.splitlines() if 'warning ' in line])})
    (out / 'result.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(name, result.returncode, '\n'.join(errors[:10]), flush=True)
    if result.returncode:
        raise SystemExit(result.returncode)
