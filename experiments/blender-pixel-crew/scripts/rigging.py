"""唯一骨架与姿势空间适配；服饰顶点绑定这些骨骼，共享所有动作。"""

import math

import bpy
from mathutils import Euler, Vector


def create_rig():
    data = bpy.data.armatures.new("Crew.SharedSkeleton")
    rig = bpy.data.objects.new("CrewRig", data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    rig.show_in_front = True
    bpy.ops.object.mode_set(mode="EDIT")

    def add(name, head, tail, parent=None):
        bone = data.edit_bones.new(name)
        bone.head, bone.tail = head, tail
        if parent:
            bone.parent = data.edit_bones[parent]
        return bone

    add("Root", (0, 0, 0), (0, 0, .25))
    add("Hips", (0, 0, .64), (0, 0, .83), "Root")
    add("Spine", (0, 0, .73), (0, 0, 1.12), "Hips")
    add("Head", (0, 0, 1.12), (0, 0, 1.77), "Spine")
    for side, sign in (("L", 1), ("R", -1)):
        shoulder = (sign * .29, 0, 1.075)
        elbow = (sign * .37, 0, .835)
        wrist = (sign * .405, -.025, .65)
        add("UpperArm." + side, shoulder, elbow, "Spine")
        add("Forearm." + side, elbow, wrist, "UpperArm." + side)
        add("Hand." + side, wrist, (sign * .405, -.025, .55),
            "Forearm." + side)
        add("Thigh." + side, (sign * .135, 0, .64),
            (sign * .135, 0, .385), "Hips")
        add("Shin." + side, (sign * .135, 0, .385),
            (sign * .135, 0, .15), "Thigh." + side)
        add("Foot." + side, (sign * .135, 0, .15),
            (sign * .135, -.16, .10), "Shin." + side)
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.select_set(False)
    rig["eyes_open"] = 1.0
    rig["contract"] = "One skeleton; feet at Z=0; forward=-Y; no gameplay events"
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
    return rig


def rotation(rig, name, xyz_degrees):
    bone = rig.pose.bones[name]
    basis = bone.bone.matrix_local.to_quaternion()
    delta = Euler(tuple(math.radians(v) for v in xyz_degrees), "XYZ")
    bone.rotation_quaternion = basis.inverted() @ delta.to_quaternion() @ basis


def translation(rig, name, xyz):
    bone = rig.pose.bones[name]
    bone.location = bone.bone.matrix_local.to_quaternion().inverted() @ Vector(xyz)


def reset(rig):
    for bone in rig.pose.bones:
        bone.location = (0, 0, 0)
        bone.rotation_quaternion = (1, 0, 0, 0)
        bone.scale = (1, 1, 1)
    rig["eyes_open"] = 1.0


def select_action(rig, name):
    action = bpy.data.actions["Crew." + name]
    rig.animation_data_create()
    rig.animation_data.action = action
    if hasattr(action, "slots") and action.slots:
        rig.animation_data.action_slot = action.slots[0]
    scene = bpy.context.scene
    scene.frame_start = 1
    scene.frame_end = int(action["sprite_frames"])
    scene.render.fps = int(action["sprite_fps"])
    scene.frame_set(1)
    return action


def choose_preset(name, config):
    enabled = set(config["presets"][name])
    for target in bpy.data.collections:
        variant = target.get("crew_variant")
        if variant:
            target.hide_render = variant not in enabled
            target.hide_viewport = variant not in enabled
    bpy.context.scene["crew_preset"] = name
