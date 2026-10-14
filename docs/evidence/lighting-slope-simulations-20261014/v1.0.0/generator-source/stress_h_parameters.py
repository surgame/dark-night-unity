"""H parameter ablation and fixed-parameter extreme geometric comparisons.
No game files, Unity process, assets, or old reports are modified.
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
spec = importlib.util.spec_from_file_location("tuning", OUT / "tune_existing_parameters.py")
t = importlib.util.module_from_spec(spec)
spec.loader.exec_module(t)
ORIGINAL_SHAPE = t.shape
H = dict(cone=60., reach=3., gain=1., match=False, softness=.65)
WALL = [[-1, 0], [3, 2], [5, 1], [5, -6], [-1, -6]]
REFERENCE = dict(name="参考位置", wall=WALL, light=[-.75, 1.1], angle=-93.44, ylim=[-4, 3.3])
ABLATIONS = [
    ("A 现状", "baseline", {}),
    ("B 只缩小返光半径至3格", "radius_only", dict(reach=3.)),
    ("C 只扩大直射锥角至60°", "cone_only", dict(cone=60.)),
    ("D 半径3格＋锥角60°", "radius_and_cone", dict(reach=3., cone=60.)),
    ("E 完整H", "full_h", H),
    ("F H恢复原返光半径4.69格", "h_large_radius", dict(H, reach=4.69)),
]
SCENES = [
    dict(name="向墙内移0.75格", wall=WALL, light=[0., 1.1], angle=-93.44, ylim=[-4, 3.3]),
    dict(name="贴墙：法向间隙约0.11格", wall=WALL, light=[-.75, .25], angle=-93.44, ylim=[-4, 3.3]),
    dict(name="朝墙内偏35°", wall=WALL, light=[-.75, 1.1], angle=-55., ylim=[-4, 3.3]),
    dict(name="沿斜面掠射", wall=WALL, light=[1., 1.9], angle=-153.435, ylim=[-4, 3.3]),
    dict(name="45°陡坡", wall=[[-1, 0], [3, 4], [5, 1], [5, -6], [-1, -6]], light=[-.75, 1.1], angle=-93.44, ylim=[-4, 4.6]),
    dict(name="约14°缓坡", wall=[[-1, 0], [3, 1], [5, .5], [5, -6], [-1, -6]], light=[-.75, 1.1], angle=-93.44, ylim=[-4, 3.3]),
    dict(name="远离墙：进入5～6格关闭带", wall=WALL, light=[0., 6.1], angle=-93.44, ylim=[-2, 7.]),
    dict(name="更远：受光墙距灯超过6格", wall=WALL, light=[0., 8.], angle=-93.44, ylim=[-2, 9.]),
]


def effective_shape(points, along, normal):
    fade = t.C["_fade"]
    if fade <= 1e-10 or not t.C["_profile"]:
        return np.zeros_like(along), np.zeros_like(along), np.ones_like(along) * .001
    radius = t.C.get("reach", 4.69)
    t.C["reach"] = radius * fade
    try:
        local, coverage, probe_radius = ORIGINAL_SHAPE(points, along, normal)
    finally:
        t.C["reach"] = radius
    return local, coverage * fade, probe_radius


t.shape = effective_shape


def configure(scene, overrides):
    t.C = dict(t.BASE, **overrides)
    m = t.m
    m.WALL = np.array(scene["wall"], dtype=float)
    edges = np.roll(m.WALL, -1, axis=0) - m.WALL
    m.NORMALS = np.stack([-edges[:, 1], edges[:, 0]], -1)
    m.NORMALS /= np.linalg.norm(m.NORMALS, axis=1)[:, None]
    m.LIGHT = np.array(scene["light"], dtype=float)
    angle = np.deg2rad(scene["angle"])
    m.FORWARD = np.array([np.cos(angle), np.sin(angle)])
    m.PERP = np.array([-m.FORWARD[1], m.FORWARD[0]])
    m.HALF_WIDTH = t.C["aperture"] * .5
    m.CONE = t.C.get("cone", 46.2)
    original_cutoff = np.cos(np.deg2rad(46.2 / 2))
    original_center = float(m.smoothstep(original_cutoff, original_cutoff + .119, 1))
    cutoff = np.cos(np.deg2rad(m.CONE / 2))
    center = float(m.smoothstep(cutoff, cutoff + .119, 1))
    m.INTENSITY = 2.78 * original_center / center
    t.C["actual_intensity"] = m.INTENSITY
    prepare()


def prepare():
    m = t.m
    a, b = m.WALL[:2]
    length = np.linalg.norm(b - a)
    tangent = (b - a) / length

    def incident(points):
        d = np.linalg.norm(points - m.LIGHT, axis=-1)
        value = t.direct(points + m.NORMALS[0] * .003)[0]
        value = np.where(d < 6, value, 0.)
        quality = value * np.clip(np.sum(-(points - m.LIGHT) / np.maximum(d[..., None], .001) * m.NORMALS[0], axis=-1), 0, 1)
        return value if t.C["match"] else quality, quality

    count = int(np.clip(np.ceil(length / .125), 32, 128))
    lit = np.flatnonzero(incident(a + np.arange(count + 1)[:, None] / count * (b - a))[1] > 1e-6)
    if not len(lit):
        m.START, m.TANGENT, m.LENGTH = a, tangent, length
        m.MEAN, m.DENSITY, m.WALL_DISTANCE = 0., np.zeros(33), None
        t.C["_fade"], t.C["_profile"] = 0., False
        return
    first, last = max(0, lit[0] - 1), min(count, lit[-1] + 1)
    m.START, m.TANGENT, m.LENGTH = a + first / count * (b - a), tangent, length * (last - first) / count
    nodes = m.START + np.linspace(0, m.LENGTH, 33)[:, None] * tangent
    density, quality = incident(nodes)
    weights = np.ones(33); weights[[0, -1]] = .5
    quality_sum = max(float(np.sum(quality * weights)), 1e-20)
    m.DENSITY = density
    m.MEAN = np.sum(density * quality * weights) / quality_sum if t.C["match"] else np.sum(density * weights) / 32
    m.WALL_DISTANCE = float(np.sum(np.linalg.norm(nodes - m.LIGHT, axis=1) * quality * weights) / quality_sum)
    t.C["_fade"] = float(1 - m.smoothstep(5, 6, m.WALL_DISTANCE))
    t.C["_profile"] = True


def evaluate(points):
    direct, q_direct = t.direct(points)
    returned, q_bounce = t.bounce(points, direct)
    total = direct + returned
    total = np.where(t.m.solid(points), np.minimum(.65, total), total)
    return total, direct, returned, dict(direct=q_direct, bounce=q_bounce, total=q_direct + q_bounce)


def boundary_metric():
    # Same rings and angles for every method; no reusing only the formerly favorable P point.
    radii = np.array([.5, 1., 1.5, 2., 2.5, 3.])
    angle = np.deg2rad(np.linspace(181, 265, 1025))
    points = t.m.WALL[0] + radii[:, None, None] * np.stack([np.cos(angle), np.sin(angle)], -1)
    total, direct, returned, _ = evaluate(points)
    jumps = np.abs(np.diff(returned, axis=1))
    at = np.unravel_index(np.argmax(jumps), jumps.shape)
    i, j = at
    total_jump = float(abs(total[i, j + 1] - total[i, j]))
    return dict(max_return_jump=float(jumps[at]), total_jump_at_that_boundary=total_jump,
                direct_at_sides=[float(direct[i, j]), float(direct[i, j + 1])],
                returned_at_sides=[float(returned[i, j]), float(returned[i, j + 1])],
                point=((points[i, j] + points[i, j + 1]) * .5).tolist(), radius=float(radii[i]))


def compute(scene, overrides, width=260, height=210):
    configure(scene, overrides)
    x = np.linspace(-5.2, 5.2, width)
    y = np.linspace(*scene["ylim"], height)
    points = np.stack(np.meshgrid(x, y), -1)
    field, direct, returned, queries = evaluate(points)
    record = dict(parameters=t.C.copy(), profile_distance=t.m.WALL_DISTANCE,
                  effective_radius=t.C.get("reach", 4.69) * t.C["_fade"],
                  metric=boundary_metric(), queries=queries,
                  lit_length=float(t.m.LENGTH), scene=scene)
    view = dict(field=field, wall=t.m.WALL.copy(), light=t.m.LIGHT.copy(), forward=t.m.FORWARD.copy(),
                profile_start=t.m.START.copy(), profile_end=t.m.START + t.m.TANGENT * t.m.LENGTH,
                profile=t.C["_profile"], metric=record["metric"], ylim=scene["ylim"])
    return view, record


def draw(ax, view, title):
    rgb = .20 * (.10 + view["field"][..., None] * np.array([1., .74, .435]))
    ax.imshow(t.srgb(np.clip(rgb, 0, 1)), extent=[-5.2, 5.2, *view["ylim"]], origin="lower")
    ax.add_patch(Polygon(view["wall"], facecolor="none", edgecolor="#a0a7af", linewidth=.8))
    ax.plot(*view["light"], "*", color="#67d9ef", markersize=8)
    ax.annotate("", xy=view["light"] + view["forward"] * .7, xytext=view["light"], arrowprops={"arrowstyle": "->", "color": "#67d9ef", "lw": 1})
    if view["profile"]:
        p, q = view["profile_start"], view["profile_end"]
        ax.plot([p[0], q[0]], [p[1], q[1]], color="#6ba68a", linewidth=1)
    else:
        ax.text(.03, .05, "无有效返光墙段", transform=ax.transAxes, color="#d3d7dd", fontproperties=FONT, fontsize=10)
    if view["metric"]["max_return_jump"] > .03:
        ax.plot(*view["metric"]["point"], "x", color="#f27979", markersize=6)
    ax.set_title(title, fontproperties=FONT, fontsize=12)
    ax.set_xlim(-5.2, 5.2); ax.set_ylim(*view["ylim"]); ax.set_aspect("equal")
    ax.set_xticks([]); ax.set_yticks([])


def main():
    ablations, records = [], {}
    for label, name, params in ABLATIONS:
        view, record = compute(REFERENCE, params, 320, 260)
        ablations.append((label, view))
        records[name] = record
    fig, axes = plt.subplots(3, 2, figsize=(10.5, 11), layout="constrained")
    for ax, (label, view) in zip(axes.flat, ablations):
        draw(ax, view, label)
    fig.suptitle("拆解H：半径与锥角分别起什么作用？\n60°方案均配平中心亮度；红×为采样环上的返光跳变位置", fontproperties=FONT, fontsize=14)
    fig.savefig(OUT / "h-ablation.png", dpi=150)
    plt.close(fig)
    extreme = {}
    for batch in range(2):
        fig, axes = plt.subplots(4, 2, figsize=(10.5, 14.5), layout="constrained")
        for row, scene in enumerate(SCENES[batch * 4:batch * 4 + 4]):
            current, current_record = compute(scene, {})
            h_view, h_record = compute(scene, H)
            ratio = h_record["queries"]["total"] / max(1, current_record["queries"]["total"])
            extreme[scene["name"]] = dict(baseline=current_record, H=h_record, query_ratio_H_to_baseline=ratio)
            draw(axes[row, 0], current, scene["name"] + "｜现状")
            draw(axes[row, 1], h_view, "固定H｜模型查询×" + format(ratio, ".2f"))
            print(scene["name"] + " complete", flush=True)
        fig.suptitle("H极端情况：每行同一场景，左现状／右固定H\n同曝光；红×标出仍存在的返光跳变；查询倍数不代表GPU耗时", fontproperties=FONT, fontsize=14)
        fig.savefig(OUT / ("h-extremes-" + str(batch + 1) + ".png"), dpi=150)
        plt.close(fig)
    report = dict(scope="H ablation and eight stress scenes. Controlled CPU approximations; no game assets or algorithms changed.",
                  H_parameters=H, reference=REFERENCE, ablations=records, extremes=extreme,
                  limitations=["Only one convex planar face generates reflected light; concave caves and multiple reflective faces are not represented.",
                               "Exact convex-polygon intersections substitute GPU grid DDA and direct fixed-step traversal.",
                               "All distant profiles apply BOTH existing distance fade factors: radius shrink and contribution amplitude. No unlit profile is fabricated.",
                               "Comparison uses constant albedo 0.20, warm light, ambient 0.10 and fixed sRGB display. No game albedo/normal relief/camera reconstruction.",
                               "Boundary metric searches six radii and 1025 angles around the convex endpoint. It is a diagnostic discontinuity measure, not a perceptual pass criterion or whole-scene maximum.",
                               "All H parameters are fixed across scenes; only authored position, direction and slope are changed.",
                               "Query counts omit traversal lengths, caches, preparation, dispatch and other GPU work."])
    (OUT / "h-stress-result.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(dict(ablations={name: rec["metric"] for name, rec in records.items()},
                          extremes={name: dict(baseline_jump=rec["baseline"]["metric"]["max_return_jump"],
                                               H_jump=rec["H"]["metric"]["max_return_jump"],
                                               H_effective_radius=rec["H"]["effective_radius"],
                                               H_profile=rec["H"]["parameters"]["_profile"],
                                               query_ratio=rec["query_ratio_H_to_baseline"]) for name, rec in extreme.items()}), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
