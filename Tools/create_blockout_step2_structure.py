import bpy
import math
import os
from mathutils import Vector


ROOT = r"C:\Users\MBC-501-14\Desktop\LabHorror\Blender"
STEP1 = os.path.join(ROOT, "LabHorror_Blockout_Step1.blend")
STEP2 = os.path.join(ROOT, "LabHorror_Blockout_Step2_Structure.blend")
PREVIEW_DIR = os.path.join(ROOT, "Previews")
TOP_PREVIEW = os.path.join(PREVIEW_DIR, "step2_structure_topdown.png")
ISO_PREVIEW = os.path.join(PREVIEW_DIR, "step2_structure_isometric.png")
STAIR_ASSET = r"C:\Users\MBC-501-14\Desktop\3D 게임벽 미니프로잭트\계단.glb"

WALL_HEIGHT = 3.20
WALL_THICKNESS = 0.22
# Corner seal: a wall centred on x runs to its neighbour's OUTER face, not to
# the neighbour's centre line. Stopping at the centre line leaves a
# 0.11 x 0.11 m slot open through the full storey height at every corner.
HALF = WALL_THICKNESS * 0.5


def ensure_material(name, color, roughness=0.82):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


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
    obj.data.materials.append(material)
    move_to_collection(obj, collection)
    if bevel > 0:
        modifier = obj.modifiers.new("Edge_Soften", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
    return obj


def add_wall_x(name, x1, x2, y, z1=0.0, z2=WALL_HEIGHT):
    if x2 - x1 <= 0.001 or z2 - z1 <= 0.001:
        return None
    return add_box(
        name,
        ((x1 + x2) * 0.5, y, (z1 + z2) * 0.5),
        (x2 - x1, WALL_THICKNESS, z2 - z1),
        WALL_MAT,
        WALLS,
        bevel=0.0015,
    )


def add_wall_y(name, x, y1, y2, z1=0.0, z2=WALL_HEIGHT):
    if y2 - y1 <= 0.001 or z2 - z1 <= 0.001:
        return None
    return add_box(
        name,
        (x, (y1 + y2) * 0.5, (z1 + z2) * 0.5),
        (WALL_THICKNESS, y2 - y1, z2 - z1),
        WALL_MAT,
        WALLS,
        bevel=0.0015,
    )


def wall_x_with_openings(name, x1, x2, y, openings):
    """Build a horizontal wall. Openings are center, width, sill, height."""
    ordered = sorted(openings, key=lambda item: item[0])
    cursor = x1
    for index, (center, width, sill, height) in enumerate(ordered, start=1):
        opening_start = center - width * 0.5
        opening_end = center + width * 0.5
        add_wall_x(f"{name}_SOLID_{index:02d}", cursor, opening_start, y)
        if sill > 0:
            add_wall_x(f"{name}_SILL_{index:02d}", opening_start, opening_end, y, 0, sill)
        top = sill + height
        if top < WALL_HEIGHT:
            add_wall_x(f"{name}_HEADER_{index:02d}", opening_start, opening_end, y, top, WALL_HEIGHT)
        cursor = opening_end
    add_wall_x(f"{name}_SOLID_END", cursor, x2, y)


def wall_y_with_openings(name, x, y1, y2, openings):
    """Build a vertical wall. Openings are center, width, sill, height."""
    ordered = sorted(openings, key=lambda item: item[0])
    cursor = y1
    for index, (center, width, sill, height) in enumerate(ordered, start=1):
        opening_start = center - width * 0.5
        opening_end = center + width * 0.5
        add_wall_y(f"{name}_SOLID_{index:02d}", x, cursor, opening_start)
        if sill > 0:
            add_wall_y(f"{name}_SILL_{index:02d}", x, opening_start, opening_end, 0, sill)
        top = sill + height
        if top < WALL_HEIGHT:
            add_wall_y(f"{name}_HEADER_{index:02d}", x, opening_start, opening_end, top, WALL_HEIGHT)
        cursor = opening_end
    add_wall_y(f"{name}_SOLID_END", x, cursor, y2)


def look_at(camera, target):
    direction = Vector(target) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def add_camera(name, location, target, ortho_scale=None):
    bpy.ops.object.camera_add(location=location)
    camera = bpy.context.object
    camera.name = name
    if ortho_scale is not None:
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = ortho_scale
    else:
        camera.data.type = "PERSP"
        camera.data.lens = 48
    look_at(camera, target)
    move_to_collection(camera, HELPERS)
    return camera


def add_area_light(name, location, energy, size):
    bpy.ops.object.light_add(type="AREA", location=location)
    light = bpy.context.object
    light.name = name
    light.data.energy = energy
    light.data.shape = "DISK"
    light.data.size = size
    move_to_collection(light, HELPERS)
    return light


def object_bounds(objects):
    points = []
    for obj in objects:
        if obj.type != "MESH":
            continue
        points.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    if not points:
        raise RuntimeError("Imported stair asset contains no mesh bounds")
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


def import_user_stair():
    if not os.path.exists(STAIR_ASSET):
        raise FileNotFoundError(STAIR_ASSET)

    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=STAIR_ASSET)
    imported = list(set(bpy.data.objects) - before)
    bpy.context.view_layer.update()
    raw_minimum, raw_maximum = object_bounds(imported)
    raw_center = (raw_minimum + raw_maximum) * 0.5
    raw_size = raw_maximum - raw_minimum

    root = bpy.data.objects.new("USER_STAIR_ROOT", None)
    STAIRS.objects.link(root)
    for obj in imported:
        world_matrix = obj.matrix_world.copy()
        move_to_collection(obj, STAIRS)
        if obj.parent not in imported:
            obj.parent = root
            obj.matrix_world = world_matrix

    # Keep the original horizontal proportions. The 4.40 m run leaves a
    # 30 cm clearance at each end of the approved 5 m stair room.
    horizontal_scale = 4.40 / raw_size.y
    # The model's upper walking platform lands at about 2.97 m. Its railings
    # continue above that level naturally into the next storey.
    vertical_scale = 5.50
    root.scale = (horizontal_scale, horizontal_scale, vertical_scale)
    root.location = (
        10.85 - raw_center.x * horizontal_scale,
        4.50 - raw_center.y * horizontal_scale,
        -raw_minimum.z * vertical_scale,
    )
    bpy.context.view_layer.update()

    final_minimum, final_maximum = object_bounds(imported)
    root["SOURCE_ASSET"] = STAIR_ASSET
    root["FINAL_SIZE_METRES"] = tuple(round(value, 4) for value in (final_maximum - final_minimum))
    root["PLACEMENT_NOTE"] = "Bottom flight faces the stair-room door; west-side access clearance retained."
    return root, imported, final_minimum, final_maximum


