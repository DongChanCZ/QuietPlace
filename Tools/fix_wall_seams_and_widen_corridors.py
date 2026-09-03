"""Apply the approved seam and corridor fix to the latest Blender source."""
import bpy
import json
import shutil
from pathlib import Path


ROOT = Path(r"C:\Users\MBC-501-14\Desktop\LabHorror")
SOURCE = ROOT / "Blender/LabHorror_Step10_3F_Rooms.blend"
BACKUP = ROOT / "Blender/LabHorror_Step10_3F_Rooms_PreWideCorridor.blend"
CORRIDOR_SOUTH_Y = -1.40
CORRIDOR_NORTH_Y = 2.60
SEAM_BEVEL = 0.0015


if not SOURCE.exists():
    raise FileNotFoundError(SOURCE)
if not BACKUP.exists():
    shutil.copy2(SOURCE, BACKUP)

bpy.ops.wm.open_mainfile(filepath=str(SOURCE))


def require(name):
    obj = bpy.data.objects.get(name)
    if obj is None or obj.type != "MESH":
        raise RuntimeError(f"Required wall mesh is missing: {name}")
    return obj


def set_y_span(name, start, end):
    obj = require(name)
    if obj.data.users > 1:
        obj.data = obj.data.copy()
    old_span = obj.dimensions.y
    if old_span <= 0.001:
        raise RuntimeError(f"Invalid Y span on {name}: {old_span}")
    factor = (end - start) / old_span
    for vertex in obj.data.vertices:
        vertex.co.y *= factor
    obj.data.update()
    obj.location.y = (start + end) * 0.5


corridor_targets = {
    "2F_CORRIDOR_NORTH": CORRIDOR_NORTH_Y,
    "2F_CORRIDOR_SOUTH": CORRIDOR_SOUTH_Y,
    "3F_CORRIDOR_NORTH": CORRIDOR_NORTH_Y,
    "3F_CORRIDOR_SOUTH": CORRIDOR_SOUTH_Y,
    "2F_ELEVATOR_SHAFT_NORTH": CORRIDOR_SOUTH_Y,
    "3F_ELEVATOR_SHAFT_NORTH": CORRIDOR_SOUTH_Y,
}

moved = []
for prefix, target_y in corridor_targets.items():
    matches = [obj for obj in bpy.data.objects if obj.type == "MESH" and obj.name.startswith(prefix)]
    if not matches:
        raise RuntimeError(f"No wall pieces found for {prefix}")
    for obj in matches:
        obj.location.y = target_y
        moved.append(obj.name)

for name, start, end in (
    ("2F_TOP_OFFICE_MEETING_DIVIDER", CORRIDOR_NORTH_Y, 7.0),
    ("2F_TOP_MEETING_SAMPLE_DIVIDER", CORRIDOR_NORTH_Y, 7.0),
    ("2F_BOTTOM_CABINET_RECORDS_DIVIDER", -7.0, CORRIDOR_SOUTH_Y),
    ("2F_BOTTOM_RECORDS_BREAK_DIVIDER", -7.0, CORRIDOR_SOUTH_Y),
    ("2F_SERVICE_SOUTH_DIVIDER", -7.0, CORRIDOR_SOUTH_Y),
    ("3F_CONTROL_MACHINE_DIVIDER", CORRIDOR_NORTH_Y, 7.0),
    ("3F_SOUTH_ZONE_DIVIDER", -7.0, CORRIDOR_SOUTH_Y),
    ("3F_SERVICE_SOUTH_DIVIDER", -7.0, CORRIDOR_SOUTH_Y),
):
    set_y_span(name, start, end)

wall_collections = (
    "01_WALLS_AND_OPENINGS",
    "01_2F_WALLS_AND_OPENINGS",
    "01_3F_WALLS_AND_OPENINGS",
    "01_3F_ROOM_WALLS",
)
bevel_changes = 0
for collection_name in wall_collections:
    collection = bpy.data.collections.get(collection_name)
    if collection is None:
        raise RuntimeError(f"Required wall collection is missing: {collection_name}")
    for obj in collection.all_objects:
        if obj.type != "MESH":
            continue
        for modifier in obj.modifiers:
            if modifier.type == "BEVEL":
                modifier.width = SEAM_BEVEL
                bevel_changes += 1

bpy.context.view_layer.update()
for prefix, expected_y in corridor_targets.items():
    matches = [obj for obj in bpy.data.objects if obj.type == "MESH" and obj.name.startswith(prefix)]
    if any(abs(obj.matrix_world.translation.y - expected_y) > 0.001 for obj in matches):
        raise RuntimeError(f"{prefix} did not reach y={expected_y}")

width_2f = require("2F_CORRIDOR_NORTH_SOLID_01").location.y - require("2F_CORRIDOR_SOUTH_SOLID_01").location.y
width_3f = require("3F_CORRIDOR_NORTH_SOLID_01").location.y - require("3F_CORRIDOR_SOUTH_SOLID_01").location.y
if abs(width_2f - 4.0) > 0.001 or abs(width_3f - 4.0) > 0.001:
    raise RuntimeError(f"Corridor width check failed: 2F={width_2f}, 3F={width_3f}")
if bevel_changes < 100:
    raise RuntimeError(f"Too few wall bevels changed: {bevel_changes}")

scene = bpy.context.scene
scene["CORRIDOR_WIDTH_METRES"] = 4.0
scene["WALL_SEAM_BEVEL_METRES"] = SEAM_BEVEL
scene["WALL_SEAM_FIX"] = "Reduced modular wall bevels; aligned corridor/elevator junctions"
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))

print(json.dumps({
    "saved": str(SOURCE),
    "backup": str(BACKUP),
    "moved_wall_pieces": len(moved),
    "bevels_reduced": bevel_changes,
    "corridor_width_2f_m": round(width_2f, 3),
    "corridor_width_3f_m": round(width_3f, 3),
}, indent=2))
