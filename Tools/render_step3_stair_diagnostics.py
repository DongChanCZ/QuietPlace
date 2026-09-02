import bpy
import os
from mathutils import Vector


BLEND = r"C:\Users\MBC-501-14\Desktop\LabHorror\Blender\LabHorror_Blockout_Step3_2F_Access.blend"
OUT_DIR = r"C:\Users\MBC-501-14\Desktop\LabHorror\Blender\Previews\Diagnostics"

bpy.ops.wm.open_mainfile(filepath=BLEND)
os.makedirs(OUT_DIR, exist_ok=True)

root = bpy.data.objects.get("USER_STAIR_ROOT")
stair_objects = set(root.children_recursive) | {root}
for obj in bpy.context.scene.objects:
    if obj.type == "MESH" and obj not in stair_objects:
        obj.hide_render = True
    if obj.type == "LIGHT":
        obj.hide_render = True

bpy.ops.object.light_add(type="AREA", location=(7.0, 1.0, 8.0))
light = bpy.context.object
light.data.energy = 1000
light.data.size = 5.0
direction = Vector((10.85, 4.3, 2.0)) - light.location
light.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

bpy.ops.object.camera_add()
camera = bpy.context.object
camera.data.type = "ORTHO"
camera.data.ortho_scale = 5.6
bpy.context.scene.camera = camera

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 700
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.view_settings.look = "AgX - Medium High Contrast"

views = {
    "south": ((10.85, -3.0, 2.0), (10.85, 4.3, 2.0)),
    "north": ((10.85, 11.0, 2.0), (10.85, 4.3, 2.0)),
    "west": ((4.0, 4.3, 2.0), (10.85, 4.3, 2.0)),
    "top": ((10.85, 4.3, 10.0), (10.85, 4.3, 2.0)),
}

for name, (location, target) in views.items():
    camera.location = location
    direction = Vector(target) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(OUT_DIR, f"stair_{name}.png")
    bpy.ops.render.render(write_still=True)

print({"diagnostic_dir": OUT_DIR, "views": list(views)})
