"""Compare frozen scene captures without treating pixel differences as automatic visual acceptance."""

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


def file_record(path):
    data = path.read_bytes()
    return {"path": str(path.resolve()), "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}


def measurements(reference, candidate):
    difference = np.abs(reference.astype(np.int16) - candidate.astype(np.int16))
    peak = difference.max(axis=2)
    return {
        "pixels": int(peak.size),
        "mean_absolute_channel_delta": float(difference.mean()),
        "maximum_channel_delta": int(peak.max()),
        "pixels_above_2": int((peak > 2).sum()),
        "fraction_above_2": float((peak > 2).mean()),
    }


def compare(reference_dir, candidate_dir, output_dir):
    output_dir.mkdir(parents=True, exist_ok=True)
    report = {
        "method": "RGB absolute differences at native resolution; numerical indicators require separate visual review.",
        "automatic_visual_acceptance": False,
        "cases": [],
    }
    for name in ("menu", "camp", "night", "remnants", "zoom_out", "wide"):
        source_path = reference_dir / f"{name}.png"
        current_path = candidate_dir / f"{name}.png"
        source = Image.open(source_path).convert("RGB")
        current = Image.open(current_path).convert("RGB")
        if source.size != current.size:
            raise ValueError(f"Size mismatch for {name}: {source.size} vs {current.size}")
        width, height = source.size
        a, b = np.asarray(source), np.asarray(current)
        pair = Image.new("RGB", (width * 2, height + 28), "#111820")
        draw = ImageDraw.Draw(pair)
        draw.text((12, 7), f"Godot frozen reference / {name}", fill="white")
        draw.text((width + 12, 7), f"Unity / {name}", fill="white")
        pair.paste(source, (0, 28))
        pair.paste(current, (width, 28))
        pair_path = output_dir / f"{name}-pair.png"
        pair.save(pair_path)
        delta = np.abs(a.astype(np.int16) - b.astype(np.int16)).max(axis=2)
        heat = np.zeros_like(a)
        heat[:, :, 0] = np.minimum(delta * 4, 255)
        heat[:, :, 1] = np.minimum(delta, 160)
        heat_path = output_dir / f"{name}-difference.png"
        Image.fromarray(heat).save(heat_path)
        report["cases"].append({
            "name": name,
            "resolution": [width, height],
            "reference": file_record(source_path),
            "candidate": file_record(current_path),
            "pair": file_record(pair_path),
            "difference": file_record(heat_path),
            "full": measurements(a, b),
            "world_band": measurements(a[88:height - 185], b[88:height - 185]),
            "world_band_bounds": [0, 88, width, height - 185],
            "visual_review": "pending",
        })
    (output_dir / "comparison.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reference", type=Path)
    parser.add_argument("candidate", type=Path)
    parser.add_argument("output", type=Path)
    arguments = parser.parse_args()
    result = compare(arguments.reference, arguments.candidate, arguments.output)
    print(json.dumps([{ "name": c["name"], **c["world_band"] } for c in result["cases"]], indent=2))
