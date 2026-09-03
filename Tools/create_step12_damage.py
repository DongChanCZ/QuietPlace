"""Step 12 - structural damage, starting with the guard room.

The building is meant to have been attacked and was still perfectly square. This
adds the first layer of the damage vocabulary: broken concrete, exposed rebar,
a cracked ceiling slab and lifted floor tiles.

Deliberately ADDITIVE. Nothing existing is cut or edited, for three reasons:
  * booleaning a wall changes that mesh, and its Unity fileID goes with it,
    taking the collider and static flags that were applied by hand,
  * a real hole in a wall or slab changes where the player can walk and fall,
    which the five play-mode walk checks would then have to be re-authored for,
  * a re-export regenerates everything, so a Unity-side repair would not last.
Damage that reads as a hole is built as geometry sitting in front of, or hanging
below, the intact surface. In a building lit only by a torch this reads the same
and costs nothing.

Nothing here gets a collider in Unity - see the DMG_ / BLOCK_ split in
Docs/Route_And_Build_Parts.md. These pieces must not change navigation.

Guard room, in design metres (the Blender module, 1 unit = 1 m):
    x -10.00 .. -5.00, y 2.00 .. 7.00, floor z 0.00, ceiling underside z 3.00
"""
import bpy
import math
import os


ROOT = r"C:\Users\MBC-501-14\Desktop\LabHorror\Blender"
SOURCE = os.path.join(ROOT, "LabHorror_Step11_B1_Rooms.blend")
TARGET = os.path.join(ROOT, "LabHorror_Step12_Damage.blend")

CEILING = 3.00
FLOOR = 0.00

# Guard room bounds, and the faces damage can sit against.
GX1, GX2 = -10.00, -5.00
GY1, GY2 = 2.00, 7.00
WEST_FACE = GX1 + 0.11        # inner face of EXT_WEST
NORTH_FACE = GY2 - 0.11       # inner face of EXT_NORTH_WEST


def ensure_material(name, color, roughness=0.90, metallic=0.0):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return material


def move_to_collection(obj, collection):
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def add_box(name, location, dimensions, material, rotation=(0, 0, 0), bevel=0.0015):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    obj.rotation_euler = tuple(math.radians(a) for a in rotation)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    move_to_collection(obj, DAMAGE)
    if bevel:
        modifier = obj.modifiers.new("Edge_Soften", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
    return obj


def add_bar(name, location, length, radius, rotation, material):
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=radius, depth=length,
                                        location=location)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = tuple(math.radians(a) for a in rotation)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    move_to_collection(obj, DAMAGE)
    return obj


bpy.ops.wm.open_mainfile(filepath=SOURCE)

