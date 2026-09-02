import bpy
import math
import os
from mathutils import Matrix, Vector


ROOT = r"C:\Users\MBC-501-14\Desktop\LabHorror\Blender"
SOURCE = os.path.join(ROOT, "LabHorror_Blockout_Step3_2F_Access.blend")
TARGET = os.path.join(ROOT, "LabHorror_Blockout_Step4_WalkableStair.blend")
PREVIEW_DIR = os.path.join(ROOT, "Previews")
ENTRY_PREVIEW = os.path.join(PREVIEW_DIR, "step4_walkable_stair_entry.png")
OVERVIEW_PREVIEW = os.path.join(PREVIEW_DIR, "step4_walkable_stair_overview.png")

FLOOR_2 = 3.20
MID_LEVEL = 1.60
RISER_COUNT_PER_FLIGHT = 9
RISER_HEIGHT = MID_LEVEL / RISER_COUNT_PER_FLIGHT
TREAD_DEPTH = 0.27
TREAD_OVERHANG = 0.012
FLIGHT_RUN = TREAD_DEPTH * RISER_COUNT_PER_FLIGHT
FLIGHT_WIDTH = 1.40
FLIGHT_Y_SOUTH = 3.20
FLIGHT_Y_NORTH = FLIGHT_Y_SOUTH + FLIGHT_RUN
LANDING_Y_NORTH = 6.65

# Unity walks the player up a ramp collider rather than up the treads, so the
# ramp is wider than the flight: the extra width closes the gap along the outer
# stairwell wall the player could otherwise slip into, and the thickness guards
# against tunnelling.
RAMP_WIDTH = 1.70
RAMP_THICKNESS = 0.20
RAMP_WALL_MARGIN = RAMP_WIDTH - FLIGHT_WIDTH
RAMP_SLOPE = math.atan2(MID_LEVEL, FLIGHT_RUN)
RAMP_LENGTH = math.hypot(FLIGHT_RUN, MID_LEVEL)


def ensure_material(name, color, metallic=0.0, roughness=0.75):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return material


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


def add_floor_box(name, x1, x2, y1, y2, material, collection):
    return add_box(
        name,
        ((x1 + x2) * 0.5, (y1 + y2) * 0.5, 3.10),
        (x2 - x1, y2 - y1, 0.20),
        material,
        collection,
        bevel=0.01,
    )


def add_beam_box(name, start, end, thickness, material, collection):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    length = direction.length
    obj = add_box(
        name,
        (start + end) * 0.5,
        (thickness, length, thickness),
        material,
        collection,
        bevel=thickness * 0.12,
    )
    obj.rotation_euler = direction.to_track_quat("Y", "Z").to_euler()
    return obj


def add_pipe(name, start, end, radius, material, collection):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=16,
        radius=radius,
        depth=direction.length,
        location=(start + end) * 0.5,
    )
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    obj.data.materials.append(material)
    move_to_collection(obj, collection)
    return obj


def add_post(name, x, y, base_z, height, material, collection):
    return add_pipe(
        name,
        (x, y, base_z),
        (x, y, base_z + height),
        0.035,
        material,
        collection,
    )


def add_flight_ramp(name, x1, x2, top_nosing, climb, material, collection):
    """Traversal ramp for one flight, lying on the tread-nosing plane: the low
    end rests on the surface the flight departs from, the high end on the nosing
    of the highest tread.  `top_nosing` is that nosing as (y, z), and `climb` is
    +1 for a flight climbing north, -1 for one climbing south.

    Both ends are computed from the stair numbers instead of being measured off
    the placed treads, and the whole transform is assigned at once.  matrix_world
    is depsgraph output and Blender only evaluates what the view layer can see,
    so reading it back off a collision guide can hand over an identity matrix.
    """
    top_y, top_z = top_nosing
    bottom_y = top_y - FLIGHT_RUN * climb
    bottom_z = top_z - MID_LEVEL
    centre = Vector(
        ((x1 + x2) * 0.5, (bottom_y + top_y) * 0.5, (bottom_z + top_z) * 0.5)
    )
    pitch = RAMP_SLOPE * climb
    ramp = add_box(
        name,
        centre,
        (x2 - x1, RAMP_LENGTH, RAMP_THICKNESS),
        material,
        collection,
    )
    ramp.matrix_world = Matrix.Translation(centre) @ Matrix.Rotation(pitch, 4, "X")
    ramp.display_type = "WIRE"
    ramp.hide_render = True
    ramp["UNITY_COMPONENT"] = "BoxCollider"
    ramp["SLOPE_DEGREES"] = round(math.degrees(RAMP_SLOPE), 3)
    # Step 6 stacks a copy of this ramp onto the 2F-to-3F flight and reads the
    # transform from here, so that it never has to touch matrix_world either.
    ramp["RAMP_CENTRE"] = tuple(centre)
    ramp["RAMP_PITCH_RAD"] = pitch
    return ramp


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


