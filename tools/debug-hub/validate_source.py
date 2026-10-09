"""Bounded source checks; this does not invoke Unity's UXML/USS importer."""
import hashlib
import json
import re
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

repo = Path(__file__).resolve().parents[2]
assets = [repo / '.deps/YYGC-grid-business/Runtime/Debugging/Resources/YYGC/Debugging/RuntimeDebugHub.uxml',
          repo / 'Game/Assets/DarkNights/Res/UI/DebugHub/Resources/DarkNights/Debugging/Objects.uxml']
required = [{'debugHubRoot', 'window', 'header', 'navigation', 'contentHost', 'status', 'closeButton'},
            {'objectsRoot', 'objectGrid', 'search', 'target', 'godMode', 'quantity', 'instance', 'selectedName',
             'selectedKey', 'selectedStatus', 'definitionCount', 'addObject', 'removeObject', 'equipmentTarget',
             'jetpackStatus', 'removeJetpack', 'recentActions'} |
            {name + str(index) for name in ['slot', 'removeSlot'] for index in range(4)}]
report = {'scope': 'XML syntax, stylesheet source subset, control contracts and file lengths; not Unity import', 'assets': []}
for path, names in zip(assets, required):
    root = ET.parse(path).getroot()
    assert root.tag == '{UnityEngine.UIElements}UXML', path
    nodes = list(root.iter())
    actual = [node.get('name') for node in nodes if node.get('name')]
    assert len(actual) == len(set(actual)) and names <= set(actual), path
    assert not any(node.get('style') is not None for node in nodes), path
    assert len([node for node in root if not node.tag.endswith('}Style')]) == 1, path
    for style in root.findall('{UnityEngine.UIElements}Style'):
        sheet = path.parent / style.get('src')
        text = sheet.read_text(encoding='utf-8')
        assert text.count('{') == text.count('}'), sheet
        assert not re.search(r'(?:^|[;{])\s*(?:gap|z-index|pointer-events|filter|outline|box-shadow|border)\s*:', text), sheet
        assert not re.search(r'(?:display\s*:\s*grid|calc\(|gradient\(|@media|:nth-child|:first-child|:last-child)', text), sheet
        report['assets'].append({'path': str(sheet.relative_to(repo)), 'sha256': hashlib.sha256(sheet.read_bytes()).hexdigest()})
    report['assets'].append({'path': str(path.relative_to(repo)), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'controls': len(actual)})

changed = subprocess.check_output(['git', 'diff', '--name-only', 'HEAD'], cwd=repo, text=True).splitlines()
new = subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard'], cwd=repo, text=True).splitlines()
files = {repo / path for path in changed + new if path.endswith('.cs')}
files |= set((repo / '.deps/YYGC-grid-business/Runtime/Debugging').glob('*.cs'))
report['csharp_lines'] = {}
for path in sorted(files):
    count = len(path.read_text(encoding='utf-8-sig').splitlines())
    assert count <= 300, (path, count)
    report['csharp_lines'][str(path.relative_to(repo))] = count
target = repo / 'artifacts/debug-hub-20261008/source-check.json'
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text(json.dumps(report, indent=2), encoding='utf-8')
print('XML/USS source contracts:', len(assets), 'templates; C# length checks:', len(files), 'passed')
