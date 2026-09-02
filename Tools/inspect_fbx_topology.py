import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def component_labels(vertex_count, edges):
    parent = list(range(vertex_count))

    def find(index):
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return index

    def union(a, b):
        root_a, root_b = find(a), find(b)
        if root_a != root_b:
            parent[root_b] = root_a

    for edge in edges:
        union(edge.vertices[0], edge.vertices[1])
    return [find(index) for index in range(vertex_count)]


def world_bbox(points, matrix):
    transformed = [matrix @ point for point in points]
    low = [min(point[axis] for point in transformed) for axis in range(3)]
    high = [max(point[axis] for point in transformed) for axis in range(3)]
    return low, high


def rounded(values):
    return [round(float(value), 5) for value in values]


def inspect_object(obj):
    mesh = obj.data
    labels = component_labels(len(mesh.vertices), mesh.edges)
    groups = {}
    for index, label in enumerate(labels):
        groups.setdefault(label, []).append(index)

    islands = []
    for indices in groups.values():
        points = [mesh.vertices[index].co for index in indices]
        low, high = world_bbox(points, obj.matrix_world)
        size = [high[axis] - low[axis] for axis in range(3)]
        islands.append({
            "vertices": len(indices),
            "min": rounded(low),
            "max": rounded(high),
            "size": rounded(size),
            "center": rounded([(low[axis] + high[axis]) * 0.5 for axis in range(3)]),
            "bbox_volume": round(math.prod(size), 7),
        })
    islands.sort(key=lambda item: item["bbox_volume"], reverse=True)

    low, high = world_bbox([Vector(corner) for corner in obj.bound_box], obj.matrix_world)
    return {
        "name": obj.name,
        "parent": obj.parent.name if obj.parent else None,
        "mesh": mesh.name,
        "vertices": len(mesh.vertices),
        "edges": len(mesh.edges),
        "polygons": len(mesh.polygons),
        "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
        "location": rounded(obj.location),
        "rotation_euler": rounded(obj.rotation_euler),
        "scale": rounded(obj.scale),
        "min": rounded(low),
        "max": rounded(high),
        "size": rounded([high[axis] - low[axis] for axis in range(3)]),
        "island_count": len(islands),
        "islands": islands,
    }


def main():
    assert component_labels(5, [type("E", (), {"vertices": (0, 1)})(), type("E", (), {"vertices": (3, 4)})()])[0] == 0
    args = sys.argv[sys.argv.index("--") + 1:]
    source, destination = map(Path, args[:2])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    report = {
        "source": str(source),
        "scene_unit_scale": bpy.context.scene.unit_settings.scale_length,
        "object_count": len(bpy.context.scene.objects),
        "mesh_object_count": len(meshes),
        "objects": [inspect_object(obj) for obj in meshes],
    }
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"REPORT={destination}")


main()
