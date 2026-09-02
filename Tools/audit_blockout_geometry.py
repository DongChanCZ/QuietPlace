"""Measure the shipped blockout for gaps, notches and off-module sizes.

Reads the base boxes (pre-modifier), so it reports design intent, not bevel
rounding.  Reports only; it changes nothing.  Run against the newest stage:

  blender --background Blender/LabHorror_Step9_3F_Walls_Roof.blend \
          --python Tools/audit_blockout_geometry.py

What it deliberately does NOT flag:
  * a wall running 0.20 m up into the slab above - that is how every storey
    is joined here, so it is design, not interpenetration;
  * door/window casings and liners, which are 0.03 m deep by design and stop
    at the opening head rather than at a slab;
  * sills, headers and parapets, which legitimately start or stop mid-storey.
"""
import bpy
import json
from collections import defaultdict
from mathutils import Vector

EPS = 0.001          # 1 mm - below this two faces are touching
SLIVER = 0.06        # a real door/window opening is never this narrow
WALL_THICKNESS = 0.22
SLAB_THICKNESS = 0.20
STOREYS = (0.00, 3.20, 6.40)
STEP = 0.02          # perimeter scan resolution

DETAIL_MARKERS = ("_Casing_", "_Liner", "_Jamb", "_Reveal", "_Stop")
PARTIAL_MARKERS = ("_SILL_", "_HEADER_", "PARAPET") + DETAIL_MARKERS

STRUCTURE = (
    "01_ZONE_FLOORS",
    "01_WALLS_AND_OPENINGS",
    "01_2F_STRUCTURAL_FLOOR",
    "02_2F_STAIR_OPENING",
    "01_2F_WALLS_AND_OPENINGS",
    "01_3F_STRUCTURAL_FLOOR",
    "01_3F_WALLS_AND_OPENINGS",
    "01_3F_ROOF_SLAB",
    "STEP_8_1F_OPENING_DETAILS_ONLY",
)


def reveal():
    for collection in bpy.data.collections:
        collection.hide_viewport = False

    def walk(layer):
        layer.exclude = False
        layer.hide_viewport = False
        for child in layer.children:
            walk(child)

    walk(bpy.context.view_layer.layer_collection)
    bpy.context.view_layer.update()


class Box:
    def __init__(self, obj, group):
        corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
        self.name = obj.name
        self.group = group
        self.min = Vector([min(c[i] for c in corners) for i in range(3)])
        self.max = Vector([max(c[i] for c in corners) for i in range(3)])
        self.size = self.max - self.min
        self.rotated = any(abs(a) > 0.001 for a in obj.matrix_world.to_euler())
        self.detail = any(m in obj.name for m in DETAIL_MARKERS)
        self.partial = any(m in obj.name for m in PARTIAL_MARKERS)

    @property
    def is_slab(self):
        return self.size.z <= 0.25 and self.size.x >= 0.25 and self.size.y >= 0.25

    @property
    def is_wall(self):
        return self.size.z > 0.35 and min(self.size.x, self.size.y) <= 0.35

    def wall_axis(self):
        """0 if the wall is thin on X, 1 if thin on Y."""
        return 0 if self.size.x < self.size.y else 1

    def contains_xy(self, x, y):
        return self.min.x - EPS <= x <= self.max.x + EPS and \
               self.min.y - EPS <= y <= self.max.y + EPS

    def spans_z(self, z):
        return self.min.z - EPS <= z <= self.max.z + EPS


reveal()

boxes, rotated = [], []
for name in STRUCTURE:
    collection = bpy.data.collections.get(name)
    if collection is None:
        continue
    for obj in collection.all_objects:
        if obj.type != "MESH":
            continue
        box = Box(obj, name)
        (rotated if box.rotated else boxes).append(box)

walls = [b for b in boxes if b.is_wall and not b.detail]
slabs = [b for b in boxes if b.is_slab]
findings = defaultdict(list)

# ---- 1. Off-module thickness (structure only; details have their own depth) --
for box in walls:
    thickness = min(box.size.x, box.size.y)
    if abs(thickness - WALL_THICKNESS) > EPS:
        findings["off_module_wall"].append(
            {"name": box.name, "thickness_m": round(thickness, 4)})
for box in slabs:
    if abs(box.size.z - SLAB_THICKNESS) > EPS:
        findings["off_module_slab"].append(
            {"name": box.name, "thickness_m": round(box.size.z, 4)})

# ---- 2. Walls that start nowhere ------------------------------------------
for box in walls:
    if box.partial:
        continue
    if not any(abs(box.min.z - f) < EPS for f in STOREYS):
        findings["wall_base_off_floor"].append(
            {"name": box.name, "base_z": round(box.min.z, 4)})

