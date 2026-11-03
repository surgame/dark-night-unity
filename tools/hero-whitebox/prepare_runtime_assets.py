"""Export the recorded hero selection as native skeletal data and a straight-alpha atlas."""

import copy
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    root = Path(__file__).resolve().parents[2]
    batch = Path(sys.argv[1]).resolve()
    hero = root / "Game/Assets/DarkNights/Res/Objects/Hero"
    target = hero / "Spine"
    if target.exists():
        raise SystemExit("Spine runtime output already exists; preserve existing assets and GUIDs.")
    selection_path = hero / "AnimationSelection.json"
    selection = json.loads(selection_path.read_text(encoding="utf-8-sig"))
    author = root / selection["authorProject"]
    source = next((batch / "native").glob("*.json"))
    data = json.loads(source.read_text(encoding="utf-8-sig"))
    if any(slot.get("blend", "normal") != "normal" or "dark" in slot for slot in data["slots"]):
        raise SystemExit("Current CampSpine material supports normal, single-color slots only.")
    images = author.parent / "images"
    hidden = set()
    for skin in data["skins"]:
        for slot, attachments in skin["attachments"].items():
            for name, attachment in list(attachments.items()):
                if not (images / (attachment.get("path", name) + ".png")).is_file():
                    hidden.add((slot, name))
                    del attachments[name]
    for slot in data["slots"]:
        if (slot["name"], slot.get("attachment")) in hidden:
            slot.pop("attachment", None)
    for animation in data["animations"].values():
        for slot, timelines in animation.get("slots", {}).items():
            for key in timelines.get("attachment", []):
                if (slot, key.get("name")) in hidden:
                    key.pop("name", None)
    chosen = {
        "idle": copy.deepcopy(data["animations"][selection["idle"]]),
        "move": copy.deepcopy(data["animations"][selection["move"]]),
    }
    for name, legs in (("jump_move", "airMovingLegs"), ("jump_idle", "airStationaryLegs")):
        animation = copy.deepcopy(data["animations"][selection["jumpBase"]])
        animation.get("bones", {}).get("root", {}).pop("translate", None)
        for bone, timelines in data["animations"][selection[legs]]["bones"].items():
            animation["bones"].setdefault(bone, {}).update(copy.deepcopy(timelines))
        chosen[name] = animation
    data["animations"] = chosen
    data["skeleton"]["images"] = "./images/"
    data["skeleton"]["audio"] = ""
    input_dir = batch / "runtime-input"
    input_dir.mkdir()
    image_rows = {}
    for skin in data["skins"]:
        for attachments in skin["attachments"].values():
            for name, attachment in attachments.items():
                relative = attachment.get("path", name) + ".png"
                destination = input_dir / "images" / relative
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(images / relative, destination)
                image_rows[relative] = sha(images / relative)
    derived = input_dir / "HeroRuntime.json"
    derived.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
    pack = batch / "runtime-pack-settings.json"
    pack.write_text(json.dumps({
        "premultiplyAlpha": False, "bleed": True, "rotation": False,
        "filterMin": "Nearest", "filterMag": "Nearest",
        "maxWidth": 2048, "maxHeight": 2048, "paddingX": 4, "paddingY": 4,
        "stripWhitespaceX": True, "stripWhitespaceY": True,
    }), encoding="utf-8")
    target.mkdir()
    spine = "D:/Program Files/Spine/Spine.com"
    commands = [
        ["-i", str(derived), "-o", str(target), "-e", "binary"],
        ["-i", str(input_dir / "images"), "-o", str(target), "-n", "HeroRuntime", "-p", str(pack)],
    ]
    for index, args in enumerate(commands):
        process = subprocess.run([spine, "-u", "4.3.26"] + args, capture_output=True, encoding="utf-8", errors="replace")
        (batch / ("runtime-export-%d.log" % index)).write_text(
            process.stdout + process.stderr, encoding="utf-8"
        )
        if process.returncode:
            raise SystemExit("Native Spine export failed; preserve the partial output and logs.")
    for extension in ("skel", "atlas"):
        path = target / ("HeroRuntime." + extension)
        path.rename(path.with_suffix(path.suffix + (".bytes" if extension == "skel" else ".txt")))
    atlas = target / "HeroRuntime.atlas.txt"
    lines = atlas.read_text(encoding="utf-8").splitlines()
    explicit = []
    for index, line in enumerate(lines):
        explicit.append(line)
        if line.endswith(".png") and (index == 0 or not lines[index - 1]):
            explicit.append("pma:false")
    atlas.write_text("\n".join(explicit) + "\n", encoding="utf-8")
    shutil.copyfile(derived, target / "HeroRuntime.source.json.txt")
    receipt = {
        "authorProject": selection["authorProject"], "authorSha256": sha(author),
        "selectionSha256": sha(selection_path), "nativeJsonSha256": sha(source),
        "derivedJsonSha256": sha(derived), "sourceImages": image_rows,
        "hiddenMissingAttachments": sorted([list(value) for value in hidden]),
        "runtime": "spine-unity 4.3, native binary, straight-alpha/bleed atlas",
        "derivation": "Selected run5/idle; jump2 without root translation plus authored air leg timelines.",
        "outputs": {p.name: sha(p) for p in target.iterdir() if p.is_file()},
    }
    (target / "RuntimeManifest.json").write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(json.dumps({"files": len(receipt["outputs"]), "images": len(image_rows), "hidden": len(hidden)}))


if __name__ == "__main__":
    main()
