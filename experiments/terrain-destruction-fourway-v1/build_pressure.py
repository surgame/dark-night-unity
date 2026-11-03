from pathlib import Path
from datetime import datetime
import base64
import hashlib
import json
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
OUTPUT = Path(sys.argv[1]).resolve()
names = ["geometry.js", "solver.js", "meshes.js", "adapters.js", "gpu.js", "benchmark.js",
         "pressure-model.js", "pressure-benchmark.js", "pressure-panel.js", "app.js"]
hero = ROOT / "美术素材/地图/20261029-wfc-cave-kit/sources/hero-scale-reference.png"
view = (HERE / "view.html").read_text(encoding="utf-8")
start = '  <div class="viz-controls">\n    <button class="btn btn-primary" type="button" id="tf-bench">'
assert start in view
view = view.replace(start, (HERE / "pressure-panel.html").read_text(encoding="utf-8")
                    + '\n<details id="tf-single"><summary>单点旧基准（原口径）</summary>\n' + start)
view = view.replace('  <div id="tf-error"', '  </details>\n  <div id="tf-error"')
parts = [view, '<script src="https://cdn.jsdelivr.net/npm/d3@7.9.0/dist/d3.min.js"></script>']
sources = [HERE / "view.html", HERE / "pressure-panel.html"]
for name in names:
    if name == "app.js":
        data = "data:image/png;base64," + base64.b64encode(hero.read_bytes()).decode("ascii")
        parts.append("<script>TerrainFour.hero=" + json.dumps(data) + ";</script>")
        old = HERE / "revisions/single-cell-v1"
        references = json.loads((old / "references.json").read_text(encoding="utf-8"))
        for case in references.values():
            for source in case["sources"]:
                assert hashlib.sha256((old / source["path"]).read_bytes()).hexdigest() == source["sha256"]
            case["historical"] = True
        parts.append("<script>TerrainFour.references=" + json.dumps(references) + ";</script>")
        sources.append(old / "references.json")
        pressure = HERE / "pressure-references.json"
        if pressure.exists():
            refs = json.loads(pressure.read_text(encoding="utf-8"))
            for case in refs.values():
                for source in case["sources"]:
                    actual = hashlib.sha256((HERE / source["path"]).read_bytes()).hexdigest()
                    assert actual == source["sha256"], "Pressure benchmark source changed: " + source["path"]
            parts.append("<script>TerrainFour.pressureReferences=" + json.dumps(refs) + ";</script>")
            sources.append(pressure)
    parts.append("<script>\n" + (HERE / name).read_text(encoding="utf-8") + "\n</script>")
    sources.append(HERE / name)
text = "\n".join(parts) + "\n"
assert len(text.encode("utf-8")) < 1_000_000
assert '\\n<div' not in text and '<div id=\\"' not in text
OUTPUT.write_text(text, encoding="utf-8")
sources += [hero, Path(__file__).resolve()]
record = {"date": "2026-11-01", "scope": "Grouped CPU throughput and independent batch latency; no Unity writes.",
          "output": str(OUTPUT), "bytes": OUTPUT.stat().st_size,
          "sha256": hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),
          "sources": [{"path": str(p), "sha256": hashlib.sha256(p.read_bytes()).hexdigest()} for p in sources]}
records = HERE / "records"
receipt = records / (datetime.now().strftime("%Y%m%d-%H%M%S-%f") + "-pressure-build.json")
receipt.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"output": str(OUTPUT), "bytes": record["bytes"], "record": str(receipt)}, ensure_ascii=False))
