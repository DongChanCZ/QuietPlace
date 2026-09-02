import bpy
import os
from mathutils import Matrix, Vector


ROOT = r"C:\Users\MBC-501-14\Desktop\LabHorror\Blender"
SOURCE = os.path.join(ROOT, "LabHorror_Blockout_Step5_2F_Walls.blend")
TARGET = os.path.join(ROOT, "LabHorror_Blockout_Step6_Stair_2F_to_3F.blend")
PREVIEW_DIR = os.path.join(ROOT, "Previews")
ENTRY_PREVIEW = os.path.join(PREVIEW_DIR, "step6_stair_2f_to_3f_entry.png")
OVERVIEW_PREVIEW = os.path.join(PREVIEW_DIR, "step6_stair_2f_to_3f_overview.png")

STOREY_HEIGHT = 3.20
TOP_LEVEL = 6.40


def move_to_collection(obj, collection):
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def add_box(name, location, dimensions, material, collection, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if material:
        obj.data.materials.append(material)
    move_to_collection(obj, collection)
    if bevel:
        modifier = obj.modifiers.new("Edge_Soften", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
    return obj


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def add_camera(name, location, target, collection):
    bpy.ops.object.camera_add(location=location)
    camera = bpy.context.object
    camera.name = name
    camera.data.lens = 48
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


def world_bounds(objects):
    points = [
        obj.matrix_world @ Vector(corner)
        for obj in objects
        if obj.type == "MESH"
        for corner in obj.bound_box
    ]
    minimum = Vector(
        (
            min(point.x for point in points),
            min(point.y for point in points),
            min(point.z for point in points),
        )
    )
    maximum = Vector(
        (
            max(point.x for point in points),
            max(point.y for point in points),
            max(point.z for point in points),
        )
    )
    return minimum, maximum


if not os.path.exists(SOURCE):
    raise FileNotFoundError(SOURCE)

os.makedirs(PREVIEW_DIR, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=SOURCE)

old_step6 = bpy.data.collections.get("STEP_6_STAIR_2F_TO_3F_ONLY")
if old_step6:
    for obj in list(old_step6.all_objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.collections.remove(old_step6)

step6 = bpy.data.collections.new("STEP_6_STAIR_2F_TO_3F_ONLY")
bpy.context.scene.collection.children.link(step6)
visible_collection = bpy.data.collections.new("01_STAIR_2F_TO_3F_VISIBLE")
collision_collection = bpy.data.collections.new("02_STAIR_2F_TO_3F_COLLISION")
helper_collection = bpy.data.collections.new("99_STEP6_RENDER_HELPERS")
step6.children.link(visible_collection)
step6.children.link(collision_collection)
step6.children.link(helper_collection)

source_stair_collection = bpy.data.collections.get("01_U_STAIR_VISIBLE")
if source_stair_collection is None:
    raise RuntimeError("Source U-stair collection not found")

offset = Matrix.Translation((0.0, 0.0, STOREY_HEIGHT))
duplicated_visible = []
for source_obj in source_stair_collection.objects:
    duplicate = source_obj.copy()
    if source_obj.data:
        duplicate.data = source_obj.data.copy()
    duplicate.name = "23_" + source_obj.name
    visible_collection.objects.link(duplicate)
    duplicate.matrix_world = offset @ source_obj.matrix_world
    duplicated_visible.append(duplicate)

# The ramp colliders are stacked from the transform Step 4 stamped onto each
# guide, not from source_obj.matrix_world.  matrix_world is depsgraph output and
# Blender only evaluates objects the view layer can see, so a guide sitting in a
# hidden collection reports an identity matrix here; multiplying that by `offset`
# is what once dropped both 2F-to-3F ramps at (0, 0, 3.2), flat and unrotated.
source_collision_collection = bpy.data.collections.get("03_UNITY_COLLISION_GUIDES")
duplicated_collision = []
if source_collision_collection:
    for source_obj in source_collision_collection.objects:
        centre = source_obj.get("RAMP_CENTRE")
        pitch = source_obj.get("RAMP_PITCH_RAD")
        if centre is None or pitch is None:
            raise RuntimeError(
                f"{source_obj.name} carries no authored ramp transform; rebuild "
                "the stair with Tools/create_step4_walkable_stair.py first"
            )
        duplicate = source_obj.copy()
        if source_obj.data:
            duplicate.data = source_obj.data.copy()
        duplicate.name = "23_" + source_obj.name
        collision_collection.objects.link(duplicate)
        centre = offset @ Vector(tuple(centre))
        duplicate.matrix_world = (
            Matrix.Translation(centre) @ Matrix.Rotation(pitch, 4, "X")
        )
        duplicate["RAMP_CENTRE"] = tuple(centre)
        duplicate.hide_render = True
        duplicated_collision.append(duplicate)

# A small landing belongs to the stair itself. The rest of the 3F floor is not
# created in this step.
landing_material = bpy.data.materials.get("STAIR_Landing_Metal")
top_landing = add_box(
    "23_STAIR_TOP_LANDING_ONLY",
    (9.10, 2.60, TOP_LEVEL - 0.06),
    (2.00, 1.20, 0.12),
    landing_material,
    visible_collection,
    bevel=0.015,
)
duplicated_visible.append(top_landing)

collision_collection.hide_render = True
# Left visible to the view layer on purpose, for the reason given above: a
# hidden collection is never evaluated, so its objects would report an identity
# matrix_world to the Step 7 Unity export.  Every guide is drawn as wireframe
# and is already excluded from renders object by object.

scene = bpy.context.scene
scene["BUILD_STAGE"] = "STEP 6 - STAIR 2F TO 3F ONLY"
scene["STEP6_SCOPE"] = "One stair only; no 3F structural floor, walls, rooms, or elevator"
scene["STAIR_2F_START_LEVEL_M"] = 3.20
scene["STAIR_3F_TOP_LEVEL_M"] = TOP_LEVEL
scene["STAIR_2F_TO_3F_VISIBLE_OBJECTS"] = len(duplicated_visible)

# Use only Step 6 lights for predictable inspection renders.
for obj in scene.objects:
    if obj.type in {"CAMERA", "LIGHT"}:
        obj.hide_viewport = True
        if obj.type == "LIGHT":
            obj.hide_render = True

entry_camera = add_camera(
    "STEP6_2F_ENTRY_CAMERA",
    (8.42, 2.62, 4.78),
    (10.45, 4.25, 4.45),
    helper_collection,
)
overview_camera = add_camera(
    "STEP6_STACKED_STAIR_CAMERA",
    (15.0, 0.1, 10.0),
    (10.0, 4.25, 3.90),
    helper_collection,
)
key = add_light("STEP6_KEY", (8.0, 1.0, 11.5), (10.0, 4.3, 4.2), 1150, 6.0, helper_collection)
fill = add_light("STEP6_FILL", (13.0, 6.0, 9.0), (10.0, 4.3, 4.2), 550, 5.0, helper_collection)

world = scene.world or bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.008, 0.011, 0.016, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.34

for obj in (entry_camera, overview_camera, key, fill):
    obj.hide_viewport = False
    obj.hide_render = False

scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1100
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"

scene.camera = entry_camera
scene.render.filepath = ENTRY_PREVIEW
bpy.ops.render.render(write_still=True)

# Cut away only the east stair-room walls for the stacked-stair overview.
temporary_hidden = []
for name in ("EXT_EAST", "2F_EXT_EAST", "EAST_BLOCK_LOBBY_DIVIDER", "2F_STAIR_ROOM_WEST"):
    obj = bpy.data.objects.get(name)
    if obj:
        obj.hide_render = True
        temporary_hidden.append(obj)

scene.camera = overview_camera
scene.render.filepath = OVERVIEW_PREVIEW
bpy.ops.render.render(write_still=True)

for obj in temporary_hidden:
    obj.hide_render = False
for obj in scene.objects:
    if obj.type in {"CAMERA", "LIGHT"}:
        obj.hide_viewport = True

bpy.context.view_layer.update()
minimum, maximum = world_bounds(duplicated_visible)
scene.camera = entry_camera
bpy.ops.wm.save_as_mainfile(filepath=TARGET)

print(
    {
        "blend": TARGET,
        "entry_preview": ENTRY_PREVIEW,
        "overview_preview": OVERVIEW_PREVIEW,
        "duplicated_stair_objects": len(duplicated_visible),
        "ramp_centres": {
            ramp.name: tuple(round(value, 4) for value in ramp["RAMP_CENTRE"])
            for ramp in duplicated_collision
        },
        "stair_bounds_min": tuple(round(value, 4) for value in minimum),
        "stair_bounds_max": tuple(round(value, 4) for value in maximum),
        "scope": "stair only",
    }
)
