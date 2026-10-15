"""生成原生PNG、图集、预览及来源清单；拒绝覆盖已被手工改变的成果。"""

import argparse
import hashlib
import json
import subprocess
from pathlib import Path

from PIL import Image

from creatures import ambush_grub, ore_mimic
from palette import COLORS
from props import clue, egg_clutch, nest
from previews import build_previews

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]
GROUPS = [
    ('ore-mimic', (32, 32), (16, 30), ore_mimic,
     ['dormant', 'warning', 'reveal', 'crawl_a', 'crawl_b', 'lunge', 'recoil', 'fallen']),
    ('ambush-grub', (32, 24), (16, 23), ambush_grub,
     ['idle_a', 'idle_b', 'alert', 'crawl_a', 'crawl_b', 'lunge', 'recoil', 'fallen']),
    ('egg-clutch', (24, 24), (12, 23), egg_clutch,
     ['intact', 'pulse', 'cracked', 'empty']),
    ('burrow', (32, 24), (16, 23), nest, ['empty', 'occupied']),
    ('clues', (16, 16), (8, 15), clue, ['scratch', 'slime', 'molt']),
]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def protect_existing(output):
    previous = output / 'manifest.json'
    if not previous.exists():
        for name in ('native', 'sheets', 'preview', 'editable'):
            if (output / name).exists():
                raise SystemExit(f'输出目录已有未登记文件，拒绝覆盖：{output / name}')
        return
    data = json.loads(previous.read_text(encoding='utf-8'))
    for item in data['outputs']:
        path = output / item['path']
        if not path.exists() or digest(path) != item['sha256']:
            raise SystemExit(f'已有成果变化，拒绝重建，请另选空输出目录：{path}')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=ROOT)
    parser.add_argument('--aseprite', type=Path)
    args = parser.parse_args()
    out = args.output.resolve()
    protect_existing(out)
    if args.aseprite and not args.aseprite.is_file():
        raise SystemExit('指定的Aseprite程序不存在')
    if not args.aseprite and any((out / 'editable').glob('*.aseprite')):
        raise SystemExit('已有可编辑文件：请指定 --aseprite 一同重建，或 --output 指定新空目录')
    out.mkdir(parents=True, exist_ok=True)
    for name in ('native', 'sheets', 'preview', 'editable'):
        (out / name).mkdir(exist_ok=True)
    records = []
    images = {}
    all_frames = []
    for group, size, pivot, draw, states in GROUPS:
        folder = out / 'native' / group
        folder.mkdir(exist_ok=True)
        frames = [draw(state) for state in states]
        images[group] = frames
        sheet = Image.new('RGBA', (size[0] * len(frames), size[1]))
        for i, (state, frame) in enumerate(zip(states, frames)):
            if frame.size != size:
                raise ValueError(f'{group}/{state} 尺寸错误')
            if set(frame.getchannel('A').getdata()) != {0, 255}:
                raise ValueError(f'{group}/{state} alpha非二值')
            if frame.getbbox() is None:
                raise ValueError(f'{group}/{state} 空帧')
            path = folder / f'{state}.png'
            frame.save(path)
            sheet.paste(frame, (i * size[0], 0))
            records.append({'group': group, 'state': state,
                            'path': path.relative_to(out).as_posix(),
                            'size': list(size), 'pivot_top_left': list(pivot),
                            'sheet_rect_top_left': [i * size[0], 0, *size],
                            'opaque_bbox': list(frame.getbbox()),
                            'pixel_colors': len(frame.getcolors(65536)) - 1})
            all_frames.append(frame)
        sheet.save(out / 'sheets' / f'{group}.png')
    atlas = Image.new('RGBA', (256, 128))
    for i, frame in enumerate(all_frames):
        atlas.paste(frame, ((i % 8) * 32, (i // 8) * 32))
    atlas.save(out / 'sheets' / 'all-assets-32-cell.png')
    palette = Image.new('RGBA', (len(COLORS), 1))
    for x, color in enumerate(COLORS.values()):
        palette.putpixel((x, 0), tuple(bytes.fromhex(color[1:])) + (255,))
    palette.save(out / 'sheets' / 'palette.png')
    build_previews(out, REPO, images, GROUPS, COLORS)
    write_aseprite_import(out)
    if args.aseprite:
        subprocess.run([str(args.aseprite), '--batch', '--script-param', 'verified_native_import=1', '--script',
                        str(out / 'editable' / 'import-native.lua')], check=True)
    outputs = []
    for folder in ('native', 'sheets', 'preview', 'editable'):
        for path in sorted((out / folder).rglob('*')):
            if path.is_file():
                outputs.append({'path': path.relative_to(out).as_posix(),
                                'bytes': path.stat().st_size, 'sha256': digest(path)})
    refs = ['Game/Assets/DarkNights/Res/Art/Original/sprites/spr_worker_idle/spr_worker_idle_0.png',
            'Game/Assets/DarkNights/Res/Art/Original/sprites/spr_zombie_idle/spr_zombie_idle_0.png',
            'Game/Assets/DarkNights/Res/Art/Original/sprites/spr_res_iron/spr_res_iron_0.png',
            'Game/Assets/DarkNights/Res/Art/Custom/CaveExploration/cave-rock.png',
            'experiments/ore-dualgrid-v1/preview/cave-grid-ores-2x.png']
    data = {'module': 'CL-04', 'date': '2026-10-15', 'status': 'art-candidates-not-integrated',
            'method': 'native-integer-pixel-authoring; no AI image returned; no downscaling',
            'image_api': {'requested_model': 'gpt-image-2.5',
                          'status': 'not-sent-missing-OPENAI_API_KEY-in-auth.json'},
            'palette': COLORS, 'frame_count': len(records), 'frames': records,
            'source_references': [{'path': path, 'sha256': digest(REPO / path)} for path in refs],
            'source_inputs': [{'path': path.relative_to(ROOT).as_posix(), 'sha256': digest(path)}
                              for path in sorted((ROOT / 'source').iterdir()) if path.is_file()],
            'authoring_sources': [{'path': path.relative_to(ROOT).as_posix(), 'sha256': digest(path)}
                                  for path in sorted((ROOT / 'authoring').glob('*.py'))],
            'recommended_import': {'ppu': 100, 'filter': 'Point', 'mipmaps': False,
                                   'compression': 'None', 'wrap': 'Clamp', 'sRGB': True,
                                   'alpha': 'binary', 'color_space': 'Linear project'},
            'outputs': outputs}
    (out / 'manifest.json').write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n',
                                       encoding='utf-8')
    print(json.dumps({'output': str(out), 'frames': len(records), 'files': len(outputs),
                      'bytes': sum(item['bytes'] for item in outputs), 'status': 'complete'},
                     ensure_ascii=False))


def write_aseprite_import(out):
    """生成只读PNG导入配方；调用Aseprite后获得带命名Tag的可编辑逐帧文件。"""
    lines = ['-- 只允许构建器完成哈希保护后调用；不修改原生PNG。',
             'if app.params["verified_native_import"] ~= "1" then',
             '  error("Use authoring/build.py with --aseprite after output hash verification")',
             'end']
    for group, size, pivot, draw, states in GROUPS:
        lines += [f'local sprite = Sprite({size[0]}, {size[1]}, ColorMode.RGB)',
                  'sprite.layers[1].name = "Pixels"']
        for i, state in enumerate(states):
            source = (out / 'native' / group / f'{state}.png').as_posix()
            if i:
                lines.append('sprite:newEmptyFrame()')
            lines += [f'local png = Image{{ fromFile = [[{source}]] }}',
                      f'sprite:newCel(sprite.layers[1], {i + 1}, png, Point(0, 0))',
                      f'sprite.frames[{i + 1}].duration = 0.16',
                      f'local tag = sprite:newTag({i + 1}, {i + 1})',
                      f'tag.name = "{state}"']
        target = (out / 'editable' / f'{group}.aseprite').as_posix()
        lines += [f'sprite:saveAs([[{target}]])', 'sprite:close()']
    (out / 'editable' / 'import-native.lua').write_text('\n'.join(lines) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
