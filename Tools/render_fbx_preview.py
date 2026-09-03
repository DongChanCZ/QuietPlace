import sys
from pathlib import Path

import bpy
from mathutils import Vector


source = Path(sys.argv[sys.argv.index("--") + 1])
output = Path(sys.argv[sys.argv.index("--") + 2])
output.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source))

objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
corners = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
low = Vector(tuple(min(point[axis] for point in corners) for axis in range(3)))
high = Vector(tuple(max(point[axis] for point in corners) for axis in range(3)))
center = (low + high) * 0.5
span = max(high - low)

camera_data = bpy.data.cameras.new("Preview Camera")
camera = bpy.data.objects.new("Preview Camera", camera_data)
bpy.context.scene.collection.objects.link(camera)
bpy.context.scene.camera = camera
camera.data.type = "ORTHO"
camera.data.ortho_scale = span * 1.35

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 800
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.world = bpy.data.worlds.new("Preview World")
scene.world.color = (0.02, 0.02, 0.02)

for name, position, energy, size in (
    ("Key", center + Vector((span, span, span * 1.5)), 1000, span * 2.0),
    ("Fill", center + Vector((-span, span * 0.5, span * 0.5)), 700, span * 2.5),
    ("Rim", center + Vector((0.0, -span, span)), 900, span * 1.5),
):
    light_data = bpy.data.lights.new(name, "AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    bpy.context.scene.collection.objects.link(light)
    light.location = position
    light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()

views = {
    "front_y": Vector((0.0, 1.0, 0.0)),
    "back_y": Vector((0.0, -1.0, 0.0)),
    "side_x": Vector((1.0, 0.0, 0.0)),
    "top_z": Vector((0.0, 0.0, 1.0)),
    "iso": Vector((1.2, 1.4, 1.0)).normalized(),
}
for name, direction in views.items():
    camera.location = center + direction * span * 3.0
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(output / f"swichboard_textured_{name}.png")
    bpy.ops.render.render(write_still=True)

print(f"PREVIEWS={output}")
