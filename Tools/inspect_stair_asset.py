import bpy
import os
from mathutils import Vector


ASSET = r"C:\Users\MBC-501-14\Desktop\3D 게임벽 미니프로잭트\계단.glb"
PREVIEW = r"C:\Users\MBC-501-14\Desktop\LabHorror\Blender\Previews\stair_asset_inspection.png"

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=ASSET)
bpy.context.view_layer.update()

mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
points = []
for obj in mesh_objects:
    points.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)

if not points:
    raise RuntimeError("No mesh objects found in stair asset")

minimum = Vector((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points)))
maximum = Vector((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points)))
size = maximum - minimum

horizontal_levels = {}
for obj in mesh_objects:
    normal_matrix = obj.matrix_world.to_3x3()
    for polygon in obj.data.polygons:
        world_normal = (normal_matrix @ polygon.normal).normalized()
        if world_normal.z < 0.94:
            continue
        center_world = obj.matrix_world @ polygon.center
        key = round(center_world.z, 3)
        record = horizontal_levels.setdefault(key, {"area": 0.0, "min_y": center_world.y, "max_y": center_world.y})
        record["area"] += polygon.area
        record["min_y"] = min(record["min_y"], center_world.y)
        record["max_y"] = max(record["max_y"], center_world.y)

print(
    {
        "mesh_count": len(mesh_objects),
        "bounds_min": tuple(round(value, 5) for value in minimum),
        "bounds_max": tuple(round(value, 5) for value in maximum),
        "size_xyz": tuple(round(value, 5) for value in size),
        "object_names": [obj.name for obj in mesh_objects[:20]],
        "largest_horizontal_levels": [
            (z, round(data["area"], 5), round(data["min_y"], 4), round(data["max_y"], 4))
            for z, data in sorted(horizontal_levels.items(), key=lambda item: item[1]["area"], reverse=True)[:18]
        ],
    }
)

# Neutral inspection render; it does not modify the asset.
center = (minimum + maximum) * 0.5
for obj in mesh_objects:
    for slot in obj.material_slots:
        if slot.material:
            slot.material.diffuse_color = (0.35, 0.38, 0.42, 1.0)

bpy.ops.object.camera_add(location=(1.35, -1.7, 1.2))
camera = bpy.context.object
camera.data.lens = 52
direction = center - camera.location
camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
bpy.context.scene.camera = camera

bpy.ops.object.light_add(type="AREA", location=(0.0, -0.4, 1.8))
light = bpy.context.object
light.data.energy = 700
light.data.shape = "DISK"
light.data.size = 2.0

world = bpy.context.scene.world or bpy.data.worlds.new("Inspection World")
bpy.context.scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.012, 0.016, 0.022, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.5

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1000
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = PREVIEW
scene.view_settings.look = "AgX - Medium High Contrast"
os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
bpy.ops.render.render(write_still=True)
print({"preview": PREVIEW})
