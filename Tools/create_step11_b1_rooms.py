"""Step 11 - B1, built to the plan's bottom-right corner only.

The user asked for exactly what the crop shows: come down a stair and see two
rooms, 비밀 생체실험실 and 진화된 알, with the 봉쇄문 as the west boundary.
The rest of B1 on the plan - 격리실, 폐기물 처리실, 주파수 실험실, 대형 환기구 -
is deliberately not built; the lockdown door is what explains its absence.

Two constraints the user set explicitly:
  * no hole is cut in the 1F slab, and
  * the stair is scenery inside B1, not a working 1F->B1 connection.
So this stage is purely additive. It edits no existing object, which means no
existing FBX node name changes and no Unity fileID is disturbed - the collider
and static flags already applied to the blockout all survive the re-export.

B1 needs no ceiling of its own: the six 01_ZONE_FLOORS slabs already span
x -10..12, y -7..7 at z -0.20..0.00 and roof the whole storey.
"""
import bpy
import os
from mathutils import Vector


ROOT = r"C:\Users\MBC-501-14\Desktop\LabHorror\Blender"
SOURCE = os.path.join(ROOT, "LabHorror_Step10_3F_Rooms.blend")
TARGET = os.path.join(ROOT, "LabHorror_Step11_B1_Rooms.blend")
PREVIEW_DIR = os.path.join(ROOT, "Previews")
TOP_PREVIEW = os.path.join(PREVIEW_DIR, "step11_b1_rooms_topdown.png")

# 1F sits at z 0.00 and its slab is 0.20 thick, so B1 gets the same 3.20 storey
# height as every other floor and 3.00 m of clear headroom under that slab.
FLOOR = -3.20
CEILING = -0.20
SLAB_THICKNESS = 0.20
WALL_THICKNESS = 0.22
HALF = WALL_THICKNESS * 0.5

# Openings, recorded here so the door pass can find them by name later.
LOCKDOWN_DOOR = (-4.00, 1.60, 2.40)      # 봉쇄문: centre y, width, height
STAIR_DOOR = (2.50, 1.40, 2.40)          # centre x, width, height
EGG_ARCH = (-4.00, 2.40, 2.60)           # centre y, width, height

# Straight flight in the stair alcove. It stops at the slab underside because
# there is no opening above it - scenery, exactly as asked.
TREAD_COUNT = 17
TREAD_RUN = 0.27
TREAD_THICKNESS = 0.08
STAIR_RISE = (CEILING - FLOOR) / TREAD_COUNT
STAIR_X1, STAIR_X2 = 1.60, 3.00
STAIR_Y0 = 0.40


def ensure_material(name, color, roughness=0.82):
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


