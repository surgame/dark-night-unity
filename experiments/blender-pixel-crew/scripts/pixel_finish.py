"""固定色板、硬透明和一像素轮廓；不裁切、不逐帧重心对齐、不使用抖色。"""

import argparse
import hashlib
import json
from collections import defaultdict
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont


def rgb(hex_color):
    return tuple(int(hex_color[i:i + 2], 16) for i in (1, 3, 5))


def palette_image(config):
    colors = list(dict.fromkeys(
        color for swatches in config["colors"].values() for color in swatches))
    if config["outline_color"] not in colors:
        colors.append(config["outline_color"])
    entries = [rgb(color) for color in colors]
    padded = entries + [entries[0]] * (256 - len(entries))
    palette = Image.new("P", (1, 1))
    palette.putpalette([component for color in padded for component in color])
    return palette, set(entries)


def finish_frame(raw, config, palette):
    alpha = raw.getchannel("A").point(
        lambda value: 255 if value >= config["alpha_threshold"] else 0)
    color = raw.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE)
    body = color.convert("RGBA")
    body.putalpha(alpha)
    width = config["outline_pixels"]
    if width == 0:
        return body
    outside = ImageChops.subtract(alpha.filter(ImageFilter.MaxFilter(width * 2 + 1)), alpha)
    outline = Image.new("RGBA", raw.size, rgb(config["outline_color"]) + (255,))
    outline.putalpha(outside)
    return Image.alpha_composite(outline, body)


def contact_sheet(records, root, output):
    columns = min(4, len(records))
    tile, header = 224, 52
    rows = (len(records) + columns - 1) // columns
    sheet = Image.new("RGB", (columns * tile, header + rows * (tile + 32)), "#e8dcc6")
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 16)
    draw.text((20, 16), "PIXEL CREW / render probe", fill="#453a38", font=font)
    for i, entry in enumerate(records):
        x, y = (i % columns) * tile, header + (i // columns) * (tile + 32)
        sprite = Image.open(root / entry["file"]).convert("RGBA")
        zoom = max(1, (tile - 24) // entry["size"])
        sprite = sprite.resize((entry["size"] * zoom,) * 2, Image.Resampling.NEAREST)
        sheet.paste(sprite, (x + (tile - sprite.width) // 2, y), sprite)
        draw.text((x + 12, y + tile - 12),
                  f'{entry["preset"]} / {entry["direction"]}', fill="#453a38", font=font)
    sheet.save(output)


def write_atlases(records, output):
    groups = defaultdict(list)
    for entry in records:
        groups[(entry["size"], entry["preset"])].append(entry)
    atlases, clips = [], []
    for (size, preset), group in groups.items():
        by_clip = defaultdict(list)
        for entry in group:
            by_clip[(entry["direction"], entry["action"])].append(entry)
        width = max(len(entries) for entries in by_clip.values()) * size
        height = len(by_clip) * size
        sheet = Image.new("RGBA", (width, height))
        atlas_id = f"{preset}_{size}"
        relative = "sheets/" + atlas_id + ".png"
        for row, ((direction, action), entries) in enumerate(by_clip.items()):
            entries.sort(key=lambda item: item["index"])
            frames = []
            for column, entry in enumerate(entries):
                x, y = column * size, row * size
                sprite = Image.open(output / entry["file"]).convert("RGBA")
                sheet.paste(sprite, (x, y))
                frames.append({
                    "rect": [x, y, size, size], "source_index": entry["index"],
                    "pivot_pixels": entry["pivot_pixels"],
                    "pivot_unity": entry["pivot_unity"],
                })
            clips.append({
                "atlas": atlas_id, "preset": preset, "size": size, "direction": direction,
                "action": action, "fps": entries[0]["fps"], "loop": entries[0]["loop"],
                "frames": frames,
            })
        (output / "sheets").mkdir(exist_ok=True)
        sheet.save(output / relative)
        atlases.append({"id": atlas_id, "file": relative, "width": width, "height": height})
    return atlases, clips


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, nargs="+")
    parser.add_argument("--output", required=True)
    parser.add_argument("--replace-duplicates", action="store_true",
                        help="In a fresh output, later inputs replace matching frame paths.")
    args = parser.parse_args()
    output = Path(args.output).resolve()
    if output.exists() and any(output.iterdir()):
        raise FileExistsError("后处理目标须为空，避免覆盖人工修帧。")
    output.mkdir(parents=True, exist_ok=True)
    records, failures, sources = [], [], []
    config = None
    unique_paths = set()
    replaced = 0
    for source_arg in args.input:
        source = Path(source_arg).resolve()
        manifest = json.loads((source / "render_manifest.json").read_text(encoding="utf-8"))
        if config is None:
            config = manifest["config"]
            palette, allowed = palette_image(config)
        if config != manifest["config"]:
            raise ValueError("不能合并不同色板或模型配置的渲染。")
        sources.append({key: manifest[key] for key in (
            "source_blend", "source_sha256", "blender_version", "engine",
            "frame_count", "elapsed_seconds")})
        for entry in manifest["frames"]:
            if entry["file"] in unique_paths:
                if not args.replace_duplicates:
                    raise ValueError("重复输出帧：" + entry["file"])
                records = [row for row in records if row["file"] != entry["file"]]
                failures = [row for row in failures if row["file"] != entry["file"]]
                replaced += 1
            unique_paths.add(entry["file"])
            with Image.open(source / entry["file"]) as image:
                raw = image.convert("RGBA")
            frame = finish_frame(raw, config, palette)
            target = output / entry["file"]
            target.parent.mkdir(parents=True, exist_ok=True)
            frame.save(target)
            bbox = frame.getbbox()
            alpha_values = set(frame.getchannel("A").getdata())
            visible = {pixel[:3] for pixel in frame.getdata() if pixel[3]}
            problem = []
            if frame.size != (entry["size"], entry["size"]):
                problem.append("size")
            if not bbox:
                problem.append("empty")
            elif min(bbox[:2]) < 1 or max(bbox[2:]) > entry["size"] - 1:
                problem.append("clipped_or_touches_border")
            if not alpha_values.issubset({0, 255}) or 0 not in alpha_values:
                problem.append("alpha")
            if not visible.issubset(allowed):
                problem.append("palette")
            if problem:
                failures.append({"file": entry["file"], "failures": problem, "bbox": bbox})
            records.append(dict(entry, bbox=bbox,
                                render_source_sha256=manifest["source_sha256"],
                                sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
    atlases, clips = write_atlases(records, output)
    atlas = {"schema": 1, "config": config, "sources": sources, "atlases": atlases, "clips": clips}
    (output / "atlas.json").write_text(json.dumps(atlas, ensure_ascii=False, indent=2), encoding="utf-8")
    (output / "frames.json").write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")
    report = {
        "status": "passed" if not failures else "failed", "frame_count": len(records),
        "clip_count": len(clips), "atlas_count": len(atlases), "palette_size": len(allowed),
        "checks": ["nonempty", "fixed_canvas", "transparent_binary_alpha", "fixed_palette",
                   "one_pixel_margin", "unique_frame_paths"],
        "failures": failures, "sources": sources, "replaced_frames": replaced,
    }
    (output / "verification.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    samples = [e for e in records if e["action"] == "idle" and e["index"] == 0 and e["size"] == 64]
    if samples:
        contact_sheet(samples, output, output / "contact-sheet.png")
    print(json.dumps({"status": report["status"], "frames": len(records), "clips": len(clips),
                      "failures": len(failures), "output": str(output)}))
    if failures:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
