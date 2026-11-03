from pathlib import Path
from datetime import datetime
import base64
import hashlib
import json
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
OUTPUT = Path(sys.argv[1]).resolve()
names = ["geometry.js", "solver.js", "meshes.js", "adapters.js", "gpu.js", "benchmark.js", "app.js"]
hero = ROOT / "美术素材/地图/20261029-wfc-cave-kit/sources/hero-scale-reference.png"
parts = [(HERE / "view.html").read_text(encoding="utf-8")]
for name in names:
    if name == "app.js":
        data = "data:image/png;base64," + base64.b64encode(hero.read_bytes()).decode("ascii")
        parts.append("<script>TerrainFour.hero=" + json.dumps(data) + ";</script>")
        reference = HERE / "reference.json"
        references = HERE / "references.json"
        if references.exists():
            for case in json.loads(references.read_text(encoding="utf-8")).values():
                for source in case["sources"]:
                    actual = hashlib.sha256((HERE / source["path"]).read_bytes()).hexdigest()
                    assert actual == source["sha256"], "Benchmark source changed: " + source["path"]
            parts.append("<script>TerrainFour.references=" + references.read_text(encoding="utf-8") + ";</script>")
        elif reference.exists():
            parts.append("<script>TerrainFour.reference=" + reference.read_text(encoding="utf-8") + ";</script>")
    parts.append("<script>\n" + (HERE / name).read_text(encoding="utf-8") + "\n</script>")
text = "\n".join(parts) + "\n"
assert len(text.encode("utf-8")) < 1_000_000
assert '\\n<div' not in text and '<div id=\\"' not in text
OUTPUT.write_text(text, encoding="utf-8")
sources = [HERE / "view.html", *[HERE / name for name in names], hero, Path(__file__).resolve()]
if (HERE / "references.json").exists():
    sources.append(HERE / "references.json")
elif (HERE / "reference.json").exists():
    sources.append(HERE / "reference.json")
record = {"date": "2026-11-01", "scope": "Independent four-way destructible terrain prototype; no Unity writes.",
          "output": str(OUTPUT), "bytes": OUTPUT.stat().st_size,
          "sha256": hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),
          "sources": [{"path": str(p), "sha256": hashlib.sha256(p.read_bytes()).hexdigest()} for p in sources]}
records = HERE / "records"
records.mkdir(exist_ok=True)
receipt = records / (datetime.now().strftime("%Y%m%d-%H%M%S-%f") + "-build.json")
receipt.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"output": str(OUTPUT), "bytes": record["bytes"], "record": str(receipt)}, ensure_ascii=False))
