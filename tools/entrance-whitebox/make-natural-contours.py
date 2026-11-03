"""Create compact, untextured contour studies and deterministic native mesh inputs."""
from pathlib import Path
import hashlib
import json
import math

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "artifacts/natural-contour-blockout-20261102"
SOURCE = ROOT / "美术素材/地图/20261102-natural-contour-blockout"
CUSTOM = SOURCE / "Custom"
CUSTOM.mkdir(parents=True, exist_ok=False)
PALETTE = {
    "sky": "#b4d3d7", "rock": "#adb4a6", "floorShade": "#8d9d91",
    "floorLip": "#d8d3b6", "roofLip": "#c0c6b2", "cavity": "#304c57",
}


def curve(start, segments):
    points = [list(start)]
    for segment in segments:
        points.extend([list(p) for p in segment])
    return points


PROFILES = [
    {
        "id": "M01-Eroded", "name": "侵蚀拱厅", "folder": "Eroded",
        "description": "短入口进入不对称宽厅；低缓凹底与大小不同的岩肩，洞顶有自然收口。",
        "floor": curve((0, 0), [
            ((.3, .025), (.7, 0), (1.15, 0)),
            ((1.5, 0), (1.65, -.12), (1.95, -.28)),
            ((2.35, -.52), (2.25, -.7), (2.8, -.82)),
            ((3.25, -.9), (3.35, -1.35), (3.9, -1.38)),
            ((4.4, -1.35), (4.48, -1.53), (5, -1.6)),
            ((5.5, -1.65), (5.65, -1.5), (6.1, -1.47)),
            ((6.5, -1.5), (6.65, -1.32), (7, -1)),
            ((7.25, -.65), (7.45, -.75), (7.8, -.6)),
            ((8.1, -.45), (8.2, -.13), (8.65, -.16)),
            ((9, -.17), (9.15, -.32), (9.6, -.32)),
        ]),
        "roof": curve((1.1, 3.8), [
            ((1.05, 3), (1.15, 2.25), (1.7, 2.05)),
            ((2, 1.95), (2.08, 2.55), (2.5, 2.68)),
            ((2.94, 2.85), (3.25, 2.62), (3.6, 2.7)),
            ((4.03, 2.78), (4.18, 2.95), (4.75, 2.88)),
            ((5.25, 2.82), (5.4, 2.55), (5.9, 2.5)),
            ((6.2, 2.47), (6.55, 2.65), (6.88, 2.48)),
            ((7.1, 2.35), (7, 1.92), (7.38, 1.7)),
            ((7.75, 1.48), (8.05, 1.67), (8.35, 1.48)),
            ((8.75, 1.28), (9.03, 1.24), (9.6, 1.28)),
        ]),
    },
    {
        "id": "M01-Shoulder", "name": "岩肩抱厅", "folder": "Shoulder",
        "description": "圆钝岩肩包住洞厅，短岩台在厅内起伏；不做等宽台阶或统一圆角。",
        "floor": curve((0, 0), [
            ((.25, .015), (.65, 0), (.95, 0)),
            ((1.35, 0), (1.42, -.18), (1.8, -.27)),
            ((2.1, -.33), (2.3, -.2), (2.5, -.35)),
            ((2.78, -.56), (2.65, -1.02), (3.08, -1.19)),
            ((3.45, -1.33), (3.7, -1.56), (4.17, -1.6)),
            ((4.63, -1.65), (4.89, -1.48), (5.15, -1.22)),
            ((5.5, -.89), (5.5, -.69), (6, -.69)),
            ((6.3, -.69), (6.5, -.64), (6.7, -.83)),
            ((6.94, -1.06), (6.98, -1.31), (7.35, -1.3)),
            ((7.85, -1.28), (7.98, -.78), (8.37, -.62)),
            ((8.76, -.46), (9.15, -.48), (9.6, -.48)),
        ]),
        "roof": curve((1.3, 3.8), [
            ((1.21, 3), (1.29, 2.16), (1.73, 1.78)),
            ((1.95, 1.6), (2.17, 1.82), (2.42, 2.19)),
            ((2.71, 2.6), (3.05, 2.72), (3.5, 2.69)),
            ((3.96, 2.65), (4.18, 2.9), (4.64, 2.82)),
            ((5.01, 2.75), (5.05, 2.29), (5.4, 2.09)),
            ((5.65, 1.94), (5.8, 2.26), (6.11, 2.45)),
            ((6.45, 2.64), (6.78, 2.61), (7.07, 2.36)),
            ((7.4, 2.08), (7.25, 1.8), (7.66, 1.61)),
            ((8.05, 1.43), (8.22, 1.63), (8.61, 1.42)),
            ((8.94, 1.24), (9.27, 1.15), (9.6, 1.2)),
        ]),
    },
    {
        "id": "M01-Rift", "name": "偏心深厅", "folder": "Rift",
        "description": "入口较短，深处向一侧下沉；洞顶大弯与洞底起伏错位，强调紧凑的纵向空间。",
        "floor": curve((0, 0), [
            ((.25, .01), (.64, 0), (.95, 0)),
            ((1.3, 0), (1.53, -.08), (1.76, -.18)),
            ((2.05, -.3), (1.94, -.59), (2.24, -.87)),
            ((2.58, -1.18), (2.71, -1.1), (2.97, -1.46)),
            ((3.3, -1.9), (3.41, -2.08), (3.93, -2.12)),
            ((4.38, -2.16), (4.55, -1.97), (4.95, -2.01)),
            ((5.34, -2.05), (5.48, -1.69), (5.78, -1.57)),
            ((6.15, -1.42), (6.32, -1.48), (6.57, -1.23)),
            ((6.9, -.91), (6.95, -.55), (7.35, -.49)),
            ((7.8, -.42), (8.01, -.64), (8.42, -.58)),
            ((8.83, -.52), (9.18, -.64), (9.6, -.64)),
        ]),
        "roof": curve((1.05, 3.8), [
            ((1.02, 3.1), (1.07, 2.44), (1.48, 2.07)),
            ((1.83, 1.77), (2.2, 1.69), (2.6, 1.84)),
            ((3.04, 2), (2.95, 2.63), (3.45, 2.88)),
            ((3.9, 3.08), (4.2, 2.85), (4.6, 2.93)),
            ((5.03, 3.01), (5.17, 2.7), (5.54, 2.67)),
            ((5.94, 2.64), (6.17, 2.74), (6.49, 2.44)),
            ((6.81, 2.13), (6.7, 1.88), (7.03, 1.69)),
            ((7.38, 1.48), (7.76, 1.71), (8.05, 1.49)),
            ((8.38, 1.23), (8.49, 1.18), (8.86, 1.17)),
            ((9.14, 1.16), (9.4, 1.13), (9.6, 1.12)),
        ]),
    },
]