if not os.path.exists(STEP1):
    raise FileNotFoundError(STEP1)

os.makedirs(PREVIEW_DIR, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=STEP1)

# Step 1 stays untouched. Re-running this script only replaces Step 2 content.
old_step2 = bpy.data.collections.get("STEP_2_STRUCTURE")
if old_step2:
    for obj in list(old_step2.all_objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.collections.remove(old_step2)

STEP2_COLLECTION = bpy.data.collections.new("STEP_2_STRUCTURE")
bpy.context.scene.collection.children.link(STEP2_COLLECTION)
WALLS = bpy.data.collections.new("01_WALLS_AND_OPENINGS")
STAIRS = bpy.data.collections.new("02_STAIR_STRUCTURE")
HELPERS = bpy.data.collections.new("99_RENDER_HELPERS")
STEP2_COLLECTION.children.link(WALLS)
STEP2_COLLECTION.children.link(STAIRS)
STEP2_COLLECTION.children.link(HELPERS)

WALL_MAT = ensure_material("STRUCTURE_Wall", (0.34, 0.39, 0.45))
STAIR_MAT = ensure_material("STRUCTURE_Stair", (0.22, 0.25, 0.29))

# Hide the Step 1 measuring lines and text in Step 2. The approved floor frame remains visible.
for collection_name in ("02_GUIDE_LINES", "03_LABELS"):
    collection = bpy.data.collections.get(collection_name)
    if collection:
        collection.hide_render = True
        collection.hide_viewport = True

# Exterior shell. Door and window openings are real empty spaces in the wall geometry.
wall_y_with_openings(
    "EXT_WEST",
    -10.0,
    -7.0 - HALF,
    7.0 + HALF,
    [(4.5, 2.40, 1.10, 1.20)],  # Guard-room entry window.
)
wall_x_with_openings(
    "EXT_SOUTH",
    -10.0,
    12.0,
    -7.0,
    [(-1.0, 2.40, 1.10, 1.20), (4.1, 2.40, 1.10, 1.20)],
)
add_wall_y("EXT_EAST", 12.0, -7.0 - HALF, 7.0 + HALF)
add_wall_x("EXT_NORTH_WEST", -10.0, -2.0, 7.0)
add_wall_x("EXT_NORTH_EAST", 2.0, 12.0, 7.0)
add_wall_y("VESTIBULE_WEST", -2.0, 7.0 - HALF, 9.0 + HALF)
add_wall_y("VESTIBULE_EAST", 2.0, 7.0 - HALF, 9.0 + HALF)
wall_x_with_openings(
    "MAIN_ENTRANCE",
    -2.0,
    2.0,
    9.0,
    [(0.0, 2.60, 0.0, 2.50)],
)

# West wing: two rooms with only the lobby-facing door openings.
wall_y_with_openings(
    "WEST_WING_LOBBY_DIVIDER",
    -5.0,
    -7.0,
    7.0,
    [(-2.5, 1.30, 0.0, 2.40), (4.5, 1.50, 0.0, 2.40)],
)
add_wall_x("GUARD_MEDICAL_DIVIDER", -10.0, -5.0, 2.0)

# East circulation block: one elevator opening and one stair-room opening.
wall_y_with_openings(
    "EAST_BLOCK_LOBBY_DIVIDER",
    8.0,
    -7.0,
    7.0,
    [(-2.5, 1.50, 0.0, 2.40), (2.85, 1.40, 0.0, 2.40)],
)
add_wall_x("STAIR_ELEVATOR_DIVIDER", 8.0, 12.0, 2.0)

# Use the user's authored stair asset instead of procedural blockout steps.
stair_root, stair_parts, stair_minimum, stair_maximum = import_user_stair()

scene = bpy.context.scene
scene["BUILD_STAGE"] = "STEP 2 - STRUCTURE ONLY"
scene["WALL_HEIGHT_METRES"] = WALL_HEIGHT
scene["SOURCE_FRAME"] = "LabHorror_Blockout_Step1.blend"
scene["STAIR_SOURCE"] = STAIR_ASSET

# Reuse the Step 1 world and light, then add a soft fill for readable structure previews.
world = scene.world or bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.008, 0.011, 0.016, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.45

for old_name in ("STEP1_TOP_CAMERA", "STEP1_AREA_LIGHT"):
    old_obj = bpy.data.objects.get(old_name)
    if old_obj:
        old_obj.hide_render = True
        old_obj.hide_viewport = True

top_camera = add_camera("STEP2_TOP_CAMERA", (1.0, 0.0, 30.0), (1.0, 0.0, 0.0), 28.0)
iso_camera = add_camera("STEP2_ISOMETRIC_CAMERA", (22.0, -27.0, 23.0), (1.0, 0.2, 1.0))
add_area_light("STEP2_KEY_LIGHT", (0.0, -3.0, 15.0), 1750, 17.0)
fill = add_area_light("STEP2_FILL_LIGHT", (-10.0, 10.0, 9.0), 850, 10.0)
look_at(fill, (0.0, 0.0, 1.0))

scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1200
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"

# A true wall keeps its sill and lintel. For the top preview only, hide those
# pieces so every door/window position reads like an architectural cut plan.
top_cutaway_parts = [
    obj
    for obj in WALLS.objects
    if "_HEADER_" in obj.name or "_SILL_" in obj.name
]
for obj in top_cutaway_parts:
    obj.hide_render = True

scene.camera = top_camera
scene.render.filepath = TOP_PREVIEW
bpy.ops.render.render(write_still=True)

for obj in top_cutaway_parts:
    obj.hide_render = False

scene.camera = iso_camera
scene.render.filepath = ISO_PREVIEW
bpy.ops.render.render(write_still=True)

# Save with the top camera active so the first inspection is the full floor structure.
scene.camera = top_camera
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=STEP2)

print(
    {
        "blend": STEP2,
        "top_preview": TOP_PREVIEW,
        "isometric_preview": ISO_PREVIEW,
        "wall_objects": len(WALLS.objects),
        "stair_objects": len(STAIRS.objects),
        "stair_final_size_m": tuple(round(value, 4) for value in (stair_maximum - stair_minimum)),
        "wall_height_m": WALL_HEIGHT,
    }
)
