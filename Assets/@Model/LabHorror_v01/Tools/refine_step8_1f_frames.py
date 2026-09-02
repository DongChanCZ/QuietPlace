"""Refine only existing 1F apertures; retain Step7 and every structural transform."""
import array
import bmesh
import bpy
import hashlib
import json
import math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(r"C:\Users\MBC-501-14\Desktop\LabHorror")
SOURCE = ROOT / "Blender/LabHorror_Blockout_Step7_3F_Floor.blend"
TARGET = ROOT / "Blender/LabHorror_Step8_1F_Detail_20260831.blend"
PREVIEWS = ROOT / "Blender/Previews"
WALL_DEPTH = 0.22
CASING_DEPTH = 0.03
OPENINGS = [
    ("MainEntrance", "X", 9.0, -1.30, 1.30, 0.0, 2.50),
    ("GuardDoor", "Y", -5.0, 3.75, 5.25, 0.0, 2.40),
    ("MedicalDoor", "Y", -5.0, -3.15, -1.85, 0.0, 2.40),
    ("ElevatorDoor", "Y", 8.0, -3.25, -1.75, 0.0, 2.40),
    ("StairDoor", "Y", 8.0, 2.15, 3.55, 0.0, 2.40),
    ("GuardWindow", "Y", -10.0, 3.30, 5.70, 1.10, 2.30),
    ("SouthWindowA", "X", -7.0, -2.20, 0.20, 1.10, 2.30),
    ("SouthWindowB", "X", -7.0, 2.90, 5.30, 1.10, 2.30),
]


def mesh_state(obj):
    coords = array.array("f", [0]) * (len(obj.data.vertices) * 3)
    loops = array.array("i", [0]) * len(obj.data.loops)
    obj.data.vertices.foreach_get("co", coords)
    obj.data.loops.foreach_get("vertex_index", loops)
    return (tuple(v for row in obj.matrix_world for v in row),
            hashlib.sha256(coords.tobytes() + loops.tobytes()).hexdigest(),
            tuple(m.name if m else None for m in obj.data.materials),
            obj.hide_viewport, obj.hide_render)


def material(name, color, metallic, roughness):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    return mat


def world_point(axis, u, n, z):
    return (u, n, z) if axis == "X" else (n, u, z)


def move_to(obj, collection):
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    collection.objects.link(obj)