# ---- 3. Sliver gaps between coplanar neighbours (walls, then slabs) --------
def tiling_gaps(group_boxes, key_fn, along, label):
    lines = defaultdict(list)
    for box in group_boxes:
        lines[key_fn(box)].append(box)
    for key, group in lines.items():
        group.sort(key=lambda b: b.min[along])
        for previous, following in zip(group, group[1:]):
            gap = following.min[along] - previous.max[along]
            if EPS < gap < SLIVER:
                findings[label].append(
                    {"between": [previous.name, following.name],
                     "gap_mm": round(gap * 1000, 2), "line": list(key)})


for axis in (0, 1):
    same = [b for b in walls if b.wall_axis() == axis]
    tiling_gaps(
        same,
        lambda b, a=axis: (round((b.min[a] + b.max[a]) * 0.5, 3),
                           round(b.min.z, 3), round(b.max.z, 3)),
        1 - axis,
        "wall_sliver_gap",
    )
for storey in STOREYS:
    level = [b for b in slabs if abs(b.max.z - storey) < 0.01]
    tiling_gaps(level, lambda b: (round(b.max.z, 3), round(b.min.y, 3), round(b.max.y, 3)), 0, "slab_sliver_gap")
    tiling_gaps(level, lambda b: (round(b.max.z, 3), round(b.min.x, 3), round(b.max.x, 3)), 1, "slab_sliver_gap")

# ---- 4. Wall-on-wall interpenetration (wall into slab above is by design) --
for index, a in enumerate(walls):
    for b in walls[index + 1:]:
        o = [min(a.max[i], b.max[i]) - max(a.min[i], b.min[i]) for i in range(3)]
        if all(v > EPS for v in o) and o[0] * o[1] * o[2] > 1e-4:
            findings["wall_overlaps_wall"].append(
                {"between": [a.name, b.name],
                 "overlap_m": [round(v, 4) for v in o],
                 "volume_m3": round(o[0] * o[1] * o[2], 5)})

# ---- 4b. Walls that read as one line but are not -----------------------------
# Two walls on the same axis and storey, separated along their run by a corridor
# or an opening, whose planes are close but not equal. On site that reads as a
# wall that jumped sideways.
def storey_of(box):
    for index, level in enumerate(STOREYS):
        if abs(box.min.z - level) < 0.35:
            return index
    return -1


for index, a in enumerate(walls):
    axis_a = a.wall_axis()
    for b in walls[index + 1:]:
        if b.wall_axis() != axis_a or storey_of(a) != storey_of(b) or storey_of(a) < 0:
            continue
        plane_a = (a.min[axis_a] + a.max[axis_a]) * 0.5
        plane_b = (b.min[axis_a] + b.max[axis_a]) * 0.5
        offset = abs(plane_a - plane_b)
        if not (EPS < offset <= 0.60):
            continue
        # Two walls butted face to face are offset by their own thickness on
        # purpose; only a real slot or jog counts.
        if min(a.max[axis_a], b.max[axis_a]) - max(a.min[axis_a], b.min[axis_a]) >= -EPS:
            continue
        run = 1 - axis_a
        run_gap = max(a.min[run], b.min[run]) - min(a.max[run], b.max[run])
        if not (-EPS <= run_gap <= 4.0):
            continue                       # not end to end across an opening
        findings["near_miss_alignment"].append(
            {"between": [a.name, b.name],
             "axis": "XY"[axis_a],
             "planes": [round(plane_a, 3), round(plane_b, 3)],
             "offset_mm": round(offset * 1000),
             "run_gap_m": round(run_gap, 3)})

# ---- 4c. Walls that do not stack onto anything below -------------------------
for box in walls:
    level = storey_of(box)
    if level <= 0 or box.partial:
        continue
    axis = box.wall_axis()
    run = 1 - axis
    plane = (box.min[axis] + box.max[axis]) * 0.5
    supported = 0.0
    for other in walls:
        if storey_of(other) != level - 1 or other.wall_axis() != axis:
            continue
        if abs((other.min[axis] + other.max[axis]) * 0.5 - plane) > 0.06:
            continue
        supported += max(0.0, min(box.max[run], other.max[run]) - max(box.min[run], other.min[run]))
    length = box.size[run]
    if length > 0.5 and supported / length < 0.5:
        findings["unstacked_wall"].append(
            {"name": box.name, "storey": level, "axis": "XY"[axis],
             "plane": round(plane, 3), "length_m": round(length, 2),
             "supported_m": round(supported, 2)})

