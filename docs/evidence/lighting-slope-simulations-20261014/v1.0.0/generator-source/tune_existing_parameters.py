"""Parameter-only experiments: same downward lamp, convex slope and current ray budgets.
No game files are modified. CPU analytic occlusion substitutes the game map traversal.
"""
from pathlib import Path
import importlib.util
import json
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties
from matplotlib.patches import Polygon

OUT = Path(__file__).resolve().parent
FONT = FontProperties(fname="C:/Windows/Fonts/msyh.ttc")
spec = importlib.util.spec_from_file_location("slope_model", OUT / "simulate_slope.py")
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
m.LIGHT = np.array([-.75, 1.1])
m.FORWARD = np.array([-.06, -np.sqrt(1 - .06 ** 2)])
m.PERP = np.array([-m.FORWARD[1], m.FORWARD[0]])
BASE = dict(softness=.22, aperture=.967, gain=2., match=True, probes=2, shape="SoftBlob",
            near_range=2.2, near_intensity=0., ambient=.1, spread=.65)
CASES = [
    ("A 现状", "baseline", {}),
    ("B 阴影柔度0.22→0.75", "softness", dict(softness=.75)),
    ("C 返光增益2→1", "gain_1", dict(gain=1.)),
    ("D 低成本组合", "low_cost", dict(gain=1., match=False, softness=.65)),
    ("E D＋弱近身环境散光", "near_weak", dict(gain=1., match=False, softness=.65, near_range=3.5, near_intensity=.25)),
    ("F D＋较强近身环境散光", "near_strong", dict(gain=1., match=False, softness=.65, near_range=4.5, near_intensity=.45)),
    ("G 返光探针2→4", "probes_4", dict(probes=4)),
    ("H 改沿墙光形＋增益1", "wall_band", dict(gain=1., shape="WallBand")),
    ("I D＋环境底光0.10→0.16", "ambient", dict(gain=1., match=False, softness=.65, ambient=.16)),
]
C = BASE.copy()


def direct(points):
    d = np.linalg.norm(points - m.LIGHT, axis=-1)
    power = m.beam(points) * np.clip(1 - d / m.RANGE, 0, 1) ** 1.15 * m.INTENSITY
    active = power > 0
    visible = np.zeros_like(d)
    for s in range(-2, 3):
        mouth = m.LIGHT + m.PERP * (s * .5) * max(m.HALF_WIDTH, C["softness"])
        visible[active] += m.visibility(m.LIGHT, mouth) * m.visibility(mouth, points[active], m.WALL_DEPTH) * (3 - abs(s)) / 9
    return power * visible, 5 * int(np.count_nonzero(active))


def prepare():
    a, b = m.WALL[:2]
    length = np.linalg.norm(b - a)
    tangent = (b - a) / length

    def sample(points):
        distance = np.linalg.norm(points - m.LIGHT, axis=-1)
        value = direct(points + m.NORMALS[0] * .003)[0]
        value = np.where(distance < 6, value, 0.)
        quality = value * np.clip(np.sum(-(points - m.LIGHT) / np.maximum(distance[..., None], .001) * m.NORMALS[0], axis=-1), 0, 1)
        return value if C["match"] else quality, quality

    steps = int(np.clip(np.ceil(length / .125), 32, 128))
    scanned = a + np.arange(steps + 1)[:, None] / steps * (b - a)
    lit = np.flatnonzero(sample(scanned)[1] > 1e-6)
    first, last = max(0, lit[0] - 1), min(steps, lit[-1] + 1)
    m.START = a + first / steps * (b - a)
    m.TANGENT, m.LENGTH = tangent, length * (last - first) / steps
    nodes = m.START + np.linspace(0, m.LENGTH, 33)[:, None] * tangent
    density, quality = sample(nodes)
    weights = np.ones(33); weights[[0, -1]] = .5
    m.DENSITY = density
    m.MEAN = np.sum(density * quality * weights) / np.sum(quality * weights) if C["match"] else np.sum(density * weights) / 32
    m.WALL_DISTANCE = np.sum(np.linalg.norm(nodes - m.LIGHT, axis=1) * quality * weights) / np.sum(quality * weights)


