"""Bundle the editable experiment into one offline visualization fragment."""
import argparse
import hashlib
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
source_root = Path(__file__).resolve().parent
names = ["catalog.js", "plan.js", "solver.js", "renderer.js", "app.js"]
template = (source_root / "fragment.html").read_text(encoding="utf-8")
scripts = "\n".join((source_root / name).read_text(encoding="utf-8") for name in names)
fragment = template.replace("<!-- CAVE_WFC_SCRIPTS -->", "<script>\n" + scripts + "\n</script>")
if len(fragment.encode("utf-8")) >= 1_000_000:
    raise ValueError("Inline visualization exceeded its size limit.")
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(fragment, encoding="utf-8", newline="\n")
hashes = {
    name: hashlib.sha256((source_root / name).read_bytes()).hexdigest()
    for name in names + ["fragment.html", "build_preview.py"]
}
manifest = {
    "source": str(source_root),
    "output": str(args.output.resolve()),
    "sha256": hashlib.sha256(args.output.read_bytes()).hexdigest(),
    "bytes": args.output.stat().st_size,
    "sources": hashes,
}
args.output.with_suffix(".sources.json").write_text(
    json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
)
print(json.dumps({"output": manifest["output"], "bytes": manifest["bytes"]}, ensure_ascii=False))
