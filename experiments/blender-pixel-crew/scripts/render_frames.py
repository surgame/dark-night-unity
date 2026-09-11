"""从已经保存的模型批量导出；保留手工网格、权重、Action、灯光与材质。"""

import argparse
import hashlib
import json
import sys
import time
from pathlib import Path

import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))

from rigging import choose_preset, select_action
from scene_setup import configure_camera, read_config


def names(value, available):
    result = list(available) if value == "all" else value.split(",")
    unknown = set(result) - set(available)
    if unknown:
        raise ValueError("Unknown selections: " + ", ".join(sorted(unknown)))
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--presets", default="all")
    parser.add_argument("--actions", default="all")
    parser.add_argument("--directions", default="right,left")
    parser.add_argument("--sizes", default="64")
    parser.add_argument("--frames", default="all")
    parser.add_argument("--comparison-sizes", default="")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    output = Path(args.output).resolve()
    if output.exists() and any(output.iterdir()):
        raise FileExistsError("导出目录已有内容；选择新目录保留既有结果。")
    output.mkdir(parents=True, exist_ok=True)
    config = read_config()
    rig = bpy.data.objects["CrewRig"]
    presets = names(args.presets, config["presets"])
    actions = names(args.actions, config["actions"])
    directions = names(args.directions, config["directions"])
    sizes = [int(size) for size in args.sizes.split(",")]
    comparison_sizes = [int(s) for s in args.comparison_sizes.split(",") if s]
    if set(sizes) & set(comparison_sizes):
        raise ValueError("Comparison sizes must differ from the animation sizes.")
    sizes += comparison_sizes
    if any(size < 16 or size > 512 for size in sizes):
        raise ValueError("试验画布尺寸限制为 16–512。")
    if sum(obj.type == "ARMATURE" for obj in bpy.data.objects) != 1:
        raise ValueError("共享骨架合同：场景必须只有一个 Armature。")
    source = Path(bpy.data.filepath)
    source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
    manifest = {
        "schema": 1, "source_blend": source.name, "source_sha256": source_hash,
        "blender_version": bpy.app.version_string, "engine": bpy.context.scene.render.engine,
        "coordinate_system": "PNG rects top-left; pivot_pixels top-left; Unity pivot bottom-left",
        "config": config, "frames": [],
    }
    started = time.perf_counter()
    for size in sizes:
        for direction in (["right"] if size in comparison_sizes else directions):
            pivot = configure_camera(config, direction, size)
            bpy.context.view_layer.update()
            projected = world_to_camera_view(bpy.context.scene, bpy.context.scene.camera,
                                             Vector((0, 0, 0)))
            actual = [projected.x * size, (1 - projected.y) * size]
            if max(abs(a - b) for a, b in zip(actual, pivot)) > .001:
                raise AssertionError("Foot origin did not project to the pixel anchor.")
            for preset in presets:
                choose_preset(preset, config)
                for action_name in (["idle"] if size in comparison_sizes else actions):
                    action = select_action(rig, action_name)
                    frame_count = int(action["sprite_frames"])
                    frames = list(range(1, frame_count + 1)) if args.frames == "all" else (
                        [int(value) for value in args.frames.split(",")])
                    if size in comparison_sizes:
                        frames = [1]
                    if not frames or min(frames) < 1 or max(frames) > frame_count:
                        raise ValueError("Requested frame outside the Action export range.")
                    folder = output / str(size) / preset / direction / action_name
                    folder.mkdir(parents=True, exist_ok=True)
                    for frame in frames:
                        bpy.context.scene.frame_set(frame)
                        path = folder / f"{frame - 1:03d}.png"
                        bpy.context.scene.render.filepath = str(path)
                        bpy.ops.render.render(write_still=True)
                        manifest["frames"].append({
                            "file": path.relative_to(output).as_posix(), "size": size,
                            "preset": preset, "direction": direction, "action": action_name,
                            "index": frame - 1, "fps": int(action["sprite_fps"]),
                            "loop": bool(action["loop"]), "pivot_pixels": pivot,
                            "pivot_unity": [pivot[0] / size, 1 - pivot[1] / size],
                        })
                    print(f"CREW_CLIP {size} {preset} {direction} {action_name} {len(frames)}",
                          flush=True)
    manifest["elapsed_seconds"] = round(time.perf_counter() - started, 3)
    manifest["frame_count"] = len(manifest["frames"])
    manifest_path = output / "render_manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    if hashlib.sha256(source.read_bytes()).hexdigest() != source_hash:
        raise AssertionError("Rendering changed the saved model.")
    print("CREW_RENDER " + json.dumps({
        "status": "passed", "frames": len(manifest["frames"]),
        "seconds": manifest["elapsed_seconds"], "manifest": str(manifest_path),
        "source_unchanged": True,
    }), flush=True)


if __name__ == "__main__":
    main()