old = bpy.data.collections.get("STEP_12_DAMAGE")
if old:
    for obj in list(old.all_objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for child in list(old.children):
        bpy.data.collections.remove(child)
    bpy.data.collections.remove(old)

STEP12 = bpy.data.collections.new("STEP_12_DAMAGE")
bpy.context.scene.collection.children.link(STEP12)
DAMAGE = bpy.data.collections.new("01_DAMAGE_STRUCTURE")
STEP12.children.link(DAMAGE)

BROKEN = ensure_material("DMG_BrokenConcrete", (0.30, 0.29, 0.27))
VOID = ensure_material("DMG_Void", (0.020, 0.020, 0.022), roughness=0.98)
REBAR = ensure_material("DMG_Rebar", (0.24, 0.17, 0.12), roughness=0.62, metallic=0.75)

# ---------------------------------------------------------------- ceiling break
# Directly above the fallen slabs already scattered in Unity, so the debris on
# the floor and the wound in the ceiling are the same event.
BX, BY = -7.35, 4.35

# The dark recess reads as depth. It hangs below the slab rather than being cut
# into it, and it must clear the slab face completely: a void panel that
# straddles the surface it is meant to be a hole in z-fights, and the intact
# surface shows through exactly where the hole should be.
add_box("DMG_1F_Guard_CeilingVoid", (BX, BY, CEILING - 0.035),
        (1.90, 1.55, 0.03), VOID, bevel=0.0)

# Broken slab edges around the recess, tilted so no two read as a straight line.
for name, loc, dim, rot in (
    ("N", (BX, BY + 0.86, CEILING - 0.075), (2.00, 0.32, 0.13), (7, 0, 0)),
    ("S", (BX + 0.10, BY - 0.83, CEILING - 0.070), (1.75, 0.28, 0.12), (-9, 0, 0)),
    ("W", (BX - 1.02, BY - 0.05, CEILING - 0.080), (0.26, 1.50, 0.14), (0, 6, 0)),
    ("E", (BX + 1.00, BY + 0.12, CEILING - 0.065), (0.24, 1.20, 0.11), (0, -8, 0)),
):
    add_box("DMG_1F_Guard_CeilingLip_" + name, loc, dim, BROKEN, rot)

# A slab corner still attached, sagging into the room.
add_box("DMG_1F_Guard_CeilingSlab_Hanging", (BX - 0.42, BY + 0.30, CEILING - 0.30),
        (0.95, 0.80, 0.09), BROKEN, rotation=(22, 14, 0))

# Rebar left spanning the gap once the concrete came away.
for index, (offset, tilt) in enumerate(
        ((-0.55, 4), (-0.18, -3), (0.22, 6), (0.62, -5)), start=1):
    add_bar("DMG_1F_Guard_Rebar_%02d" % index,
            (BX + offset, BY + 0.05, CEILING - 0.09),
            1.62, 0.012, (90, 0, tilt), REBAR)
for index, (offset, tilt) in enumerate(((-0.30, 3), (0.34, -6)), start=5):
    add_bar("DMG_1F_Guard_Rebar_%02d" % index,
            (BX + 0.05, BY + offset, CEILING - 0.055),
            1.95, 0.011, (0, 90, tilt), REBAR)

# ------------------------------------------------------------------ wall spall
# Concrete blown off the west wall's inner face. The wall itself stays whole.
SX, SY, SZ = WEST_FACE + 0.055, 5.85, 1.55
add_box("DMG_1F_Guard_WallVoid", (SX - 0.020, SY, SZ), (0.03, 1.42, 1.16), VOID, bevel=0.0)
for name, loc, dim, rot in (
    ("Top", (SX, SY + 0.05, SZ + 0.60), (0.13, 1.42, 0.22), (0, 0, 5)),
    ("Bottom", (SX, SY - 0.08, SZ - 0.58), (0.15, 1.24, 0.20), (0, 0, -7)),
    ("Left", (SX, SY - 0.70, SZ + 0.05), (0.12, 0.24, 1.05), (0, 4, 0)),
    ("Right", (SX, SY + 0.68, SZ - 0.10), (0.14, 0.22, 0.92), (0, -6, 0)),
):
    add_box("DMG_1F_Guard_WallLip_" + name, loc, dim, BROKEN, rot)
for index, (dz, tilt) in enumerate(((0.34, 6), (-0.02, -4), (-0.38, 8)), start=1):
    add_bar("DMG_1F_Guard_WallRebar_%02d" % index,
            (SX - 0.02, SY, SZ + dz), 1.18, 0.010, (90, 0, tilt), REBAR)

# ----------------------------------------------------------------- floor break
# Tiles lifted where the slab cracked. Kept under 0.10 m so the player steps over
# them - these carry no collider anyway, but a low profile keeps them honest.
for name, loc, dim, rot in (
    ("01", (-7.90, 3.15, 0.045), (0.86, 0.74, 0.07), (0, 9, 12)),
    ("02", (-7.20, 2.85, 0.035), (0.68, 0.60, 0.06), (5, -7, -20)),
    ("03", (-8.35, 2.70, 0.030), (0.52, 0.58, 0.05), (-6, 4, 34)),
    ("04", (-6.75, 3.40, 0.025), (0.44, 0.40, 0.05), (0, 0, -8)),
):
    add_box("DMG_1F_Guard_FloorTile_" + name, loc, dim, BROKEN, rot)
add_box("DMG_1F_Guard_FloorCrackVoid", (-7.65, 3.05, 0.014),
        (1.65, 1.20, 0.016), VOID, rotation=(0, 0, 14), bevel=0.0)

new_meshes = len([o for o in DAMAGE.all_objects if o.type == "MESH"])

scene = bpy.context.scene
scene["BUILD_STAGE"] = "STEP 12 - STRUCTURAL DAMAGE"
scene["STEP12_NEW_MESHES"] = new_meshes

bpy.ops.wm.save_as_mainfile(filepath=TARGET)
print({"blend": TARGET, "new_meshes": new_meshes,
       "rooms_damaged": ["1F guard room"]})
