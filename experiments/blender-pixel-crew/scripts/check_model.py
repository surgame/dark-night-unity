"""真实 .blend 检查：共用骨架、部件绑定、动作变化、编辑保存与重开。"""

import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from rigging import choose_preset, select_action
from scene_setup import read_config


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    source = Path(bpy.data.filepath)
    original_sha = hashlib.sha256(source.read_bytes()).hexdigest()
    rig = bpy.data.objects["CrewRig"]
    config = read_config()
    armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    assert len(armatures) == 1 and len(rig.data.bones) == 16
    meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    for mesh in meshes:
        modifiers = [m for m in mesh.modifiers if m.type == "ARMATURE"]
        assert len(modifiers) == 1 and modifiers[0].object == rig, mesh.name
        assert len(mesh.vertex_groups) == 1, mesh.name
        assert mesh.vertex_groups[0].name in rig.data.bones, mesh.name
        assert all(len(v.groups) == 1 and abs(v.groups[0].weight - 1) < .00001
                   for v in mesh.data.vertices), mesh.name
    available = {c.get("crew_variant") for c in bpy.data.collections}
    outfit_counts = {}
    for name, parts in config["presets"].items():
        assert set(parts).issubset(available), name
        choose_preset(name, config)
        outfit_counts[name] = sum(
            len(c.objects) for c in bpy.data.collections if not c.hide_render)
    variations = {}
    for name in config["actions"]:
        action = select_action(rig, name)
        matrices = []
        for frame in range(1, int(action["sprite_frames"]) + 1):
            bpy.context.scene.frame_set(frame)
            values = tuple(round(v, 6) for bone in rig.pose.bones
                           for row in bone.matrix for v in row)
            matrices.append(values)
        assert len(set(matrices)) > 1, name
        variations[name] = len(set(matrices))
        if action["loop"]:
            bpy.context.scene.frame_set(int(action["sprite_frames"]) + 1)
            closing = tuple(round(v, 6) for bone in rig.pose.bones
                            for row in bone.matrix for v in row)
            assert closing == matrices[0], name
    choose_preset("worker", config)
    select_action(rig, "idle")
    probe_path = output / "edited-roundtrip.blend"
    if probe_path.exists():
        raise FileExistsError("Probe copy exists; use a fresh verification directory.")
    mesh_name = "Head.Skin"
    changed_z = bpy.data.objects[mesh_name].data.vertices[0].co.z + .007
    bpy.data.objects[mesh_name].data.vertices[0].co.z = changed_z
    bpy.context.scene["crew_persistence_probe"] = "edited-and-reopened"
    bpy.ops.wm.save_as_mainfile(filepath=str(probe_path), compress=True)
    bpy.ops.wm.open_mainfile(filepath=str(probe_path))
    assert bpy.context.scene["crew_persistence_probe"] == "edited-and-reopened"
    assert abs(bpy.data.objects[mesh_name].data.vertices[0].co.z - changed_z) < .000001
    assert hashlib.sha256(source.read_bytes()).hexdigest() == original_sha
    report = {
        "status": "passed", "source_sha256": original_sha, "blender": bpy.app.version_string,
        "armatures": len(armatures), "bones": 16, "meshes": len(meshes),
        "presets": outfit_counts, "unique_pose_samples": variations,
        "checks": ["one_rig", "all_meshes_bound", "normalized_rigid_weights",
                   "all_parts_resolve", "nine_animated_actions", "loop_pose_closure",
                   "edit_save_reopen", "source_unchanged"],
        "art_acceptance": "failed: user judged the style insufficiently close to reference",
        "limitations": ["rigid limb weights", "no cloth simulation", "no production motion cleanup"],
    }
    (output / "model-verification.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("CREW_MODEL_CHECK " + json.dumps(report, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
