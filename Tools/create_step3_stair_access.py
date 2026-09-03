import bpy
import os
from mathutils import Vector


ROOT = r"C:\Users\MBC-501-14\Desktop\LabHorror\Blender"
SOURCE = os.path.join(ROOT, "LabHorror_Blockout_Step2_Structure.blend")
TARGET = os.path.join(ROOT, "LabHorror_Blockout_Step3_2F_Access.blend")
PREVIEW_DIR = os.path.join(ROOT, "Previews")
TOP_PREVIEW = os.path.join(PREVIEW_DIR, "step3_stair_access_top.png")
CUTAWAY_PREVIEW = os.path.join(PREVIEW_DIR, "step3_stair_access_cutaway.png")

FLOOR_LEVEL_2 = 3.20
FLOOR_THICKNESS = 0.20
FIRST_TREAD_HEIGHT = 0.20

# Measured walking surfaces in the user's original GLB coordinate system.
SOURCE_FIRST_TREAD_Z = -0.297
SOURCE_TOP_LANDING_Z = 0.184
SOURCE_BOUNDS_CENTER_X = (-0.16629 + 0.16622) * 0.5
SOURCE_BOUNDS_CENTER_Y = (-0.50490 + 0.50042) * 0.5
SOURCE_LENGTH_Y = 1.00532


def ensure_material(name, color, roughness=0.84):
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


def add_box(name, x1, x2, y1, y2, z1, z2, material, collection, bevel=0.01):
    bpy.ops.mesh.primitive_cube_add(
        location=((x1 + x2) * 0.5, (y1 + y2) * 0.5, (z1 + z2) * 0.5)
    )
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (x2 - x1, y2 - y1, z2 - z1)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
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


def add_camera(name, location, target, collection, ortho_scale=None):
    bpy.ops.object.camera_add(location=location)
    camera = bpy.context.object
    camera.name = name
    if ortho_scale:
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = ortho_scale
    else:
        camera.data.type = "PERSP"
        camera.data.lens = 52
    look_at(camera, target)
    move_to_collection(camera, collection)
    return camera


def add_area_light(name, location, target, energy, size, collection):
    bpy.ops.object.light_add(type="AREA", location=location)
    light = bpy.context.object
    light.name = name
    light.data.energy = energy
    light.data.shape = "DISK"
    light.data.size = size
    look_at(light, target)
    move_to_collection(light, collection)
    return light


def mesh_bounds(objects):
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

