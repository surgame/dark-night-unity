"""Compile candidate C# and pure regressions using read-only Local references.

Does not import Unity, write Local, or substitute for Editor/Player acceptance.
Artifacts remain in the candidate repository for later inventory and archival.
"""
import argparse
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--local', type=Path, required=True)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
repo = Path(__file__).resolve().parents[2]
local = a.local.resolve()
out = a.output.resolve()
if not out.is_relative_to(repo / 'artifacts') or out.exists():
    raise SystemExit('Use a new candidate artifacts directory')
out.mkdir(parents=True)
sdk = subprocess.check_output(['dotnet', '--version'], text=True).strip()
csc = Path('C:/Program Files/dotnet/sdk') / sdk / 'Roslyn/bincore/csc.dll'
rspdir = local / 'Game/Library/Bee/artifacts/1900b0aE.dag'
names = ['Core', 'Runtime', 'View', 'Entry', 'Editor', 'Tests']
result = {'candidate': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=repo, text=True).strip(),
          'scope': 'read-only Local compiler references; no Unity import or runtime', 'assemblies': []}
for name in names:
    assembly = 'DarkNights.' + name
    lines = []
    original = (rspdir / (assembly + '.rsp')).read_text(encoding='utf-8-sig')
    result['assemblies'].append({'name': assembly,
                                'rsp_sha256': hashlib.sha256(original.encode()).hexdigest()})
    for line in original.splitlines():
        if line.startswith('"Assets/') and line.endswith('.cs"'):
            continue
        if line.startswith('-out:') or line.startswith('-refout:'):
            ref = '.ref' if line.startswith('-refout:') else ''
            line = line.split(':', 1)[0] + ':"' + str(out / (assembly + ref + '.dll')) + '"'
        for prefix in ('-r:', '-analyzer:', '/additionalfile:'):
            if line.startswith(prefix):
                path = Path(line[len(prefix):].strip('"'))
                if not path.is_absolute():
                    path = local / 'Game' / path
                if prefix == '-r:' and path.stem.removesuffix('.ref') in ['DarkNights.' + n for n in names]:
                    path = out / path.name
                line = prefix + '"' + str(path.resolve()) + '"'
        lines.append(line)
    sources = sorted((repo / 'Game/Assets/DarkNights/Scripts' / name).rglob('*.cs'))
    lines += ['"' + str(s) + '"' for s in sources]
    rsp = out / (assembly + '.rsp')
    rsp.write_text('\n'.join(lines), encoding='utf-8')
    run = subprocess.run(['dotnet', str(csc), '@' + str(rsp)], cwd=local / 'Game',
                         capture_output=True, text=True, encoding='utf-8', errors='replace')
    (out / (assembly + '.log')).write_text(run.stdout + run.stderr, encoding='utf-8')
    result['assemblies'][-1].update(exit_code=run.returncode, sources=len(sources))
    (out / 'compile.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(assembly, run.returncode, flush=True)
    if run.returncode:
        raise SystemExit(run.returncode)

pure = out / 'pure'
projects = ['CoreBuild', 'CoreRegression', 'PlanetFlowRegression']
newtonsoft = next((local / 'Game/Library/PackageCache').glob('com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll'))
for name in projects:
    source = repo / 'tools' / name
    tree = ET.parse(source / (name + '.csproj'))
    root = tree.getroot()
    props = root.find('PropertyGroup')
    default = props.find('EnableDefaultCompileItems')
    if default is None:
        ET.SubElement(props, 'EnableDefaultCompileItems').text = 'false'
        items = ET.SubElement(root, 'ItemGroup')
        ET.SubElement(items, 'Compile', Include=str(source / '*.cs'))
    for item in root.findall('.//Compile'):
        path = Path(item.attrib['Include'])
        if not path.is_absolute(): item.set('Include', str(source / path))
    for item in root.findall('.//ProjectReference'):
        ref = Path(item.attrib['Include']).stem
        item.set('Include', str(pure / ref / (ref + '.csproj')))
    for item in root.findall('.//HintPath'):
        item.text = str(newtonsoft)
    target = pure / name / (name + '.csproj')
    target.parent.mkdir(parents=True)
    tree.write(target, encoding='utf-8', xml_declaration=True)
run = subprocess.run(['dotnet', 'build', str(pure / 'PlanetFlowRegression/PlanetFlowRegression.csproj'),
                      '--nologo', '-v:q'], capture_output=True, text=True, encoding='utf-8', errors='replace')
(out / 'pure-build.log').write_text(run.stdout + run.stderr, encoding='utf-8')
result['pure_build_exit'] = run.returncode
(out / 'compile.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print('Pure runner build', run.returncode, flush=True)
raise SystemExit(run.returncode)
