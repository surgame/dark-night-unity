"""Run existing Core and architecture checks with every generated file outside the thin worktree."""
import argparse
import json
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('local', type=Path)
p.add_argument('output', type=Path)
a = p.parse_args()
repo = Path(__file__).resolve().parents[2]
out = a.output.resolve()
if out.is_relative_to(repo) or out.is_relative_to(a.local.resolve()):
    raise SystemExit('Output must be outside worktrees')
out.mkdir(parents=True, exist_ok=True)
results = []
for tool in ['CoreRegression', 'ArchitectureGuard']:
    target = out / tool
    target.mkdir(exist_ok=True)
    project = ET.parse(repo / 'tools' / tool / (tool + '.csproj')).getroot()
    props = project.find('PropertyGroup')
    ET.SubElement(props, 'EnableDefaultCompileItems').text = 'false'
    items = ET.SubElement(project, 'ItemGroup')
    for item in project.findall('.//ProjectReference'):
        item.tag = 'Reference'; item.attrib['Include'] = 'DarkNights.Core'
        ET.SubElement(item, 'HintPath').text = str(out.parent / 'compile/Core/bin/netstandard2.1/DarkNights.Core.dll')
    for item in project.findall('.//HintPath'):
        if 'Newtonsoft' in (item.text or ''):
            item.text = str(a.local.resolve() / 'Game/Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll')
    for item in project.findall('.//Compile'):
        item.attrib['Include'] = str((repo / 'tools' / tool / item.attrib['Include']).resolve())
    for source in (repo / 'tools' / tool).glob('*.cs'):
        if source.name == 'Program.cs':
            # Redirect only the report path; all assertions and guard rules are unchanged.
            content = source.read_text(encoding='utf-8-sig')
            prefix = 'RuleScenario.RepositoryRoot' if tool == 'CoreRegression' else 'root'
            original = f'Path.Combine({prefix}, "artifacts/migration/'
            content = content.replace(original, f'Path.Combine(@"{target}", "')
            program = target / 'Program.cs'; program.write_text(content, encoding='utf-8')
            ET.SubElement(items, 'Compile', Include=str(program))
        else:
            ET.SubElement(items, 'Compile', Include=str(source))
    path = target / (tool + '.csproj'); ET.ElementTree(project).write(path, encoding='utf-8')
    result = subprocess.run(['dotnet', 'run', '--project', str(path), '--', str(repo)],
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, encoding='utf-8', errors='replace')
    (target / 'run.log').write_text(result.stdout, encoding='utf-8')
    results.append(dict(tool=tool, exit_code=result.returncode, output=result.stdout[-5000:]))
print(json.dumps(results, ensure_ascii=False))
raise SystemExit(any(r['exit_code'] for r in results))
