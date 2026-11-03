"""Author deterministic SVG terrain layers from explicit native blockout profiles."""
import hashlib
import json
import math
import random
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RECORDS = ROOT / "artifacts/vault-svg-styles-20261102"
SOURCES = ROOT / "美术素材/地图/20261102-vault-blockout-svg/Custom"
ASSETS = ROOT / "Game/Assets/DarkNights/Res/Scenes/Workbenches/Terrain/EntranceWhiteboxAssets/VaultStyles"
W, H = 1536, 960


def points(items):
    return [[round((x + .5) * .16, 4), round(y * .16, 4)] for x, y in items]


def xy(p):
    return [round((p[0] + .08) * 100, 4), round((4.8 - p[1]) * 100, 4)]


def path(points_, close=False):
    return "M" + " L".join(f"{x:.3f},{y:.3f}" for x, y in points_) + (" Z" if close else "")


def sample(line, x):
    for a, b in zip(line, line[1:]):
        if b[0] > a[0] and x < b[0]:
            t = max(0, min(1, (x - a[0]) / (b[0] - a[0])))
            return a[1] + (b[1] - a[1]) * t
    return line[-1][1]


def clip(poly, nx, ny, edge):
    result = []
    for a, b in zip(poly, poly[1:] + poly[:1]):
        da, db = a[0] * nx + a[1] * ny - edge, b[0] * nx + b[1] * ny - edge
        if da <= 1e-7:
            result.append(a)
        if (da < 0) != (db < 0):
            t = da / (da - db)
            result.append([a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t])
    return result


def facets(palette, seed):
    rng = random.Random(seed)
    sites = [[(x + rng.uniform(.18, .82)) * W / 13, (y + rng.uniform(.12, .88)) * H / 8]
             for y in range(8) for x in range(13)]
    shapes = []
    for i, a in enumerate(sites):
        poly = [[0, 0], [W, 0], [W, H], [0, H]]
        for j, b in enumerate(sites):
            if i == j:
                continue
            nx, ny = b[0] - a[0], b[1] - a[1]
            poly = clip(poly, nx, ny, (b[0] ** 2 + b[1] ** 2 - a[0] ** 2 - a[1] ** 2) / 2)
            if not poly:
                break
        if len(poly) < 3:
            continue
        fill = rng.choice(palette["faces"])
        shapes.append(f'<path d="{path(poly, True)}" fill="{fill}" stroke="{palette["ink"]}" stroke-opacity=".23" stroke-width="2"/>')
        if len(poly) >= 4:
            center = [sum(p[k] for p in poly) / len(poly) for k in [0, 1]]
            shapes.append(f'<path d="{path([poly[0], poly[1], center], True)}" fill="{palette["rim"]}" opacity=".09"/>')
        if i % 4 == 0:
            shapes.append(f'<path d="{path(poly[:3])}" fill="none" stroke="{palette["ink"]}" opacity=".45" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/>')
    return "".join(shapes)


