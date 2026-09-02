import bpy
import os
from mathutils import Vector


ROOT = r"C:\Users\MBC-501-14\Desktop\LabHorror\Blender"
SOURCE = os.path.join(ROOT, "LabHorror_Blockout_Step4_WalkableStair.blend")
TARGET = os.path.join(ROOT, "LabHorror_Blockout_Step5_2F_Walls.blend")
PREVIEW_DIR = os.path.join(ROOT, "Previews")
TOP_PREVIEW = os.path.join(PREVIEW_DIR, "step5_2f_walls_topdown.png")
ISO_PREVIEW = os.path.join(PREVIEW_DIR, "step5_2f_walls_isometric.png")

FLOOR_Z = 3.20
WALL_HEIGHT = 3.20
WALL_TOP = FLOOR_Z + WALL_HEIGHT
WALL_THICKNESS = 0.22
CORRIDOR_SOUTH_Y = -1.40
CORRIDOR_NORTH_Y = 2.60
# Corner seal - see the same note in create_blockout_step2_structure.py.
HALF = WALL_THICKNESS * 0.5


def ensure_material(name, color, emission=0.0, roughness=0.82):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission
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


def add_wall_x(name, x1, x2, y, z1=FLOOR_Z, z2=WALL_TOP):
    if x2 - x1 <= 0.001 or z2 - z1 <= 0.001:
        return None
    return add_box(
        name,
        ((x1 + x2) * 0.5, y, (z1 + z2) * 0.5),
        (x2 - x1, WALL_THICKNESS, z2 - z1),
        WALL_MAT,
        WALLS,
    )


def add_wall_y(name, x, y1, y2, z1=FLOOR_Z, z2=WALL_TOP):
    if y2 - y1 <= 0.001 or z2 - z1 <= 0.001:
        return None
    return add_box(
        name,
        (x, (y1 + y2) * 0.5, (z1 + z2) * 0.5),
        (WALL_THICKNESS, y2 - y1, z2 - z1),
        WALL_MAT,
        WALLS,
    )


def wall_x_with_openings(name, x1, x2, y, openings):
    """Openings: center, width, sill above 2F, height."""
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
    """Openings: center, width, sill above 2F, height."""
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


def add_label(text, x, y, size=0.47):
    bpy.ops.object.text_add(location=(x, y, FLOOR_Z + 0.035))
    obj = bpy.context.object
    obj.name = "2F_LABEL_" + text.replace(" ", "_")
    obj.data.body = text
    obj.data.align_x = "CENTER"
    obj.data.align_y = "CENTER"
    obj.data.size = size
    obj.data.extrude = 0.006
    obj.data.materials.append(LABEL_MAT)
    move_to_collection(obj, LABELS)
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