def near(points):
    d = np.linalg.norm(points - m.LIGHT, axis=-1)
    strength = np.clip(1 - d / C["near_range"], 0, 1) ** 1.1 * C["near_intensity"]
    active = strength > 0
    v = np.zeros_like(d)
    if np.any(active):
        radius = C["softness"] * .5
        value = 4 * m.visibility(m.LIGHT, points[active], m.WALL_DEPTH)
        for offset in ([radius, 0], [-radius, 0], [0, radius], [0, -radius]):
            value += m.visibility(m.LIGHT + offset, points[active], m.WALL_DEPTH)
        v[active] = value / 8
    return strength * v, 5 * int(np.count_nonzero(active))


def triangle_coverage(along, length, radius):
    a, b = np.clip(along / radius, -1, 1), np.clip((along - length) / radius, -1, 1)
    ca = np.where(a < 0, .5 * (a + 1) ** 2, 1 - .5 * (1 - a) ** 2)
    cb = np.where(b < 0, .5 * (b + 1) ** 2, 1 - .5 * (1 - b) ** 2)
    return np.maximum(0, ca - cb)


def shape(points, along, normal):
    reach, step = C.get("reach", 4.69), m.LENGTH / 32
    if C["shape"] == "WallBand":
        radius = np.minimum(.65 * reach, .03 + m.LENGTH * (.035 + C["spread"] * m.smoothstep(0, .8 * reach, np.maximum(0, normal))))
        total = np.zeros_like(along)
        for j in range(32):
            x0, x1 = j * step, (j + 1) * step
            slope = (m.DENSITY[j + 1] - m.DENSITY[j]) / step
            for side in range(2):
                lo = np.maximum(x0, along + (-radius if side == 0 else 0))
                hi = np.minimum(x1, along + (0 if side == 0 else radius))
                span = np.maximum(0, hi - lo)
                incident = m.DENSITY[j] + slope * (lo - x0)
                k = 1 + (lo - along) / radius if side == 0 else 1 - (lo - along) / radius
                ks = 1 / radius if side == 0 else -1 / radius
                total += incident * k * span + (incident * ks + slope * k) * span ** 2 * .5 + slope * ks * span ** 3 / 3
        coverage = triangle_coverage(along, m.LENGTH, radius)
        local = np.maximum(0, total / radius) / np.maximum(coverage, 1e-20)
        surface = (normal < 0) & m.solid(m.START + np.clip(along, 0, m.LENGTH)[..., None] * m.TANGENT + normal[..., None] * m.NORMALS[0])
        coverage *= 1 - m.smoothstep(0, reach, np.abs(normal))
        valid = ((normal >= 0) | surface) & (np.abs(normal) < reach) & (along > -radius) & (along < m.LENGTH + radius)
        return np.where(valid, local, 0.), np.where(valid, coverage, 0.), radius
    mass, weight = np.zeros_like(along), np.zeros_like(along)
    for j in range(32):
        for fraction in (.2113248654, .7886751346):
            offset = (j + fraction) * step - along
            k = 1 - m.smoothstep(0, reach, np.sqrt(offset ** 2 + normal ** 2))
            w = k * step * .5
            mass += (m.DENSITY[j] * (1 - fraction) + m.DENSITY[j + 1] * fraction) * w
            weight += w
    valid = (mass > 1e-8) & (weight > 1e-8)
    return np.where(valid, mass / np.maximum(weight, 1e-20), 0.), np.where(valid, np.clip(weight / min(reach, m.LENGTH), 0, 1), 0.), np.full_like(along, reach)


