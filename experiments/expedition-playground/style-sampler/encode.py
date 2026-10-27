"""Encode independent sampler output, preserving all source pixels."""
from pathlib import Path
import json
import sys
from PIL import Image

raw, output = (Path(value) for value in sys.argv[1:3])
output.mkdir(parents=True, exist_ok=True)
records = []
for source in sorted(raw.glob("*.rgba")):
    target = output / (source.stem + ".png")
    image = Image.frombytes("RGBA", (768, 336), source.read_bytes())
    image.save(target, optimize=True)
    records.append({"id": source.stem, "file": target.name, "nativeWidth": 768, "nativeHeight": 336})
(output / "manifest.json").write_text(json.dumps(records, indent=2) + "\n", encoding="utf-8")
print(f"Encoded {len(records)} lossless native backgrounds.")