def midpoint(a, b):
    return [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2]


def distance_line(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    return abs(dx * (a[1] - p[1]) - (a[0] - p[0]) * dy) / max(math.hypot(dx, dy), 1e-9)


def sample_curve(points):
    result = [points[0]]

    def split(a, b, c, d, depth=0):
        if depth >= 12 or max(distance_line(b, a, d), distance_line(c, a, d)) <= .0018:
            result.append(d)
            return
        ab, bc, cd = midpoint(a, b), midpoint(b, c), midpoint(c, d)
        abc, bcd = midpoint(ab, bc), midpoint(bc, cd)
        m = midpoint(abc, bcd)
        split(a, ab, abc, m, depth + 1)
        split(m, bcd, cd, d, depth + 1)

    for i in range(0, len(points) - 1, 3):
        split(*points[i:i + 4])
    return result


def cross(a, b, c):
    return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def triangulate(poly):
    area = sum(p[0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * p[1] for i, p in enumerate(poly))
    ring = list(range(len(poly)))
    if area < 0:
        ring.reverse()
    result = []
    while len(ring) > 3:
        for j, b in enumerate(ring):
            a, c = ring[j - 1], ring[(j + 1) % len(ring)]
            if cross(poly[a], poly[b], poly[c]) <= 1e-10:
                continue
            blocked = False
            for p in ring:
                if p in (a, b, c):
                    continue
                if min(cross(poly[a], poly[b], poly[p]), cross(poly[b], poly[c], poly[p]), cross(poly[c], poly[a], poly[p])) > -1e-10:
                    blocked = True
                    break
            if blocked:
                continue
            result.extend([a, b, c])
            del ring[j]
            break
        else:
            raise ValueError("Contour is not a simple triangulatable polygon")
    result.extend(ring)
    return result


def polygon_mesh(name, poly, color, order):
    triangles = triangulate(poly)
    return {"name": name, "vertices": [[x, y, 0] for x, y in poly], "triangles": triangles,
            "colors": [color] * len(poly), "sorting": order}


def strip_mesh(name, points, thickness, color, order):
    vertices, triangles = [], []
    for x, y in points:
        vertices.extend([[x, y, -.01], [x, y + thickness, -.01]])
    for i in range(len(points) - 1):
        a = i * 2
        triangles.extend([a, a + 1, a + 2, a + 2, a + 1, a + 3])
    return {"name": name, "vertices": vertices, "triangles": triangles,
            "colors": [color] * len(vertices), "sorting": order}


def backdrop():
    mesh = {"name": "CavityFade", "vertices": [], "triangles": [], "colors": [], "sorting": -40}
    for x, alpha in [(.55, 0), (1.05, .04), (1.5, .15), (2, .4), (2.7, .8), (3.4, 1), (10, 1)]:
        mesh["vertices"].extend([[x, -3.5, 1], [x, 4.1, 1]])
        mesh["colors"].extend([PALETTE["cavity"] + f"{round(alpha * 255):02x}"] * 2)
    for i in range(len(mesh["vertices"]) // 2 - 1):
        a = i * 2
        mesh["triangles"].extend([a, a + 1, a + 2, a + 2, a + 1, a + 3])
    return mesh


def path_data(points):
    result = f"M{points[0][0]} {points[0][1]}"
    for i in range(1, len(points), 3):
        result += " C" + " ".join(str(v) for p in points[i:i + 3] for v in p)
    return result


native = []
for profile in PROFILES:
    floor, roof = sample_curve(profile["floor"]), sample_curve(profile["roof"])
    floor_poly = [[-.4, 0]] + floor + [[10, floor[-1][1]], [10, -3.5], [-.4, -3.5]]
    roof_poly = roof + [[10, roof[-1][1]], [10, 4.1], [roof[0][0], 4.1]]
    meshes = [
        polygon_mesh("Ground", floor_poly, PALETTE["rock"], 0),
        polygon_mesh("Roof", roof_poly, PALETTE["rock"], 0),
        strip_mesh("GroundInnerBand", [[-.4, 0]] + floor + [[10, floor[-1][1]]], -.075, PALETTE["floorShade"], 1),
        strip_mesh("GroundLip", [[-.4, 0]] + floor + [[10, floor[-1][1]]], -.018, PALETTE["floorLip"], 2),
        strip_mesh("RoofLip", roof + [[10, roof[-1][1]]], .025, PALETTE["roofLip"], 1),
        backdrop(),
    ]
    # Keep original Bezier commands as the editable SVG source; sampled mesh is derived.
    floor_path = "M-.4 0 L0 0" + path_data(profile["floor"])[len("M0 0"):] + f" L10 {floor[-1][1]} L10 -3.5 L-.4 -3.5 Z"
    roof_path = path_data(profile["roof"]) + f" L10 {roof[-1][1]} L10 4.1 L{roof[0][0]} 4.1 Z"
    svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="1536" height="960" viewBox="-.32 -3.28 10.24 6.4">
<title>{profile["name"]} · 纯色天然轮廓白模</title>
<defs><linearGradient id="cavity" x1=".55" y1="0" x2="3.4" y2="0" gradientUnits="userSpaceOnUse">
<stop stop-color="{PALETTE["cavity"]}" stop-opacity="0"/><stop offset="1" stop-color="{PALETTE["cavity"]}"/></linearGradient></defs>
<rect x="-.4" y="-3.28" width="10.4" height="6.4" fill="{PALETTE["sky"]}"/>
<g transform="scale(1,-1)">
<rect x=".55" y="-3.5" width="9.45" height="7.6" fill="url(#cavity)"/>
<path id="ground" d="{floor_path}" fill="{PALETTE["rock"]}"/>
<path id="roof" d="{roof_path}" fill="{PALETTE["rock"]}"/>
<path d="{path_data(profile["floor"])}" fill="none" stroke="{PALETTE["floorLip"]}" stroke-width=".018"/>
</g></svg>
'''
    svg_path = CUSTOM / (profile["folder"] + ".svg")
    svg_path.write_text(svg, encoding="utf-8")
    native.append({**profile, "floorSamples": floor, "roofSamples": roof, "meshes": meshes,
                   "svg": str(svg_path), "svgSha256": hashlib.sha256(svg_path.read_bytes()).hexdigest(),
                   "vertexCount": sum(len(m["vertices"]) for m in meshes)})

manifest = {
    "date": "2026-11-02", "width": 9.6, "previousWidth": 15.28,
    "worldCamera": {"x": 4.8, "y": .08, "halfHeight": 3.2, "pixels": [1600, 1000]},
    "sourceSvgSize": [1536, 960], "flatteningToleranceWorld": .0018,
    "palette": PALETTE, "profiles": native,
    "scope": "Native untextured visual contour studies. No grid collision, gameplay entry, transitions or items. Existing user Play is not stopped.",
}
(OUT / "design-inputs.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
author = {k: v for k, v in manifest.items() if k != "profiles"}
author["profiles"] = [{k: v for k, v in p.items() if k not in ["meshes", "floorSamples", "roofSamples"]} for p in native]
(SOURCE / "designs.json").write_text(json.dumps(author, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"profiles": len(native), "meshCount": sum(len(p["meshes"]) for p in native),
                  "vertices": [p["vertexCount"] for p in native], "source": str(SOURCE)}, ensure_ascii=False))
