import bpy
import os
from mathutils import Vector


ROOT = r"C:\Users\MBC-501-14\Desktop\LabHorror\Blender"
SOURCE = os.path.join(ROOT, "LabHorror_Blockout_Step6_Stair_2F_to_3F.blend")
TARGET = os.path.join(ROOT, "LabHorror_Blockout_Step7_3F_Floor.blend")
PREVIEW_DIR = os.path.join(ROOT, "Previews")
TOP_PREVIEW = os.path.join(PREVIEW_DIR, "step7_3f_floor_topdown.png")
ISO_PREVIEW = os.path.join(PREVIEW_DIR, "step7_3f_floor_isometric.png")

FLOOR_TOP = 6.40
FLOOR_BOTTOM = 6.20


def ensure_material(name, color, roughness=0.84):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return material


def move_to_collection(obj, collection):
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def add_floor_box(name, x1, x2, y1, y2, material, collection):
    bpy.ops.mesh.primitive_cube_add(
        location=((x1 + x2) * 0.5, (y1 + y2) * 0.5, (FLOOR_BOTTOM + FLOOR_TOP) * 0.5)
    )
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (x2 - x1, y2 - y1, FLOOR_TOP - FLOOR_BOTTOM)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    move_to_collection(obj, collection)
    modifier = obj.modifiers.new("Edge_Soften", "BEVEL")
    modifier.width = 0.012
    modifier.segments = 2
    return obj


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def add_camera(name, location, target, collection, ortho_scale=None):
    bpy.ops.object.camera_add(location=location)
    camera = bpy.context.object
    camera.name = name
    if ortho_scale:
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = ortho_scale
    else:
        camera.data.type = "PERSP"
        camera.data.lens = 50
    look_at(camera, target)
    move_to_collection(camera, collection)
    return camera


def add_light(name, location, target, energy, size, collection):
    bpy.ops.object.light_add(type="AREA", location=location)
    light = bpy.context.object
    light.name = name
    light.data.energy = energy
    light.data.shape = "DISK"
    light.data.size = size
    look_at(light, target)
    move_to_collection(light, collection)
    return light


if not os.path.exists(SOURCE):
    raise FileNotFoundError(SOURCE)

os.makedirs(PREVIEW_DIR, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=SOURCE)

