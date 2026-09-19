"""One-time pixel artwork authoring into an EMPTY directory; never run during import/build.

RGBA Aseprite layers/tags follow https://github.com/aseprite/aseprite/blob/main/docs/ase-file-specs.md
After hand editing the .aseprite source, export from Aseprite instead of rerunning this initializer.
"""
import argparse
import json
import struct
import zlib
from pathlib import Path
from PIL import Image, ImageDraw


PALETTE = {
    '.': (0, 0, 0, 0), 'd': '#393c3b', 's': '#656b68', 'm': '#9ba29b', 'h': '#d1cebc',
    'w': '#69462e', 'b': '#96704b', 'l': '#b29566', 'r': '#984d3f', 'R': '#c17858',
    't': '#c7b58b', 'f': '#e8bc67', 'F': '#f4e2b5', 'a': '#d2b58b', 'A': '#a88864'
}


def stamp(rows, offset=(0, 0), size=(24, 24), keep=None):
    image = Image.new('RGBA', size)
    draw = ImageDraw.Draw(image)
    for y, row in enumerate(rows):
        for x, code in enumerate(row):
            if code != '.' and (keep is None or code in keep):
                draw.point((x + offset[0], y + offset[1]), fill=PALETTE[code])
    return image


def string(value):
    data = value.encode('utf-8')
    return struct.pack('<H', len(data)) + data


def chunk(kind, data):
    return struct.pack('<IH', len(data) + 6, kind) + data


def aseprite(path, names, frames, tags, duration=100):
    encoded = []
    for index, layers in enumerate(frames):
        chunks = []
        if index == 0:
            for name in names:
                chunks.append(chunk(0x2004, struct.pack('<6HB3x', 3, 0, 0, 0, 0, 0, 255) + string(name)))
            tag_data = struct.pack('<H8x', len(tags))
            for name, start, end in tags:
                tag_data += struct.pack('<HHBH6x3BB', start, end, 0, 0, 150, 173, 154, 0) + string(name)
            chunks.append(chunk(0x2018, tag_data))
            chunks.append(chunk(0x2007, struct.pack('<HHI8x', 1, 0, 0)))
        for layer, image in enumerate(layers):
            data = struct.pack('<HhhBHh5xHH', layer, 0, 0, 255, 2, 0, *image.size)
            chunks.append(chunk(0x2005, data + zlib.compress(image.tobytes())))
        payload = b''.join(chunks)
        encoded.append(struct.pack('<IHHH2xI', len(payload) + 16, 0xF1FA, len(chunks), duration, 0) + payload)
    data = b''.join(encoded)
    header = bytearray(128)
    struct.pack_into('<IHHHHHIH', header, 0, len(data) + 128, 0xA5E0, len(frames), 24, 24, 32, 1, duration)
    struct.pack_into('<BBhhHH', header, 34, 1, 1, 0, 0, 1, 1)
    path.write_bytes(header + data)


def composite(layers):
    output = Image.new('RGBA', layers[0].size)
    for layer in layers:
        output.alpha_composite(layer)
    return output


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = args.output
    if root.exists() and any(root.iterdir()):
        raise SystemExit('Refusing to overwrite authored artwork: output must be empty.')
    root.mkdir(parents=True, exist_ok=True)
    drawings = {
        'Pistol': ([ '..d....d..', '.shhhhhhmd', '.smmmmmmmd', '..sddddd..', '..wb.d....', '..wb......', '..ww......' ], (6, 7), ['Wood grip', 'Iron barrel', 'Metal highlights']),
        'Pickaxe': ([ '....hhh....', '..hmmmmsh..', '.mmssdsssm.', 'ms...wb..ss', '.....wb....', '.....wb....', '.....wb....', '.....wb....', '.....wb....', '.....w.....' ], (3, 3), ['Wood shaft', 'Iron pick', 'Metal highlights']),
        'Bomb': ([ '...tf....', '...t.....', '.dRRRd...', '.rRrRrd..', '.rRrRrd..', '.tttttd..', '.rRrRrd..', '.drdrdd..' ], (6, 6), ['Dynamite sticks', 'Rope and fuse', 'Highlights'])
    }
    manifest = {'canvas': [24, 24], 'grip_pixel_top_left': [8, 12], 'pixels_per_unit': 100, 'palette': PALETTE, 'items': {}}
    for name, (rows, offset, names) in drawings.items():
        groups = ['wbl', 'dsm', 'h'] if name != 'Bomb' else ['drR', 't', 'fF']
        layers = [stamp(rows, offset, keep=group) for group in groups]
        held = composite(layers)
        action = [Image.new('RGBA', (24, 24)) for _ in layers]
        for i, layer in enumerate(layers):
            action[i].alpha_composite(layer, (-1, -1) if name == 'Pistol' else (0, -1))
        icon_layers = []
        bbox = held.getbbox()
        for layer in layers:
            crop = layer.crop(bbox)
            factor = 2 if max(crop.size) <= 11 else 1
            crop = crop.resize((crop.width * factor, crop.height * factor), Image.Resampling.NEAREST)
            icon = Image.new('RGBA', (24, 24))
            icon.alpha_composite(crop, ((24 - crop.width) // 2, (24 - crop.height) // 2))
            icon_layers.append(icon)
        aseprite(root / f'{name}.aseprite', names, [layers, action, icon_layers], [('held', 0, 0), ('recoil' if name == 'Pistol' else 'swing' if name == 'Pickaxe' else 'charge', 1, 1), ('icon', 2, 2)])
        held.save(root / f'{name}.png')
        composite(icon_layers).save(root / f'{name}Icon.png')
        manifest['items'][name] = {'source': f'{name}.aseprite', 'held_frame': 0, 'icon_frame': 2, 'layers': names}
    stamp(['....bbAa..', '....wwAA..'], (0, 11)).save(root / 'Hand.png')
    stamp(['..f...', '.fFFf.', 'fFFFFf', '.fFFf.', '..f...'], (0, 0), (6, 5)).save(root / 'Muzzle.png')
    stamp(['.ffF', 'fFFF', '.ffF'], (0, 0), (4, 3)).save(root / 'Bullet.png')
    explosion_frames = []
    for i, radius in enumerate([5, 8, 11, 9]):
        outer = Image.new('RGBA', (24, 24)); inner = outer.copy()
        a, b = ImageDraw.Draw(outer), ImageDraw.Draw(inner)
        a.polygon([(12-radius, 9), (8, 7), (9, 12-radius), (14, 6), (17, 4+i), (17, 8), (12+radius, 12), (18, 15), (18, 18), (13, 12+radius), (9, 18), (5, 18), (7, 14), (2+i, 12)], fill=PALETTE['r' if i == 3 else 'R'])
        b.rectangle((8+i, 8+i, 15, 15), fill=PALETTE['t' if i == 3 else 'f'])
        if i < 2: b.rectangle((10, 10, 13, 13), fill=PALETTE['F'])
        composite([outer, inner]).save(root / f'Explosion{i}.png')
        explosion_frames.append([outer, inner])
    aseprite(root / 'Explosion.aseprite', ['Smoke silhouette', 'Fire core'], explosion_frames, [('explode', 0, 3)], 75)
    (root / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print(f'Authored 3 layered items, 3 icons, hand, bullet, muzzle, 4 explosion frames: {root}')


if __name__ == '__main__':
    main()