def render(style, seed):
    p = style["palette"]
    floor = [[0, 0]] + points(style["floor"])
    roof = points(style["roof"])
    style["floor"] = floor
    style["ceiling"] = roof
    del style["roof"]
    fp, cp = [xy(v) for v in floor], [xy(v) for v in roof]
    fp[0][0] = 0
    rock = path(fp + [[W, H], [0, H]], True) + " " + path([[cp[0][0], 0], [W, 0]] + cp[::-1], True)
    cave = path(cp + [[W, xy([15.28, sample(floor, 15.28)])[1]]] +
                [xy(v) for v in floor[::-1] if v[0] >= roof[0][0]] +
                [[cp[0][0], xy([roof[0][0], sample(floor, roof[0][0])])[1]]], True)
    header = f'<svg xmlns="http://www.w3.org/2000/svg" width="1920" height="1200" viewBox="0 0 {W} {H}">'
    defs = f'<defs><clipPath id="rock"><path d="{rock}"/></clipPath><clipPath id="cave"><path d="{cave}"/></clipPath></defs>'
    terrain = [header, defs, '<g clip-path="url(#rock)">', f'<path d="{rock}" fill="{p["base"]}"/>', facets(p, seed)]
    for i in range(6):
        y = 130 + i * 145 + (seed % 4) * 5
        terrain.append(f'<path d="M-20,{y} C330,{y-38} 560,{y+58} 830,{y+21} S1290,{y-42} 1570,{y+40}" fill="none" stroke="{p["band"]}" stroke-width="{18+i%3*9}" opacity=".17"/>')
    terrain += [
        f'<path d="{path(fp)}" fill="none" stroke="{p["shadow"]}" stroke-width="34" stroke-linejoin="round"/>',
        f'<path d="{path(fp)}" fill="none" stroke="{p["rim"]}" stroke-width="15" stroke-linejoin="round"/>',
        f'<path d="{path(cp)}" fill="none" stroke="{p["rim"]}" stroke-opacity=".65" stroke-width="12" stroke-linejoin="round"/>',
        f'<path d="{path(fp)}" fill="none" stroke="{p["light"]}" stroke-width="3" stroke-linejoin="round"/>',
        f'<path d="{path(cp)}" fill="none" stroke="{p["ink"]}" stroke-width="2.2" stroke-linejoin="round"/>',
        '</g></svg>'
    ]
    background = [
        header,
        f'<defs><clipPath id="cave"><path d="{cave}"/></clipPath>'
        f'<linearGradient id="sky" x2="0" y2="1"><stop stop-color="{p["sky"]}"/><stop offset="1" stop-color="{p["haze"]}"/></linearGradient>'
        f'<linearGradient id="wall"><stop stop-color="{p["wallNear"]}"/><stop offset="1" stop-color="{p["wallFar"]}"/></linearGradient></defs>',
        '<rect width="1536" height="960" fill="url(#sky)"/>',
        f'<path d="M0,420 L0,280 L82,245 L121,181 L172,210 L218,150 L259,175 L295,247 L349,211 L406,240 L470,219 L550,280 L620,250 L720,420 Z" fill="{p["distant"]}" opacity=".45"/>',
        f'<path d="M0,440 L0,360 L67,327 L136,359 L224,288 L295,342 L356,320 L430,369 L520,337 L620,440 Z" fill="{p["distant"]}" opacity=".23"/>',
        '<g clip-path="url(#cave)">', f'<path d="{cave}" fill="url(#wall)"/>'
    ]
    for layer in range(3):
        y = 270 + layer * 122
        background.append(f'<path d="M300,{y+210} L300,{y+44} L384,{y+12} L454,{y+40} L550,{y-17} L620,{y+16} L714,{y-38} L830,{y+6} L950,{y-21} L1070,{y+15} L1180,{y-10} L1300,{y+30} L1440,{y-7} L1560,{y+20} L1560,960 L300,960 Z" fill="{p["distant"]}" opacity="{.12 + layer*.045:.3f}"/>')
    rng = random.Random(seed + 1)
    for i in range(15):
        x, y = rng.uniform(420, 1490), rng.uniform(260, 850)
        pts = [[x, y], [x-18, y+29], [x+11, y+44], [x+3, y+82]]
        background.append(f'<path d="{path(pts)}" fill="none" stroke="{p["wallLine"]}" opacity=".21" stroke-width="3" stroke-linejoin="round"/>')
    background += ['</g></svg>']
    return "".join(terrain), "".join(background)


