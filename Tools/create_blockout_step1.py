import bpy
import math
import os

ROOT = r"C:\Users\MBC-501-14\Desktop\LabHorror\Blender"
BLEND = os.path.join(ROOT, "LabHorror_Blockout_Step1.blend")
PREVIEW = os.path.join(ROOT, "Previews", "step1_frame_topdown.png")

bpy.ops.wm.read_factory_settings(use_empty=True)

def material(name, color, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.82
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission
    return mat

MATS = {
    "guard": material("ZONE_Guard", (0.08, 0.27, 0.32)),
    "medical": material("ZONE_Medical", (0.17, 0.19, 0.22)),
    "lobby": material("ZONE_Lobby", (0.12, 0.14, 0.17)),
    "stairs": material("ZONE_Stairs", (0.27, 0.16, 0.08)),
    "elevator": material("ZONE_Elevator", (0.10, 0.13, 0.23)),
    "entrance": material("ZONE_Entrance", (0.11, 0.29, 0.23)),
    "outer": material("GUIDE_Outer", (0.12, 0.78, 0.88), 0.8),
    "inner": material("GUIDE_Inner", (0.92, 0.47, 0.08), 0.5),
    "text": material("GUIDE_Text", (0.80, 0.88, 0.90), 0.4),
}

frame = bpy.data.collections.new("STEP_1_FRAME_ONLY")
bpy.context.scene.collection.children.link(frame)
floors = bpy.data.collections.new("01_ZONE_FLOORS")
guides = bpy.data.collections.new("02_GUIDE_LINES")
labels = bpy.data.collections.new("03_LABELS")
frame.children.link(floors); frame.children.link(guides); frame.children.link(labels)

def cube(name, location, scale, mat, collection):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = (scale[0] / 2, scale[1] / 2, scale[2] / 2)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    for col in list(obj.users_collection): col.objects.unlink(obj)
    collection.objects.link(obj)
    return obj

def line(name, x, y, sx, sy, mat):
    return cube(name, (x, y, 0.035), (sx, sy, 0.07), mat, guides)

def label(text, x, y, size=0.46):
    bpy.ops.object.text_add(location=(x, y, 0.10))
    obj = bpy.context.object
    obj.name = "LABEL_" + text.replace(" ", "_")
    obj.data.body = text
    obj.data.align_x = 'CENTER'
    obj.data.align_y = 'CENTER'
    obj.data.size = size
    obj.data.extrude = 0.006
    obj.data.materials.append(MATS["text"])
    for col in list(obj.users_collection): col.objects.unlink(obj)
    labels.objects.link(obj)

# 1 Blender unit = 1 metre. No walls, ceilings, doors, windows or gameplay objects.
zones = [
    ("Guard Room 5x5m", (-7.5, 4.5, -0.10), (5, 5, 0.20), "guard", "GUARD 5 x 5m"),
    ("Medical Room 5x9m", (-7.5, -2.5, -0.10), (5, 9, 0.20), "medical", "MEDICAL 5 x 9m"),
    ("Main Lobby 13x14m", (1.5, 0, -0.10), (13, 14, 0.20), "lobby", "LOBBY 13 x 14m"),
    ("Emergency Stair 4x5m", (10, 4.5, -0.10), (4, 5, 0.20), "stairs", "STAIRS 4 x 5m"),
    ("Elevator Block 4x9m", (10, -2.5, -0.10), (4, 9, 0.20), "elevator", "ELEVATOR 4 x 9m"),
    ("Entrance Vestibule 4x2m", (0, 8, -0.10), (4, 2, 0.20), "entrance", "ENTRANCE 4 x 2m"),
]
for name, loc, scale, mat, text in zones:
    cube(name, loc, scale, MATS[mat], floors)
    label(text, loc[0], loc[1])

w = 0.09
# Irregular exterior outline.
line("OUTER_West", -10, 0, w, 14, MATS["outer"])
line("OUTER_South", 1, -7, 22, w, MATS["outer"])
line("OUTER_East", 12, 0, w, 14, MATS["outer"])
line("OUTER_North_West", -6, 7, 8, w, MATS["outer"])
line("OUTER_North_East", 7, 7, 10, w, MATS["outer"])
line("OUTER_Entry_West", -2, 8, w, 2, MATS["outer"])
line("OUTER_Entry_East", 2, 8, w, 2, MATS["outer"])
line("OUTER_Entry_North", 0, 9, 4, w, MATS["outer"])
# Internal boundary guides only.
line("GUIDE_West_Wing", -5, 0, w, 14, MATS["inner"])
line("GUIDE_Guard_Medical", -7.5, 2, 5, w, MATS["inner"])
line("GUIDE_East_Block", 8, 0, w, 14, MATS["inner"])
line("GUIDE_Stair_Elevator", 10, 2, 4, w, MATS["inner"])
# Scale reference.
line("SCALE_5m", -7.5, -8.2, 5, 0.14, MATS["outer"])
label("5 METRES", -7.5, -8.65, 0.36)

world = bpy.context.scene.world or bpy.data.worlds.new("World")
bpy.context.scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.007, 0.010, 0.014, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.35

bpy.ops.object.camera_add(location=(1, 0, 27))
camera = bpy.context.object
camera.name = "STEP1_TOP_CAMERA"
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 28
bpy.context.scene.camera = camera

bpy.ops.object.light_add(type='AREA', location=(1, 0, 12))
light = bpy.context.object
light.name = "STEP1_AREA_LIGHT"
light.data.energy = 1200
light.data.shape = 'RECTANGLE'
light.data.size = 25
light.data.size_y = 18

scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 1200
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = PREVIEW
scene.render.film_transparent = False
scene.view_settings.look = 'AgX - Medium High Contrast'

bpy.ops.wm.save_as_mainfile(filepath=BLEND)
bpy.ops.render.render(write_still=True)
print({"blend": BLEND, "preview": PREVIEW, "zones": len(zones), "walls": 0})
