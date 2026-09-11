"""初始化时统一缩短腿部、放大头部；变换同时作用于网格与骨架静置姿态。"""

import bpy


def apply_proportions(rig, config):
    leg_scale = config["proportions"]["leg_scale"]
    head_scale = config["proportions"]["head_scale"]
    torso_scale = config["proportions"]["torso_scale"]
    body_width = config["proportions"]["body_width"]
    hip_z, neck_z = .66, 1.12
    mapped_neck = hip_z * leg_scale + (neck_z - hip_z) * torso_scale

    def map_z(z):
        if z <= hip_z:
            return z * leg_scale
        if z <= neck_z:
            return hip_z * leg_scale + (z - hip_z) * torso_scale
        return mapped_neck + (z - neck_z) * head_scale

    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        is_head = obj.get("bone_binding") == "Head"
        for vertex in obj.data.vertices:
            vertex.co.z = map_z(vertex.co.z)
            if is_head:
                vertex.co.x *= head_scale
                vertex.co.y *= head_scale
            else:
                vertex.co.x *= body_width
        obj.data.update()
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for bone in rig.data.edit_bones:
        bone.head.z = map_z(bone.head.z)
        bone.tail.z = map_z(bone.tail.z)
        bone.head.x *= body_width
        bone.tail.x *= body_width
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.select_set(False)
