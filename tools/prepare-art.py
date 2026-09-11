"""Explicit one-time migration: verify frozen bytes and export reviewable visual input.

Usage: python tools/prepare-art.py --source ../projects --output <empty staging directory>
The result is consumed by the explicit Unity art installer, never by ordinary builds.
"""
import argparse
import hashlib
import json
import pathlib
import re
import shutil


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def properties(body):
    return dict(re.findall(r'^([\w/]+) = (.+)$', body, re.M))


def numbers(text):
    return [float(n) for n in text[text.index('(') + 1:text.rindex(')')].split(',')]


def resource_path(value, resources):
    return resources[re.fullmatch(r'ExtResource\("([^"]+)"\)', value)[1]]


def resources(text):
    return {i: p for p, i in re.findall(r'\[ext_resource type="[^"]+" path="([^"]+)" id="([^"]+)"\]', text)}


def animations(path):
    text = path.read_text(encoding='utf-8-sig')
    refs = resources(text)
    clips = []
    for match in re.finditer(r'\[sub_resource type="Animation" id="[^"]+"\]([^\[]*(?:(?!\n\[sub_resource|\n\[resource)[\s\S])*?)(?=\n\[sub_resource|\n\[resource|\Z)', text):
        body = match[1]
        props = properties(body)
        clip = {'name': json.loads(props['resource_name']), 'duration': float(props.get('length', '1')), 'loop': props.get('loop_mode', '0') == '1', 'tracks': []}
        for index, target in re.findall(r'tracks/(\d+)/path = NodePath\("([^"]+)"\)', body):
            assert props[f'tracks/{index}/type'] == '"value"'
            assert props.get(f'tracks/{index}/enabled', 'true') == 'true'
            keys = re.search(r'tracks/' + index + r'/keys = \{(.*?)\n\}', body, re.S)[1]
            times = numbers(re.search(r'"times": (PackedFloat32Array\([^)]*\))', keys)[1])
            raw = re.search(r'"values": \[(.*?)\]', keys, re.S)[1]
            node, prop = target.rsplit(':', 1)
            assert prop in ('texture', 'offset', 'visible'), target
            if prop == 'texture':
                values = [refs[i] for i in re.findall(r'ExtResource\("([^"]+)"\)', raw)]
            elif prop == 'offset':
                values = [numbers(v) for v in re.findall(r'Vector2\([^)]*\)', raw)]
            else:
                values = [v.strip() == 'true' for v in raw.split(',')]
                assert all(v.strip() in ('true', 'false') for v in raw.split(','))
            assert len(times) == len(values) and times == sorted(times)
            clip['tracks'].append({'path': node, 'property': prop, 'times': times, 'values': values})
        assert clip['tracks'], path
        clips.append(clip)
    assert clips, path
    return clips


def visual(path, source):
    text = path.read_text(encoding='utf-8-sig')
    refs = resources(text)
    result = {'name': path.stem, 'source': path.relative_to(source).as_posix(), 'sha256': digest(path), 'nodes': [], 'clips': []}
    for header, body in re.findall(r'\[node ([^\n]+)\]\n(.*?)(?=\n\[node |\Z)', text, re.S):
        attrs = dict(re.findall(r'(\w+)="([^"]*)"', header))
        p = properties(body)
        node = {'name': attrs['name'], 'type': attrs['type'], 'parent': attrs.get('parent', ''), 'position': numbers(p.get('position', 'Vector2(0, 0)')), 'visible': p.get('visible', 'true') == 'true'}
        if 'parent' not in attrs:
            result['pickBounds'] = numbers(p['PickBounds'])
            result['bindings'] = {k: re.fullmatch(r'NodePath\("([^"]+)"\)', v)[1] for k, v in p.items() if v.startswith('NodePath(')}
            result['standing'] = numbers(p.get('StandingOffset', 'Vector2(0, -6)'))
            result['death'] = numbers(p.get('DeathOffset', 'Vector2(0, -6)'))
            result['fade'] = p.get('FadeConstruction', 'false') == 'true'
        if attrs['type'] == 'Sprite2D':
            assert p.get('centered') == 'false'
            node.update(texture=resource_path(p['texture'], refs), offset=numbers(p.get('offset', 'Vector2(0, 0)')), color=numbers(p.get('modulate', 'Color(1, 1, 1, 1)')))
        elif attrs['type'] == 'Polygon2D':
            node.update(polygon=numbers(p['polygon']), color=numbers(p['color']))
        assert attrs['type'] in ('Node2D', 'Sprite2D', 'Marker2D', 'AnimationPlayer', 'Polygon2D'), attrs
        result['nodes'].append(node)
    libraries = [v for v in refs.values() if v.startswith('res://resources/animations/')]
    for lib in libraries:
        libpath = source / lib.removeprefix('res://')
        result['clips'] += animations(libpath)
        result.setdefault('animationSources', []).append({'path': lib, 'sha256': digest(libpath)})
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=pathlib.Path, required=True)
    parser.add_argument('--output', type=pathlib.Path, required=True)
    args = parser.parse_args()
    source, output = args.source.resolve(), args.output.resolve()
    assert not output.exists() or not any(output.iterdir()), 'Output must be empty'
    manifestpath = source / 'assets/manifest.json'
    manifest = json.loads(manifestpath.read_text(encoding='utf-8-sig'))
    assert len(manifest['files']) == 551
    seen = set()
    for item in manifest['files']:
        path = source / item['path']
        assert path.resolve().is_relative_to(source / 'assets')
        assert item['path'] not in seen
        seen.add(item['path'])
        assert path.stat().st_size == item['bytes'] and digest(path) == item['sha256'], path
    visuals = []
    for category in ('actors', 'buildings', 'worksites'):
        for path in sorted((source / 'scenes' / category / 'visuals').glob('*.tscn')):
            spec = visual(path, source)
            spec['category'] = category
            visuals.append(spec)
    assert len(visuals) == 15
    output.mkdir(parents=True, exist_ok=True)
    for item in manifest['files']:
        destination = output / 'Original' / pathlib.PurePosixPath(item['path']).relative_to('assets')
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source / item['path'], destination)
        assert digest(destination) == item['sha256']
    shutil.copyfile(manifestpath, output / 'Original/manifest.json')
    (output / 'visual-input.json').write_text(json.dumps({'manifestSha256': digest(manifestpath), 'visuals': visuals}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'success': True, 'files': 551, 'visuals': len(visuals), 'clips': sum(len(v['clips']) for v in visuals), 'output': str(output)}))


if __name__ == '__main__':
    main()
