from pathlib import Path
from datetime import datetime
import base64
import hashlib
import json
import sys
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
OUT = Path(sys.argv[1]).resolve()
source = ROOT / "美术素材/地图/20261030-flat-cave-kit/sources"
assets = HERE / "derived"
assets.mkdir(exist_ok=True)
inputs = [source / n for n in ("rock.png", "floor-shelf.png", "ceiling-edge.png")]
hero = ROOT / "美术素材/地图/20261029-wfc-cave-kit/sources/hero-scale-reference.png"
inputs.append(hero)
# Deterministic runtime test textures; originals remain byte-identical.
specs = {
    "rock": (inputs[0], None, (64, 64)),
    "floor": (inputs[1], (0, 580, 1536, 850), (64, 4)),
    "ceiling": (inputs[2], (0, 0, 1536, 220), (64, 4)),
    "hero": (hero, None, (94, 103)),
}
images = {}
for name, (path, crop, size) in specs.items():
    im = Image.open(path).convert("RGBA")
    if crop:
        im = im.crop(crop)
    im = im.resize(size, Image.Resampling.LANCZOS)
    if name == "ceiling":
        im = im.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    target = assets / (name + ".png")
    im.save(target, optimize=True)
    images[name] = "data:image/png;base64," + base64.b64encode(target.read_bytes()).decode("ascii")
files = [HERE / n for n in ("model.js", "textures.js", "webgl.js", "benchmark.js", "app.js")]
parts = [(HERE / "view.html").read_text(encoding="utf-8")]
parts.append("<script>window.TERRAIN_IMAGES=" + json.dumps(images) + ";</script>")
measured = HERE / "measured-browser.json"
if measured.exists():
    parts.append("<script>window.TERRAIN_MEASURED=" + measured.read_text(encoding="utf-8") + ";</script>")
unity = HERE / "measured-unity.json"
if unity.exists():
    parts.append("<script>window.TERRAIN_UNITY=" + unity.read_text(encoding="utf-8") + ";</script>")
for p in files:
    parts.append("<script>\n" + p.read_text(encoding="utf-8") + "\n</script>")
OUT.write_text("\n".join(parts) + "\n", encoding="utf-8")
assert OUT.stat().st_size < 1_000_000
records = HERE / "records"
records.mkdir(exist_ok=True)
record = {"date": "2026-11-01", "output": str(OUT), "bytes": OUT.stat().st_size,
          "sha256": hashlib.sha256(OUT.read_bytes()).hexdigest(),
          "sources": [{"path": str(p), "sha256": hashlib.sha256(p.read_bytes()).hexdigest()}
                      for p in [HERE / "view.html", *files, *inputs, Path(__file__).resolve(),
                                *[p for p in (measured, unity) if p.exists()],
                                *sorted((ROOT / "Game/Assets/DarkNights/Scripts/Editor/Terrain/PerformanceStudy").glob("*.cs")),
                                ROOT / "Game/Assets/DarkNights/Scripts/Editor/Terrain/PerformanceStudy/TerrainRenderBench.shader"]],
          "textureContract": {"rock": [64, 64], "strips": [64, 4], "atlas": [288, 288],
                              "bakedChunk": [512, 512], "pixelsPerCell": 16,
                              "purpose": "Deterministic cropped/resized prototype textures, not final production tiles."}}
receipt = records / (datetime.now().strftime("%Y%m%d-%H%M%S-%f") + "-build.json")
receipt.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"output": str(OUT), "bytes": OUT.stat().st_size, "record": str(receipt)}, ensure_ascii=False))
