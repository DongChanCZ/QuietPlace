"""Create non-destructive fuse/switchboard assets from the two supplied FBX files."""
import json
import math
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


ROOT = Path(r"C:\Users\MBC-501-14\Desktop\LabHorror")
BOARD_SOURCE = Path(r"C:\Users\MBC-501-14\Desktop\미니프로잭트\swichboard.fbx")
FUSE_SOURCE = Path(r"C:\Users\MBC-501-14\Desktop\미니프로잭트\fuze.fbx")
OUTPUT = ROOT / "Blender/FusePuzzle"
PREVIEWS = ROOT / "Blender/Previews"
BLEND_PATH = OUTPUT / "FusePuzzle_Cleaned.blend"
BOARD_FBX = OUTPUT / "Switchboard_Empty.fbx"
FUSE_FBX = OUTPUT / "Fuse_Clean.fbx"
REPORT = OUTPUT / "FusePuzzle_Cleaned_Report.json"

SOCKET = Vector((0.296, 0.420, 0.020))
APPROACH = Vector((0.296, 0.650, 0.020))
TARGET_FUSE_LENGTH = 0.47


def bounds(vertices):
    low = Vector((min(v.co.x for v in vertices), min(v.co.y for v in vertices), min(v.co.z for v in vertices)))
    high = Vector((max(v.co.x for v in vertices), max(v.co.y for v in vertices), max(v.co.z for v in vertices)))
    return low, high


def connected_groups(mesh):
    parent = list(range(len(mesh.vertices)))

    def find(index):
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return index

    for edge in mesh.edges:
        a, b = map(find, edge.vertices)
        if a != b:
            parent[b] = a
    groups = {}
    for index in range(len(mesh.vertices)):
        groups.setdefault(find(index), []).append(index)
    return list(groups.values())


def right_fuse_vertices(mesh):
    chosen = set()
    selected_groups = []
    for group in connected_groups(mesh):
        points = [mesh.vertices[index].co for index in group]
        low, high = bounds([mesh.vertices[index] for index in group])
        center = (low + high) * 0.5
        # Select whole disconnected pieces in the right-front cartridge volume.
        # Pieces reaching behind y=.28 are socket hardware and remain on the board.
        if (0.245 <= center.x <= 0.337 and
                0.392 <= center.y <= 0.468 and
                -0.250 <= center.z <= 0.280):
            chosen.update(group)
            selected_groups.append({
                "vertices": len(group),
                "low": [round(v, 6) for v in low],
                "high": [round(v, 6) for v in high],
                "center": [round(v, 6) for v in center],
            })
    return chosen, selected_groups


def keep_vertices(obj, indices):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    delete = [vertex for vertex in bm.verts if vertex.index not in indices]
    bmesh.ops.delete(bm, geom=delete, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def remove_vertices(obj, indices):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[index] for index in sorted(indices)], context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def move_to(obj, collection):
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def empty(name, location, collection):
    obj = bpy.data.objects.new(name, None)
    collection.objects.link(obj)
    obj.location = location
    obj.empty_display_type = "ARROWS"
    obj.empty_display_size = 0.09
    return obj


def select_only(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def export(objects, path):
    select_only(objects)
    bpy.ops.export_scene.fbx(
        filepath=str(path), check_existing=False, use_selection=True,
        object_types={"MESH", "EMPTY"}, global_scale=1, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
        bake_space_transform=False, use_mesh_modifiers=True, mesh_smooth_type="OFF",
        use_custom_props=True, add_leaf_bones=False, bake_anim=False,
        path_mode="COPY", embed_textures=True,
    )


def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_camera(name, location, target, lens=52):
    data = bpy.data.cameras.new(name)
    data.lens = lens
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    aim(obj, target)
    return obj


def add_light(name, location, target, energy, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    aim(obj, target)


def render(camera, path):
    scene = bpy.context.scene
    scene.camera = camera
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(BOARD_SOURCE))
board = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
assert len([obj for obj in bpy.context.scene.objects if obj.type == "MESH"]) == 1
board.name = "Switchboard_Empty_Body"
board.data.name = "Switchboard_Empty_Geometry"
original_vertices = len(board.data.vertices)
original_faces = len(board.data.polygons)
original_bounds = [Vector(corner) for corner in board.bound_box]
selected, selected_groups = right_fuse_vertices(board.data)
assert 100 < len(selected) < original_vertices * 0.20, "Unsafe cartridge selection"

removed = board.copy()
removed.data = board.data.copy()
bpy.context.scene.collection.objects.link(removed)
removed.name = "REMOVED_Right_Fuse_Reference"
removed.data.name = "REMOVED_Right_Fuse_Geometry"
keep_vertices(removed, selected)
remove_vertices(board, selected)
assert len(board.data.vertices) + len(removed.data.vertices) == original_vertices
assert len(board.data.polygons) + len(removed.data.polygons) == original_faces
assert len(board.data.polygons) > original_faces * 0.85

empty_collection = bpy.data.collections.new("01_SWITCHBOARD_EMPTY")
fuse_collection = bpy.data.collections.new("02_FUSE_CLEAN")
preview_collection = bpy.data.collections.new("03_INSTALLED_PREVIEW")
reference_collection = bpy.data.collections.new("99_REMOVED_REFERENCE")
for collection in (empty_collection, fuse_collection, preview_collection, reference_collection):
    bpy.context.scene.collection.children.link(collection)
move_to(board, empty_collection)
move_to(removed, reference_collection)
removed.hide_viewport = True
removed.hide_render = True
socket = empty("FuseInstallAnchor", SOCKET, empty_collection)
socket["purpose"] = "Snap installed fuse here; local long axis Z"
socket["insert_direction"] = "+Y to -Y"
approach = empty("FuseApproachAnchor", APPROACH, empty_collection)
approach["purpose"] = "Start of future insertion animation"

# Import the supplied standalone fuse and normalize it to the board's right slot.
bpy.ops.import_scene.fbx(filepath=str(FUSE_SOURCE))
fuse_candidates = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj not in (board, removed)]
assert len(fuse_candidates) == 1
fuse = fuse_candidates[0]
fuse.name = "Fuse_Clean"
fuse.data.name = "Fuse_Clean_Geometry"
source_fuse_size = fuse.dimensions.copy()
uniform_scale = TARGET_FUSE_LENGTH / max(source_fuse_size)
fuse.rotation_euler = (math.pi / 2, 0, 0)  # source Y length -> local Z length
fuse.scale = (uniform_scale,) * 3
select_only([fuse])
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
fuse.location = (0, 0, 0)
move_to(fuse, fuse_collection)
fuse["role"] = "Pickup visual; use a simple Capsule/Box Collider in Unity"
assert abs(fuse.dimensions.z - TARGET_FUSE_LENGTH) < 0.002

