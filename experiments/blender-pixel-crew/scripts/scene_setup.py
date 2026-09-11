"""固定正交相机、色彩管理与灯光；像素原点由世界脚底投影确定。"""

import json
import math

import bpy
from mathutils import Vector


def read_config(path=None):
    if path:
        with open(path, encoding="utf-8") as handle:
            return json.load(handle)
    return json.loads(bpy.context.scene["crew_config_json"])


def pivot_pixels(config, size):
    return [int(component * size + .5)
            for component in config["pivot_bottom_normalized"]]


def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def configure_camera(config, direction="right", size=64):
    scene = bpy.context.scene
    camera = scene.camera
    elevation = math.radians(config["elevation_degrees"])
    azimuth = math.radians(config["directions"][direction])
    scale = config["ortho_scale"]
    pivot_x, pivot_y = pivot_pixels(config, size)
    height = (0.5 - pivot_y / size) * scale / math.cos(elevation)
    target = Vector((0, 0, height))
    offset = Vector((math.sin(azimuth) * math.cos(elevation),
                     -math.cos(azimuth) * math.cos(elevation),
                     math.sin(elevation))) * 8
    camera.location = target + offset
    point_at(camera, target)
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = scale
    camera.data.shift_x = .5 - pivot_x / size
    camera.data.shift_y = 0
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene["crew_direction"] = direction
    scene["crew_size"] = size
    return [pivot_x, size - pivot_y]


def create_stage(config):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 35
    scene.render.use_file_extension = True
    scene.render.dither_intensity = 0
    scene.render.use_compositing = False
    scene.render.use_sequencer = False
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0
    scene.view_settings.gamma = 1
    scene.eevee.taa_render_samples = config["render_samples"]
    if scene.world is None:
        scene.world = bpy.data.worlds.new("Pixel.World")
    scene.world.color = (.12, .12, .12)
    camera_data = bpy.data.cameras.new("SpriteCamera")
    camera = bpy.data.objects.new("SpriteCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    light_data = bpy.data.lights.new("Key.Sun", "SUN")
    light_data.energy = 2.0
    light_data.angle = 0
    light = bpy.data.objects.new("Key.Sun", light_data)
    scene.collection.objects.link(light)
    light.rotation_euler = (math.radians(27), math.radians(-26), math.radians(-32))
    fill_data = bpy.data.lights.new("Fill.Soft", "AREA")
    fill_data.energy = 65
    fill_data.shape = "DISK"
    fill_data.size = 5
    fill_data.use_shadow = False
    fill = bpy.data.objects.new("Fill.Soft", fill_data)
    scene.collection.objects.link(fill)
    fill.location = (3, -4, 4)
    point_at(fill, (0, 0, 1))
    scene["crew_config_json"] = json.dumps(config, ensure_ascii=False)
    scene["crew_contract"] = "Z-up, forward=-Y, rigid shared rig, feet origin, fixed canvas"
    configure_camera(config, "right", config["size"])


def set_viewport():
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                area.spaces.active.region_3d.view_perspective = "CAMERA"
                area.spaces.active.region_3d.view_camera_zoom = 15
                area.spaces.active.shading.type = "MATERIAL"
                area.spaces.active.overlay.show_floor = False
                area.spaces.active.overlay.show_extras = False