def frame(opening, thickness, depth, offset, mat, suffix):
    name, axis, plane, left, right, bottom, top = opening
    if bottom == 0:
        profile = [(left-thickness,bottom), (left-thickness,top+thickness),
                   (right+thickness,top+thickness), (right+thickness,bottom),
                   (right,bottom), (right,top), (left,top), (left,bottom)]
        faces = [tuple(range(7,-1,-1)), tuple(range(8,16))]
        faces += [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    else:
        profile = [(left-thickness,bottom-thickness),
                   (right+thickness,bottom-thickness),
                   (right+thickness,top+thickness), (left-thickness,top+thickness),
                   (left,bottom), (right,bottom), (right,top), (left,top)]
        faces = []
        for i in range(4):
            j = (i+1)%4
            faces += [(i,j,4+j,4+i), (8+i,12+i,12+j,8+j),
                      (i,8+i,8+j,j), (4+i,4+j,12+j,12+i)]
    vertices = [world_point(axis,u,plane+offset+n,z)
                for n in (-depth/2,depth/2) for u,z in profile]
    mesh = bpy.data.meshes.new("F1_DTL_" + name + suffix)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    assert all(e.is_manifold for e in bm.edges), name + " frame is not closed"
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(mesh.name, mesh)
    details.objects.link(obj)
    mesh.materials.append(mat)
    bevel = obj.modifiers.new("Small_Fabricated_Edge", "BEVEL")
    bevel.width = 0.0025
    bevel.segments = 3
    bevel.harden_normals = True
    normal = obj.modifiers.new("Planar_Face_Normals", "WEIGHTED_NORMAL")
    normal.keep_sharp = True
    obj["opening"] = name
    obj["scope"] = "1F cosmetic casing outside existing clear aperture"
    return obj


def fasteners(opening, mat):
    name, axis, plane, left, right, bottom, top = opening
    parts = []
    for side in (-1,1):
        for u in (left-0.041,right+0.041):
            for z in (bottom+0.18,top-0.18):
                bpy.ops.mesh.primitive_cylinder_add(
                    vertices=12, radius=0.012, depth=0.006,
                    location=world_point(axis,u,plane+side*(WALL_DEPTH/2+CASING_DEPTH+0.003),z))
                obj = bpy.context.object
                obj.rotation_euler = (math.pi/2,0,0) if axis == "X" else (0,math.pi/2,0)
                move_to(obj, details)
                obj.data.materials.append(mat)
                parts.append(obj)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = parts[0]
    obj.name = "F1_DTL_" + name + "_Fasteners"
    bevel = obj.modifiers.new("Screw_Edge", "BEVEL")
    bevel.width = 0.001
    bevel.segments = 2
    return obj


def aim(obj, target):
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat("-Z","Y").to_euler()


def camera(name, position, target, lens):
    data = bpy.data.cameras.new(name)
    data.lens = lens
    data.clip_start = 0.03
    obj = bpy.data.objects.new(name, data)
    helpers.objects.link(obj)
    obj.location = position
    aim(obj, target)
    return obj


def light(name, position, target, energy, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    helpers.objects.link(obj)
    obj.location = position
    aim(obj, target)


def render(cam, name):
    scene.camera = cam
    scene.render.filepath = str(PREVIEWS / name)
    bpy.ops.render.render(write_still=True)


bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene = bpy.context.scene
original_meshes = {o.name: mesh_state(o) for o in scene.objects if o.type == "MESH"}
details = bpy.data.collections.new("STEP_8_1F_OPENING_DETAILS_ONLY")
scene.collection.children.link(details)
helpers = bpy.data.collections.new("99_STEP8_DETAIL_PREVIEW_HELPERS")
scene.collection.children.link(helpers)

for obj in list(scene.objects):
    if obj.type == "LIGHT":
        obj.hide_render = True
light("DETAIL_Lobby_Softbox", (0,0.5,2.82), (-3,1,0), 430, 7)
light("DETAIL_Guard_Softbox", (-7.4,4.4,2.82), (-9,4.5,1), 115, 3)
light("DETAIL_Exterior_Softbox", (-4,13,6), (0,8,1.5), 950, 6)
light("DETAIL_Window_Fill", (-14,5,4), (-8,4.5,1.5), 320, 4)
lobby_cam = camera("DETAIL_Lobby_Doors", (2.3,1.0,1.68), (-5,1.0,1.36), 25)
guard_cam = camera("DETAIL_Guard_Window", (-6.35,3.0,1.65), (-10,4.5,1.55), 30)
entrance_cam = camera("DETAIL_Main_Entrance", (-3.8,13.3,1.8), (0,8.9,1.45), 34)

scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1200
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
PREVIEWS.mkdir(exist_ok=True)
render(lobby_cam, "step8_before_lobby.png")

liner = material("DETAIL_Recessed_Steel", (0.07,0.085,0.095), 0.68, 0.43)
casing = material("DETAIL_Painted_Steel", (0.15,0.18,0.195), 0.48, 0.46)
bolt = material("DETAIL_Fastener_Steel", (0.24,0.265,0.28), 0.78, 0.34)
for opening in OPENINGS:
    frame(opening, 0.025, WALL_DEPTH, 0, liner, "_Liner")
    for side in (-1,1):
        frame(opening, 0.075, CASING_DEPTH, side*(WALL_DEPTH+CASING_DEPTH)/2,
              casing, "_Casing_" + str(side))
    fasteners(opening, bolt)

# Large bevels on adjacent blockout wall pieces exaggerate hairline joints.
# Keep every vertex and transform; reduce only the existing cosmetic chamfer.
bevel_changes = 0
for obj in bpy.data.collections["01_WALLS_AND_OPENINGS"].objects:
    for mod in obj.modifiers:
        if mod.type == "BEVEL":
            mod.width = 0.0015
            mod.segments = 3
            bevel_changes += 1

bpy.context.view_layer.update()
assert all(mesh_state(bpy.data.objects[name]) == state
           for name,state in original_meshes.items()), "Original structure changed"
assert len(details.objects) == 32, "Unexpected detail object count"

# Rays through a 7x7 aperture grid must hit no newly-added detail geometry.
depsgraph = bpy.context.evaluated_depsgraph_get()
trees = []
for obj in details.objects:
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    vertices = [evaluated.matrix_world @ v.co for v in mesh.vertices]
    trees.append(BVHTree.FromPolygons(vertices, [list(p.vertices) for p in mesh.polygons]))
    evaluated.to_mesh_clear()
clearance_rays = 0
for name,axis,plane,left,right,bottom,top in OPENINGS:
    direction = Vector((0,1,0) if axis == "X" else (1,0,0))
    for fu in (0.01,0.1,0.3,0.5,0.7,0.9,0.99):
        for fz in (0.01,0.1,0.3,0.5,0.7,0.9,0.99):
            origin = Vector(world_point(axis,left+(right-left)*fu,plane-0.4,bottom+(top-bottom)*fz))
            assert not any(tree.ray_cast(origin,direction,0.8)[0] is not None for tree in trees), name + " obstructed"
            clearance_rays += 1

render(lobby_cam, "step8_after_lobby.png")
render(guard_cam, "step8_guard_window.png")
render(entrance_cam, "step8_main_entrance.png")

scene.camera = lobby_cam
scene["DETAIL_PASS"] = "1F existing apertures only; no door leaves, glazing or gameplay changes"
scene["DETAIL_CLEARANCE_RAYS"] = clearance_rays
for obj in helpers.objects:
    obj.hide_viewport = True
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == "VIEW_3D":
            space = area.spaces.active
            space.shading.type = "SOLID"
            space.shading.color_type = "MATERIAL"
            space.shading.show_shadows = True
            space.shading.show_cavity = True
            space.overlay.show_overlays = False
            space.clip_start = 0.03
            space.region_3d.view_perspective = "PERSP"
            space.region_3d.view_rotation = lobby_cam.rotation_euler.to_quaternion()
            space.region_3d.view_location = Vector((-5,1.0,1.36))
            space.region_3d.view_distance = (lobby_cam.location-Vector((-5,1.0,1.36))).length
bpy.ops.object.select_all(action="DESELECT")
bpy.ops.wm.save_as_mainfile(filepath=str(TARGET))
print(json.dumps({"saved":str(TARGET),
                  "original_meshes_preserved":len(original_meshes),
                  "detail_meshes":len(details.objects), "openings":len(OPENINGS),
                  "wall_bevels_refined":bevel_changes,"clearance_rays_passed":clearance_rays}, indent=2))
