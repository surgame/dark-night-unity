"""Offline geometric counterexample; no Unity, author assets, or screenshot pixels are changed."""
from pathlib import Path
import hashlib
import json
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties
from matplotlib.patches import Polygon

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[1]
FONT = FontProperties(fname="C:/Windows/Fonts/msyh.ttc")
WALL = np.array([[-1., 0.], [3., 2.], [5., 1.], [5., -6.], [-1., -6.]])
LIGHT = np.array([.6, 2.4])
FORWARD = np.array([-.6, -.8])
PERP = np.array([.8, -.6])
RANGE, INTENSITY, HALF_WIDTH = 24., 2.78, .967 / 2
FEATHER, CONE, GAIN, WALL_DEPTH = .119, 46.2, 2., .375
NORMALS = np.roll(WALL, -1, axis=0) - WALL
NORMALS = np.stack([-NORMALS[:, 1], NORMALS[:, 0]], axis=1)
NORMALS /= np.linalg.norm(NORMALS, axis=1)[:, None]


def smoothstep(lo, hi, value):
    t = np.clip((value - lo) / (hi - lo), 0., 1.)
    return t * t * (3 - 2 * t)


def solid(points):
    return np.all(np.sum((points[..., None, :] - WALL) * NORMALS, axis=-1) <= 1e-9, axis=-1)


def interval(origin, points):
    """Analytic convex-polygon clipping: interval of the ray segment inside rock."""
    delta = points - origin
    enter, leave = np.zeros(points.shape[:-1]), np.ones(points.shape[:-1])
    valid = np.ones(points.shape[:-1], dtype=bool)
    for start, normal in zip(WALL, NORMALS):
        side = np.sum((origin - start) * normal, axis=-1)
        rate = np.sum(delta * normal, axis=-1)
        crossing = -side / np.where(np.abs(rate) > 1e-12, rate, 1.)
        enter = np.where(rate < -1e-12, np.maximum(enter, crossing), enter)
        leave = np.where(rate > 1e-12, np.minimum(leave, crossing), leave)
        valid &= ~((np.abs(rate) <= 1e-12) & (side > 1e-9))
    valid &= leave - enter > 1e-7
    return enter, leave, valid


def visibility(origin, points, wall_depth=0.):
    enter, _, hits = interval(origin, points)
    target_solid = solid(points)
    depth = (1 - enter) * np.linalg.norm(points - origin, axis=-1)
    value = np.where(hits, 0., 1.)
    if wall_depth > 0:
        value = np.where(target_solid & hits, np.clip(1 - depth / wall_depth, 0, 1), value)
    return np.where(solid(np.asarray(origin)), 0., value)


def beam(points):
    delta = points - LIGHT
    axial = delta @ FORWARD
    lateral = np.maximum(0, np.abs(delta @ PERP) - HALF_WIDTH)
    norm = np.sqrt(axial * axial + lateral * lateral)
    cosine = np.divide(axial, norm, out=np.ones_like(axial), where=norm > .0001)
    cutoff = np.cos(np.deg2rad(CONE / 2))
    return np.where(axial < 0, 0., smoothstep(cutoff, cutoff + FEATHER, cosine))


def direct(points, wall_depth=.375):
    d = np.linalg.norm(points - LIGHT, axis=-1)
    power = beam(points) * np.clip(1 - d / RANGE, 0, 1) ** 1.15 * INTENSITY
    visible = np.zeros_like(d)
    for s in range(-2, 3):
        mouth = LIGHT + PERP * (s * .5) * max(HALF_WIDTH, .22)
        visible += visibility(LIGHT, mouth) * visibility(mouth, points, wall_depth) * (3 - abs(s)) / 9
    return power * visible


def incident(points):
    distance = np.linalg.norm(points - LIGHT, axis=-1)
    value = direct(points + NORMALS[0] * .003, 0.)
    value = np.where(distance < 6, value, 0.)
    quality = value * np.clip(np.sum(-(points - LIGHT) / np.maximum(distance[..., None], .001) * NORMALS[0], axis=-1), 0, 1)
    return value, quality


