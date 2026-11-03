"""Bake the user's recorded Spine selection; preserve author and previous exports."""

import copy
import hashlib
import json
from pathlib import Path
import subprocess
import sys

from PIL import Image


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def duration(value):
    if isinstance(value, dict):
        return max([value.get("time", 0)] + [duration(item) for item in value.values()])
    if isinstance(value, list):
        return max([0] + [duration(item) for item in value])
    return 0


root = Path(__file__).resolve().parents[2]
batch = Path(sys.argv[1]).resolve()
hero = root / "Game/Assets/DarkNights/Res/Objects/Hero"
selection = json.loads((hero / "AnimationSelection.json").read_text(encoding="utf-8-sig"))
author_images = root / "美术素材/角色/主角/images"
source = next((batch / "author29").glob("*.json"))
data = json.loads(source.read_text(encoding="utf-8-sig"))
for name in ("idle", "move", "jumpBase", "airMovingLegs", "airStationaryLegs"):
    assert selection[name] in data["animations"], selection[name]
target = hero / "SelectedFrames"
if target.exists():
    raise SystemExit("SelectedFrames already exists; existing candidate is protected.")

# The original browser preview skipped unavailable attachments instead of showing
# Spine's orange placeholders. Apply that same policy only to this derived input.
missing = {}
for skin in data["skins"]:
    for slot, attachments in skin["attachments"].items():
        for name, attachment in list(attachments.items()):
            path = attachment.get("path", name)
            if not (author_images / (path + ".png")).is_file():
                missing[(slot, name)] = path
                del attachments[name]
for slot in data["slots"]:
    if (slot["name"], slot.get("attachment")) in missing:
        slot.pop("attachment", None)
for animation in data["animations"].values():
    for slot, timelines in animation.get("slots", {}).items():
        for key in timelines.get("attachment", []):
            if (slot, key.get("name")) in missing:
                key.pop("name", None)

base = data["animations"][selection["jumpBase"]]
for pose, leg_key in (("jump_move", "airMovingLegs"), ("jump_idle", "airStationaryLegs")):
    animation = copy.deepcopy(base)
    animation.get("bones", {}).get("root", {}).pop("translate", None)
    for bone, timelines in data["animations"][selection[leg_key]]["bones"].items():
        animation["bones"].setdefault(bone, {}).update(copy.deepcopy(timelines))
    data["animations"][pose] = animation
data["skeleton"]["images"] = author_images.as_posix() + "/"
derived = batch / "playback-source.json"
derived.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")

spine = "D:/Program Files/Spine/Spine.com"
poses = [
    ("idle", selection["idle"], duration(data["animations"][selection["idle"]])),
    ("move", selection["move"], duration(data["animations"][selection["move"]])),
    ("jump_move", "jump_move", duration(data["animations"]["jump_move"])),
    ("jump_idle", "jump_idle", duration(data["animations"]["jump_idle"])),
]
result = []
for pose, animation, seconds in poses:
    settings = {
        "class": "export-png", "exportType": "animation", "skeletonType": "single",
        "skeleton": derived.stem, "animationType": "single", "animation": animation,
        "skinType": "current", "renderImages": True,
        "fps": selection["framesPerSecond"], "includeLastFrame": False,
    }
    settings_path = batch / (pose + "-settings.json")
    settings_path.write_text(json.dumps(settings), encoding="utf-8")
    output = batch / ("selected-" + pose)
    run = subprocess.run(
        [spine, "-i", str(derived), "-o", str(output), "-e", str(settings_path)],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    (batch / (pose + "-export.log")).write_text(run.stdout + run.stderr, encoding="utf-8")
    frames = sorted(batch.glob("selected-" + pose + "_*.png"))
    if run.returncode or not frames:
        raise SystemExit("Spine export failed: " + pose)
    # Keep the animation's first-frame foot baseline throughout its frames.
    # Per-frame alpha-bound pivots would cancel the authored leg/body motion.
    first = Image.open(frames[0]).convert("RGBA")
    bounds = first.getbbox()
    pivot = [0.5, (first.height - bounds[3]) / first.height]
    rows = []
    for index, frame in enumerate(frames):
        destination = target / pose / (pose + "-%03d.png" % index)
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(frame.read_bytes())
        rows.append({
            "file": destination.relative_to(hero).as_posix(), "sha256": sha(destination),
            "nativeSource": frame.name, "pivot": pivot,
        })
    result.append({"pose": pose, "sourceAnimation": animation, "duration": seconds, "frames": rows})

manifest = {
    "selection": selection, "authorSha256": sha(root / selection["authorProject"]),
    "sourceJsonSha256": sha(source), "derivedJsonSha256": sha(derived),
    "pixelsPerUnit": 2083.3333333333335,
    "rootMotion": "Derived air animations remove author root translation; gameplay owns position.",
    "hiddenMissingAttachments": [
        {"slot": slot, "attachment": name, "path": path}
        for (slot, name), path in sorted(missing.items())
    ],
    "poses": result,
}
(hero / "SelectedFramesManifest.json").write_text(
    json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
)
print(json.dumps({p["pose"]: len(p["frames"]) for p in result}))
