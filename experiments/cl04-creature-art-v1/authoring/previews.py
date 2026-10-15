"""仅供美术审阅的整数倍静图与动画；不模拟正式照明、战斗或状态时长。"""

import json

from PIL import Image, ImageDraw, ImageFont

BG = '#1c1b18'
PANEL = '#25231f'
INK = '#d4c7ab'
MUTED = '#a0957f'
NAMES = {
    'ore-mimic': '拟态矿虫', 'ambush-grub': '伏击幼虫',
    'egg-clutch': '虫卵簇', 'burrow': '巢穴口', 'clues': '环境线索',
}
STATES = {
    'dormant': '伪装', 'warning': '前兆', 'reveal': '显形',
    'crawl_a': '爬行 A', 'crawl_b': '爬行 B', 'lunge': '攻击姿势',
    'recoil': '退缩', 'fallen': '倒伏', 'idle_a': '休眠 A', 'idle_b': '休眠 B',
    'alert': '警觉', 'intact': '完整', 'pulse': '蠕动', 'cracked': '开裂',
    'empty': '空壳 / 空巢', 'occupied': '占据', 'scratch': '抓痕',
    'slime': '黏液痕', 'molt': '蜕壳',
}


def font(size):
    return ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', size)


def stamp(target, sprite, xy, scale=4):
    scaled = sprite.resize((sprite.width * scale, sprite.height * scale), Image.Resampling.NEAREST)
    target.paste(scaled, xy, scaled)


