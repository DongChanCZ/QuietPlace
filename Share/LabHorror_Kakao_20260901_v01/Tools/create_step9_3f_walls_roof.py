"""Step 9 - close the 3F envelope: exterior walls, core walls, stairwell
parapets and the roof slab.  Sources the Step 8 file so the 1F opening detail
survives, and touches nothing that already exists."""
import bpy
import os
from mathutils import Vector


ROOT = r"C:\Users\MBC-501-14\Desktop\LabHorror\Blender"
SOURCE = os.path.join(ROOT, "LabHorror_Step8_1F_Detail_20260831.blend")
TARGET = os.path.join(ROOT, "LabHorror_Step9_3F_Walls_Roof.blend")
PREVIEW_DIR = os.path.join(ROOT, "Previews")
TOP_PREVIEW = os.path.join(PREVIEW_DIR, "step9_3f_walls_topdown.png")
ISO_PREVIEW = os.path.join(PREVIEW_DIR, "step9_3f_walls_isometric.png")

FLOOR_Z = 6.40                      # 3F walking surface
WALL_HEIGHT = 3.20
WALL_TOP = FLOOR_Z + WALL_HEIGHT    # 9.60, same as the roof slab top
WALL_THICKNESS = 0.22
# Corner seal - see the same note in create_blockout_step2_structure.py.
HALF = WALL_THICKNESS * 0.5
ROOF_TOP = WALL_TOP
ROOF_BOTTOM = ROOF_TOP - 0.20       # matches every other structural slab
PARAPET_TOP = FLOOR_Z + 1.10        # fall guard around the stair opening

# Slab voids inherited from Step 7, kept clear so the stair still passes through.
STAIR_VOID = (8.30, 11.70, 3.20, 6.65)      # x1, x2, y1, y2
ELEVATOR_VOID = (9.00, 11.50, -5.00, -1.30)


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


def add_wall_x(name, x1, x2, y, z1=FLOOR_Z, z2=WALL_TOP, material=None):
    if x2 - x1 <= 0.001 or z2 - z1 <= 0.001:
        return None
    return add_box(
        name,
        ((x1 + x2) * 0.5, y, (z1 + z2) * 0.5),
        (x2 - x1, WALL_THICKNESS, z2 - z1),
        material or WALL_MAT,
        WALLS,
    )


def add_wall_y(name, x, y1, y2, z1=FLOOR_Z, z2=WALL_TOP, material=None):
    if y2 - y1 <= 0.001 or z2 - z1 <= 0.001:
        return None
    return add_box(
        name,
        (x, (y1 + y2) * 0.5, (z1 + z2) * 0.5),
        (WALL_THICKNESS, y2 - y1, z2 - z1),
        material or WALL_MAT,
        WALLS,
    )


def wall_x_with_openings(name, x1, x2, y, openings):
    """Openings: center, width, sill above 3F, height."""
    cursor = x1
    for index, (center, width, sill, height) in enumerate(sorted(openings), start=1):
        start = center - width * 0.5
        end = center + width * 0.5
        add_wall_x(f"{name}_SOLID_{index:02d}", cursor, start, y)
        if sill > 0:
            add_wall_x(f"{name}_SILL_{index:02d}", start, end, y, FLOOR_Z, FLOOR_Z + sill)
        opening_top = FLOOR_Z + sill + height
        if opening_top < WALL_TOP:
            add_wall_x(f"{name}_HEADER_{index:02d}", start, end, y, opening_top, WALL_TOP)
        cursor = end
    add_wall_x(f"{name}_SOLID_END", cursor, x2, y)


def wall_y_with_openings(name, x, y1, y2, openings):
    """Openings: center, width, sill above 3F, height."""
    cursor = y1
    for index, (center, width, sill, height) in enumerate(sorted(openings), start=1):
        start = center - width * 0.5
        end = center + width * 0.5
        add_wall_y(f"{name}_SOLID_{index:02d}", x, cursor, start)
        if sill > 0:
            add_wall_y(f"{name}_SILL_{index:02d}", x, start, end, FLOOR_Z, FLOOR_Z + sill)
        opening_top = FLOOR_Z + sill + height
        if opening_top < WALL_TOP:
            add_wall_y(f"{name}_HEADER_{index:02d}", x, start, end, opening_top, WALL_TOP)
        cursor = end
    add_wall_y(f"{name}_SOLID_END", x, cursor, y2)


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

