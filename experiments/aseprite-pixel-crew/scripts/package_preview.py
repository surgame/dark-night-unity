"""核对 Aseprite 原生导出并打包离线 H5；不重采样或重绘正式 PNG 帧。"""

import argparse
import base64
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def sample(sheet, record):
    r = record["frame"]
    return sheet.crop((r["x"], r["y"], r["x"]+r["w"], r["y"]+r["h"]))


def data_url(path):
    return "data:image/png;base64," + base64.b64encode(path.read_bytes()).decode("ascii")


def check_parts(root, data, native_sheet, failures):
    manifest = json.loads((root / "parts.json").read_text(encoding="utf-8"))
    layers = manifest["layers"]
    sheets = [Image.open(root / item["file"]).convert("RGBA") for item in layers]
    choices = {"headwear": ["cap", "hard_hat", "hair"],
               "outfit": ["overalls", "work_vest"],
               "pack": ["backpack", "canvas_bag", "none"]}
    defaults = {"pack": "none"}
    for part in choices:
        visible = [l["choice"] for l in layers if l["part"] == part and l["default_visible"]]
        if len(visible) > 1 or (part != "pack" and not visible):
            failures.append(f"{part}: enable exactly one alternative layer in the source")
        if visible:
            defaults[part] = visible[0]
        actual = {l["choice"] for l in layers if l["part"] == part}
        if actual != set(choices[part]) - {"none"}:
            failures.append(f"{part}: alternative layers or role metadata are missing")
    all_colors = set()
    for info, layer in zip(layers, sheets):
        if layer.size != native_sheet.size:
            failures.append(info["name"] + ": layer sheet dimensions differ")
        if not set(layer.getchannel("A").getdata()).issubset({0, 255}):
            failures.append(info["name"] + ": unsupported semi-transparent layer")
        all_colors.update(p[:3] for p in layer.getdata() if p[3])

    def compose(selection):
        result = Image.new("RGBA", native_sheet.size)
        for spec, layer in zip(layers, sheets):
            if spec["part"] == "base" or selection.get(spec["part"]) == spec["choice"]:
                result = Image.alpha_composite(result, layer)
        return result

    if compose(defaults).tobytes() != native_sheet.tobytes():
        failures.append("default layered composite differs from Aseprite native sheet")
    for combination in manifest["combinations"]:
        native = Image.open(root / combination["file"]).convert("RGBA")
        composite = compose(combination)
        if native.tobytes() != composite.tobytes():
            failures.append(combination["id"] + ": layer order differs from native composite")
        for index, record in enumerate(data["frames"]):
            box = sample(native, record).getbbox()
            if not box or min(box[:2]) < 1 or max(box[2:]) > 63:
                failures.append(f"{combination['id']}: frame {index} is clipped")
    if len(manifest["combinations"]) != 18:
        failures.append("expected all 18 clothing combinations")
    if len(all_colors) > 24:
        failures.append("alternative layers exceed the designed 24-color palette")
    manifest["defaults"] = defaults
    manifest["choices"] = choices
    for info in layers:
        info["data"] = data_url(root / info["file"])
    return manifest, len(all_colors)


def make_gifs(root, data, sheet):
    font = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 17)
    for tag in data["meta"]["frameTags"]:
        images, durations = [], []
        records = data["frames"][tag["from"]:tag["to"]+1]
        for record in records:
            sprite = sample(sheet, record).resize((256, 256), Image.Resampling.NEAREST)
            board = Image.new("RGB", (288, 310), "#e8dcc6")
            board.paste(sprite, (16, 32), sprite)
            draw = ImageDraw.Draw(board)
            draw.text((18, 12), "ASEPRITE / " + tag["name"], font=font, fill="#453a38")
            draw.text((18, 285), "64 px / 4x nearest", font=font, fill="#796450")
            images.append(board)
            durations.append(record["duration"])
        palette = images[0].quantize(colors=128, dither=Image.Dither.NONE)
        images = [image.quantize(palette=palette, dither=Image.Dither.NONE) for image in images]
        images[0].save(root / (tag["name"] + ".gif"), save_all=True,
                       append_images=images[1:], duration=durations, loop=0,
                       disposal=2, optimize=False)


def make_contact_sheet(root, data, sheet):
    font = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 16)
    board = Image.new("RGB", (1408, 516), "#f4eee3")
    draw = ImageDraw.Draw(board)
    for row, tag in enumerate(data["meta"]["frameTags"]):
        y = row * 172
        draw.text((16, y+58), tag["name"].upper(), font=font, fill="#a74432")
        for index, record in enumerate(data["frames"][tag["from"]:tag["to"]+1]):
            x = 128+index*128
            sprite = sample(sheet, record).resize((128, 128), Image.Resampling.NEAREST)
            board.paste(sprite, (x, y+12), sprite)
            draw.text((x+24, y+145), f"{index+1:02} / {record['duration']}ms", font=font, fill="#80746a")
    board.save(root / "contact-sheet.png")


