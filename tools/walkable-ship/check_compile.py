"""Compile the thin worktree against read-only assemblies from an existing Local Unity import.

This is a C# API check, not an Editor import, PlayMode or Player acceptance result.
All compiler output goes to an explicitly supplied directory outside both checkouts.
"""
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
local = a.local.resolve()
out = a.output.resolve()
if out.is_relative_to(repo) or out.is_relative_to(local):
    raise SystemExit('Compiler output must be outside worktrees')
out.mkdir(parents=True, exist_ok=True)
names = ['Core', 'Runtime', 'View', 'Entry', 'Editor', 'Tests']
ns = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}

for name in names:
    original = ET.parse(local / 'Game' / f'DarkNights.{name}.csproj').getroot()
    project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
    props = ET.SubElement(project, 'PropertyGroup')
    for key, value in dict(TargetFramework='netstandard2.1', LangVersion='9.0',
        AssemblyName=f'DarkNights.{name}', EnableDefaultCompileItems='false',
        GenerateAssemblyInfo='false', AllowUnsafeBlocks='true', NoWarn='0169;0649',
        DefineConstants=original.find('.//m:DefineConstants', ns).text,
        BaseIntermediateOutputPath=str(out / name / 'obj') + '/',
        OutputPath=str(out / name / 'bin') + '/').items():
        ET.SubElement(props, key).text = value
    items = ET.SubElement(project, 'ItemGroup')
    ET.SubElement(items, 'Compile', Include=str(repo / 'Game/Assets/DarkNights/Scripts' / name / '**/*.cs'))
    for elem in original.findall('.//m:Analyzer', ns):
        ET.SubElement(items, 'Analyzer', Include=elem.attrib['Include'])
    for elem in original.findall('.//m:Reference', ns):
        hint = elem.find('m:HintPath', ns)
        if hint is None or elem.attrib['Include'] in ('mscorlib', 'netstandard') or elem.attrib['Include'].startswith('System'):
            continue
        path = Path(hint.text)
        if not path.is_absolute(): path = local / 'Game' / path
        reference = ET.SubElement(items, 'Reference', Include=elem.attrib['Include'])
        ET.SubElement(reference, 'HintPath').text = str(path.resolve())
    for elem in original.findall('.//m:ProjectReference', ns):
        assembly = Path(elem.attrib['Include']).stem
        if assembly in [f'DarkNights.{n}' for n in names]:
            ET.SubElement(items, 'ProjectReference', Include=str(out / assembly / (assembly + '.csproj')))
        else:
            reference = ET.SubElement(items, 'Reference', Include=assembly)
            ET.SubElement(reference, 'HintPath').text = str(local / 'Game/Library/ScriptAssemblies' / (assembly + '.dll'))
    target = out / f'DarkNights.{name}' / f'DarkNights.{name}.csproj'
    target.parent.mkdir(exist_ok=True)
    ET.ElementTree(project).write(target, encoding='utf-8', xml_declaration=True)

result = subprocess.run(['dotnet', 'build', str(out / 'DarkNights.Tests/DarkNights.Tests.csproj'), '--nologo', '-v:q'],
    stdout=subprocess.PIPE, stderr=subprocess.STDOUT, encoding='utf-8', errors='replace')
(out / 'compile.log').write_text(result.stdout, encoding='utf-8')
errors = [line.strip() for line in result.stdout.splitlines() if ': error ' in line]
print(json.dumps({'exit_code': result.returncode, 'errors': list(dict.fromkeys(errors))[:35],
    'log': str(out / 'compile.log'), 'scope': 'read-only Local references; no Unity import or runtime'}, ensure_ascii=False))
raise SystemExit(result.returncode)