def bounce(points, primary):
    delta = points - m.START
    along, normal = delta @ m.TANGENT, delta @ m.NORMALS[0]
    local, coverage, radius = shape(points, along, normal)
    harmonic = 2 * local * m.MEAN / np.maximum(local + m.MEAN, 1e-6)
    target = C["gain"] * (.8 * local + .2 * (m.MEAN if C["match"] else harmonic))
    excess = m.positive_excess(target, primary)
    active = (local > 0) & (coverage > 0) & (excess > 0)
    v_sum, v_weight = np.zeros_like(primary[active]), np.zeros_like(primary[active])
    inset = min(.01, m.LENGTH * .25)
    nearest_at = np.clip(along[active], inset, m.LENGTH - inset)
    queries = 0
    for probe in range(C["probes"]):
        offset = 0 if probe == 0 else np.clip((m.LENGTH * .5 - along[active]) * .5, -radius[active] * .65, radius[active] * .65) if probe == 1 else radius[active] * (-.65 if probe == 2 else .65)
        at = np.clip(along[active] + offset, inset, m.LENGTH - inset)
        emitter = m.START + at[..., None] * m.TANGENT + m.NORMALS[0] * .003
        w = 1 / (.04 + np.sum((points[active] - emitter) ** 2, axis=-1))
        if probe == 0:
            v = m.visibility(emitter, points[active], m.WALL_DEPTH)
            nearest_v = v
            queries += len(v)
        else:
            changed = at != nearest_at
            v = nearest_v.copy()
            v[changed] = m.visibility(emitter[changed], points[active][changed], m.WALL_DEPTH)
            queries += int(np.count_nonzero(changed))
        v_sum += v * w; v_weight += w
    visible = np.zeros_like(primary)
    visible[active] = v_sum / np.maximum(v_weight, 1e-20)
    return excess * coverage * visible, queries


def evaluate(points):
    primary, q_direct = direct(points)
    spill, q_near = near(points)
    returned, q_bounce = bounce(points, primary + spill)
    energy = primary + spill + returned
    energy = np.where(m.solid(points), np.minimum(.65, energy), energy)
    return energy, {"direct": q_direct, "near": q_near, "bounce": q_bounce, "total": q_direct + q_near + q_bounce}


def srgb(x):
    return np.where(x <= .0031308, 12.92 * x, 1.055 * np.maximum(x, 0) ** (1 / 2.4) - .055)


def plot(fields, configs, warm, output_name):
    fig, axes = plt.subplots(3, 3, figsize=(12, 10.4), layout="constrained")
    for ax, (label, name, _) in zip(axes.flat, CASES):
        if warm:
            rgb = .20 * (configs[name]["ambient"] + fields[name][..., None] * np.array([1., .74, .435]))
            image = ax.imshow(srgb(np.clip(rgb, 0, 1)), extent=[-5.2, 4.4, -4, 3.3], origin="lower")
        else:
            image = ax.imshow(fields[name] + configs[name]["ambient"], extent=[-5.2, 4.4, -4, 3.3], origin="lower", cmap="inferno", vmin=0, vmax=3.6)
        ax.add_patch(Polygon(m.WALL, facecolor="none", edgecolor="#a0a7af", linewidth=.8))
        ax.plot(*m.LIGHT, "*", color="#67d9ef", markersize=8)
        ax.annotate("", xy=m.LIGHT + m.FORWARD * .6, xytext=m.LIGHT, arrowprops={"arrowstyle": "->", "color": "#67d9ef", "lw": 1})
        ax.text(1.8, -2, "岩体", color="#a0a7af", fontproperties=FONT, fontsize=10)
        ax.set_xlim(-5.2, 4.4); ax.set_ylim(-4, 3.3); ax.set_aspect("equal")
        ax.set_xticks([]); ax.set_yticks([])
        ax.set_title(label, fontproperties=FONT, fontsize=13)
        if "_query_ratio" in configs[name]:
            ax.text(.97, .035, "模型查询×" + format(configs[name]["_query_ratio"], ".2f"), transform=ax.transAxes,
                    ha="right", color="#cfd3d8", fontproperties=FONT, fontsize=11)
    if not warm:
        cb = fig.colorbar(image, ax=list(axes.flat), shrink=.6, fraction=.02, pad=.015)
        cb.set_label("直射＋返光＋环境底光", fontproperties=FONT, fontsize=11)
    subtitle = "统一灰色受光面／暖色照明／固定曝光；不是实际游戏截图" if warm else "同一墙形、灯口、向下朝向和亮度标尺"
    if any("_query_ratio" in config for config in configs.values()):
        subtitle += "\n查询倍数为离线模型计数，不是GPU耗时倍数"
    fig.suptitle("现有参数九组对照（离线近似）\n" + subtitle, fontproperties=FONT, fontsize=15)
    fig.savefig(OUT / output_name, dpi=150)
    plt.close(fig)