old_step3 = bpy.data.collections.get("STEP_3_2F_STAIR_ACCESS")
if old_step3:
    for obj in list(old_step3.all_objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.collections.remove(old_step3)

step3 = bpy.data.collections.new("STEP_3_2F_STAIR_ACCESS")
bpy.context.scene.collection.children.link(step3)
floor_collection = bpy.data.collections.new("01_2F_STRUCTURAL_FLOOR")
helper_collection = bpy.data.collections.new("99_STEP3_RENDER_HELPERS")
step3.children.link(floor_collection)
step3.children.link(helper_collection)

floor_mat = ensure_material("STRUCTURE_2F_Concrete", (0.20, 0.24, 0.29))
landing_mat = ensure_material("STRUCTURE_2F_StairLanding", (0.25, 0.29, 0.34))

# Correct the user's stair without changing its horizontal proportions.
stair_root = bpy.data.objects.get("USER_STAIR_ROOT")
if stair_root is None:
    raise RuntimeError("USER_STAIR_ROOT not found in Step 2")

vertical_scale = (FLOOR_LEVEL_2 - FIRST_TREAD_HEIGHT) / (
    SOURCE_TOP_LANDING_Z - SOURCE_FIRST_TREAD_Z
)
horizontal_scale = 4.00 / SOURCE_LENGTH_Y
stair_root.scale.x = horizontal_scale
stair_root.scale.y = horizontal_scale
stair_root.scale.z = vertical_scale
stair_root.location.x = 10.85 - SOURCE_BOUNDS_CENTER_X * horizontal_scale
stair_root.location.y = 4.30 - SOURCE_BOUNDS_CENTER_Y * horizontal_scale
stair_root.location.z = FIRST_TREAD_HEIGHT - SOURCE_FIRST_TREAD_Z * vertical_scale
stair_root["FIRST_TREAD_HEIGHT_M"] = FIRST_TREAD_HEIGHT
stair_root["TOP_LANDING_HEIGHT_M"] = FLOOR_LEVEL_2
stair_root["VERTICAL_SCALE_REASON"] = "First tread 0.20m and top landing 3.20m"
bpy.context.view_layer.update()

# Main second-storey structural slab. The entrance vestibule remains one storey.
slab_bottom = FLOOR_LEVEL_2 - FLOOR_THICKNESS
add_box(
    "2F_MAIN_DECK",
    -10.0,
    8.0,
    -7.0,
    7.0,
    slab_bottom,
    FLOOR_LEVEL_2,
    floor_mat,
    floor_collection,
)

# Elevator/service block with a real shaft opening (x 9.0..11.5, y -5.0..-1.3).
add_box("2F_ELEVATOR_SOUTH", 8.0, 12.0, -7.0, -5.0, slab_bottom, FLOOR_LEVEL_2, floor_mat, floor_collection)
add_box("2F_ELEVATOR_NORTH", 8.0, 12.0, -1.3, 2.0, slab_bottom, FLOOR_LEVEL_2, floor_mat, floor_collection)
add_box("2F_ELEVATOR_WEST", 8.0, 9.0, -5.0, -1.3, slab_bottom, FLOOR_LEVEL_2, floor_mat, floor_collection)
add_box("2F_ELEVATOR_EAST", 11.5, 12.0, -5.0, -1.3, slab_bottom, FLOOR_LEVEL_2, floor_mat, floor_collection)

# Floor around the stair. The 0.85 m north strip is the turning/exit landing;
# the stair's own metal platform overlaps it slightly for a gap-free transition.
add_box("2F_STAIR_WEST_WALKWAY", 8.0, 10.05, 2.0, 6.15, slab_bottom, FLOOR_LEVEL_2, landing_mat, floor_collection)
add_box("2F_STAIR_TOP_EXIT_LANDING", 8.0, 12.0, 6.15, 7.0, slab_bottom, FLOOR_LEVEL_2, landing_mat, floor_collection)
add_box("2F_STAIR_EAST_RIM", 11.70, 12.0, 2.0, 6.15, slab_bottom, FLOOR_LEVEL_2, landing_mat, floor_collection)
add_box("2F_STAIR_SOUTH_RIM", 10.05, 11.70, 2.0, 2.28, slab_bottom, FLOOR_LEVEL_2, landing_mat, floor_collection)

# Store exact structure facts for later Unity collision/export work.
scene = bpy.context.scene
scene["BUILD_STAGE"] = "STEP 3 - WALKABLE STAIR TO 2F"
scene["FLOOR_2_LEVEL_METRES"] = FLOOR_LEVEL_2
scene["FLOOR_2_THICKNESS_METRES"] = FLOOR_THICKNESS
scene["STAIR_OPENING_XY"] = "x 10.05..11.70, y 2.28..6.15"
scene["UNITY_NOTE"] = "Use stair mesh or a simple ramp collider; first tread is 0.20m."

# Existing helper objects caused black guide lines in the viewport. Keep them
# available for rendering but hide their viewport displays in the saved file.
for obj in scene.objects:
    if obj.type in {"CAMERA", "LIGHT"}:
        obj.hide_viewport = True
        if obj.type == "LIGHT":
            obj.hide_render = True

top_camera = add_camera(
    "STEP3_STAIR_TOP_CAMERA",
    (10.0, 4.5, 11.0),
    (10.0, 4.5, 2.5),
    helper_collection,
    ortho_scale=6.4,
)
cutaway_camera = add_camera(
    "STEP3_STAIR_CUTAWAY_CAMERA",
    (15.2, 0.4, 5.9),
    (10.75, 4.25, 2.05),
    helper_collection,
)
key_light = add_area_light(
    "STEP3_STAIR_KEY_LIGHT",
    (7.0, 1.0, 9.0),
    (10.4, 4.5, 2.0),
    900,
    6.0,
    helper_collection,
)
fill_light = add_area_light(
    "STEP3_STAIR_FILL_LIGHT",
    (12.0, 6.0, 8.0),
    (10.5, 4.5, 2.0),
    480,
    5.0,
    helper_collection,
)

world = scene.world or bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.008, 0.011, 0.016, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.30

scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1100
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"

# Render helpers must be enabled during rendering.
for obj in (top_camera, cutaway_camera, key_light, fill_light):
    obj.hide_viewport = False

scene.camera = top_camera
scene.render.filepath = TOP_PREVIEW
bpy.ops.render.render(write_still=True)

# The east exterior wall is hidden only for this inspection render. It remains
# present and enabled in the saved Blender structure.
east_wall = bpy.data.objects.get("EXT_EAST")
if east_wall:
    east_wall.hide_render = True
scene.camera = cutaway_camera
scene.render.filepath = CUTAWAY_PREVIEW
bpy.ops.render.render(write_still=True)
if east_wall:
    east_wall.hide_render = False

# Hide all helper wireframes for the user's normal modelling view.
for obj in scene.objects:
    if obj.type in {"CAMERA", "LIGHT"}:
        obj.hide_viewport = True

stair_parts = [obj for obj in stair_root.children_recursive if obj.type == "MESH"]
stair_minimum, stair_maximum = mesh_bounds(stair_parts)
scene.camera = cutaway_camera
bpy.ops.wm.save_as_mainfile(filepath=TARGET)

print(
    {
        "blend": TARGET,
        "top_preview": TOP_PREVIEW,
        "cutaway_preview": CUTAWAY_PREVIEW,
        "first_tread_m": FIRST_TREAD_HEIGHT,
        "top_landing_m": FLOOR_LEVEL_2,
        "stair_bounds": (
            tuple(round(value, 4) for value in stair_minimum),
            tuple(round(value, 4) for value in stair_maximum),
        ),
        "floor_objects": len(floor_collection.objects),
    }
)