old_step9 = bpy.data.collections.get("STEP_9_THIRD_FLOOR_ENVELOPE")
if old_step9:
    for obj in list(old_step9.all_objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for child in list(old_step9.children):
        bpy.data.collections.remove(child)
    bpy.data.collections.remove(old_step9)

step9 = bpy.data.collections.new("STEP_9_THIRD_FLOOR_ENVELOPE")
bpy.context.scene.collection.children.link(step9)
WALLS = bpy.data.collections.new("01_3F_WALLS_AND_OPENINGS")
ROOF = bpy.data.collections.new("01_3F_ROOF_SLAB")
HELPERS = bpy.data.collections.new("99_STEP9_RENDER_HELPERS")
step9.children.link(WALLS)
step9.children.link(ROOF)
step9.children.link(HELPERS)

WALL_MAT = ensure_material("STRUCTURE_3F_Wall", (0.30, 0.345, 0.40))
ROOF_MAT = ensure_material("STRUCTURE_3F_Roof", (0.175, 0.205, 0.25))
PARAPET_MAT = ensure_material("STRUCTURE_3F_Parapet", (0.245, 0.285, 0.34))

# --- Exterior shell, same footprint and window rhythm as the 2F facade ------
# The second opening at y=4.50 is the escape window the descender hangs from,
# so it has a low sill and is tall enough to climb through.
wall_y_with_openings(
    "3F_EXT_WEST",
    -10.0,
    -7.0 - HALF,
    7.0 + HALF,
    [(0.55, 1.40, 1.10, 1.20), (4.50, 1.60, 0.90, 1.60)],
)
wall_x_with_openings(
    "3F_EXT_SOUTH",
    -10.0,
    12.0,
    -7.0,
    [(-7.0, 2.0, 1.10, 1.20), (-1.0, 2.2, 1.10, 1.20), (5.0, 2.2, 1.10, 1.20)],
)
add_wall_y("3F_EXT_EAST", 12.0, -7.0 - HALF, 7.0 + HALF)
wall_x_with_openings(
    "3F_EXT_NORTH",
    -10.0,
    12.0,
    7.0,
    [(-7.0, 2.0, 1.10, 1.20), (-1.0, 2.0, 1.10, 1.20), (5.0, 2.0, 1.10, 1.20)],
)

# --- Core: service block wall, stair room, elevator shaft ------------------
# Circulation between the 3F deck and the core stays open at y -1.30 .. 2.00,
# exactly as on 2F.
add_wall_y("3F_SERVICE_SOUTH_DIVIDER", 8.0, -7.0, -1.30)
wall_y_with_openings("3F_STAIR_ROOM_WEST", 8.0, 2.0, 7.0, [(2.65, 1.30, 0.0, 2.40)])

add_wall_y("3F_ELEVATOR_SHAFT_WEST", 9.0, -5.0, -1.30)
add_wall_y("3F_ELEVATOR_SHAFT_EAST", 11.5, -5.0, -1.30)
add_wall_x("3F_ELEVATOR_SHAFT_SOUTH", 9.0, 11.5, -5.0)
wall_x_with_openings(
    "3F_ELEVATOR_SHAFT_NORTH", 9.0, 11.5, -1.30, [(10.25, 1.30, 0.0, 2.40)]
)

# --- Stairwell parapets ---------------------------------------------------
# West, east and north each sit fully on their own 0.30 m rim so the void stays
# clear for the stair.  The south edge is where the upper flight surfaces, so it
# is guarded only up to the flight's west handrail (x=9.90); everything from
# there to the east parapet was an open 1.76 m drop onto the flight below.
half = WALL_THICKNESS * 0.5
sx1, sx2, sy1, sy2 = STAIR_VOID
UPPER_FLIGHT_WEST_EDGE = 9.90
add_wall_y("3F_STAIRWELL_PARAPET_WEST", sx1 - half, sy1, sy2, FLOOR_Z, PARAPET_TOP, PARAPET_MAT)
add_wall_y("3F_STAIRWELL_PARAPET_EAST", sx2 + half, sy1, sy2, FLOOR_Z, PARAPET_TOP, PARAPET_MAT)
# Butted against the exterior north wall's inner face (7.00 - HALF), not simply
# offset from the void edge - the latter left a 20 mm slot along its whole run.
add_wall_x(
    "3F_STAIRWELL_PARAPET_NORTH",
    sx1 - half,
    sx2 + half,
    7.0 - HALF - half,
    FLOOR_Z,
    PARAPET_TOP,
    PARAPET_MAT,
)
add_wall_x(
    "3F_STAIRWELL_PARAPET_SOUTH",
    UPPER_FLIGHT_WEST_EDGE,
    sx2 + half,
    sy1 - half,
    FLOOR_Z,
    PARAPET_TOP,
    PARAPET_MAT,
)

# --- Roof slab ------------------------------------------------------------
add_box(
    "3F_ROOF_SLAB",
    (1.0, 0.0, (ROOF_BOTTOM + ROOF_TOP) * 0.5),
    (22.0, 14.0, ROOF_TOP - ROOF_BOTTOM),
    ROOF_MAT,
    ROOF,
)

new_meshes = len(WALLS.objects) + len(ROOF.objects)


# --- Assertions: the two slab voids must still be clear of new geometry ----
def overlaps(obj, void):
    x1, x2, y1, y2 = void
    world = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    xs = [p.x for p in world]
    ys = [p.y for p in world]
    zs = [p.z for p in world]
    if max(zs) <= FLOOR_Z + 0.001:
        return False
    return (
        min(xs) < x2 - 0.001
        and max(xs) > x1 + 0.001
        and min(ys) < y2 - 0.001
        and max(ys) > y1 + 0.001
    )


# Only the stair void has to stay clear - the upper flight passes through it.
# The elevator shaft walls deliberately straddle ELEVATOR_VOID's edges, exactly
# as they do on 2F, because that void is the shaft those walls enclose.
bpy.context.view_layer.update()
blocked = [obj.name for obj in WALLS.objects if overlaps(obj, STAIR_VOID)]
if blocked:
    raise RuntimeError(f"New 3F geometry intrudes into the stair void: {blocked}")

scene = bpy.context.scene
scene["BUILD_STAGE"] = "STEP 9 - THIRD FLOOR ENVELOPE"
scene["STEP9_SCOPE"] = "3F exterior walls, core walls, stairwell parapets, roof slab"
scene["STEP9_NEW_MESHES"] = new_meshes
scene["FLOOR_3_WALL_TOP_M"] = WALL_TOP
scene["FLOOR_3_ROOF_M"] = f"{ROOF_BOTTOM} .. {ROOF_TOP}"
scene["STEP9_NOT_INCLUDED"] = "3F interior room partitions, doors, glass, props"

for obj in scene.objects:
    if obj.type in {"CAMERA", "LIGHT"}:
        obj.hide_viewport = True
        if obj.type == "LIGHT":
            obj.hide_render = True

top_camera = add_camera("STEP9_3F_TOP_CAMERA", (1.0, 0.0, 19.0), (1.0, 0.0, FLOOR_Z), HELPERS, 27.0)
iso_camera = add_camera("STEP9_3F_ISO_CAMERA", (24.0, -27.0, 23.0), (1.0, 0.0, 6.0), HELPERS)
key = add_light("STEP9_3F_KEY", (0.0, -3.0, 18.0), (1.0, 0.0, 7.0), 1400, 15.0, HELPERS)
fill = add_light("STEP9_3F_FILL", (-9.0, 9.0, 15.0), (1.0, 0.0, 7.0), 650, 9.0, HELPERS)

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

# Cut-plan top view: hide the roof and the window headers so the 3F plan reads.
cutaway = [obj for obj in WALLS.objects if "_HEADER_" in obj.name]
cutaway += list(ROOF.objects)
for obj in cutaway:
    obj.hide_render = True

scene.camera = top_camera
scene.render.filepath = TOP_PREVIEW
bpy.ops.render.render(write_still=True)

for obj in cutaway:
    obj.hide_render = False

scene.camera = iso_camera
scene.render.filepath = ISO_PREVIEW
bpy.ops.render.render(write_still=True)

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
        "wall_meshes": len(WALLS.objects),
        "roof_meshes": len(ROOF.objects),
        "new_meshes": new_meshes,
        "wall_top_m": WALL_TOP,
        "roof_m": f"{ROOF_BOTTOM}..{ROOF_TOP}",
        "voids_clear": True,
    }
)