def build_previews(out, repo, groups, specs, palette):
    sheet = Image.new('RGB', (1440, 1120), BG)
    d = ImageDraw.Draw(sheet)
    d.text((34, 22), 'DARK NIGHTS  /  CL-04 生物遭遇素材', font=font(31), fill=INK)
    d.text((36, 69), '原生像素绘制 · 透明 PNG · 首批造型候选 · 尚未接入 Unity', font=font(18), fill=MUTED)
    y = 118
    for group, size, pivot, draw, states in specs:
        d.text((36, y), f'{NAMES[group]}  {size[0]} × {size[1]} px', font=font(22), fill=INK)
        for i, (state, frame) in enumerate(zip(states, groups[group])):
            x = 36 + i * 172
            d.rounded_rectangle((x, y + 40, x + 155, y + 188), radius=7, fill=PANEL)
            stamp(sheet, frame, (x + (156 - frame.width * 4) // 2, y + 45), 4)
            d.text((x + 10, y + 163), STATES[state], font=font(15), fill=MUTED)
        y += 199
    for i, color in enumerate(palette.values()):
        x = 855 + i * 21
        d.rectangle((x, 1080, x + 18, 1094), fill=color)
    sheet.save(out / 'preview' / 'contact-sheet.png')
    # 同一像素密度的12px角色、16px参考格与候选怪物；画面只做离线摆放。
    native = Image.new('RGB', (320, 104), '#2c251e')
    tile = Image.open(repo / 'Game/Assets/DarkNights/Res/Art/Custom/CaveExploration/cave-rock.png').convert('RGB')
    nd = ImageDraw.Draw(native)
    native.paste(tile.crop((0, 0, 256, 16)), (0, 88))
    native.paste(tile.crop((0, 0, 64, 16)), (256, 88))
    nd.line((0, 88, 320, 88), fill='#776348')
    hero = Image.open(repo / 'Game/Assets/DarkNights/Res/Art/Original/sprites/spr_worker_idle/spr_worker_idle_0.png').convert('RGBA')
    placements = [(hero, 18, 76), (groups['ore-mimic'][0], 52, 58),
                  (groups['ore-mimic'][3], 103, 58), (groups['ambush-grub'][2], 157, 65),
                  (groups['egg-clutch'][0], 210, 65), (groups['burrow'][1], 267, 65)]
    for sprite, x, y in placements:
        native.paste(sprite, (x, y), sprite)
    native.save(out / 'preview' / 'scale-scene-1x.png')
    scene = Image.new('RGB', (1440, 600), BG)
    sd = ImageDraw.Draw(scene)
    sd.text((34, 20), '同一像素密度对照', font=font(29), fill=INK)
    sd.text((36, 66), '原角色 12×12 · 地形格 16 px · 下图为 4 倍最近邻放大；离线摆放，非运行截图', font=font(18), fill=MUTED)
    stamp(scene, native.convert('RGBA'), (80, 112), 4)
    for x, title in ((146, '原角色'), (310, '伪装'), (520, '显形'), (740, '伏击虫'), (950, '虫卵'), (1190, '巢穴')):
        sd.text((x, 540), title, font=font(18), fill=INK)
    scene.save(out / 'preview' / 'scale-scene-4x.png')
    write_gif(out, groups)
    write_viewer(out, groups, specs)


def write_gif(out, groups):
    loops = {'ore-mimic': [0, 0, 1, 1, 2, 3, 4, 3, 4, 5, 6, 3],
             'ambush-grub': [0, 1, 0, 1, 2, 2, 3, 4, 3, 4, 5, 6],
             'egg-clutch': [0, 0, 1, 0, 1, 2, 2, 3, 3, 3, 0, 0]}
    frames = []
    for i in range(12):
        image = Image.new('RGB', (800, 280), BG)
        d = ImageDraw.Draw(image)
        for j, (group, indices) in enumerate(loops.items()):
            x = 32 + j * 262
            d.text((x + 20, 18), NAMES[group], font=font(22), fill=INK)
            sprite = groups[group][indices[i]]
            stamp(image, sprite, (x + 24, 60 + (32 - sprite.height) * 5), 5)
        d.text((32, 248), '动作姿势预览；播放顺序与时长不代表正式玩法配置', font=font(15), fill=MUTED)
        frames.append(image)
    frames[0].save(out / 'preview' / 'states.gif', save_all=True, append_images=frames[1:],
                   duration=180, loop=0, disposal=2)


def write_viewer(out, groups, specs):
    sections = []
    for group, size, pivot, draw, states in specs:
        figures = ''.join(f'<figure><div class="pixel"><img width="{size[0] * 5}" height="{size[1] * 5}" '
                          f'src="../native/{group}/{state}.png" alt="{STATES[state]}"></div>'
                          f'<figcaption>{STATES[state]} · {state}</figcaption></figure>' for state in states)
        sections.append(f'<section><h2>{NAMES[group]} <small>{size[0]}×{size[1]} px</small></h2>'
                        f'<div class="grid">{figures}</div></section>')
    html = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>CL-04 素材审阅</title>
<style>*{box-sizing:border-box}body{margin:32px;background:#191917;color:#d4c7ab;font:15px system-ui}h1{font-size:28px}h2{font-size:22px}p,small,figcaption{color:#a0957f}small{font-size:14px}button{background:#373226;color:#ead8b1;border:1px solid #796b4b;padding:9px 16px;border-radius:6px;cursor:pointer}.grid{display:flex;flex-wrap:wrap;gap:10px}figure{margin:0}.pixel{width:180px;height:178px;display:flex;align-items:center;justify-content:center;background:var(--paper,#29251f)}img{image-rendering:pixelated;image-rendering:crisp-edges}figcaption{font-size:12px;margin:8px 0 18px}section{margin-top:30px}.scene{max-width:100%;height:auto}footer{margin:40px 0;color:#a0957f}body.checker .pixel{background-color:#b0aca1;background-image:linear-gradient(45deg,#8e8b83 25%,transparent 25%),linear-gradient(-45deg,#8e8b83 25%,transparent 25%),linear-gradient(45deg,transparent 75%,#8e8b83 75%),linear-gradient(-45deg,transparent 75%,#8e8b83 75%);background-size:16px 16px;background-position:0 0,0 8px,8px -8px,-8px 0}
</style><h1>Dark Nights · CL-04 生物遭遇素材</h1><p>原生像素造型候选 · 透明 PNG · 尚未接入 Unity</p>
<button onclick="document.body.classList.toggle('checker')">切换透明边缘对照</button>
<button onclick="document.body.style.setProperty('--paper', '#111a26')">冷暗底</button>
<button onclick="document.body.style.setProperty('--paper', '#29251f')">岩层底</button>
<p><img src="states.gif" alt="状态姿势循环预览"></p>''' + ''.join(sections) + '''
<section><h2>原生比例与场景摆放</h2><p>下方第一张为真实 1×，第二张为 4×。仅离线摆放，未模拟照明。</p>
<img src="scale-scene-1x.png" alt="原生尺寸"><p><img class="scene" src="scale-scene-4x.png" alt="整数倍比例对照"></p></section>
<footer>帧名只表达美术意图，不定义伤害、碰撞、奖励、触发距离、状态转换或持续时间。角色朝右，左向拟用镜像，尚无独立左向帧。</footer></html>'''
    (out / 'preview' / 'index.html').write_text(html, encoding='utf-8')
