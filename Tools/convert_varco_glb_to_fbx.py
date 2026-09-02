import bpy
import json
import math
from pathlib import Path

from mathutils import Matrix, Vector


ROOT = Path(r"C:\Users\MBC-501-14\Desktop\LabHorror")
SOURCE = ROOT / "Assets" / "LabBlockout" / "Models" / "VARCO"
OUTPUT = SOURCE / "FBX"

# Width, depth, height in metres from the approved VARCO prompts.
TARGETS = {
    "HidingTable": (1.80, 0.75, 0.80),
    "BlueShelf": (1.50, 0.55, 2.00),
    "Cabinet": (1.20, 0.60, 2.00),
    "Obstacle": (2.40, 1.00, 1.20),
    "LongBench": (2.20, 0.65, 0.75),
    "SlidingDoor": (1.80, 0.22, 2.60),
    "Keypad": (0.18, 0.09, 0.32),
    "BroadcastConsole": (1.20, 0.60, 1.35),
    "EscapeDescender": (0.38, 0.20, 0.48),
    "TransformerModule": (0.55, 0.35, 0.45),
}


def bounds(mesh):
    points = [Vector(vertex.co) for vertex in mesh.data.vertices]
    low = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    high = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    return low, high


def import_and_flatten(source_path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source_path))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError(f"No mesh found in {source_path.name}")

    for obj in meshes:
        obj.data = obj.data.copy()
        obj.data.transform(obj.matrix_world)
        obj.matrix_world = Matrix.Identity(4)
        obj.parent = None

    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    mesh = bpy.context.object

    for obj in list(bpy.context.scene.objects):
        if obj != mesh:
            bpy.data.objects.remove(obj, do_unlink=True)
    return mesh


def normalize(mesh, target):
    low, high = bounds(mesh)
    size = high - low
    if size.x < size.y:
        mesh.data.transform(Matrix.Rotation(math.radians(90.0), 4, "Z"))
        low, high = bounds(mesh)
        size = high - low

    if min(size) <= 0.0001:
        raise RuntimeError(f"Degenerate bounds for {mesh.name}: {tuple(size)}")

    center = (low + high) * 0.5
    scale = Matrix.Diagonal(Vector((target[0] / size.x, target[1] / size.y, target[2] / size.z, 1.0)))
    translate = Matrix.Translation(Vector((-center.x, -center.y, -low.z)))
    mesh.data.transform(scale @ translate)
    mesh.data.update()

    final_low, final_high = bounds(mesh)
    final_size = final_high - final_low
    for actual, expected in zip(final_size, target):
        if abs(actual - expected) > 0.005:
            raise RuntimeError(f"Size check failed: actual={tuple(final_size)}, expected={target}")

    # Unity's FBX importer keeps these generated meshes in file axes. Rotate
    # Blender Z-up into Unity Y-up now, so scene instances do not lie on back.
    mesh.data.transform(Matrix.Rotation(math.radians(-90.0), 4, "X"))
    mesh.data.update()
    return tuple(round(value, 4) for value in final_size)


def export_fbx(mesh, output_path):
    mesh.name = output_path.stem
    mesh.data.name = output_path.stem + "_Mesh"
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.export_scene.fbx(
        filepath=str(output_path),
        use_selection=True,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        use_space_transform=True,
        add_leaf_bones=False,
        mesh_smooth_type="FACE",
        path_mode="COPY",
        embed_textures=True,
    )


OUTPUT.mkdir(parents=True, exist_ok=True)
report = []
for name, target in TARGETS.items():
    source_path = SOURCE / f"{name}.glb"
    if not source_path.exists():
        raise FileNotFoundError(source_path)
    mesh = import_and_flatten(source_path)
    final_size = normalize(mesh, target)
    output_path = OUTPUT / f"LH_VARCO_{name}.fbx"
    export_fbx(mesh, output_path)
    report.append({"name": name, "size_m": final_size, "fbx": str(output_path)})

print(json.dumps(report, ensure_ascii=False))
