"""Create native pixel placeholders once; retain authored outputs on subsequent runs."""
from pathlib import Path
from PIL import Image, ImageDraw
import json
import hashlib

ROOT = Path(__file__).resolve().parents[1] / 'Game/Assets/DarkNights/Res/Art/Custom/ExpeditionPrototype'
ROOT.mkdir(parents=True, exist_ok=True)
if list(ROOT.glob('*.png')):
    raise SystemExit('Existing source art is preserved; select a new empty output directory to regenerate.')
INK, EDGE, METAL, LIGHT = '#151c2b', '#34495d', '#76969b', '#d6e5c2'
CYAN, GOLD, RED = '#75dbd1', '#e6b766', '#c76657'
rows = []

def save(name, im):
    path = ROOT / (name + '.png'); im.save(path)
    rows.append(dict(file=path.name, size=list(im.size), sha256=hashlib.sha256(path.read_bytes()).hexdigest()))

for mask in range(8):
    im = Image.new('RGBA', (192, 48)); d = ImageDraw.Draw(im)
    d.polygon([(2, 36), (9, 14), (32, 7), (160, 7), (184, 23), (189, 36)], fill=INK)
    d.polygon([(7, 33), (15, 16), (35, 10), (158, 10), (177, 24), (183, 33)], fill=METAL)
    d.rectangle((18, 24, 174, 37), fill=EDGE)
    d.rectangle((9, 37, 31, 43), fill=INK); d.rectangle((157, 37, 178, 43), fill=INK)
    d.rectangle((2, 25, 13, 34), fill=GOLD); d.rectangle((164, 18, 177, 27), fill=CYAN)
    d.rectangle((160, 30, 169, 41), fill=INK)
    for module in range(3):
        x = 48 + module * 32
        d.rectangle((x, 8, x + 30, 37), fill=EDGE, outline=INK, width=2)
        color = [CYAN, GOLD, LIGHT][module] if mask & (1 << module) else '#53636d'
        d.rectangle((x + 4, 13, x + 26, 19), fill=color)
        for y in [26, 30]: d.line((x + 5, y, x + 25, y), fill=METAL)
    save('ship-' + str(mask), im)
for name in ['oxygen', 'storage', 'turret', 'lamp']:
    w = 16 if name == 'lamp' else 32
    im = Image.new('RGBA', (w, 32)); d = ImageDraw.Draw(im)
    d.rectangle((1, 27, w - 2, 30), fill=INK)
    if name == 'oxygen':
        for x in [4, 18]:
            d.rounded_rectangle((x, 5, x + 9, 26), radius=3, fill=METAL, outline=INK, width=2)
            d.rectangle((x + 2, 10, x + 7, 16), fill=CYAN)
        d.line((8, 4, 23, 4), fill=GOLD, width=2)
    elif name == 'storage':
        d.rectangle((3, 11, 28, 26), fill=EDGE, outline=INK, width=2)
        d.rectangle((5, 8, 26, 12), fill=METAL)
        d.line((15, 13, 15, 24), fill=GOLD, width=3)
    elif name == 'turret':
        d.rectangle((12, 13, 20, 27), fill=METAL)
        d.rectangle((5, 7, 24, 16), fill=EDGE, outline=INK, width=2)
        d.rectangle((20, 8, 30, 11), fill=METAL); d.rectangle((8, 9, 12, 11), fill=RED)
    else:
        d.rectangle((7, 9, 9, 27), fill=METAL)
        d.rectangle((3, 3, 12, 11), fill=INK)
        d.rectangle((5, 5, 10, 9), fill=GOLD)
    save(name, im)
im = Image.new('RGBA', (16, 16)); d = ImageDraw.Draw(im)
d.rectangle((2, 3, 13, 11), fill=INK); d.rectangle((3, 4, 12, 9), fill=METAL)
d.rectangle((4, 5, 6, 6), fill=CYAN); d.rectangle((9, 5, 11, 6), fill=CYAN)
d.rectangle((0, 7, 2, 12), fill=EDGE); d.rectangle((13, 7, 15, 12), fill=EDGE)
d.line((4, 13, 11, 13), fill=CYAN); save('hauler', im)
(ROOT / 'source.json').write_text(json.dumps(dict(method='native-pixel Pillow drawing; no image generation',
    density='16 pixels per terrain cell', constraints='transparent hard edges, Point sampling; ship interfaces at x=48/80/112/144',
    artifacts=rows), ensure_ascii=False, indent=2), encoding='utf8')
print('Created', len(rows), 'native pixel sources.')