def add_box(name, location, dimensions, material, collection, bevel=0.0015):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    move_to_collection(obj, collection)
    if bevel:
        modifier = obj.modifiers.new("Edge_Soften", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
    return obj


def add_wall_x(name, x1, x2, y, z1=FLOOR, z2=CEILING):
    """A wall running along x, at depth y."""
    if x2 - x1 <= 0.001 or z2 - z1 <= 0.001:
        return None
    return add_box(name, ((x1 + x2) * 0.5, y, (z1 + z2) * 0.5),
                   (x2 - x1, WALL_THICKNESS, z2 - z1), WALL_MAT, B1)


def add_wall_y(name, x, y1, y2, z1=FLOOR, z2=CEILING):
    """A wall running along y, at x."""
    if y2 - y1 <= 0.001 or z2 - z1 <= 0.001:
        return None
    return add_box(name, (x, (y1 + y2) * 0.5, (z1 + z2) * 0.5),
                   (WALL_THICKNESS, y2 - y1, z2 - z1), WALL_MAT, B1)


def wall_x_with_openings(name, x1, x2, y, openings):
    """openings: (centre x, width, height above the B1 floor)."""
    cursor = x1
    for index, (centre, width, height) in enumerate(sorted(openings), start=1):
        start, end = centre - width * 0.5, centre + width * 0.5
        add_wall_x(f"{name}_SOLID_{index:02d}", cursor, start, y)
        add_wall_x(f"{name}_HEADER_{index:02d}", start, end, y, FLOOR + height, CEILING)
        cursor = end
    add_wall_x(f"{name}_SOLID_END", cursor, x2, y)


def wall_y_with_openings(name, x, y1, y2, openings):
    """openings: (centre y, width, height above the B1 floor)."""
    cursor = y1
    for index, (centre, width, height) in enumerate(sorted(openings), start=1):
        start, end = centre - width * 0.5, centre + width * 0.5
        add_wall_y(f"{name}_SOLID_{index:02d}", x, cursor, start)
        add_wall_y(f"{name}_HEADER_{index:02d}", x, start, end, FLOOR + height, CEILING)
        cursor = end
    add_wall_y(f"{name}_SOLID_END", x, cursor, y2)


bpy.ops.wm.open_mainfile(filepath=SOURCE)

# A re-run must not stack a second copy of B1 on top of the first.
old = bpy.data.collections.get("STEP_11_B1_ROOMS")
if old:
    for obj in list(old.all_objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for child in list(old.children):
        bpy.data.collections.remove(child)
    bpy.data.collections.remove(old)

STEP11 = bpy.data.collections.new("STEP_11_B1_ROOMS")
bpy.context.scene.collection.children.link(STEP11)
B1 = bpy.data.collections.new("01_B1_ROOMS")
STEP11.children.link(B1)

WALL_MAT = ensure_material("B1_Wall", (0.26, 0.29, 0.33))
SLAB_MAT = ensure_material("B1_Slab", (0.20, 0.22, 0.25))
STAIR_MAT = ensure_material("B1_Stair", (0.22, 0.25, 0.29))
DOOR_MAT = ensure_material("B1_LockdownDoor", (0.42, 0.31, 0.12))

# ---------------------------------------------------------------- floor slabs
add_box("B1_FLOOR_MAIN",
        (6.00, -3.50, FLOOR - SLAB_THICKNESS * 0.5),
        (12.22, 7.22, SLAB_THICKNESS), SLAB_MAT, B1)
add_box("B1_FLOOR_STAIR_ALCOVE",
        (2.50, 2.60, FLOOR - SLAB_THICKNESS * 0.5),
        (5.22, 5.00, SLAB_THICKNESS), SLAB_MAT, B1)

# ------------------------------------------------------------------ perimeter
add_wall_x("B1_SOUTH", 0.0 - HALF, 12.0 + HALF, -7.0)
add_wall_y("B1_EAST", 12.0, -7.0 - HALF, 0.0 + HALF)
add_wall_x("B1_NORTH_EGG", 9.0, 12.0 + HALF, 0.0)
add_wall_x("B1_NORTH_LAB", 5.0, 9.0, 0.0)
add_wall_x("B1_STAIR_NORTH", 0.0 - HALF, 5.0 + HALF, 5.0)
add_wall_y("B1_STAIR_EAST", 5.0, 0.0, 5.0 + HALF)

# 봉쇄문 - the sealed west boundary, and the reason the rest of B1 is not built.
# It is a solid panel, not an opening: the plan draws it padlocked, and nothing
# is built beyond it, so an opening here would just be a hole to fall through.
# The door pass can swap this one named box for a real leaf later.
add_wall_y("B1_LOCKDOWN_WEST", 0.0, -7.0 - HALF, 5.0 + HALF)
add_box("B1_LOCKDOWN_DOOR_SEALED",
        (0.0, LOCKDOWN_DOOR[0], FLOOR + LOCKDOWN_DOOR[2] * 0.5),
        (WALL_THICKNESS + 0.10, LOCKDOWN_DOOR[1], LOCKDOWN_DOOR[2]),
        DOOR_MAT, B1)

# ------------------------------------------------------------------- interior
wall_x_with_openings("B1_STAIR_LAB_DIVIDER", 0.0, 5.0, 0.0, [STAIR_DOOR])
wall_y_with_openings("B1_LAB_EGG_DIVIDER", 9.0, -7.0, 0.0, [EGG_ARCH])

# ----------------------------------------------------------------- the stair
for step in range(1, TREAD_COUNT + 1):
    top = FLOOR + step * STAIR_RISE
    y1 = STAIR_Y0 + (step - 1) * TREAD_RUN
    add_box(f"B1_STAIR_TREAD_{step:02d}",
            ((STAIR_X1 + STAIR_X2) * 0.5, y1 + TREAD_RUN * 0.5, top - TREAD_THICKNESS * 0.5),
            (STAIR_X2 - STAIR_X1, TREAD_RUN, TREAD_THICKNESS), STAIR_MAT, B1)

# One solid ramp per side so the treads read as a flight and the climb has a
# continuous surface under it, the same trick the 1F U-stair uses.
stair_run = TREAD_COUNT * TREAD_RUN
for label, x in (("WEST", STAIR_X1 - 0.06), ("EAST", STAIR_X2 + 0.06)):
    add_box(f"B1_STAIR_STRINGER_{label}",
            (x, STAIR_Y0 + stair_run * 0.5, FLOOR + (CEILING - FLOOR) * 0.5),
            (0.12, stair_run, CEILING - FLOOR), STAIR_MAT, B1)

new_meshes = len([obj for obj in B1.all_objects if obj.type == "MESH"])

scene = bpy.context.scene
scene["BUILD_STAGE"] = "STEP 11 - B1 ROOMS"
scene["STEP11_NEW_MESHES"] = new_meshes
scene["B1_FLOOR_LEVEL"] = FLOOR

bpy.ops.wm.save_as_mainfile(filepath=TARGET)

print({
    "blend": TARGET,
    "new_meshes": new_meshes,
    "floor_level": FLOOR,
    "rooms": {
        "STAIR_ALCOVE": "x 0.11..4.89, y 0.11..4.89",
        "SECRET_BIOLAB (비밀 생체실험실)": "x 0.11..8.89, y -6.89..-0.11",
        "EVOLVED_EGG (진화된 알)": "x 9.11..11.89, y -6.89..-0.11",
    },
    "stair_rise_per_tread": round(STAIR_RISE, 4),
})