def make_outfit_gif(root, data):
    names = ["cap-overalls-backpack", "hard_hat-work_vest-canvas_bag", "hair-overalls-none"]
    sources = [Image.open(root / "native-combinations" / (name+".png")).convert("RGBA") for name in names]
    font = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 16)
    walk = next(tag for tag in data["meta"]["frameTags"] if tag["name"] == "walk")
    images, durations = [], []
    for record in data["frames"][walk["from"]:walk["to"]+1]:
        board = Image.new("RGB", (696, 268), "#e8dcc6")
        draw = ImageDraw.Draw(board)
        for index, (source, label) in enumerate(zip(sources, ["01 / WORKER", "02 / BUILDER", "03 / LIGHT"])):
            sprite = sample(source, record).resize((192, 192), Image.Resampling.NEAREST)
            board.paste(sprite, (20+index*232, 36), sprite)
            draw.text((27+index*232, 18), label, font=font, fill="#a74432")
        draw.text((27, 240), "SAME WALK / SWAPPABLE LAYERS / 64 PX", font=font, fill="#796450")
        images.append(board)
        durations.append(record["duration"])
    palette = images[0].quantize(colors=128, dither=Image.Dither.NONE)
    images = [image.quantize(palette=palette, dither=Image.Dither.NONE) for image in images]
    images[0].save(root / "outfits-walk.gif", save_all=True, append_images=images[1:],
                   duration=durations, loop=0, disposal=2, optimize=False)
    images[0].convert("RGB").save(root / "outfits.png")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--model", required=True)
    args = parser.parse_args()
    root, model = Path(args.input).resolve(), Path(args.model).resolve()
    data = json.loads((root / "sheet.json").read_text(encoding="utf-8"))
    sheet = Image.open(root / "sheet.png").convert("RGBA")
    tags = data["meta"]["frameTags"]
    layer_names = [item["name"] for item in data["meta"]["layers"]]
    origin = next(item for item in data["meta"]["slices"] if item["name"] == "feet_origin")
    pivot = origin["keys"][0]["pivot"]
    assert pivot == {"x": 32, "y": 58}, pivot
    failures, visible_colors, bounds = [], set(), []
    native_frames = sorted((root / "frames").glob("*.png"))
    if len(native_frames) != len(data["frames"]):
        failures.append("individual frame count does not match sheet metadata")
    for index, record in enumerate(data["frames"]):
        frame = sample(sheet, record)
        bounds.append(frame.getbbox())
        if frame.size != (64, 64) or record["trimmed"] or record["rotated"]:
            failures.append(f"frame {index}: canvas was trimmed or rotated")
        if not frame.getbbox() or min(frame.getbbox()[:2]) < 1 or max(frame.getbbox()[2:]) > 63:
            failures.append(f"frame {index}: empty or touches border")
        if not set(frame.getchannel("A").getdata()).issubset({0, 255}):
            failures.append(f"frame {index}: soft alpha")
        visible_colors.update(pixel[:3] for pixel in frame.getdata() if pixel[3])
        if index < len(native_frames):
            native = Image.open(native_frames[index]).convert("RGBA")
            if native.tobytes() != frame.tobytes():
                failures.append(f"frame {index}: sheet differs from native frame")
    coverage = [index for tag in tags for index in range(tag["from"], tag["to"]+1)]
    if coverage != list(range(len(data["frames"]))):
        failures.append("tags do not cover each frame exactly once")
    clips = []
    for tag in tags:
        records = data["frames"][tag["from"]:tag["to"]+1]
        hashes = [hashlib.sha256(sample(sheet, r).tobytes()).hexdigest() for r in records]
        if len(records) > 1 and len(set(hashes)) < 2:
            failures.append(tag["name"] + ": no visible animation")
        clips.append({"name": tag["name"], "frames": len(records),
                      "duration_ms": sum(r["duration"] for r in records),
                      "unique_images": len(set(hashes)), "loop": tag["name"] != "jump"})
    if {c["name"]: c["frames"] for c in clips} != {"idle": 6, "walk": 8, "jump": 10}:
        failures.append("expected idle/walk/jump with 6/8/10 frames")
    grounded = [bounds[index][3] for tag in tags if tag["name"] in ("idle", "walk")
                for index in range(tag["from"], tag["to"]+1)]
    if any(bottom != 58 for bottom in grounded):
        failures.append("idle/walk feet drift vertically from y=58")
    if len(visible_colors) > 24:
        failures.append("unexpected colors outside the 24-color design")
    parts, all_colors = check_parts(root, data, sheet, failures)
    report = {
        "status": "passed" if not failures else "failed", "frames": len(data["frames"]),
        "canvas": [64, 64], "pivot": [32, 58], "colors": len(visible_colors),
        "layers": [item["name"] for item in parts["layers"]], "visible_layers": layer_names,
        "clips": clips, "failures": failures,
        "source_sha256": hashlib.sha256(model.read_bytes()).hexdigest(),
        "sheet_sha256": hashlib.sha256((root / "sheet.png").read_bytes()).hexdigest(),
        "aseprite_version": data["meta"]["version"],
        "parts": {"layer_count": len(parts["layers"]), "choices": parts["choices"],
                  "combinations": len(parts["combinations"]), "verified_composite_frames": 18*len(data["frames"]),
                  "colors_including_alternatives": all_colors, "defaults": parts["defaults"]},
        "checks": ["native_frame_sheet_identity", "binary_alpha", "fixed_canvas",
                   "no_clipping", "tag_coverage", "visible_motion", "grounded_idle_walk",
                   "18_native_layer_composites", "all_alternatives_within_canvas"],
        "art_acceptance": "pending user review",
    }
    (root / "verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    if failures:
        print(json.dumps(report, ensure_ascii=False))
        raise SystemExit(1)
    make_gifs(root, data, sheet)
    make_contact_sheet(root, data, sheet)
    make_outfit_gif(root, data)
    bundle = dict(data, pivot=[32, 58], artStatus="待评审", parts=parts,
                  sheetData=data_url(root / "sheet.png"))
    template = Path(__file__).parents[1] / "web" / "viewer.html"
    html = template.read_text(encoding="utf-8").replace(
        "__SPRITE_DATA__", json.dumps(bundle, ensure_ascii=False).replace("</", "<\\/"))
    (root / "index.html").write_text(html, encoding="utf-8")
    print(json.dumps({"status": "passed", "frames": report["frames"], "colors": len(visible_colors),
                      "clips": clips, "preview": str(root / "index.html")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
