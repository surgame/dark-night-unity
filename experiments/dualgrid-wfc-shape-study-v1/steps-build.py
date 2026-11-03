from pathlib import Path
from datetime import datetime
import base64
import hashlib
import json
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
OLD = ROOT / "experiments/cave-wfc-whitebox-v1"
OUTPUT = Path(sys.argv[1]).resolve()
sources = [OLD / name for name in ("plan.js", "catalog.js", "solver.js")]
sources += [HERE / name for name in (
    "model.js", "macro.js", "hybrid.js", "renderer.js",
    "steps-trace.js", "steps-renderer.js", "steps-app.js")]
hero = ROOT / "美术素材/地图/20261029-wfc-cave-kit/sources/hero-scale-reference.png"
parts = [(HERE / "steps-view.html").read_text(encoding="utf-8")]
parts.append('<script>window.SHAPE_STUDY_HERO="data:image/png;base64,'
             + base64.b64encode(hero.read_bytes()).decode("ascii") + '";</script>')
for source in sources:
    parts.append("<script>\n" + source.read_text(encoding="utf-8") + "\n</script>")
OUTPUT.write_text("\n".join(parts) + "\n", encoding="utf-8")
assert OUTPUT.stat().st_size < 1_000_000
record = {
    "date": "2026-10-31", "scope": "Independent real algorithm steps; no Unity changes.",
    "output": str(OUTPUT), "bytes": OUTPUT.stat().st_size,
    "sha256": hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),
    "sources": [{"path": str(p), "sha256": hashlib.sha256(p.read_bytes()).hexdigest()}
                for p in [HERE / "steps-view.html", *sources, Path(__file__).resolve(), hero]]
}
receipt = HERE / "records" / (datetime.now().strftime("%Y%m%d-%H%M%S-%f") + "-steps-build.json")
receipt.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"output": str(OUTPUT), "bytes": record["bytes"], "record": str(receipt)}, ensure_ascii=False))