def prepare():
    a, b = WALL[:2]
    length = np.linalg.norm(b - a)
    tangent = (b - a) / length
    steps = int(np.clip(np.ceil(length / .125), 32, 128))
    scan = a + np.arange(steps + 1)[:, None] / steps * (b - a)
    lit = np.flatnonzero(incident(scan)[1] > 1e-6)
    first, last = max(0, lit[0] - 1), min(steps, lit[-1] + 1)
    start = a + first / steps * (b - a)
    lit_length = length * (last - first) / steps
    nodes = start + np.linspace(0, lit_length, 33)[:, None] * tangent
    density, quality = incident(nodes)
    weights = np.ones(33)
    weights[[0, -1]] = .5
    mean = np.sum(density * quality * weights) / np.sum(quality * weights)
    distance = np.sum(np.linalg.norm(nodes - LIGHT, axis=1) * quality * weights) / np.sum(quality * weights)
    return start, tangent, lit_length, density, mean, distance


START, TANGENT, LENGTH, DENSITY, MEAN, WALL_DISTANCE = prepare()


def positive_excess(target, base):
    x, epsilon = target - base, .04 * target
    return np.where((target <= 0) | (x <= 0), 0., np.where(x < epsilon, x * x / np.maximum(2 * epsilon, 1e-20), x - .5 * epsilon))


def diffuse(points, base, radius=4.69, integrated_visibility=False, decouple_fade=False):
    fade = float(1 - smoothstep(5, 6, WALL_DISTANCE))
    reach = radius if decouple_fade else radius * fade
    delta = points - START
    along, normal = delta @ TANGENT, delta @ NORMALS[0]
    mass, weight, visible_energy, total_energy = [np.zeros(points.shape[:-1]) for _ in range(4)]
    step = LENGTH / 32
    for j in range(32):
        for fraction in (.2113248654, .7886751346):
            at = (j + fraction) * step
            kernel = 1 - smoothstep(0, reach, np.sqrt((at - along) ** 2 + normal ** 2))
            w = kernel * step * .5
            density = DENSITY[j] * (1 - fraction) + DENSITY[j + 1] * fraction
            mass += density * w
            weight += w
            if integrated_visibility:
                emitter = START + TANGENT * at + NORMALS[0] * .003
                e = (.8 * density + .2 * MEAN) * w
                total_energy += e
                visible_energy += e * visibility(emitter, points, WALL_DEPTH)
    local = mass / np.maximum(weight, 1e-20)
    coverage = np.clip(weight / min(reach, LENGTH), 0, 1)
    target = GAIN * (.8 * local + .2 * MEAN)
    if integrated_visibility:
        visible = visible_energy / np.maximum(total_energy, 1e-20)
    else:
        v_sum, v_weight = np.zeros_like(along), np.zeros_like(along)
        inset = min(.01, LENGTH * .25)
        for probe in range(2):
            offset = 0 if probe == 0 else np.clip((LENGTH * .5 - along) * .5, -reach * .65, reach * .65)
            at = np.clip(along + offset, inset, LENGTH - inset)
            emitter = START + at[..., None] * TANGENT + NORMALS[0] * .003
            w = 1 / (.04 + np.sum((points - emitter) ** 2, axis=-1))
            v_sum += visibility(emitter, points, WALL_DEPTH) * w
            v_weight += w
        visible = v_sum / np.maximum(v_weight, 1e-20)
    value = positive_excess(target, base) * np.clip(coverage * visible * fade, 0, 1)
    return np.where((mass > 1e-8) & (weight > 1e-8), value, 0.)