if not os.path.exists(SOURCE):
    raise FileNotFoundError(SOURCE)

os.makedirs(PREVIEW_DIR, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=SOURCE)

# Preserve the user's GLB in the file, but hide it instead of forcing it into
# an unsuitable stairwell ratio.
original_root = bpy.data.objects.get("USER_STAIR_ROOT")
if original_root:
    original_root.hide_viewport = True
    original_root.hide_render = True
    original_root["STATUS"] = "Archived hidden - original asset does not fit 4x5m walkable stairwell"
    for child in original_root.children_recursive:
        child.hide_viewport = True
        child.hide_render = True

# Remove only the Step 3 floor pieces around the former straight stair.
for name in (
    "2F_STAIR_WEST_WALKWAY",
    "2F_STAIR_TOP_EXIT_LANDING",
    "2F_STAIR_EAST_RIM",
    "2F_STAIR_SOUTH_RIM",
):
    obj = bpy.data.objects.get(name)
    if obj:
        bpy.data.objects.remove(obj, do_unlink=True)

old_step4 = bpy.data.collections.get("STEP_4_REAL_U_STAIR")
if old_step4:
    for obj in list(old_step4.all_objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.collections.remove(old_step4)

step4 = bpy.data.collections.new("STEP_4_REAL_U_STAIR")
bpy.context.scene.collection.children.link(step4)
stair_collection = bpy.data.collections.new("01_U_STAIR_VISIBLE")
floor_collection = bpy.data.collections.new("02_2F_STAIR_OPENING")
collision_collection = bpy.data.collections.new("03_UNITY_COLLISION_GUIDES")
helper_collection = bpy.data.collections.new("99_STEP4_RENDER_HELPERS")
step4.children.link(stair_collection)
step4.children.link(floor_collection)
step4.children.link(collision_collection)
step4.children.link(helper_collection)

tread_mat = ensure_material("STAIR_Dark_Tread", (0.105, 0.12, 0.135), metallic=0.72, roughness=0.42)
rail_mat = ensure_material("STAIR_Dark_Rail", (0.075, 0.085, 0.095), metallic=0.82, roughness=0.36)
landing_mat = ensure_material("STAIR_Landing_Metal", (0.13, 0.145, 0.16), metallic=0.66, roughness=0.46)
floor_mat = bpy.data.materials.get("STRUCTURE_2F_StairLanding") or bpy.data.materials.get("STRUCTURE_2F_Concrete")

# Lower flight: east side, climbing north from the 1.09 m deep entry landing.
lower_x1, lower_x2 = 10.10, 11.50
upper_x1, upper_x2 = 8.50, 9.90
tread_thickness = 0.075

for index in range(RISER_COUNT_PER_FLIGHT):
    top_z = RISER_HEIGHT * (index + 1)
    center_y = FLIGHT_Y_SOUTH + TREAD_DEPTH * (index + 0.5)
    add_box(
        f"LOWER_TREAD_{index + 1:02d}",
        ((lower_x1 + lower_x2) * 0.5, center_y, top_z - tread_thickness * 0.5),
        (lower_x2 - lower_x1, TREAD_DEPTH + TREAD_OVERHANG, tread_thickness),
        tread_mat,
        stair_collection,
        bevel=0.012,
    )

# Upper flight: west side, turning 180 degrees and climbing south to 2F.
for index in range(RISER_COUNT_PER_FLIGHT):
    top_z = MID_LEVEL + RISER_HEIGHT * (index + 1)
    center_y = FLIGHT_Y_NORTH - TREAD_DEPTH * (index + 0.5)
    add_box(
        f"UPPER_TREAD_{index + 1:02d}",
        ((upper_x1 + upper_x2) * 0.5, center_y, top_z - tread_thickness * 0.5),
        (upper_x2 - upper_x1, TREAD_DEPTH + TREAD_OVERHANG, tread_thickness),
        tread_mat,
        stair_collection,
        bevel=0.012,
    )

# Full-width middle landing for the 180-degree turn.
add_box(
    "MIDDLE_LANDING",
    (10.0, (FLIGHT_Y_NORTH + LANDING_Y_NORTH) * 0.5, MID_LEVEL - 0.06),
    (3.0, LANDING_Y_NORTH - FLIGHT_Y_NORTH, 0.12),
    landing_mat,
    stair_collection,
    bevel=0.015,
)

# Structural stringers below both flights.
for x in (lower_x1 + 0.12, lower_x2 - 0.12):
    add_beam_box(
        f"LOWER_STRINGER_{x:.2f}",
        (x, FLIGHT_Y_SOUTH, 0.04),
        (x, FLIGHT_Y_NORTH, MID_LEVEL - 0.10),
        0.11,
        rail_mat,
        stair_collection,
    )
for x in (upper_x1 + 0.12, upper_x2 - 0.12):
    add_beam_box(
        f"UPPER_STRINGER_{x:.2f}",
        (x, FLIGHT_Y_NORTH, MID_LEVEL + 0.04),
        (x, FLIGHT_Y_SOUTH, FLOOR_2 - 0.10),
        0.11,
        rail_mat,
        stair_collection,
    )

# Railings: 0.95 m high, with open ends at every landing.
for x in (lower_x1, lower_x2):
    add_pipe(
        f"LOWER_HANDRAIL_{x:.2f}",
        (x, FLIGHT_Y_SOUTH, 1.05),
        (x, FLIGHT_Y_NORTH, MID_LEVEL + 0.95),
        0.042,
        rail_mat,
        stair_collection,
    )
    for suffix, y, base in (
        ("S", FLIGHT_Y_SOUTH, RISER_HEIGHT),
        ("M", (FLIGHT_Y_SOUTH + FLIGHT_Y_NORTH) * 0.5, MID_LEVEL * 0.5),
        ("N", FLIGHT_Y_NORTH, MID_LEVEL),
    ):
        add_post(f"LOWER_POST_{x:.2f}_{suffix}", x, y, base, 0.95, rail_mat, stair_collection)

for x in (upper_x1, upper_x2):
    add_pipe(
        f"UPPER_HANDRAIL_{x:.2f}",
        (x, FLIGHT_Y_NORTH, MID_LEVEL + 0.95),
        (x, FLIGHT_Y_SOUTH, FLOOR_2 + 0.95),
        0.042,
        rail_mat,
        stair_collection,
    )
    for suffix, y, base in (
        ("N", FLIGHT_Y_NORTH, MID_LEVEL),
        ("M", (FLIGHT_Y_SOUTH + FLIGHT_Y_NORTH) * 0.5, MID_LEVEL + MID_LEVEL * 0.5),
        ("S", FLIGHT_Y_SOUTH, FLOOR_2),
    ):
        add_post(f"UPPER_POST_{x:.2f}_{suffix}", x, y, base, 0.95, rail_mat, stair_collection)

# Guard the outer edges of the middle landing while leaving both flight ends open.
for x in (8.50, 10.00, 11.50):
    add_post(f"MID_NORTH_POST_{x:.2f}", x, LANDING_Y_NORTH - 0.07, MID_LEVEL, 0.95, rail_mat, stair_collection)
add_pipe(
    "MID_NORTH_GUARD",
    (8.50, LANDING_Y_NORTH - 0.07, MID_LEVEL + 0.95),
    (11.50, LANDING_Y_NORTH - 0.07, MID_LEVEL + 0.95),
    0.042,
    rail_mat,
    stair_collection,
)
for x, name in ((8.50, "WEST"), (11.50, "EAST")):
    add_pipe(
        f"MID_{name}_GUARD",
        (x, FLIGHT_Y_NORTH, MID_LEVEL + 0.95),
        (x, LANDING_Y_NORTH - 0.07, MID_LEVEL + 0.95),
        0.042,
        rail_mat,
        stair_collection,
    )

# Rebuild the 2F slab around the exact U-stair opening. The south landing is
# full width and connects directly to the existing 2F main floor at x=8.
add_floor_box("2F_USTAIR_SOUTH_EXIT_LANDING", 8.0, 12.0, 2.0, 3.20, floor_mat, floor_collection)
add_floor_box("2F_USTAIR_WEST_RIM", 8.0, 8.30, 3.20, LANDING_Y_NORTH, floor_mat, floor_collection)
add_floor_box("2F_USTAIR_EAST_RIM", 11.70, 12.0, 3.20, LANDING_Y_NORTH, floor_mat, floor_collection)
add_floor_box("2F_USTAIR_NORTH_RIM", 8.0, 12.0, LANDING_Y_NORTH, 7.0, floor_mat, floor_collection)

# Smooth Unity traversal guides under each visible flight.  Every tread box is
# TREAD_DEPTH + TREAD_OVERHANG deep and centred on its going, so each nosing
# overhangs by half of TREAD_OVERHANG on the side the climb comes from: the
# south edge going north, the north edge going south.
collision_mat = ensure_material("COLLISION_Guide", (0.08, 0.8, 0.2), metallic=0.0, roughness=0.8)
top_tread_going = TREAD_DEPTH * (RISER_COUNT_PER_FLIGHT - 1)

lower_ramp = add_flight_ramp(
    "COLLIDER_LowerFlight_Ramp",
    lower_x1,
    lower_x2 + RAMP_WALL_MARGIN,  # widen east, toward the outer stairwell wall
    (FLIGHT_Y_SOUTH + top_tread_going - TREAD_OVERHANG * 0.5, MID_LEVEL),
    1,
    collision_mat,
    collision_collection,
)
upper_ramp = add_flight_ramp(
    "COLLIDER_UpperFlight_Ramp",
    upper_x1 - RAMP_WALL_MARGIN,  # widen west, toward the outer stairwell wall
    upper_x2,
    (FLIGHT_Y_NORTH - top_tread_going + TREAD_OVERHANG * 0.5, FLOOR_2),
    -1,
    collision_mat,
    collision_collection,
)

collision_collection.hide_render = True
# The collection stays visible to the view layer on purpose.  Blender only
# evaluates objects the view layer can see, and matrix_world is evaluated data,
# so hiding this collection left every collider reporting an identity matrix to
# Step 6 and to the Unity export - which is how the four stair ramps once
# reached Unity sitting at the world origin, unrotated.  The guides are drawn as
# wireframe and are already excluded from renders object by object.

scene = bpy.context.scene
scene["BUILD_STAGE"] = "STEP 4 - REAL WALKABLE U STAIR"
scene["STAIR_TOTAL_RISERS"] = RISER_COUNT_PER_FLIGHT * 2
scene["STAIR_RISER_HEIGHT_M"] = round(RISER_HEIGHT, 4)
scene["STAIR_TREAD_DEPTH_M"] = TREAD_DEPTH
scene["STAIR_FLIGHT_WIDTH_M"] = FLIGHT_WIDTH
scene["BOTTOM_LANDING_DEPTH_M"] = 1.09
scene["TOP_FLOOR_LEVEL_M"] = FLOOR_2
scene["UNITY_COLLIDER_SLOPE_DEG"] = round(math.degrees(RAMP_SLOPE), 3)
scene["UNITY_RAMP_SIZE_M"] = (RAMP_WIDTH, round(RAMP_LENGTH, 4), RAMP_THICKNESS)

# Clean render setup and hidden viewport helper lines.
for obj in scene.objects:
    if obj.type in {"CAMERA", "LIGHT"}:
        obj.hide_viewport = True
        if obj.type == "LIGHT":
            obj.hide_render = True

entry_camera = add_camera(
    "STEP4_USTAIR_ENTRY_CAMERA",
    (6.65, 2.85, 1.62),
    (10.65, 3.75, 1.00),
    helper_collection,
)
entry_camera.data.lens = 42
overview_camera = add_camera(
    "STEP4_USTAIR_OVERVIEW_CAMERA",
    (14.7, 0.2, 6.4),
    (10.0, 4.35, 1.85),
    helper_collection,
)
key = add_light("STEP4_USTAIR_KEY", (8.0, 1.0, 8.5), (10.0, 4.4, 1.8), 1050, 6.0, helper_collection)
fill = add_light("STEP4_USTAIR_FILL", (12.5, 6.0, 7.0), (10.0, 4.4, 1.8), 550, 5.0, helper_collection)

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

temporary_hidden = []
for name in ("EXT_EAST", "EAST_BLOCK_LOBBY_DIVIDER"):
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

scene.camera = entry_camera
bpy.ops.wm.save_as_mainfile(filepath=TARGET)

print(
    {
        "blend": TARGET,
        "entry_preview": ENTRY_PREVIEW,
        "overview_preview": OVERVIEW_PREVIEW,
        "risers": RISER_COUNT_PER_FLIGHT * 2,
        "riser_height_m": round(RISER_HEIGHT, 4),
        "tread_depth_m": TREAD_DEPTH,
        "flight_width_m": FLIGHT_WIDTH,
        "bottom_landing_depth_m": 1.09,
        "collider_slope_deg": round(math.degrees(RAMP_SLOPE), 3),
        "ramp_size_m": (RAMP_WIDTH, round(RAMP_LENGTH, 4), RAMP_THICKNESS),
        "ramp_centres": {
            ramp.name: tuple(round(value, 4) for value in ramp["RAMP_CENTRE"])
            for ramp in (lower_ramp, upper_ramp)
        },
    }
)