def main():
    global C
    x, y = np.linspace(-5.2, 4.4, 320), np.linspace(-4, 3.3, 260)
    xx, yy = np.meshgrid(x, y)
    grid = np.stack([xx, yy], -1)
    # Sample a fixed cross-section through the nearest-endpoint visibility boundary.
    emitter = m.WALL[0] + .01 * ((m.WALL[1] - m.WALL[0]) / np.linalg.norm(m.WALL[1] - m.WALL[0])) + m.NORMALS[0] * .003
    edge_direction = (m.WALL[0] - emitter) / np.linalg.norm(m.WALL[0] - emitter)
    anchor = m.WALL[0] + edge_direction * 2
    edge_normal = np.array([-edge_direction[1], edge_direction[0]])
    cross = np.linspace(-.15, .15, 301)
    line = anchor + cross[:, None] * edge_normal
    fields, configs, rows, profiles = {}, {}, {}, {}
    for label, name, overrides in CASES:
        C = dict(BASE, **overrides)
        configs[name] = C.copy()
        m.HALF_WIDTH = C["aperture"] * .5
        prepare()
        fields[name], counts = evaluate(grid)
        edge_values = evaluate(line)[0] + C["ambient"]
        profiles[name] = edge_values
        # Across 0.02 cells, not a claim that the analytic shadow is continuous.
        jump = float(abs(edge_values[160] - edge_values[140]))
        contrast = jump / max(float(edge_values[160] + edge_values[140]), 1e-9)
        rows[name] = {"label": label, "parameters": C.copy(), "queries_cpu_model": counts,
                      "edge_jump_across_0_02_cells": jump, "relative_edge_contrast": contrast,
                      "lit_length": float(m.LENGTH), "weighted_wall_distance": float(m.WALL_DISTANCE)}
        print(name + " complete", flush=True)
    for row in rows.values():
        row["query_count_ratio_to_baseline"] = row["queries_cpu_model"]["total"] / rows["baseline"]["queries_cpu_model"]["total"]
    plot(fields, configs, False, "existing-parameters-irradiance.png")
    plot(fields, configs, True, "existing-parameters-appearance.png")
    fig, ax = plt.subplots(figsize=(8, 4), layout="constrained")
    for name, label in [("baseline", "现状"), ("low_cost", "低成本组合"), ("near_weak", "组合＋弱近身散光"), ("wall_band", "沿墙形状")]:
        ax.plot(cross, profiles[name], label=label)
    ax.set_xlabel("横跨原横切边界的距离（格）", fontproperties=FONT)
    ax.set_ylabel("照度（含底光）", fontproperties=FONT)
    ax.set_title("固定位置剖面：降低跳变，与真正连续柔化是两回事", fontproperties=FONT)
    ax.legend(prop=FONT)
    fig.savefig(OUT / "existing-parameters-edge-profile.png", dpi=150)
    plt.close(fig)
    report = {"scope": "Existing parameter-only CPU approximations, not a GPU benchmark, scene reconstruction or accepted game fix.",
              "geometry": {"light": m.LIGHT.tolist(), "forward": m.FORWARD.tolist(), "wall": m.WALL.tolist()},
              "cross_section": {"anchor": anchor.tolist(), "normal": edge_normal.tolist(), "span": [-.15, .15], "step": .001},
              "cases": rows,
              "limitations": ["Analytic continuous convex geometry substitutes grid DDA and direct fixed-step traversal.", "Only one wall contributes reflected light; all cases have a near distance fade of 1.",
                              "SoftBlob and unjoined WallBand kernels follow current formulas; no extra feather, blur, bounce or visibility algorithm is enabled.",
                              "The near branch is the existing environment NearRange/NearIntensity shader path, not the sprite-only LocalLightFill.",
                              "Near emission origins are the lamp center and four existing softness offsets, matching the shader's branch.",
                              "CPU query counts exclude traversal lengths, preparation, shader dispatch, caches and GPU scheduling; count ratios are not GPU time ratios.",
                              "Appearance panels use uniform albedo 0.20, fixed exposure, linear warm RGB (1,.74,.435) and sRGB display; no game textures/normal relief/camera reconstruction.",
                              "A parameter-only contrast reduction does not make binary visibility continuous."]}
    (OUT / "existing-parameters-result.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({name: {"edge_jump": row["edge_jump_across_0_02_cells"], "edge_contrast": row["relative_edge_contrast"], "query_ratio": row["query_count_ratio_to_baseline"]} for name, row in rows.items()}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
