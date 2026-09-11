"""交互试验面板；切换同一个模型的穿搭、动作、固定视角和输出分辨率。"""

import bpy

from rigging import choose_preset, select_action
from scene_setup import configure_camera, read_config


def update_preset(scene, context):
    choose_preset(scene.crew_preset_choice, read_config())


def update_action(scene, context):
    select_action(bpy.data.objects["CrewRig"], scene.crew_action_choice)


def update_camera(scene, context):
    configure_camera(read_config(), scene.crew_view_choice, int(scene.crew_resolution))


class CREW_OT_render_still(bpy.types.Operator):
    """仅渲染当前视图，供人在 Render Result 中检查；不覆盖模型或序列帧。"""
    bl_idname = "crew.render_still"
    bl_label = "Render current frame"

    def execute(self, context):
        bpy.ops.render.render("INVOKE_DEFAULT", write_still=False)
        return {"FINISHED"}


class CREW_PT_controls(bpy.types.Panel):
    """同一共享骨架的样机操作面板；正式交付的动画仍来自可编辑 Action。"""
    bl_label = "Pixel Crew / Prototype"
    bl_idname = "CREW_PT_controls"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Pixel Crew"

    def draw(self, context):
        layout = self.layout
        scene = context.scene
        layout.prop(scene, "crew_preset_choice", text="Outfit")
        layout.prop(scene, "crew_action_choice", text="Action")
        layout.prop(scene, "crew_view_choice", text="View")
        layout.prop(scene, "crew_resolution", text="Pixels")
        row = layout.row(align=True)
        row.operator("screen.animation_play", text="Play / Pause", icon="PLAY")
        row.operator("screen.frame_jump", text="Start", icon="REW").end = False
        layout.operator("crew.render_still", icon="RENDER_STILL")
        box = layout.box()
        box.label(text="One rig / 16 bones / 9 actions")
        box.label(text="Edit meshes + actions, then save.")
        box.label(text="run.ps1 exports saved edits.")
        box.label(text="F12 is raw; export adds pixel outline.")


def register():
    config = read_config()
    scene = bpy.context.scene
    for cls in (CREW_OT_render_still, CREW_PT_controls):
        previous = getattr(bpy.types, cls.__name__, None)
        if previous:
            bpy.utils.unregister_class(previous)
        bpy.utils.register_class(cls)
    bpy.types.Scene.crew_preset_choice = bpy.props.EnumProperty(
        items=[(name, name.title(), "") for name in config["presets"]],
        default=scene.get("crew_preset", "worker"), update=update_preset)
    bpy.types.Scene.crew_action_choice = bpy.props.EnumProperty(
        items=[(name, name.title(), "") for name in config["actions"]],
        default="idle", update=update_action)
    bpy.types.Scene.crew_view_choice = bpy.props.EnumProperty(
        items=[(name, name.title(), "") for name in config["directions"]],
        default=scene.get("crew_direction", "right"), update=update_camera)
    bpy.types.Scene.crew_resolution = bpy.props.EnumProperty(
        items=[(str(size), str(size), "") for size in (32, 48, 64, 96)],
        default="64", update=update_camera)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                area.spaces.active.show_region_ui = True
                area.spaces.active.overlay.show_extras = False
