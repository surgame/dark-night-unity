"""Freeze effect/environment source records into an explicitly empty staging output."""
import argparse
import hashlib
import json
import pathlib
import re


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=pathlib.Path, required=True)
    parser.add_argument('--output', type=pathlib.Path, required=True)
    args = parser.parse_args()
    source, output = args.source.resolve(), args.output.resolve()
    assert not output.exists() or not any(output.iterdir()), 'Output must be empty'
    scene_paths = list((source / 'scenes/effects').glob('*.tscn'))
    scene_paths += list((source / 'scenes/environment').glob('*.tscn'))
    scene_paths += [source / 'scenes/levels/Pinewatch.tscn', source / 'scenes/audio/Audio.tscn']
    records = []
    scripts = set()
    for path in sorted(scene_paths):
        text = path.read_text(encoding='utf-8-sig')
        resources = {i: p for p, i in re.findall(r'\[ext_resource type="[^"]+" path="([^"]+)" id="([^"]+)"\]', text)}
        scripts.update(source / p.removeprefix('res://') for p in resources.values() if p.endswith('.cs'))
        nodes = []
        for header, body in re.findall(r'\[node ([^\n]+)\]\n(.*?)(?=\n\[node |\Z)', text, re.S):
            nodes.append({'attributes': dict(re.findall(r'(\w+)="([^"]*)"', header)),
                          'properties': dict(re.findall(r'^([\w/]+) = (.+)$', body, re.M))})
        records.append({'path': path.relative_to(source).as_posix(), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                        'resources': resources, 'nodes': nodes, 'sourceText': text})
    sources = [{'path': path.relative_to(source).as_posix(), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                'sourceText': path.read_text(encoding='utf-8-sig')} for path in sorted(scripts)]
    output.mkdir(parents=True, exist_ok=True)
    (output / 'EffectInput.json').write_text(json.dumps({'scenes': records, 'scripts': sources}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'scenes': len(records), 'scripts': len(sources), 'output': str(output)}))


if __name__ == '__main__':
    main()