def main():
    probes = {"U_visible": [.1, 1.6], "P_shadow": [-1.25, -1.], "D_direct": [-3.5, -3.5], "R_radius": [-5.6, 1.], "S_wall_skin": [-.9, -1.]}
    p = np.array(list(probes.values()))
    base = direct(p)
    modes = {"current": {}, "radius_6": {"radius": 6.}, "integrated_visibility": {"integrated_visibility": True}, "decoupled_fade": {"decouple_fade": True}}
    table = {}
    for name, point, d in zip(probes, p, base):
        idx = list(probes).index(name)
        table[name] = {"position": point.tolist(), "signed_slope_distance": float((point - WALL[0]) @ NORMALS[0]), "direct": float(d)}
        for mode, kwargs in modes.items():
            bounce = diffuse(p, base, **kwargs)[idx]
            table[name][mode] = {"bounce": float(bounce), "total": float(d + bounce)}
    x, y = np.linspace(-6.5, 5.5, 300), np.linspace(-5, 4, 240)
    xx, yy = np.meshgrid(x, y)
    grid = np.stack([xx, yy], axis=-1)
    primary = direct(grid)
    fields = {name: primary + diffuse(grid, primary, **kwargs) for name, kwargs in modes.items()}
    air = ~solid(grid)
    selected_emitter = START + TANGENT * (LENGTH * .5) + NORMALS[0] * .003
    blocked_samples = [float(visibility(START + TANGENT * at + NORMALS[0] * .003, p[1])) for at in np.linspace(.01, LENGTH - .01, 257)]
    front_side = air & (((grid - WALL[0]) @ NORMALS[0]) > .01)
    floor_patch = np.array([-3.3, -2.5])
    floor_distance = float(np.linalg.norm(floor_patch - LIGHT))
    floor_fade = float(1 - smoothstep(5, 10, floor_distance))
    patch_to_shadow = float(np.linalg.norm(floor_patch - p[1]))
    optional_patch = {"scope": "Conditional path check only: a hypothetical additional lit surface, absent from the plotted convex-only model. Not a complete wall-profile or predicted brightness.",
                      "position": floor_patch.tolist(), "normal": [0, 1], "incident_direct": float(direct(floor_patch + [0, .003], 0)),
                      "light_distance": floor_distance, "patch_to_P_distance": patch_to_shadow,
                      "patch_to_P_visibility": float(visibility(floor_patch + [0, .003], p[1])),
                      "eligible_at_current_far_6": bool(floor_distance < 6), "eligible_at_far_10": bool(floor_distance < 10),
                      "effective_reach_at_far_10": 4.69 * floor_fade,
                      "radial_kernel_at_P_with_far_10": float(1 - smoothstep(0, 4.69 * floor_fade, patch_to_shadow))}
    source_files = ["WallDiffuseCompose.hlsl", "WallDiffusePrepare.hlsl", "WallDiffuseShape.hlsl", "WallDiffuseVisibility.hlsl", "ExplorationLighting.compute"]
    sources = {name: hashlib.sha256((ROOT / "Game/Assets/DarkNights/Res/Shared/Lighting" / name).read_bytes()).hexdigest() for name in source_files}
    report = {"scope": "Approximate convex slope model, not a reconstruction of screenshot geometry or a Unity GPU test.",
              "approximations": ["Continuous analytic convex-polygon occlusion replaces the game grid DDA and direct fixed-step traversal.", "One lit planar face; no other cavern walls, profile selection overflow, normal maps, reconstruction, or multi-bounce transport.", "Incident evaluation uses the 0.003 outward surface offset, giving tiny numerical differences from game incident evaluation at the exact hit.", "Integrated variant uses energy-weighted visibility at 64 Gauss samples, preserving the baseline excess-light composition."],
              "geometry": {"wall": WALL.tolist(), "light": LIGHT.tolist(), "forward": FORWARD.tolist(), "slope_angle_degrees": float(np.degrees(np.arctan(.5)))},
              "profile": {"cropped_start": START.tolist(), "lit_length": float(LENGTH), "mean_incident": float(MEAN), "weighted_distance": float(WALL_DISTANCE), "fade": float(1 - smoothstep(5, 6, WALL_DISTANCE))},
              "measurements": table, "conditional_additional_surface": optional_patch,
              "checks": {"shadow_visible_samples_out_of_257": int(np.count_nonzero(blocked_samples)), "max_front_air_side_current_vs_integrated": float(np.max(np.abs(fields['current'][front_side] - fields['integrated_visibility'][front_side]))),
                         "max_all_air_current_vs_integrated": float(np.max(np.abs(fields['current'][air] - fields['integrated_visibility'][air]))),
                         "all_air_difference_note": "Nonzero maximum is next to the convex endpoint: finite outward emitter offset and nearest-probe weighting can overestimate visible energy there. Equality is asserted only on the unobstructed front air side, not throughout the frame.",
                         "max_current_vs_decoupled": float(np.max(np.abs(fields['current'] - fields['decoupled_fade'])))}, "source_sha256": sources}
    (OUT / "result.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    fig, axes = plt.subplots(2, 2, figsize=(10, 8.5), layout="constrained")
    titles = ["A 当前：两探针，半径4.69格", "B 扩大光团：半径6格", "C 改为逐受光小段计算可见性", "D 几何：上斜面到P的路径穿过岩体"]
    for ax, title in zip(axes.flat, titles):
        ax.set_title(title, fontproperties=FONT, fontsize=12)
        ax.set_xlim(x[0], x[-1]); ax.set_ylim(y[0], y[-1]); ax.set_aspect("equal")
        ax.tick_params(labelsize=9)
        ax.set_xlabel("相对位置（格，示意尺度）", fontproperties=FONT, fontsize=10)
    for ax, key in zip(list(axes.flat)[:3], ["current", "radius_6", "integrated_visibility"]):
        im = ax.imshow(np.ma.masked_where(~air, fields[key]), extent=[x[0], x[-1], y[0], y[-1]], origin="lower", cmap="inferno", vmin=0, vmax=3.5)
        ax.set_facecolor("#11121a")
        ax.add_patch(Polygon(WALL, facecolor="#3b3c44", edgecolor="#a0a3ac", linewidth=1))
        ax.plot(LIGHT[0], LIGHT[1], "*", color="#67d9ef", markersize=11)
        ax.plot([START[0], START[0] + TANGENT[0] * LENGTH], [START[1], START[1] + TANGENT[1] * LENGTH], color="#77f2bd", linewidth=3)
        for label, point in probes.items():
            if label[0] in "PDR":
                ax.plot(*point, "+", color="#ffffff", markersize=7)
                ax.text(point[0] + .16, point[1] + .15, label[0], color="white", fontsize=10)
        ax.text(2.0, -2.0, "岩体", color="#d7d9de", fontproperties=FONT, fontsize=12)
    ax = axes.flat[3]
    ax.add_patch(Polygon(WALL, facecolor="#dbdde3", edgecolor="#666c78", linewidth=1))
    ax.plot(*LIGHT, "*", color="#007d9b", markersize=12)
    ax.text(LIGHT[0] + .2, LIGHT[1] + .3, "灯口", fontproperties=FONT, fontsize=11)
    ax.plot([START[0], START[0] + TANGENT[0] * LENGTH], [START[1], START[1] + TANGENT[1] * LENGTH], color="#008852", linewidth=4)
    for idx, point in enumerate([START + TANGENT * .1, selected_emitter, START + TANGENT * (LENGTH - .1)]):
        ax.plot([point[0], p[1, 0]], [point[1], p[1, 1]], color="#bd3547", alpha=.8, linewidth=1.5)
    ax.plot([LIGHT[0], p[2, 0]], [LIGHT[1], p[2, 1]], color="#008ab0", linewidth=1.5)
    ax.text(-4.8, -2.6, "直射绕过左端", fontproperties=FONT, fontsize=10)
    ax.text(-3.6, -.8, "P：背坡暗带", fontproperties=FONT, fontsize=10)
    ax.text(1.2, -2.0, "岩体", fontproperties=FONT, fontsize=12)
    ax.plot(*p[1], "+", color="#bd3547", markersize=10)
    ax.plot(*p[2], "+", color="#008ab0", markersize=10)
    fig.suptitle("简化斜墙离线演算（非游戏实测）", fontproperties=FONT, fontsize=14)
    colorbar = fig.colorbar(im, ax=list(axes.flat), shrink=.65, fraction=.025, pad=.025)
    colorbar.set_label("相对照度（不含环境底光）", fontproperties=FONT, fontsize=10)
    fig.savefig(OUT / "slope-comparison.png", dpi=150)
    plt.close(fig)
    print(json.dumps({"profile": report["profile"], "measurements": table, "conditional_additional_surface": optional_patch, "checks": report["checks"]}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
