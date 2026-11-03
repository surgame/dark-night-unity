from pathlib import Path
import hashlib
import json
from PIL import Image, ImageChops, ImageFilter

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
ARTIFACT = ROOT / "artifacts/terrain-render-bench-20261101"
RUN = ARTIFACT / "unity-r4"
report = json.loads((RUN / "report.json").read_text(encoding="utf-8"))
frozen = ARTIFACT / "input-960x640.json"
assert report["passed"] and report["unity"] == "6000.4.9f1"
assert report["sourceVersion"] == "terrain-render-bench-20261101-v4"
assert report["inputSha256"] == hashlib.sha256(frozen.read_bytes()).hexdigest()
assert report["width"] == 960 and report["height"] == 640
checks = {"date": "2026-11-01", "inputSha256": report["inputSha256"], "images": []}
for phase in ("initial", "broken"):
    images = {mode: Image.open(RUN / f"{mode}-{phase}.png").convert("RGB")
              for mode in ("baked", "strip", "atlas")}
    masks = {}
    for mode, im in images.items():
        assert im.size == (960, 640)
        bg = im.getpixel((480, 280))
        masks[mode] = Image.frombytes("L", im.size, bytes(255 if p != bg else 0 for p in im.getdata()))
        count = sum(v != 0 for v in masks[mode].getdata())
        assert 150000 < count < 450000, (mode, phase, count)
    for mode in ("baked", "strip"):
        a, b = masks[mode], masks["atlas"]
        # Coverage may differ by raster AA, but no missing terrain away from a 2px boundary.
        away_a = ImageChops.subtract(a, b.filter(ImageFilter.MaxFilter(5)))
        away_b = ImageChops.subtract(b, a.filter(ImageFilter.MaxFilter(5)))
        outside = sum(v != 0 for v in away_a.getdata()) + sum(v != 0 for v in away_b.getdata())
        assert outside < 30, (mode, phase, outside)
        checks["images"].append({"phase": phase, "mode": mode, "unmatchedBeyond2px": outside})
for mode in ("baked", "strip", "atlas"):
    initial = Image.open(RUN / f"{mode}-initial.png").convert("RGB")
    broken = Image.open(RUN / f"{mode}-broken.png").convert("RGB")
    # All operations are inside x=22..42, y=105..115; remote pixels must stay unchanged.
    delta = ImageChops.difference(initial, broken)
    assert delta.getbbox() is not None
    assert delta.crop((0, 0, 960, 440)).getbbox() is None, mode
    assert delta.crop((0, 0, 60, 640)).getbbox() is None, (mode, "remote left")
    assert delta.crop((530, 0, 960, 640)).getbbox() is None, (mode, "remote right")
    checks["images"].append({"mode": mode, "remotePixelsStable": True, "changedBounds": delta.getbbox()})
for row in report["rows"]:
    assert len(row["stable"]["values"]) == 45
    assert len(row["initial"]["values"]) == 3
    assert len(row["single"]["values"]) == 48
    assert len(row["blast"]["values"]) == 36
    assert row["draws"] > 0 and row["vertices"] > 0
report["sourceLabel"] = (
    "Unity 6000.4.9f1 · RTX 5060 Ti · D3D12 · 3轮。冻结几何的网格装配／上传＋绘制＋同步读回；"
    "不含轮廓提取、碰撞、光照、生态或联网，不是前台帧率。纹理为未压缩、整图常驻的策略缓存。"
)
(HERE / "measured-unity.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
checks["passed"] = True
(RUN / "assessment.json").write_text(json.dumps(checks, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps(checks, ensure_ascii=False))