old_step7 = bpy.data.collections.get("STEP_7_THIRD_FLOOR_SLAB_ONLY")
if old_step7:
    for obj in list(old_step7.all_objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.collections.remove(old_step7)

# The temporary Step 6 landing is replaced by the real 3F structural floor.
temporary_landing = bpy.data.objects.get("23_STAIR_TOP_LANDING_ONLY")
if temporary_landing:
    bpy.data.objects.remove(temporary_landing, do_unlink=True)

step7 = bpy.data.collections.new("STEP_7_THIRD_FLOOR_SLAB_ONLY")
bpy.context.scene.collection.children.link(step7)
floor_collection = bpy.data.collections.new("01_3F_STRUCTURAL_FLOOR")
helper_collection = bpy.data.collections.new("99_STEP7_RENDER_HELPERS")
step7.children.link(floor_collection)
step7.children.link(helper_collection)

floor_material = ensure_material("STRUCTURE_3F_Concrete", (0.19, 0.23, 0.285))
landing_material = ensure_material("STRUCTURE_3F_StairLanding", (0.245, 0.285, 0.34))

# Main 3F deck.
add_floor_box("3F_MAIN_DECK", -10.0, 8.0, -7.0, 7.0, floor_material, floor_collection)

# Elevator/service block with the same real shaft opening as 2F.
add_floor_box("3F_ELEVATOR_SOUTH", 8.0, 12.0, -7.0, -5.0, floor_material, floor_collection)
add_floor_box("3F_ELEVATOR_NORTH", 8.0, 12.0, -1.3, 2.0, floor_material, floor_collection)
add_floor_box("3F_ELEVATOR_WEST", 8.0, 9.0, -5.0, -1.3, floor_material, floor_collection)
add_floor_box("3F_ELEVATOR_EAST", 11.5, 12.0, -5.0, -1.3, floor_material, floor_collection)

# Floor around the U-stair opening. The south piece is the actual arrival
# landing and directly meets the upper flight at y=3.20.
add_floor_box("3F_STAIR_SOUTH_ARRIVAL", 8.0, 12.0, 2.0, 3.20, landing_material, floor_collection)
add_floor_box("3F_STAIR_WEST_RIM", 8.0, 8.30, 3.20, 6.65, landing_material, floor_collection)
add_floor_box("3F_STAIR_EAST_RIM", 11.70, 12.0, 3.20, 6.65, landing_material, floor_collection)
add_floor_box("3F_STAIR_NORTH_RIM", 8.0, 12.0, 6.65, 7.0, landing_material, floor_collection)

scene = bpy.context.scene
scene["BUILD_STAGE"] = "STEP 7 - THIRD FLOOR SLAB ONLY"
scene["STEP7_SCOPE"] = "3F floor only; no 3F walls, rooms, ceiling, doors, windows, or props"
scene["FLOOR_3_TOP_LEVEL_M"] = FLOOR_TOP
scene["FLOOR_3_THICKNESS_M"] = FLOOR_TOP - FLOOR_BOTTOM
scene["FLOOR_3_STAIR_OPENING"] = "x 8.30..11.70, y 3.20..6.65"
scene["FLOOR_3_ELEVATOR_OPENING"] = "x 9.00..11.50, y -5.00..-1.30"

# Hide lower-storey planning labels/walls only in Step 7 renders. They remain
# fully present and enabled in the saved Blender file.
render_hidden_collections = []
for collection_name in ("STEP_5_SECOND_FLOOR_WALLS", "02_2F_ROOM_LABELS"):
    collection = bpy.data.collections.get(collection_name)
    if collection:
        collection.hide_render = True
        render_hidden_collections.append(collection)

for obj in scene.objects:
    if obj.type in {"CAMERA", "LIGHT"}:
        obj.hide_viewport = True
        if obj.type == "LIGHT":
            obj.hide_render = True

top_camera = add_camera("STEP7_3F_TOP_CAMERA", (1.0, 0.0, 16.0), (1.0, 0.0, FLOOR_TOP), helper_collection, 27.0)
iso_camera = add_camera("STEP7_3F_ISO_CAMERA", (22.0, -25.0, 21.0), (1.0, 0.0, 6.0), helper_collection)
key = add_light("STEP7_3F_KEY", (0.0, -3.0, 15.0), (1.0, 0.0, 6.0), 1300, 15.0, helper_collection)
fill = add_light("STEP7_3F_FILL", (-9.0, 9.0, 13.0), (1.0, 0.0, 6.0), 600, 9.0, helper_collection)

world = scene.world or bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.008, 0.011, 0.016, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.34

for obj in (top_camera, iso_camera, key, fill):
    obj.hide_viewport = False
    obj.hide_render = False

scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1200
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"

scene.camera = top_camera
scene.render.filepath = TOP_PREVIEW
bpy.ops.render.render(write_still=True)

scene.camera = iso_camera
scene.render.filepath = ISO_PREVIEW
bpy.ops.render.render(write_still=True)

for collection in render_hidden_collections:
    collection.hide_render = False
for obj in scene.objects:
    if obj.type in {"CAMERA", "LIGHT"}:
        obj.hide_viewport = True

scene.camera = top_camera
bpy.ops.wm.save_as_mainfile(filepath=TARGET)

print(
    {
        "blend": TARGET,
        "top_preview": TOP_PREVIEW,
        "isometric_preview": ISO_PREVIEW,
        "floor_objects": len(floor_collection.objects),
        "floor_top_m": FLOOR_TOP,
        "floor_thickness_m": FLOOR_TOP - FLOOR_BOTTOM,
        "scope": "3F floor only",
    }
)
