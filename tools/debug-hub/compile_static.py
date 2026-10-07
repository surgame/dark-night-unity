"""Compile the isolated candidate with frozen, read-only Local Unity references.

No Unity process, import, test runner or Player is started. Every output stays
inside a new candidate artifacts directory; Local source and cache are not written.
"""
import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--local', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--variant', choices=['editor', 'development', 'release'], default='editor')
args = parser.parse_args()
repo = Path(__file__).resolve().parents[2]
local = args.local.resolve()
output = args.output.resolve()
if not output.is_relative_to(repo / 'artifacts') or output.exists():
    raise SystemExit('Use a new directory inside candidate artifacts')
output.mkdir(parents=True)
sdk = subprocess.check_output(['dotnet', '--version'], text=True).strip()
csc = Path('C:/Program Files/dotnet/sdk') / sdk / 'Roslyn/bincore/csc.dll'
rsp_dir = local / 'Game/Library/Bee/artifacts/1900b0aE.dag'
names = ['GameCore.Runtime'] + ['DarkNights.' + n for n in ['Core', 'Runtime', 'View', 'Entry']]
if args.variant == 'editor':
    names += ['DarkNights.Editor', 'DarkNights.Tests']
report = {'variant': args.variant, 'compiler_sdk': sdk, 'scope': 'static C# only; no Unity import or tests',
          'head': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=repo, text=True).strip(),
          'assemblies': [], 'references': {}}
frozen = {}


def freeze(path):
    path = path.resolve()
    if path in frozen:
        return frozen[path]
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    target = output / 'references' / digest / path.name
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(path, target)
    frozen[path] = target
    report['references'][str(path)] = digest
    return target


prepared = []
for name in names:
    original_path = rsp_dir / (name + '.rsp')
    original = original_path.read_text(encoding='utf-8-sig')
    lines, sources = [], []
    for line in original.splitlines():
        if line.startswith('"') and line.endswith('.cs"'):
            if name == 'GameCore.Runtime':
                path = Path(line.strip('"'))
                relative = path.resolve().relative_to(local / '.deps/YYGC-grid-business')
                sources.append(repo / '.deps/YYGC-grid-business' / relative)
            continue
        if line.startswith('-define:') and args.variant != 'editor':
            flag = line.split(':', 1)[1]
            if flag.startswith('UNITY_EDITOR') or flag == 'UNITY_INCLUDE_TESTS':
                continue
        if line.startswith(('-out:', '-refout:')):
            ref = '.ref' if line.startswith('-refout:') else ''
            line = line.split(':', 1)[0] + ':"' + str(output / (name + ref + '.dll')) + '"'
        for prefix in ('-r:', '-analyzer:', '/additionalfile:'):
            if not line.startswith(prefix):
                continue
            path = Path(line[len(prefix):].strip('"'))
            if not path.is_absolute():
                path = local / 'Game' / path
            assembly = path.stem.removesuffix('.ref')
            if prefix == '-r:' and assembly in names:
                path = output / path.name
            elif prefix != '-analyzer:':
                path = freeze(path)
            line = prefix + '"' + str(path.resolve()) + '"'
        if 'generatedfilesout:' in line or line.startswith(('-doc:', '/doc:')):
            raise SystemExit('Unrecognized compiler write path: ' + line)
        lines.append(line)
    if name == 'GameCore.Runtime':
        sources = sorted(set(sources) | set((repo / '.deps/YYGC-grid-business/Runtime/Debugging').glob('*.cs')))
    else:
        sources = sorted((repo / 'Game/Assets/DarkNights/Scripts' / name.split('.')[-1]).rglob('*.cs'))
    if args.variant == 'development':
        lines.append('-define:DEVELOPMENT_BUILD')
    lines += ['"' + str(path) + '"' for path in sources]
    rsp = output / (name + '.rsp')
    rsp.write_text('\n'.join(lines), encoding='utf-8')
    entry = {'name': name, 'sources': len(sources),
             'source_sha256': {str(p.relative_to(repo)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sources},
             'original_rsp_sha256': hashlib.sha256(original.encode()).hexdigest()}
    prepared.append((rsp, entry))

for rsp, entry in prepared:
    run = subprocess.run(['dotnet', str(csc), '@' + str(rsp)], cwd=local / 'Game',
                         capture_output=True, text=True, encoding='utf-8', errors='replace')
    log = run.stdout + run.stderr
    (output / (entry['name'] + '.log')).write_text(log, encoding='utf-8')
    entry.update(exit_code=run.returncode, errors=log.count('error CS'), warnings=log.count('warning CS'))
    report['assemblies'].append(entry)
    (output / 'compile.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(entry['name'], run.returncode, 'errors', entry['errors'], 'warnings', entry['warnings'], flush=True)
    if run.returncode:
        print('\n'.join(line for line in log.splitlines() if 'error ' in line)[:10000], flush=True)
        raise SystemExit(run.returncode)