old_step5 = bpy.data.collections.get("STEP_5_SECOND_FLOOR_WALLS")
if old_step5:
    for obj in list(old_step5.all_objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.collections.remove(old_step5)

step5 = bpy.data.collections.new("STEP_5_SECOND_FLOOR_WALLS")
bpy.context.scene.collection.children.link(step5)
WALLS = bpy.data.collections.new("01_2F_WALLS_AND_OPENINGS")
LABELS = bpy.data.collections.new("02_2F_ROOM_LABELS")
HELPERS = bpy.data.collections.new("99_STEP5_RENDER_HELPERS")
step5.children.link(WALLS)
step5.children.link(LABELS)
step5.children.link(HELPERS)

WALL_MAT = ensure_material("STRUCTURE_2F_Wall", (0.31, 0.36, 0.42))
LABEL_MAT = ensure_material("GUIDE_2F_Label", (0.65, 0.92, 0.96), emission=0.6, roughness=0.6)

# Second-storey exterior shell. The one-storey entrance vestibule is not copied.
# West corridor exit is the single locked exterior opening shown on the plan.
wall_y_with_openings("2F_EXT_WEST", -10.0, -7.0 - HALF, 7.0 + HALF, [(0.55, 1.40, 0.0, 2.40)])
wall_x_with_openings(
    "2F_EXT_SOUTH",
    -10.0,
    12.0,
    -7.0,
    [(-7.0, 2.0, 1.10, 1.20), (-1.0, 2.2, 1.10, 1.20), (5.0, 2.2, 1.10, 1.20)],
)
add_wall_y("2F_EXT_EAST", 12.0, -7.0 - HALF, 7.0 + HALF)
wall_x_with_openings(
    "2F_EXT_NORTH",
    -10.0,
    12.0,
    7.0,
    [(-7.0, 2.0, 1.10, 1.20), (-1.0, 2.0, 1.10, 1.20), (5.0, 2.0, 1.10, 1.20)],
)

# Main chase corridor: widened to y -1.4 .. 2.6.
wall_x_with_openings(
    "2F_CORRIDOR_NORTH",
    -10.0,
    8.0,
    CORRIDOR_NORTH_Y,
    [(-7.0, 1.25, 0.0, 2.40), (-1.0, 1.25, 0.0, 2.40), (5.0, 1.25, 0.0, 2.40)],
)
wall_x_with_openings(
    "2F_CORRIDOR_SOUTH",
    -10.0,
    8.0,
    CORRIDOR_SOUTH_Y,
    [(-7.0, 1.25, 0.0, 2.40), (-1.0, 1.25, 0.0, 2.40), (5.0, 1.25, 0.0, 2.40)],
)

# Three rooms north of the corridor and three rooms south of it.
add_wall_y("2F_TOP_OFFICE_MEETING_DIVIDER", -4.0, CORRIDOR_NORTH_Y, 7.0)
add_wall_y("2F_TOP_MEETING_SAMPLE_DIVIDER", 2.0, CORRIDOR_NORTH_Y, 7.0)
add_wall_y("2F_BOTTOM_CABINET_RECORDS_DIVIDER", -4.0, -7.0, CORRIDOR_SOUTH_Y)
# x=2.0, not 2.3: this divider faces 2F_TOP_MEETING_SAMPLE_DIVIDER across the
# corridor and was 0.30 m out of line with it, while the other pair at x=-4.0
# already matched. Aligning also makes both south rooms 6.0 m wide.
add_wall_y("2F_BOTTOM_RECORDS_BREAK_DIVIDER", 2.0, -7.0, CORRIDOR_SOUTH_Y)

# East service circulation remains open at corridor level (y -1.3 .. 2.0).
# The stair door exactly meets the new U-stair's 2F south exit landing.
# Ends at y=-1.00, on the corridor's south wall line, not at the shaft's -1.30.
# Stopping at -1.30 left a 0.30 m hole where the corridor met the service core
# and made the wall line visibly jog. The shaft wall itself stays at -1.30
# because that is the edge of the real slab opening.
add_wall_y("2F_SERVICE_SOUTH_DIVIDER", 8.0, -7.0, CORRIDOR_SOUTH_Y)
wall_y_with_openings("2F_STAIR_ROOM_WEST", 8.0, 2.0, 7.0, [(2.65, 1.30, 0.0, 2.40)])

# Elevator shaft walls around the actual slab opening.
add_wall_y("2F_ELEVATOR_SHAFT_WEST", 9.0, -5.0, -1.30)
add_wall_y("2F_ELEVATOR_SHAFT_EAST", 11.5, -5.0, -1.30)
add_wall_x("2F_ELEVATOR_SHAFT_SOUTH", 9.0, 11.5, -5.0)
wall_x_with_openings(
    "2F_ELEVATOR_SHAFT_NORTH",
    9.0,
    11.5,
    -1.30,
    [(10.25, 1.30, 0.0, 2.40)],
)

# Planning labels; these are guides, not gameplay props.
add_label("OFFICE", -7.0, 4.6)
add_label("MEETING", -1.0, 4.6)
add_label("SAMPLE", 5.0, 4.6)
add_label("CHASE CORRIDOR", -1.0, 0.6, 0.42)
add_label("CABINET MEMO", -7.0, -4.0, 0.42)
add_label("RECORDS", -1.0, -4.0)
add_label("BREAK ROOM", 5.0, -4.0, 0.42)
add_label("STAIR", 10.0, 4.7)
add_label("ELEVATOR", 10.25, -3.2, 0.38)

scene = bpy.context.scene
scene["BUILD_STAGE"] = "STEP 5 - SECOND FLOOR WALL STRUCTURE"
scene["FLOOR_2_CORRIDOR_RANGE_Y"] = "-1.4 .. 2.6"
scene["FLOOR_2_ROOMS"] = "Office, Meeting, Sample, Cabinet Memo, Records, Break Room"
scene["FLOOR_2_WINDOWS"] = 6
scene["FLOOR_2_INTERNAL_DOOR_OPENINGS"] = 8

for obj in scene.objects:
    if obj.type in {"CAMERA", "LIGHT"}:
        obj.hide_viewport = True
        if obj.type == "LIGHT":
            obj.hide_render = True

top_camera = add_camera("STEP5_2F_TOP_CAMERA", (1.0, 0.0, 15.0), (1.0, 0.0, FLOOR_Z), HELPERS, 27.0)
iso_camera = add_camera("STEP5_2F_ISO_CAMERA", (22.0, -25.0, 20.0), (1.0, 0.0, 4.2), HELPERS)
key = add_light("STEP5_2F_KEY", (0.0, -3.0, 13.0), (1.0, 0.0, 4.0), 1250, 15.0, HELPERS)
fill = add_light("STEP5_2F_FILL", (-9.0, 9.0, 11.0), (1.0, 0.0, 4.0), 600, 9.0, HELPERS)

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

# Architectural cut-plan preview: hide only sills and headers for the top view.
cutaway_parts = [
    obj
    for obj in WALLS.objects
    if "_HEADER_" in obj.name or "_SILL_" in obj.name
]
for obj in cutaway_parts:
    obj.hide_render = True

# The ground-floor entrance vestibule sits outside the 2F footprint. Hide it
# only in Step 5 inspection renders so it cannot be mistaken for a 2F room.
ground_vestibule_parts = []
for obj in scene.objects:
    if (
        obj.name == "Entrance Vestibule 4x2m"
        or obj.name.startswith("VESTIBULE_")
        or obj.name.startswith("MAIN_ENTRANCE")
    ):
        obj.hide_render = True
        ground_vestibule_parts.append(obj)

scene.camera = top_camera
scene.render.filepath = TOP_PREVIEW
bpy.ops.render.render(write_still=True)

for obj in cutaway_parts:
    obj.hide_render = False

scene.camera = iso_camera
scene.render.filepath = ISO_PREVIEW
bpy.ops.render.render(write_still=True)

for obj in ground_vestibule_parts:
    obj.hide_render = False

for obj in scene.objects:
    if obj.type in {"CAMERA", "LIGHT"}:
        obj.hide_viewport = True

LABELS.hide_viewport = True
scene.camera = top_camera
bpy.ops.wm.save_as_mainfile(filepath=TARGET)

print(
    {
        "blend": TARGET,
        "top_preview": TOP_PREVIEW,
        "isometric_preview": ISO_PREVIEW,
        "wall_objects": len(WALLS.objects),
        "room_labels": len(LABELS.objects),
        "wall_base_z": FLOOR_Z,
        "wall_top_z": WALL_TOP,
    }
)
