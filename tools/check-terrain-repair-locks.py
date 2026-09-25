"""Verify only the four intended UPM source references changed; no network or repository writes."""
import json
import pathlib
import subprocess

ROOT = pathlib.Path(__file__).resolve().parent.parent
BASE = 'f50ccf53f71bab09d17c8c24cf9b5ea792acc38e'
PIN = 'd1a6c147bc122b87b21c8e6106d005307c387518'
PACKAGES = ('com.tsgame.anyrules', 'com.tsgame.anyrules.networking',
            'com.tsgame.anyrules.networking.fishnet', 'com.tsgame.anyrules.yygc')
for name in ('manifest.json', 'packages-lock.json'):
    relative = 'Game/Packages/' + name
    original = json.loads(subprocess.check_output(['git', '-C', str(ROOT), 'show', BASE + ':' + relative]))
    current = json.loads((ROOT / relative).read_text(encoding='utf-8-sig'))
    for package in PACKAGES:
        wanted = f'file:../../.deps/AnyRules-map-state-{PIN[:7]}/AnyRuleD~/Packages/{package}'
        if name == 'manifest.json':
            original['dependencies'][package] = wanted
        else:
            original['dependencies'][package]['version'] = wanted
    if original != current:
        expected = original['dependencies']
        actual = current['dependencies']
        differences = sorted(key for key in set(expected) | set(actual) if expected.get(key) != actual.get(key))
        raise SystemExit(f'{relative}: unexpected changes: {differences}')
lock = json.loads((ROOT / 'tools/map-framework-patch/source-lock-map-state.json').read_text(encoding='utf-8-sig'))
assert lock['commit'] == PIN and lock['format_version'] == 2
assert set(lock['package_trees']) == set(PACKAGES)
assert all(len(value) == 40 for value in lock['package_trees'].values())
print('PASS: four package references aligned; all unrelated UPM metadata unchanged.')
