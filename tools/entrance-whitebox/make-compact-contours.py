"""Resize actual contour geometry around the unchanged author hero, not the preview image."""
from pathlib import Path
import hashlib
import json

ROOT = Path(__file__).resolve().parents[2]
PREVIOUS = ROOT / "artifacts/natural-contour-blockout-20261102"
OUT = PREVIOUS / "compact"
SOURCE = ROOT / "美术素材/地图/20261102-compact-natural-entry"
OUT.mkdir(exist_ok=False)
(SOURCE / "Custom").mkdir(parents=True, exist_ok=False)
data = json.loads((PREVIOUS / "design-inputs.json").read_text(encoding="utf-8-sig"))
names = ["浅碗缓入", "岩肩浅厅", "短坡下凹"]


def path(points):
    result = f"M{points[0][0]} {points[0][1]}"
    for i in range(1, len(points), 3):
        result += " C" + " ".join(str(v) for p in points[i:i + 3] for v in p)
    return result


for index, profile in enumerate(data["profiles"]):
    profile["id"] = profile["id"].replace("M01-", "M01-Compact")
    profile["name"] = names[index]
    profile["description"] = "按参考图收紧的入口浅厅：短下行、小低地、短回升岩台；主角使用原场景4倍作者根缩放。"
    for key in ["floor", "floorSamples", "roof", "roofSamples"]:
        roof = key.startswith("roof")
        profile[key] = [[round(x * .75, 7), round(y * (.42 if roof else .5) + (.12 if roof else 0), 7)]
                        for x, y in profile[key]]
    for mesh in profile["meshes"]:
        roof = mesh["name"].startswith("Roof")
        backdrop = mesh["name"] == "CavityFade"
        vertices = []
        for x, y, z in mesh["vertices"]:
            if backdrop:
                next_y = -2.3 if y < 0 else 2.4
            else:
                next_y = y * (.42 if roof else .5) + (.12 if roof else 0)
                if mesh["name"] == "Ground" and y <= -3.49:
                    next_y = -2.3
            vertices.append([round(x * .75, 7), round(next_y, 7), z])
        mesh["vertices"] = vertices
    low = min((p for p in profile["floorSamples"] if 2 < p[0] < 4.4), key=lambda p: p[1])
    profile["heroX"], profile["heroY"] = low
    profile["heroScale"] = 4
    floor = path(profile["floor"])
    roof = path(profile["roof"])
    # String-independent join: the first command always ends before the first C.
    floor_fill = "M-.3 0 L0 0 " + floor[floor.index("C"):] + f" L7.5 {profile['floor'][-1][1]} L7.5 -2.3 L-.3 -2.3Z"
    roof_fill = roof + f" L7.5 {profile['roof'][-1][1]} L7.5 2.4 L{profile['roof'][0][0]} 2.4Z"
    palette = data["palette"]
    svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="640" viewBox="-.24 -1.756 7.68 3.072">
<title>{profile["name"]} · 紧凑入口纯色白模</title>
<defs><linearGradient id="cavity" x1=".4125" y1="0" x2="2.55" y2="0" gradientUnits="userSpaceOnUse"><stop stop-color="{palette["cavity"]}" stop-opacity="0"/><stop offset="1" stop-color="{palette["cavity"]}"/></linearGradient></defs>
<rect x="-.24" y="-1.756" width="7.68" height="3.072" fill="{palette["sky"]}"/>
<g transform="scale(1,-1)">
<rect x=".4125" y="-2.3" width="7.1" height="4.7" fill="url(#cavity)"/>
<path id="ground" d="{floor_fill}" fill="{palette["rock"]}"/>
<path id="roof" d="{roof_fill}" fill="{palette["rock"]}"/>
<path d="{floor}" fill="none" stroke="{palette["floorLip"]}" stroke-width=".009"/>
</g></svg>'''
    svg_path = SOURCE / "Custom" / (profile["folder"] + ".svg")
    svg_path.write_text(svg, encoding="utf-8")
    profile["svg"] = str(svg_path)
    profile["svgSha256"] = hashlib.sha256(svg_path.read_bytes()).hexdigest()
    profile["scaleMetrics"] = {
        "widthWorld": 7.2, "minimumFloorWorld": min(y for x, y in profile["floorSamples"]),
        "maximumRoofWorldInChamber": max(y for x, y in profile["roofSamples"] if 1.6 < x < 6),
        "authorHeroHeightWorld": .48192,
    }
data.update({
    "width": 7.2, "previousRejectedWidth": 9.6,
    "worldCamera": {"x": 3.6, "y": .22, "halfHeight": 1.536, "pixels": [1600, 640]},
    "sourceSvgSize": [1600, 640], "geometryTransform": {"x": .75, "floorY": .5, "roofY": .42, "roofOffset": .12},
    "reference": "C:/Users/Jobscn/AppData/Local/Temp/codex-clipboard-475589d2-1b2c-4481-9ab4-95de797f0796.png",
    "scope": "Compact entrance contour studies following the user's reference; unchanged real hero scale. Native visual mesh only, no gameplay/collision acceptance.",
})
(OUT / "design-inputs.json").write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
author = {k: v for k, v in data.items() if k != "profiles"}
author["profiles"] = [{k: v for k, v in p.items() if k not in ["meshes", "floorSamples", "roofSamples"]} for p in data["profiles"]]
(SOURCE / "designs.json").write_text(json.dumps(author, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"profiles": len(data["profiles"]), "width": data["width"],
                  "metrics": [p["scaleMetrics"] for p in data["profiles"]]}, ensure_ascii=False))
