"""从真实 tile 数据画诊断图；不执行游戏、不代表浏览器画面。"""
import json
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[2]
target = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else root / 'artifacts/mine-natural-caves-20261011'
maps = json.loads((target / 'geometry.json').read_text(encoding='utf-8'))
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 20)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 13)
scale, pad, block = 7, 28, 635
image = Image.new('RGB', (980, block * len(maps)), '#0b1720')
draw = ImageDraw.Draw(image)
for i, m in enumerate(maps):
    offset = i * block
    draw.text((pad, offset + 12), m['title'] + ' · 地形数据诊断', font=font, fill='#e9cf96')
    draw.text((pad, offset + 44), '实际空气 / 基岩 / 裂纹岩；虚线仅标记可搭梯竖井，开局没有梯子', font=small, fill='#a6bdc7')
    for y in range(m['rows']):
        for x in range(m['cols']):
            tile = m['tiles'][y * m['cols'] + x]
            color = '#25363e' if tile == 1 else '#ad9d79' if tile == 2 else '#669387' if y < 25 else '#918253' if y < 45 else '#9a6d65'
            xx, yy = pad + x * scale, offset + 72 + y * scale
            draw.rectangle((xx, yy, xx + scale - 1, yy + scale - 1), fill=color)
    for site in m['ladderSites']:
        x = pad + site['x'] / 24 * scale
        top, bottom = offset + 72 + site['top'] / 24 * scale, offset + 72 + site['bottom'] / 24 * scale
        for y in range(round(top), round(bottom), 8):
            draw.line((x, y, x, min(y + 3, bottom)), fill='#f0d596', width=2)
    draw.text((pad, offset + 611), f"表面矿 {m['stats']['surface']} · 墙后矿 {m['stats']['buried']} · 隐藏巢穴 {m['stats']['nests']} · 预搭梯 0", font=small, fill='#b9c9cb')
name = 'concept-geometry.png' if len(sys.argv) > 1 else 'natural-geometry.png'
image.save(target / name)
print(target / name)
