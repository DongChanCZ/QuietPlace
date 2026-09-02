import bpy
import json
import math
import os
import sys

from mathutils import Matrix


ROOT = r"C:\Users\MBC-501-14\Desktop\LabHorror"
EXPORT_DIR = os.path.join(ROOT, "Blender", "Exports")
EXPORT_PATH = os.path.join(EXPORT_DIR, "LabHorror_Blockout_Step7_3F_Floor.fbx")
STEP12 = "--step12" in sys.argv
STEP11 = STEP12 or "--step11" in sys.argv
STEP10 = STEP11 or "--step10" in sys.argv
# Each stage is built on top of the one before it, so a later flag implies
# every earlier stage's collections.
STEP9 = STEP10 or "--step9" in sys.argv
DETAIL_STEP8 = STEP9 or "--detail-step8" in sys.argv
if STEP12:
    if os.path.basename(bpy.data.filepath) != "LabHorror_Step12_Damage.blend":
        raise RuntimeError("Step12 export requires the saved Step12 Blender file")
    EXPORT_PATH = os.path.join(EXPORT_DIR, "LabHorror_Step12_Damage.fbx")
elif STEP11:
    if os.path.basename(bpy.data.filepath) != "LabHorror_Step11_B1_Rooms.blend":
        raise RuntimeError("Step11 export requires the saved Step11 Blender file")
    EXPORT_PATH = os.path.join(EXPORT_DIR, "LabHorror_Step11_B1_Rooms.fbx")
elif STEP10:
    if os.path.basename(bpy.data.filepath) != "LabHorror_Step10_3F_Rooms.blend":
        raise RuntimeError("Step10 export requires the saved Step10 Blender file")
    EXPORT_PATH = os.path.join(EXPORT_DIR, "LabHorror_Step10_3F_Rooms.fbx")
elif STEP9:
    if os.path.basename(bpy.data.filepath) != "LabHorror_Step9_3F_Walls_Roof.blend":
        raise RuntimeError("Step9 export requires the saved Step9 Blender file")
    EXPORT_PATH = os.path.join(EXPORT_DIR, "LabHorror_Step9_3F_Walls_Roof.fbx")
elif DETAIL_STEP8:
    if os.path.basename(bpy.data.filepath) != "LabHorror_Step8_1F_Detail_20260831.blend":
        raise RuntimeError("Step8 export requires the saved Step8 Blender file")
    EXPORT_PATH = os.path.join(EXPORT_DIR, "LabHorror_Step8_1F_Detail_20260831.fbx")

# Only the approved blockout geometry is exported.  The old imported stair,
# render cameras/lights, measurement guides, and room labels stay in Blender.
EXPORT_COLLECTIONS = (
    "01_ZONE_FLOORS",
    "01_WALLS_AND_OPENINGS",
    "01_2F_STRUCTURAL_FLOOR",
    "02_2F_STAIR_OPENING",
    "01_2F_WALLS_AND_OPENINGS",
    "01_U_STAIR_VISIBLE",
    "03_UNITY_COLLISION_GUIDES",
    "01_STAIR_2F_TO_3F_VISIBLE",
    "02_STAIR_2F_TO_3F_COLLISION",
    "01_3F_STRUCTURAL_FLOOR",
)

EXPECTED_MESH_COUNT = 200
if DETAIL_STEP8:
    EXPORT_COLLECTIONS += ("STEP_8_1F_OPENING_DETAILS_ONLY",)
    EXPECTED_MESH_COUNT += 32
if STEP9:
    # create_step9_3f_walls_roof.py stamps the count it actually built.
    STEP9_MESHES = int(bpy.context.scene.get("STEP9_NEW_MESHES", 0))
    if STEP9_MESHES <= 0:
        raise RuntimeError("STEP9_NEW_MESHES is missing from the Step9 scene")
    EXPORT_COLLECTIONS += ("01_3F_WALLS_AND_OPENINGS", "01_3F_ROOF_SLAB")
    EXPECTED_MESH_COUNT += STEP9_MESHES
if STEP10:
    STEP10_MESHES = int(bpy.context.scene.get("STEP10_NEW_MESHES", 0))
    if STEP10_MESHES <= 0:
        raise RuntimeError("STEP10_NEW_MESHES is missing from the Step10 scene")
    EXPORT_COLLECTIONS += ("01_3F_ROOM_WALLS",)
    EXPECTED_MESH_COUNT += STEP10_MESHES
if STEP11:
    STEP11_MESHES = int(bpy.context.scene.get("STEP11_NEW_MESHES", 0))
    if STEP11_MESHES <= 0:
        raise RuntimeError("STEP11_NEW_MESHES is missing from the Step11 scene")
    EXPORT_COLLECTIONS += ("01_B1_ROOMS",)
    EXPECTED_MESH_COUNT += STEP11_MESHES
if STEP12:
    STEP12_MESHES = int(bpy.context.scene.get("STEP12_NEW_MESHES", 0))
    if STEP12_MESHES <= 0:
        raise RuntimeError("STEP12_NEW_MESHES is missing from the Step12 scene")
    EXPORT_COLLECTIONS += ("01_DAMAGE_STRUCTURE",)
    EXPECTED_MESH_COUNT += STEP12_MESHES


