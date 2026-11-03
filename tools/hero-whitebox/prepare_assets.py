"""Package native Spine exports for the local whitebox without changing author files."""

import hashlib
import json
from pathlib import Path
import struct
import sys
import zlib
from PIL import Image


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def compress_png(data):
    """Recompress the existing PNG filter stream; preserve every decoded pixel."""
    chunks = []
    compressed = []
    offset = 8
    while offset < len(data):
        size = struct.unpack(">I", data[offset:offset + 4])[0]
        kind = data[offset + 4:offset + 8]
        payload = data[offset + 8:offset + 8 + size]
        if kind == b"IDAT":
            compressed.append(payload)
        else:
            chunks.append((kind, payload))
        offset += size + 12
    filtered = zlib.decompress(b"".join(compressed))
    packed = zlib.compress(filtered, 9)
    assert zlib.decompress(packed) == filtered
    chunks.insert(next(i for i, row in enumerate(chunks) if row[0] == b"IEND"), (b"IDAT", packed))
    output = bytearray(data[:8])
    for kind, payload in chunks:
        output.extend(struct.pack(">I", len(payload)))
        output.extend(kind)
        output.extend(payload)
        output.extend(struct.pack(">I", zlib.crc32(kind + payload) & 0xffffffff))
    return bytes(output)


root = Path(__file__).resolve().parents[2]
batch = Path(sys.argv[1]).resolve()
target = root / "Game/Assets/DarkNights/Res/Objects/Hero"
if (target / "AnimationSelection.json").exists():
    raise SystemExit("Recorded animation selection exists; use prepare_selected_assets.py, not the legacy ai_walk packager.")
if target.exists():
    raise SystemExit("Hero destination already exists; refusing to overwrite.")
author = root / "美术素材/角色/主角"
native = batch / "hero-native-export"
source_json = next(native.glob("*.json"))
source_atlas = next(native.glob("*.atlas"))
source_png = next(native.glob("*.png"))
data = json.loads(source_json.read_text(encoding="utf-8-sig"))
assert "ai_walk" in data["animations"] and "ai_idel1_ok" in data["animations"]
assert len(list(batch.glob("hero-idle-png_*.png"))) == 46
assert len(list(batch.glob("hero-walk-png_*.png"))) == 10
assert (batch / "hero-jump-png_03.png").is_file()
(target / "Export").mkdir(parents=True)
(target / "Export/Hero.json").write_bytes(source_json.read_bytes())
atlas = source_atlas.read_text(encoding="utf-8-sig")
assert source_png.name in atlas
(target / "Export/Hero.atlas.txt").write_text(atlas.replace(source_png.name, "Hero.png"), encoding="utf-8")
(target / "Export/Hero.png").write_bytes(source_png.read_bytes())
frames = []
for pose, animation in [("idle", "ai_idel1_ok"), ("walk", "ai_walk"), ("air", "ai_jump2_ok")]:
    directory = target / "AnimationFrames" / pose
    directory.mkdir(parents=True)
    sources = sorted(batch.glob("hero-" + pose + "-png_*.png")) if pose != "air" else [batch / "hero-jump-png_03.png"]
    for index, source in enumerate(sources):
        destination = directory / (pose + "-" + str(index).zfill(3) + ".png")
        destination.write_bytes(compress_png(source.read_bytes()))
        with Image.open(source) as image:
            bounds = image.getchannel("A").getbbox()
            assert bounds is not None
            pivot = [.5, (image.height - bounds[3]) / image.height]
        frames.append({"file": str(destination.relative_to(target)).replace("\\", "/"),
                       "animation": animation, "nativeSource": source.name,
                       "nativeSha256": sha(source), "sha256": sha(destination), "pivot": pivot})
manifest = {
    "authorProject": "美术素材/角色/主角/主角.spine",
    "authorFiles": [{"file": str(p.relative_to(root)).replace("\\", "/"), "sha256": sha(p)}
                    for p in sorted(author.rglob("*")) if p.is_file()],
    "spineVersion": "4.3.26 Professional",
    "nativeJsonSha256": sha(source_json),
    "nativeAtlasSha256": sha(source_atlas),
    "nativeTextureSha256": sha(source_png),
    "framesPerSecond": 12,
    "displayHeightLogicPixels": 48,
    "pixelsPerUnit": 250 * 4 / .48,
    "rendering": "Native Spine PNG export; lossless IDAT recompression; Unity sprite animation.",
    "airPose": "Frame 3 of ai_jump2_ok, held in air. World motion belongs to the existing ActorState.",
    "exportSettings": {pose: json.loads((batch / ("hero-" + pose + "-png-settings.json")).read_text(encoding="utf-8"))
                       for pose in ["idle", "walk", "jump"]},
    "frames": frames,
    "limits": "Original source has 15 missing attachments. No replacement art was generated.",
}
(target / "SourceManifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"target": str(target), "frames": len(frames),
                  "bytes": sum(p.stat().st_size for p in target.rglob("*") if p.is_file())}, ensure_ascii=False))
