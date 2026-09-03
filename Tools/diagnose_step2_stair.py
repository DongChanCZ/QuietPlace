import bpy
from mathutils import Vector


BLEND = r"C:\Users\MBC-501-14\Desktop\LabHorror\Blender\LabHorror_Blockout_Step2_Structure.blend"

bpy.ops.wm.open_mainfile(filepath=BLEND)
bpy.context.view_layer.update()

root = bpy.data.objects.get("USER_STAIR_ROOT")
if root is None:
    raise RuntimeError("USER_STAIR_ROOT not found")

parts = [obj for obj in root.children_recursive if obj.type == "MESH"]
points = [obj.matrix_world @ Vector(corner) for obj in parts for corner in obj.bound_box]
minimum = Vector((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points)))
maximum = Vector((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points)))

inner_min_x = 8.0 + 0.22 * 0.5
inner_max_x = 12.0 - 0.22 * 0.5
inner_min_y = 2.0 + 0.22 * 0.5
inner_max_y = 7.0 - 0.22 * 0.5

levels = {}
for obj in parts:
    normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
    for polygon in obj.data.polygons:
        world_normal = (normal_matrix @ polygon.normal).normalized()
        if world_normal.z < 0.94:
            continue
        center = obj.matrix_world @ polygon.center
        key = round(center.z, 2)
        level = levels.setdefault(
            key,
            {"faces": 0, "min_x": center.x, "max_x": center.x, "min_y": center.y, "max_y": center.y},
        )
        level["faces"] += 1
        level["min_x"] = min(level["min_x"], center.x)
        level["max_x"] = max(level["max_x"], center.x)
        level["min_y"] = min(level["min_y"], center.y)
        level["max_y"] = max(level["max_y"], center.y)

print(
    {
        "root_scale": tuple(round(value, 4) for value in root.scale),
        "stair_bounds_min": tuple(round(value, 4) for value in minimum),
        "stair_bounds_max": tuple(round(value, 4) for value in maximum),
        "stair_size": tuple(round(value, 4) for value in (maximum - minimum)),
        "room_inside": (inner_min_x, inner_max_x, inner_min_y, inner_max_y),
        "clearance_left_x": round(minimum.x - inner_min_x, 4),
        "clearance_right_x": round(inner_max_x - maximum.x, 4),
        "clearance_bottom_y": round(minimum.y - inner_min_y, 4),
        "clearance_top_y": round(inner_max_y - maximum.y, 4),
        "horizontal_levels_highest": [
            (
                z,
                data["faces"],
                round(data["min_x"], 3),
                round(data["max_x"], 3),
                round(data["min_y"], 3),
                round(data["max_y"], 3),
            )
            for z, data in sorted(levels.items(), reverse=True)[:16]
        ],
    }
)
