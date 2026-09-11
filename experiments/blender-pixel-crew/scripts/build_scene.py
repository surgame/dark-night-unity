"""仅在指定空目录初始化首版 .blend；后续渲染不调用此生成器。"""

import argparse
import json
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))

from character import create_character
from geometry import create_materials
from motions import create_actions
from proportions import apply_proportions
from rigging import choose_preset, create_rig
from scene_setup import create_stage, read_config, set_viewport
from wardrobe import create_wardrobe


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--config", default=str(Path(__file__).parents[1] / "pipeline.json"))
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    output = Path(args.output).resolve()
    if output.suffix != ".blend":
        raise ValueError("初始化目标必须为 .blend。")
    if output.exists() or (output.parent.exists() and any(output.parent.iterdir())):
        raise FileExistsError("初始化仅允许指定空目录；请用新目录保护人工模型。")
    output.parent.mkdir(parents=True, exist_ok=True)
    config = read_config(args.config)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    materials = create_materials(config)
    rig = create_rig()
    create_character(rig, materials)
    create_wardrobe(rig, materials)
    apply_proportions(rig, config)
    create_actions(rig, config)
    choose_preset("worker", config)
    create_stage(config)
    set_viewport()
    rig.hide_set(True)
    notes = bpy.data.texts.new("START HERE · Pixel Crew")
    notes.write(
        "Pixel Crew / 独立流程试验\n\n"
        "One CrewRig, 16 bones, nine editable shared Actions.\n"
        "Meshes use rigid vertex weights. All data is local to this file.\n"
        "Forward is -Y; feet origin is (0,0,0); camera is orthographic.\n\n"
        "用 open-blender.ps1 启动以显示 Pixel Crew 面板并连接本地 MCP。\n"
        "可直接编辑网格、材质、骨骼和 Action，保存后用 run.ps1 导出。\n"
        "日常渲染读取本文件，不重建模型。新建样板只能写入空目录。\n"
        "动作仅用于风格与复用验证，尚未接入 Dark Nights 玩法时序。\n"
    )
    bpy.ops.wm.save_as_mainfile(filepath=str(output), compress=True)
    print("CREW_INIT " + json.dumps({
        "status": "passed", "path": str(output),
        "meshes": sum(o.type == "MESH" for o in bpy.data.objects),
        "bones": len(rig.data.bones), "actions": len(bpy.data.actions),
        "presets": list(config["presets"]),
    }), flush=True)


if __name__ == "__main__":
    main()