def main():
    RECORDS.mkdir(exist_ok=False)
    SOURCES.mkdir(parents=True, exist_ok=False)
    ASSETS.mkdir(parents=True, exist_ok=False)
    protected = [p for parent in [ROOT/"Game/Assets/DarkNights/Res/Scenes", ROOT/"Game/Assets/DarkNights/Res/Objects/Hero"] for p in parent.rglob("*") if p.is_file()]
    (RECORDS/"protected-inputs.json").write_text(json.dumps([
        {"path":p.relative_to(ROOT).as_posix(), "sha256":hashlib.sha256(p.read_bytes()).hexdigest()}
        for p in protected], ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    styles = [
        {"id":"M01-Strata","name":"暖棕层岩","folder":"Strata","scene":"CaveVaultStrata",
         "floor":[(20,0),(20,-2),(28,-2),(28,-4),(36,-4),(36,-7),(44,-7),(44,-10),(60,-10),(60,-8),(66,-8),(66,-5),(74,-5),(74,-3),(82,-3),(82,-6),(95,-6)],
         "roof":[(22,20),(30,20),(30,22),(42,22),(42,20),(52,20),(52,18),(58,18),(58,20),(70,20),(70,16),(78,16),(78,12),(86,12),(86,8),(95,8)],
         "palette":{"base":"#624237","faces":["#614338","#6b4b3c","#78513d","#5a3d35","#705144","#654a3e"],"ink":"#322f30","rim":"#cfab74","light":"#f3d49b","shadow":"#8a6545","band":"#a47852","sky":"#a5d2d7","haze":"#e6dec3","distant":"#697e88","wallNear":"#596264","wallFar":"#343b4d","wallLine":"#a3a7a1"}},
        {"id":"M01-Moss","name":"青绿岩拱","folder":"Moss","scene":"CaveVaultMoss",
         "floor":[(24,0),(40,-8),(52,-14),(64,-14),(76,-8),(80,-6),(95,-6)],
         "roof":[(24,18),(32,22),(44,22),(56,16),(64,16),(72,12),(80,8),(84,6),(95,6)],
         "palette":{"base":"#31514e","faces":["#33544f","#3b5e54","#45655a","#2d484a","#3c5954","#4c6c5c"],"ink":"#253b3e","rim":"#a9b97d","light":"#e0d5a3","shadow":"#607b5c","band":"#8c9f75","sky":"#b9d8cc","haze":"#ece1be","distant":"#62898a","wallNear":"#54766b","wallFar":"#294d56","wallLine":"#a2c6b3"}},
        {"id":"M01-Blue","name":"冷蓝裂岩","folder":"Blue","scene":"CaveVaultBlue",
         "floor":[(20,0),(36,-8),(48,-8),(56,-12),(68,-18),(76,-18),(88,-12),(94,-6),(95,-6)],
         "roof":[(24,16),(32,20),(48,20),(56,16),(64,20),(72,16),(80,12),(88,8),(92,6),(95,6)],
         "palette":{"base":"#394b65","faces":["#3a526b","#425b73","#4a6178","#34435e","#45576b","#3b4e68"],"ink":"#252f47","rim":"#9bb4ba","light":"#d8dcc6","shadow":"#577c8e","band":"#ac9074","sky":"#b6d3e4","haze":"#dfe5d4","distant":"#6b8aa5","wallNear":"#597990","wallFar":"#293f61","wallLine":"#9cb6cc"}},
        {"id":"M01-Ochre","name":"赤赭断层","folder":"Ochre","scene":"CaveVaultOchre",
         "floor":[(20,0),(32,-6),(40,-10),(48,-10),(54,-4),(60,-4),(66,-10),(74,-10),(82,-6),(95,-6)],
         "roof":[(24,18),(32,22),(40,22),(44,20),(52,20),(60,16),(68,16),(76,12),(84,8),(88,6),(95,6)],
         "palette":{"base":"#824a3f","faces":["#845045","#925544","#9b6049","#754236","#895442","#a5684e"],"ink":"#452f32","rim":"#d6a273","light":"#f1cf9b","shadow":"#aa6c4b","band":"#e6af73","sky":"#c8d2c1","haze":"#f1d8ad","distant":"#93927a","wallNear":"#7e7968","wallFar":"#4a4d5c","wallLine":"#bbb298"}}
    ]
    outputs = []
    for i, style in enumerate(styles):
        terrain, background = render(style, 271+i*193)
        (ASSETS/style["folder"]).mkdir()
        for name, svg in [("Terrain",terrain),("Backdrop",background)]:
            source = SOURCES / (style["folder"]+"-"+name+".svg")
            source.write_text(svg+"\n", encoding="utf-8")
            outputs.append({"svg":str(source),"png":str(ASSETS/style["folder"]/(name+".png")),
                            "sha256":hashlib.sha256(source.read_bytes()).hexdigest()})
    manifest = {"nativeSize":[1920,1200],"viewBox":[1536,960],"pixelsPerUnityUnit":125,
                "worldRect":[-.08,-4.8,15.36,9.6],"layers":["transparent foreground terrain","independent backdrop"],
                "method":"Deterministic vector cells and strata, exact authored silhouette clipping; no image generation or copied art.",
                "profiles":styles,"outputs":outputs}
    (RECORDS/"design-inputs.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(json.dumps({"profiles":len(styles),"svgs":len(outputs),"source":str(SOURCES)},ensure_ascii=False))


if __name__ == "__main__":
    main()