# ---- 4d. Wall ends that stop in open space -----------------------------------
# Ignore a door or window jamb: that has more of the same wall plane close by.
for box in walls:
    if box.partial:
        continue
    axis = box.wall_axis()
    run = 1 - axis
    plane = (box.min[axis] + box.max[axis]) * 0.5
    z = (box.min.z + box.max.z) * 0.5
    for side, edge in (("low", box.min[run]), ("high", box.max[run])):
        reach = edge - 0.05 if side == "low" else edge + 0.05
        probe = [0.0, 0.0, z]
        probe[axis] = plane
        probe[run] = reach
        touching = False
        for other in walls:
            if other is box:
                continue
            if (other.min.x - 0.03 <= probe[0] <= other.max.x + 0.03 and
                    other.min.y - 0.03 <= probe[1] <= other.max.y + 0.03 and
                    other.min.z - 0.03 <= probe[2] <= other.max.z + 0.03):
                touching = True
                break
        if touching:
            continue
        # same plane continuing within 3 m means this end is an opening jamb
        jamb = False
        for other in walls:
            if other is box or other.wall_axis() != axis:
                continue
            if abs((other.min[axis] + other.max[axis]) * 0.5 - plane) > 0.03:
                continue
            distance = (box.min[run] - other.max[run]) if side == "low" else (other.min[run] - box.max[run])
            if -EPS < distance <= 3.0:
                jamb = True
                break
        if not jamb:
            findings["dangling_wall_end"].append(
                {"name": box.name, "end": side, "axis": "XY"[axis],
                 "at": [round(probe[0], 3), round(probe[1], 3), round(probe[2], 2)]})

# ---- 5. Exterior perimeter notches ----------------------------------------
# Walk each storey's outer wall band and look for stretches with no wall in
# them. Two walls meeting at a corner leave a square hole when each one's span
# stops at the other one's centre line instead of its outer face.
perimeter = {}
for storey in STOREYS:
    band = [b for b in walls if abs(b.min.z - storey) < EPS or
            (b.min.z < storey + 0.5 and b.max.z > storey + 1.2)]
    if not band:
        continue
    probe_z = storey + 1.00                       # below every 1.10 m sill
    band = [b for b in band if b.spans_z(probe_z)]
    xs = [b.min.x for b in band] + [b.max.x for b in band]
    ys = [b.min.y for b in band] + [b.max.y for b in band]
    x1, x2, y1, y2 = min(xs), max(xs), min(ys), max(ys)
    runs = []
    for label, fixed, start, end, horizontal in (
        ("south", y1 + 0.05, x1, x2, True),
        ("north", y2 - 0.05, x1, x2, True),
        ("west", x1 + 0.05, y1, y2, False),
        ("east", x2 - 0.05, y1, y2, False),
    ):
        open_from = None
        position = start
        while position <= end + 1e-9:
            px, py = (position, fixed) if horizontal else (fixed, position)
            covered = any(b.contains_xy(px, py) for b in band)
            if not covered and open_from is None:
                open_from = position
            elif covered and open_from is not None:
                runs.append({"side": label, "from": round(open_from, 3),
                             "to": round(position - STEP, 3),
                             "width_mm": round((position - STEP - open_from + STEP) * 1000)})
                open_from = None
            position += STEP
        if open_from is not None:
            runs.append({"side": label, "from": round(open_from, 3), "to": round(end, 3),
                         "width_mm": round((end - open_from + STEP) * 1000)})
    perimeter[str(storey)] = {
        "outline": [round(v, 3) for v in (x1, x2, y1, y2)],
        "open_runs": len(runs),
        "runs": runs[:20],
    }

bevels = defaultdict(int)
for box in boxes:
    pass
for name in STRUCTURE:
    collection = bpy.data.collections.get(name)
    if collection is None:
        continue
    for obj in collection.all_objects:
        for modifier in obj.modifiers:
            if modifier.type == "BEVEL":
                bevels[round(modifier.width, 4)] += 1

print("AUDIT_JSON_BEGIN")
print(json.dumps({
    "boxes": len(boxes), "walls": len(walls), "slabs": len(slabs),
    "rotated_skipped": len(rotated),
    "counts": {k: len(v) for k, v in sorted(findings.items())},
    "off_module_wall": findings["off_module_wall"][:20],
    "off_module_slab": findings["off_module_slab"][:20],
    "wall_base_off_floor": findings["wall_base_off_floor"][:20],
    "wall_sliver_gap": findings["wall_sliver_gap"][:20],
    "slab_sliver_gap": findings["slab_sliver_gap"][:20],
    "wall_overlaps_wall": sorted(findings["wall_overlaps_wall"],
                                 key=lambda d: -d["volume_m3"])[:6],
    "near_miss_alignment": sorted(findings["near_miss_alignment"],
                                  key=lambda d: -d["offset_mm"])[:30],
    "unstacked_wall": sorted(findings["unstacked_wall"],
                             key=lambda d: -d["length_m"])[:30],
    "dangling_wall_end": findings["dangling_wall_end"][:30],
    "perimeter": perimeter,
    "bevel_widths": {str(k): v for k, v in bevels.items()},
}, ensure_ascii=False, indent=1))
print("AUDIT_JSON_END")
