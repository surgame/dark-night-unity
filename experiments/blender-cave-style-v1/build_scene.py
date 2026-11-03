"""Build an isolated, editable 2.5D cave style study in Blender 4.4."""

import math
import random
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
SOURCE = Path("C:/Users/Jobscn/.codex/visualizations/2026/10/29/01a18683-ec6b-7c81-82f7-5707635d072c")
ROCK = SOURCE / "painted-map-workbench-v5/opaque-rock-source.png"
FAR = SOURCE / "painted-cave-depth-v4/far-source.png"
HERO = SOURCE / "painted-cave-rebuild-v2/hero-embedded.webp"
random.seed(20261029)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for collection in list(bpy.data.collections):
    if collection.users == 0:
        bpy.data.collections.remove(collection)


def collection(name):
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    return c


terrain = collection("01 Terrain - opaque meshes")
details = collection("02 Rock and moss details")
background = collection("03 Painted distant backdrop")
characters = collection("04 Scale reference")
lighting = collection("05 Cameras and lights")


def move(obj, target):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    target.objects.link(obj)


def material(name, color, roughness=1):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = roughness
    return m


def image_material(name, path, emission=False, alpha=False):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nodes = m.node_tree.nodes
    links = m.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    image = nodes.new("ShaderNodeTexImage")
    image.image = bpy.data.images.load(str(path), check_existing=True)
    image.interpolation = "Linear"
    links.new(image.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 1
    if emission:
        links.new(image.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 0.7
    if alpha:
        links.new(image.outputs["Alpha"], bsdf.inputs["Alpha"])
        m.surface_render_method = "DITHERED"
    return m


rock = image_material("Painted rock - opaque", ROCK)
far = image_material("Painted distant cave - opaque", FAR, emission=True)
hero = image_material("Character reference - alpha", HERO, emission=True, alpha=True)
moss = material("Moss and soil top", (0.27, 0.30, 0.10))
edge = material("Dark ground front edge", (0.065, 0.083, 0.052))
moss_nodes = moss.node_tree.nodes
moss_links = moss.node_tree.links
moss_noise = moss_nodes.new("ShaderNodeTexNoise")
moss_noise.inputs["Scale"].default_value = 12
moss_noise.inputs["Detail"].default_value = 3
moss_coordinates = moss_nodes.new("ShaderNodeTexCoord")
moss_links.new(moss_coordinates.outputs["Object"], moss_noise.inputs["Vector"])
moss_ramp = moss_nodes.new("ShaderNodeValToRGB")
moss_ramp.color_ramp.elements[0].position = 0.22
moss_ramp.color_ramp.elements[0].color = (0.10, 0.13, 0.04, 1)
moss_ramp.color_ramp.elements[1].position = 0.8
moss_ramp.color_ramp.elements[1].color = (0.39, 0.39, 0.15, 1)
moss_links.new(moss_noise.outputs["Fac"], moss_ramp.inputs["Fac"])
moss_links.new(moss_ramp.outputs["Color"], moss_nodes.get("Principled BSDF").inputs["Base Color"])
stone_mats = [
    material("Rock volume %02d" % i, c) for i, c in enumerate([
        (0.12, 0.16, 0.13), (0.16, 0.19, 0.15), (0.22, 0.23, 0.17),
        (0.28, 0.27, 0.18), (0.32, 0.30, 0.20)])
]
gold = material("Ore - warm ochre", (0.80, 0.45, 0.08))
gold_light = material("Ore - bright face", (0.95, 0.68, 0.18))


def mesh(name, verts, faces, mat, target, uv=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    target.objects.link(obj)
    obj.data.materials.append(mat)
    if uv:
        layer = data.uv_layers.new(name="UVMap")
        for poly in data.polygons:
            for idx in poly.loop_indices:
                layer.data[idx].uv = uv(data.vertices[data.loops[idx].vertex_index].co)
    return obj


def quad(name, verts, mat, target):
    return mesh(name, verts, [(0, 1, 2, 3)], mat, target,
                lambda v: ((v.x + 10) / 20, (v.z + 2) / 10))


# Side silhouette is the editable authoring profile. The top surface has real depth.
profile = [(-10, 1.4), (-8.4, 1.25), (-6.8, 1.16), (-5.5, 0.94),
           (-4.2, 0.65), (-2.8, 0.48), (-1.2, 0.35), (0.4, 0.30),
           (2, 0.27), (3.5, 0.23), (5, 0.20), (6.6, 0.20), (8.5, 0.22), (10, 0.25)]
verts = []
for x, z in profile:
    verts.extend([(x, -1.35, z), (x, 1.35, z), (x, -1.35, -3.1), (x, 1.35, -3.1)])
faces = []
for i in range(len(profile) - 1):
    a, b = i * 4, (i + 1) * 4
    faces.extend([(a, b, b + 1, a + 1), (a + 2, b + 2, b, a),
                  (a + 1, b + 1, b + 3, a + 3), (a + 3, b + 3, b + 2, a + 2)])
ground = mesh("Ground - continuous solid profile", verts, faces, rock, terrain,
              lambda v: ((v.x + 10) / 8, (v.z + 3.1) / 4.2))
ground.data.materials.append(moss)
for p in ground.data.polygons:
    if p.index % 4 == 0:
        p.material_index = 1

# A darker lip makes the surface/front boundary readable in a side view.
for i in range(len(profile) - 1):
    (x0, z0), (x1, z1) = profile[i:i + 2]
    quad("Front lip %02d" % i,
         [(x0, -1.38, z0), (x1, -1.38, z1),
          (x1, -1.38, z1 - 0.13), (x0, -1.38, z0 - 0.13)], edge, details)

# A sculpted ceiling profile with true thickness, not a transparent overlay.
ceiling = [(-10, 6.4), (-8.5, 6.0), (-7, 6.15), (-5.5, 5.9),
           (-4, 6.05), (-2.5, 5.75), (-1, 5.8), (0.5, 5.45),
           (2, 5.35), (3.4, 4.95), (4.6, 4.6), (5.8, 4.3),
           (7.2, 4.15), (8.4, 4.5), (10, 4.65)]
verts = []
for x, z in ceiling:
    verts.extend([(x, -0.9, z), (x, 1.1, z), (x, -0.9, 8.7), (x, 1.1, 8.7)])
faces = []
for i in range(len(ceiling) - 1):
    a, b = i * 4, (i + 1) * 4
    faces.extend([(a, a + 1, b + 1, b), (a, b, b + 2, a + 2),
                  (a + 1, a + 3, b + 3, b + 1)])
mesh("Ceiling - continuous solid profile", verts, faces, rock, terrain,
     lambda v: ((v.x + 10) / 9, (v.z - 4) / 5))

mesh("Distant painted cave", [(-15, 6, -3), (15, 6, -3), (15, 6, 10), (-15, 6, 10)],
     [(0, 1, 2, 3)], far, background, lambda v: ((v.x + 15) / 30, (v.z + 3) / 13))


def rock_volume(name, pos, scale, mat, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=pos)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    for v in obj.data.vertices:
        v.co *= random.uniform(0.87, 1.12)
    obj.data.materials.append(mat)
    move(obj, details)
    return obj


# Right wall is a volumetric assembly, with the ore in front of its surface.
for row in range(5):
    for col in range(3):
        x = 6.2 + col * 1.05 + random.uniform(-0.18, 0.18)
        z = 0.8 + row * 0.85 + random.uniform(-0.14, 0.14)
        rock_volume("Wall boulder %d-%d" % (row, col), (x, 0.15, z),
                    (random.uniform(0.68, 0.9), 0.9, random.uniform(0.55, 0.75)),
                    stone_mats[min(4, (row + col) % 5)])
for i, (x, z) in enumerate([(5.9, 1.0), (6.3, 1.8), (6.2, 2.7), (6.5, 3.45)]):
    rock_volume("Ore %02d" % i, (x, -0.83, z), (0.14, 0.08, 0.23), gold, 1)
    rock_volume("Ore glint %02d" % i, (x - 0.035, -0.92, z + 0.045),
                (0.045, 0.03, 0.09), gold_light, 1)


def ground_height(x):
    for (x0, z0), (x1, z1) in zip(profile, profile[1:]):
        if x0 <= x <= x1:
            return z0 + (z1 - z0) * (x - x0) / (x1 - x0)
    return 0.25


for i in range(40):
    x = random.uniform(-9.8, 8.8)
    y = random.uniform(-1.0, 0.9)
    size = random.uniform(0.05, 0.16)
    rock_volume("Ground pebble %02d" % i, (x, y, ground_height(x) + size * 0.28),
                (size * 1.5, size, size * 0.75), stone_mats[i % 4], 1)

# Hero is a billboard cutout only, not a replacement for the game's Spine actor.
hx, hy, hz = 2.7, -0.24, ground_height(2.7)
w, h = 0.95, 1.12
mesh("Character size reference - billboard",
     [(hx-w/2, hy, hz), (hx+w/2, hy, hz),
      (hx+w/2, hy, hz+h), (hx-w/2, hy, hz+h)], [(0, 1, 2, 3)],
     hero, characters,
     lambda v: ((v.x - (hx-w/2)) / w, (v.z-hz) / h))


def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def area(name, location, energy, color, size, target):
    data = bpy.data.lights.new(name, "AREA")
    data.energy, data.color, data.shape, data.size = energy, color, "DISK", size
    obj = bpy.data.objects.new(name, data)
    lighting.objects.link(obj)
    obj.location = location
    aim(obj, target)


area("Soft daylight", (-6, -6, 9), 1200, (0.79, 0.90, 0.78), 10, (0, 0, 0))
area("Warm mineral bounce", (7, -4, 5), 550, (1, 0.68, 0.35), 5, (6, 0, 2))
scene = bpy.context.scene
scene.world.color = (0.20, 0.20, 0.20)
camera_data = bpy.data.cameras.new("Game view - orthographic")
camera = bpy.data.objects.new("Game view - orthographic", camera_data)
lighting.objects.link(camera)
camera.location = (0, -24, 9)
aim(camera, (0, 0, 2.7))
camera_data.type = "ORTHO"
camera_data.ortho_scale = 18
scene.camera = camera
scene.render.engine = "CYCLES"
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.render.resolution_x = 1440
scene.render.resolution_y = 810
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = "AgX"
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = str(ROOT / "cave-game-view.png")
bpy.ops.file.pack_all()
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / "cave-style-study.blend"))
bpy.ops.render.render(write_still=True)

# A second camera angle makes the actual mesh depth visible for inspection.
camera.location = (7, -19, 12)
aim(camera, (0, 0, 2))
camera_data.ortho_scale = 20
scene.render.filepath = str(ROOT / "cave-depth-inspection.png")
bpy.ops.render.render(write_still=True)
