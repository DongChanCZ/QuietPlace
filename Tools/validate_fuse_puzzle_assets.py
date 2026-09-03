import json
from pathlib import Path

import bpy


ROOT = Path(r"C:\Users\MBC-501-14\Desktop\LabHorror")
BOARD_FBX = ROOT / "Blender" / "FusePuzzle" / "Switchboard_Empty.fbx"
FUSE_FBX = ROOT / "Blender" / "FusePuzzle" / "Fuse_Clean.fbx"


def rounded(values):
    return [round(float(value), 5) for value in values]


board = bpy.data.objects["Switchboard_Empty_Body"]
fuse = bpy.data.objects["Fuse_Clean"]
installed = bpy.data.objects["Fuse_Installed_Preview"]
socket = bpy.data.objects["FuseInstallAnchor"]
approach = bpy.data.objects["FuseApproachAnchor"]
removed = bpy.data.objects["REMOVED_Right_Fuse_Reference"]

assert len(board.data.vertices) == 44345
assert len(board.data.polygons) == 49170
assert len(removed.data.vertices) == 746
assert len(removed.data.polygons) == 829
assert abs(fuse.dimensions.z - 0.47) < 0.002
assert max(fuse.dimensions.x, fuse.dimensions.y) < 0.10
assert rounded(socket.location) == [0.296, 0.42, 0.02]
assert rounded(approach.location) == [0.296, 0.65, 0.02]
assert rounded(installed.location) == rounded(socket.location)
assert bpy.data.collections["02_FUSE_CLEAN"].hide_viewport
assert bpy.data.collections["03_INSTALLED_PREVIEW"].hide_viewport
assert bpy.data.collections["99_REMOVED_REFERENCE"].hide_viewport
assert not list(bpy.data.libraries)

file_images = [image for image in bpy.data.images if image.source == "FILE"]
assert file_images
assert all(image.packed_file for image in file_images)

result = {
    "blend": {
        "board_vertices": len(board.data.vertices),
        "board_faces": len(board.data.polygons),
        "removed_vertices": len(removed.data.vertices),
        "removed_faces": len(removed.data.polygons),
        "fuse_dimensions": rounded(fuse.dimensions),
        "socket": rounded(socket.location),
        "approach": rounded(approach.location),
        "packed_file_images": len(file_images),
    }
}

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(BOARD_FBX))
board_meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
board_empties = {obj.name: rounded(obj.location) for obj in bpy.context.scene.objects if obj.type == "EMPTY"}
assert len(board_meshes) == 1
assert len(board_meshes[0].data.vertices) == 44345
assert len(board_meshes[0].data.polygons) == 49170
assert any(name.startswith("FuseInstallAnchor") for name in board_empties)
assert any(name.startswith("FuseApproachAnchor") for name in board_empties)
result["board_fbx"] = {
    "meshes": len(board_meshes),
    "vertices": len(board_meshes[0].data.vertices),
    "faces": len(board_meshes[0].data.polygons),
    "empties": board_empties,
}

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(FUSE_FBX))
fuse_meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
print("FUSE_FBX_OBJECTS=" + repr([(obj.name, obj.type, rounded(obj.dimensions)) for obj in bpy.context.scene.objects]))
assert len(fuse_meshes) == 1
assert abs(fuse_meshes[0].dimensions.z - 0.47) < 0.002
assert max(fuse_meshes[0].dimensions.x, fuse_meshes[0].dimensions.y) < 0.10
result["fuse_fbx"] = {
    "meshes": len(fuse_meshes),
    "dimensions": rounded(fuse_meshes[0].dimensions),
    "vertices": len(fuse_meshes[0].data.vertices),
    "faces": len(fuse_meshes[0].data.polygons),
}

print("FUSE_PUZZLE_VALIDATION=" + json.dumps(result, ensure_ascii=False, sort_keys=True))