def link_only(obj, collection):
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def reveal_everything(view_layer):
    """Clear every view-layer visibility flag; return the undo steps.

    matrix_world is depsgraph output, and Blender only evaluates objects the
    view layer can see.  Read it from a collection that is hidden or excluded
    and it hands back an identity matrix - which is how the four stair ramp
    colliders once reached Unity sitting at the world origin, unrotated.
    """
    restore = []

    for collection in bpy.data.collections:
        if collection.hide_viewport:
            collection.hide_viewport = False
            restore.append(lambda item=collection: setattr(item, "hide_viewport", True))

    def walk(layer):
        for flag in ("exclude", "hide_viewport"):
            if getattr(layer, flag):
                setattr(layer, flag, False)
                restore.append(lambda item=layer, name=flag: setattr(item, name, True))
        for child in layer.children:
            walk(child)

    walk(view_layer.layer_collection)
    view_layer.update()
    return restore


os.makedirs(EXPORT_DIR, exist_ok=True)

scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0

# Everything below reads matrix_world, so nothing may still be hidden.
restore_visibility = reveal_everything(bpy.context.view_layer)

# Snapshot the source lists before creating any temporary objects.  Blender's
# collection iterator is live, so changing bpy.data.objects while iterating it
# can otherwise invalidate later entries.
source_groups = {}
for collection_name in EXPORT_COLLECTIONS:
    source_collection = bpy.data.collections.get(collection_name)
    if source_collection is None:
        raise RuntimeError(f"Required collection is missing: {collection_name}")
    source_groups[collection_name] = [
        obj
        for obj in list(source_collection.all_objects)
        if obj is not None and obj.type == "MESH"
    ]

temporary = bpy.data.collections.new("__UNITY_EXPORT_TEMP__")
scene.collection.children.link(temporary)

root = bpy.data.objects.new("LabHorror_Blockout_Step7", None)
temporary.objects.link(root)
root["source_blend"] = os.path.basename(bpy.data.filepath)
root["unity_units"] = "1 Blender unit = 1 metre"

exported = []
seen = set()
group_counts = {}

for collection_name in EXPORT_COLLECTIONS:
    group = bpy.data.objects.new(f"GROUP__{collection_name}", None)
    temporary.objects.link(group)
    group.parent = root
    group["source_collection"] = collection_name

    count = 0
    for source in source_groups[collection_name]:
        source_id = source.as_pointer()
        if source_id in seen:
            continue
        seen.add(source_id)

        duplicate = source.copy()
        duplicate.data = source.data
        duplicate.animation_data_clear()
        duplicate.name = source.name
        duplicate.hide_set(False)
        duplicate.hide_viewport = False
        duplicate.hide_render = False
        link_only(duplicate, temporary)

        world_matrix = source.matrix_world.copy()
        duplicate.parent = group
        duplicate.matrix_parent_inverse = Matrix.Identity(4)
        duplicate.matrix_world = world_matrix
        duplicate["source_collection"] = collection_name
        if "COLLIDER_" in duplicate.name:
            duplicate["unity_collider_only"] = True

        exported.append(duplicate)
        count += 1

    group_counts[collection_name] = count

if len(exported) != EXPECTED_MESH_COUNT:
    raise RuntimeError(
        f"Export mesh count changed: expected {EXPECTED_MESH_COUNT}, got {len(exported)}"
    )
if DETAIL_STEP8:
    assert group_counts["STEP_8_1F_OPENING_DETAILS_ONLY"] == 32

# A ramp at the origin with no pitch is the signature of a matrix_world that was
# read before the view layer had evaluated it.  Fail the export instead of
# shipping a staircase the player falls straight through.
bpy.context.view_layer.update()
ramps = sorted(
    (obj for obj in exported if "COLLIDER_" in obj.name), key=lambda obj: obj.name
)
assert len(ramps) == 4, "Missing stair collision guides"
for ramp in ramps:
    location = ramp.matrix_world.translation
    pitch = ramp.matrix_world.to_euler().x
    if location.length < 1e-4 or abs(pitch) < 1e-4:
        raise RuntimeError(
            f"{ramp.name} would export at {tuple(round(v, 4) for v in location)} "
            f"with a {math.degrees(pitch):.3f} deg pitch, so its source transform "
            "was never evaluated"
        )

bpy.ops.object.select_all(action="DESELECT")
for obj in [root, *[obj for obj in temporary.objects if obj != root]]:
    obj.hide_set(False)
    obj.select_set(True)
bpy.context.view_layer.objects.active = root

bpy.ops.export_scene.fbx(
    filepath=EXPORT_PATH,
    check_existing=False,
    use_selection=True,
    object_types={"MESH", "EMPTY"},
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=False,
    use_mesh_modifiers=True,
    mesh_smooth_type="OFF",
    use_custom_props=True,
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="AUTO",
)

for undo in reversed(restore_visibility):
    undo()

print(
    json.dumps(
        {
            "success": True,
            "source": bpy.data.filepath,
            "export": EXPORT_PATH,
            "mesh_count": len(exported),
            "group_counts": group_counts,
            "collider_ramps": {
                ramp.name: {
                    "location_m": [
                        round(value, 4) for value in ramp.matrix_world.translation
                    ],
                    "pitch_deg": round(math.degrees(ramp.matrix_world.to_euler().x), 3),
                    "size_m": [round(value, 4) for value in ramp.dimensions],
                }
                for ramp in ramps
            },
        },
        ensure_ascii=False,
        indent=2,
    )
)
