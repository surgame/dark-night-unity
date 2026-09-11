"""创建像素渲染所需的低面数网格；几何保存在 .blend 中，可继续手工编辑。"""

import math

import bpy
from mathutils import Vector


def linear_rgba(hex_color):
    """将 sRGB 色板转为线性值，Standard 输出后仍对应指定色板。"""
    channels = [int(hex_color[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return tuple(c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4
                 for c in channels) + (1.0,)


def create_materials(config):
    materials = {}
    for name, swatches in config["colors"].items():
        if name == "ink":
            swatches = [swatches[0]] * 3
        material = bpy.data.materials.new("Palette." + name)
        material.diffuse_color = linear_rgba(swatches[1])
        material.use_nodes = True
        nodes = material.node_tree.nodes
        nodes.clear()
        geometry = nodes.new("ShaderNodeNewGeometry")
        geometry.location = (-700, 0)
        direction = nodes.new("ShaderNodeVectorMath")
        direction.operation = "DOT_PRODUCT"
        direction.inputs[1].default_value = Vector(config["toon_light_direction"]).normalized()
        direction.label = "Art-directed light / deterministic / no noisy shadows"
        direction.location = (-500, 0)
        remap = nodes.new("ShaderNodeMath")
        remap.operation = "MULTIPLY_ADD"
        remap.inputs[1].default_value = .5
        remap.inputs[2].default_value = .5
        remap.location = (-300, 0)
        ramp = nodes.new("ShaderNodeValToRGB")
        ramp.location = (-120, 0)
        ramp.color_ramp.interpolation = "CONSTANT"
        ramp.color_ramp.elements.remove(ramp.color_ramp.elements[1])
        for i, (threshold, color) in enumerate(zip((0, .36, .76), swatches)):
            entry = ramp.color_ramp.elements[0] if i == 0 else (
                ramp.color_ramp.elements.new(threshold))
            entry.position = threshold
            entry.color = linear_rgba(color)
        emission = nodes.new("ShaderNodeEmission")
        emission.location = (150, 0)
        output = nodes.new("ShaderNodeOutputMaterial")
        output.location = (350, 0)
        links = material.node_tree.links
        links.new(geometry.outputs["Normal"], direction.inputs[0])
        links.new(direction.outputs["Value"], remap.inputs[0])
        links.new(remap.outputs[0], ramp.inputs["Fac"])
        links.new(ramp.outputs["Color"], emission.inputs["Color"])
        links.new(emission.outputs[0], output.inputs["Surface"])
        materials[name] = material
    return materials


def collection(name):
    target = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(target)
    return target


def finish(obj, name, material, target, rig=None, bone=None):
    obj.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for previous in tuple(obj.users_collection):
        previous.objects.unlink(obj)
    target.objects.link(obj)
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = False
    if rig and bone:
        weights = obj.vertex_groups.new(name=bone)
        weights.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
        modifier = obj.modifiers.new("Shared skeleton · rigid weights", "ARMATURE")
        modifier.object = rig
        obj["bone_binding"] = bone
    return obj


def box(name, center, size, material, target, rig=None, bone=None,
        bevel=0.02, rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.scale = size
    if rotation:
        obj.rotation_euler = rotation
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        modifier = obj.modifiers.new("Editable chamfer", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return finish(obj, name, material, target, rig, bone)


def segment(name, start, end, width, depth, material, target, rig, bone):
    vector = Vector(end) - Vector(start)
    center = (Vector(start) + Vector(end)) / 2
    rotation = vector.to_track_quat("Z", "Y").to_euler()
    return box(name, center, (width, depth, vector.length + .035),
               material, target, rig, bone, min(width, depth) * .13, rotation)


def sphere(name, center, scale, material, target, rig=None, bone=None):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=12, ring_count=6, radius=1, location=center)
    obj = bpy.context.object
    obj.scale = scale
    obj = finish(obj, name, material, target, rig, bone)
    for face in obj.data.polygons:
        face.use_smooth = True
    return obj


def profile(name, rings, material, target, rig, bone, center_y=0, power=3.0,
            segments=16, smooth=True):
    """用轮廓环构造连续曲面，控制太阳穴、脸颊、下巴和衣服收腰形状。"""
    vertices = []
    for z, width, depth in rings:
        for i in range(segments):
            angle = math.tau * i / segments
            cos, sin = math.cos(angle), math.sin(angle)
            x = width * math.copysign(abs(cos) ** (2 / power), cos)
            y = center_y + depth * math.copysign(abs(sin) ** (2 / power), sin)
            vertices.append((x, y, z))
    faces = [tuple(reversed(range(segments)))]
    for ring in range(len(rings) - 1):
        for i in range(segments):
            j = (i + 1) % segments
            a, b = ring * segments, (ring + 1) * segments
            faces.append((a + i, a + j, b + j, b + i))
    faces.append(tuple((len(rings) - 1) * segments + i for i in range(segments)))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    obj = finish(obj, name, material, target, rig, bone)
    for polygon in obj.data.polygons:
        polygon.use_smooth = smooth
    return obj


def cylinder(name, center, radius, depth, material, target, rig, bone,
             vertices=10, rotation=None, scale=None):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=depth, location=center)
    obj = bpy.context.object
    if rotation:
        obj.rotation_euler = rotation
    if scale:
        obj.scale = scale
    return finish(obj, name, material, target, rig, bone)


def eye_driver(obj, rig, closed):
    """眼睛开闭由共用 Action 的属性驱动，不为每套服饰复制表情动画。"""
    for property_name in ("hide_render", "hide_viewport"):
        driver = obj.driver_add(property_name).driver
        variable = driver.variables.new()
        variable.name = "open"
        variable.type = "SINGLE_PROP"
        variable.targets[0].id = rig
        variable.targets[0].data_path = '["eyes_open"]'
        driver.expression = "open >= .5" if closed else "open < .5"


def goggles(target, materials, rig, z=1.77):
    for side in (-1, 1):
        x = side * .19
        cylinder("Goggles.Rim", (x, -.275, z), .115, .10,
                 materials["ink"], target, rig, "Head",
                 rotation=(math.pi / 2, 0, 0))
        cylinder("Goggles.Lens", (x, -.333, z), .075, .012,
                 materials["glass"], target, rig, "Head",
                 rotation=(math.pi / 2, 0, 0))
    box("Goggles.Bridge", (0, -.30, z), (.15, .05, .055),
        materials["metal"], target, rig, "Head", .006)
