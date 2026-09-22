"""Collect measured results; refuse to publish an incomplete network run as passing."""
import argparse
import hashlib
import json
import shutil
import subprocess
from datetime import datetime, timezone
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('normal', type=Path)
p.add_argument('weak', type=Path)
p.add_argument('static', type=Path)
a = p.parse_args()
repo = Path(__file__).resolve().parents[2]
artifacts = repo / 'artifacts/walkable-ship'
destination = repo / 'docs/evidence/walkable-ship-2026-09-22'
destination.mkdir(parents=True, exist_ok=True)
def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
latest = {}
batches = []
for path in sorted(artifacts.glob('editor-r*.json')):
    report = read(path)
    batches.append(dict(file=path.name, sha256=sha(path), status=report['status'], scheduled=report['scheduled']))
    for test in report['results']:
        latest[test['FullName']] = dict(name=test['FullName'], status=test['Status'], batch=path.name, message=test['Message'])
network = {}
for label, folder in [('normal', a.normal), ('weak', a.weak)]:
    result = read(folder / 'result.json')
    assert result['passed'] and all(result['checks'].values()), label + ' has not passed'
    host = read(folder / 'host.json')
    result.update(source=str(folder), report_sha256=sha(folder / 'result.json'),
                  terrain_sha256=host['terrain']['sha256'], background_hash=host['terrain']['backgroundHash'])
    if label == 'weak': result['relay'] = read(folder / 'relay.json')
    network[label] = result
    for capture in folder.glob('*.png'):
        shutil.copy2(capture, destination / (label + '-' + capture.name))
assert network['normal']['player_sha256'] == network['weak']['player_sha256']
assert network['normal']['managed_sha256'] == network['weak']['managed_sha256']
core = read(a.static / 'CoreRegression/core-regression.json')
architecture = read(a.static / 'ArchitectureGuard/architecture.json')
art = read(artifacts / 'art-verification.json')
build = read(max(artifacts.glob('build-result*.json'), key=lambda v: v.stat().st_mtime))
managed = artifacts / 'player-mono/DarkNights_Data/Managed'
result = dict(utc=datetime.now(timezone.utc).isoformat(), branch=subprocess.check_output(['git', 'branch', '--show-current'], cwd=repo, text=True).strip(),
    checkpoint=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=repo, text=True).strip(), protocol=14, save_version=10,
    build=build, managed_sha256={path.name: sha(path) for path in managed.glob('DarkNights.*.dll')},
    editor=dict(passed=sum(v['status'] == 'Passed' for v in latest.values()), total=len(latest),
                batches=batches, tests=list(latest.values()), method='latest affected rerun per unique test; not a new full pass'),
    core={k: v for k, v in core.items() if k != 'checks'}, architecture=architecture, art=art,
    network=network, visual='Actual Mono scene camera and real UGUI offscreen captures reviewed; not foreground performance evidence.',
    boundaries=['Flight is limited to the original dock envelope; no full-cave route or arbitrary landing.',
                'Robot deployment uses an explicitly purchased-module save fixture; this does not prove earning its price.',
                'NativeButtonThemeTests.InteractableChangesUpdateWithoutPointerMovement remains a known failure.',
                'No IL2CPP, two-machine, foreground performance or old-save compatibility acceptance.'])
path = destination / 'results.json'
path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(dict(path=str(path), editor_passed=result['editor']['passed'], editor_total=len(latest),
                     normal_checks=len(network['normal']['checks']), weak_checks=len(network['weak']['checks'])), ensure_ascii=False))