installed = fuse.copy()
installed.data = fuse.data
installed.name = "Fuse_Installed_Preview"
preview_collection.objects.link(installed)
installed.location = SOCKET
installed["role"] = "Initial inactive visual in Unity"
preview_collection.hide_viewport = True
preview_collection.hide_render = True
fuse_collection.hide_viewport = False
fuse_collection.hide_render = True
reference_collection.hide_viewport = True
reference_collection.hide_render = True

OUTPUT.mkdir(parents=True, exist_ok=True)
PREVIEWS.mkdir(parents=True, exist_ok=True)
export([board, socket, approach], BOARD_FBX)
export([fuse], FUSE_FBX)
fuse_collection.hide_viewport = True

# Render empty, installed, and extraction-debug views without changing saved visibility.
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 900
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
if scene.world is None:
    scene.world = bpy.data.worlds.new("FusePuzzle_World")
scene.world.color = (0.008, 0.01, 0.014)
camera = add_camera("FusePuzzle_Preview_Camera", (1.32, 1.48, 0.94), (0, 0.08, 0.01))
add_light("FusePuzzle_Key", (1.0, 1.2, 1.5), (0, 0, 0), 620, 2.2)
add_light("FusePuzzle_Fill", (-1.1, 0.7, 0.5), (0, 0, 0), 340, 2.0)
add_light("FusePuzzle_Rim", (0, -1.1, 1.0), (0, 0, 0), 430, 1.4)
board.hide_render = False
render(camera, PREVIEWS / "fuse_puzzle_switchboard_empty.png")
preview_collection.hide_render = False
installed.hide_render = False
render(camera, PREVIEWS / "fuse_puzzle_switchboard_installed.png")

debug_material = bpy.data.materials.new("DEBUG_Removed_Right_Fuse")
debug_material.diffuse_color = (0.85, 0.015, 0.01, 1)
debug_material.use_nodes = True
debug_material.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.85, 0.015, 0.01, 1)
removed.data.materials.clear()
removed.data.materials.append(debug_material)
installed.hide_render = True
preview_collection.hide_render = True
reference_collection.hide_render = False
removed.hide_render = False
render(camera, PREVIEWS / "fuse_puzzle_removed_region_debug.png")

removed.hide_render = True
reference_collection.hide_render = True
installed.hide_render = True
for collection in (fuse_collection, preview_collection, reference_collection):
    collection.hide_viewport = True
    collection.hide_render = True
for obj in [camera, *[obj for obj in scene.objects if obj.type == "LIGHT"]]:
    obj.hide_viewport = True
    obj.hide_render = True

scene["FUSE_PUZZLE_SCOPE"] = "Clean assets and anchors only; no Unity interaction added"
scene["FUSE_SOCKET_LOCAL"] = tuple(SOCKET)
scene["FUSE_INSERT_DIRECTION"] = "+Y to -Y"
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
REPORT.write_text(json.dumps({
    "sources_preserved": [str(BOARD_SOURCE), str(FUSE_SOURCE)],
    "blend": str(BLEND_PATH), "board_fbx": str(BOARD_FBX), "fuse_fbx": str(FUSE_FBX),
    "board": {"vertices_before": original_vertices, "faces_before": original_faces,
              "vertices_after": len(board.data.vertices), "faces_after": len(board.data.polygons),
              "removed_vertices": len(removed.data.vertices), "removed_faces": len(removed.data.polygons),
              "selected_islands": len(selected_groups)},
    "socket_local": list(SOCKET), "approach_local": list(APPROACH),
    "fuse": {"source_dimensions": list(source_fuse_size), "clean_dimensions": list(fuse.dimensions),
             "uniform_scale_from_source": uniform_scale, "long_axis": "Z"},
    "unity_changes": False,
}, ensure_ascii=False, indent=2), encoding="utf-8")
print(REPORT.read_text(encoding="utf-8"))
