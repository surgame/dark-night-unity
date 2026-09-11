"""Prepare an isolated, presentation-only Godot scene capture without touching the baseline."""
import argparse
import hashlib
import json
import pathlib
import re
import shutil


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=pathlib.Path, required=True)
    parser.add_argument('--output', type=pathlib.Path, required=True)
    args = parser.parse_args()
    source, output = args.source.resolve(), args.output.resolve()
    assert not output.exists() or not any(output.iterdir()), 'Output must be empty'
    files = list((source / 'scenes/ui').rglob('*.tscn')) + list((source / 'resources/themes').glob('*.tres'))
    hashes = []
    for path in files:
        relative = path.relative_to(source)
        hashes.append({'path': relative.as_posix(), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
        text = path.read_text(encoding='utf-8-sig')
        # Strip only executable bindings in this disposable copy, preserving all native UI and theme properties.
        text = re.sub(r'^\[ext_resource type="Script"[^\n]+\]\n', '', text, flags=re.M)
        text = re.sub(r'^script = ExtResource\([^\n]+\)\n', '', text, flags=re.M)
        target = output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding='utf-8')
    referenced = set()
    for path in files:
        referenced.update(re.findall(r'path="res://(assets/[^"]+)"', path.read_text(encoding='utf-8-sig')))
    for relative in sorted(referenced):
        target = output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source / relative, target)
    (output / 'project.godot').write_text('config_version=5\n[application]\nconfig/name="Dark Nights UI Capture"\n[rendering]\nrenderer/rendering_method="gl_compatibility"\n', encoding='utf-8')
    shutil.copyfile(pathlib.Path(__file__).with_name('capture-ui.gd'), output / 'capture-ui.gd')
    (output / 'source-hashes.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
    print(json.dumps({'success': True, 'scenesAndThemes': len(files), 'textures': len(referenced), 'output': str(output)}))


if __name__ == '__main__':
    main()
