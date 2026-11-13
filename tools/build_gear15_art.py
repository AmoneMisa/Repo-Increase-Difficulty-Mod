"""Blender 5.2: --background --factory-startup --python tools/build_gear15_art.py -- --root PATH.

Original low-poly models for the 0.10.15 gear (no game assets): Smoke, Cryo and Glue grenades, the Antidote,
Adrenaline Shot and Repair Kit, the Boomerang Sickle (healthy and broken blade) and the Armored C.A.R.T. plating.
Same runtime format and conventions as tools/build_item_art.py: src/Art/<key>.mesh.json (sampled rigid-part
animation, 30 fps), <key>_skin.png label atlases, <key>_inventory.png renders; editable scene in art/gear15/.
Grenades and consumables are authored around their body centre; the sickle around the sword's grip origin
(blade up +Z, which becomes Unity +Y); the cart plating around the cart origin.
Axes: Blender X/right Y/forward Z/up -> Unity X/right Y/up Z/forward (reverse winding).
"""
import argparse, json, math, random, sys
from pathlib import Path
import bpy, bmesh
from mathutils import Matrix, Vector

args = argparse.ArgumentParser(); args.add_argument('--root', type=Path, required=True)
ROOT = args.parse_args(sys.argv[sys.argv.index('--') + 1:]).root.resolve()
OUT = ROOT / 'src' / 'Art'
ART = ROOT / 'art' / 'gear15'; ART.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'; scene.cycles.samples = 20
scene.view_settings.view_transform = 'AgX'; scene.render.image_settings.file_format = 'PNG'; scene.render.fps = 30
scene.world.color = (.18, .18, .18)

PALETTE = {'smoke': (.40, .42, .38), 'cryo': (.42, .62, .74), 'glue': (.66, .34, .52), 'antidote': (.34, .58, .30),
           'adrenaline': (.72, .22, .20), 'repairkit': (.78, .55, .18), 'sickle': (.46, .32, .19), 'armored': (.40, .43, .46)}


def symbol(k, x, y):
    if k == 'smoke':
        return any((x - a) ** 2 + (y - b) ** 2 < r * r for a, b, r in ((-.18, -.05, .2), (.12, .02, .25), (-.02, .2, .18)))
    if k == 'cryo':
        r = math.hypot(x, y)
        return r < .42 and (abs(x) < .05 or abs(y - x * 1.73) < .07 or abs(y + x * 1.73) < .07)
    if k == 'glue':
        return (x * x + (y + .1) ** 2 < .09) or (abs(x) < (.3 - (y + .1)) * .55 and -.1 < y < .42)
    if k in ('antidote', 'repairkit'):
        return (abs(x) < .12 and abs(y) < .4) or (abs(y) < .12 and abs(x) < .4)
    if k == 'adrenaline':
        return (abs(x - (y * .5)) < .09 and 0 < y < .45) or (abs(x + .05 - y * .5) < .09 and -.45 < y < .05) or (abs(y) < .07 and abs(x) < .2)
    return False


def skin(key):
    n = 128; img = bpy.data.images.new(key + '_skin', width=n, height=n, alpha=True); col = PALETTE[key]; pixels = []
    rng = random.Random(key)
    for v in range(n):
        for u in range(n):
            x = (u + .5) / n * 2 - 1; y = (v + .5) / n * 2 - 1
            c = tuple(max(0, min(1, a * .75 + rng.uniform(-.02, .02))) for a in col)
            if u < 5 or u > 122 or v < 5 or v > 122: c = (.07, .08, .075)
            if symbol(key, x * 1.15, y * 1.15): c = (.88, .89, .78)
            if (u * 71 + v * 139) % 1543 < 4: c = (.22, .21, .17)
            pixels.extend((*c, 1))
    img.pixels.foreach_set(pixels); img.filepath_raw = str(OUT / (img.name + '.png')); img.file_format = 'PNG'; img.save(); img.pack()
    return img


wear = bpy.data.images.load(str(OUT / 'workshop_wear.png'))


def material(name, color, metal=0.0, image=None):
    m = bpy.data.materials.new(name); m.diffuse_color = (*color, 1); m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF'); p.inputs['Metallic'].default_value = metal; p.inputs['Roughness'].default_value = .66
    t = m.node_tree.nodes.new('ShaderNodeTexImage'); t.image = image or wear; t.interpolation = 'Closest'
    if image:
        m.node_tree.links.new(t.outputs['Color'], p.inputs['Base Color'])
    else:
        mix = m.node_tree.nodes.new('ShaderNodeMixRGB'); mix.blend_type = 'MULTIPLY'; mix.inputs[0].default_value = 1
        mix.inputs[1].default_value = (*(c ** 2.2 for c in color), 1)
        m.node_tree.links.new(t.outputs['Color'], mix.inputs[2]); m.node_tree.links.new(mix.outputs[0], p.inputs['Base Color'])
    return m


dark = material('Graphite rubber', (.055, .068, .066)); steel = material('Worn steel', (.30, .33, .32), .55)
bright = material('Polished edge', (.78, .80, .82), .3); bone = material('Bone labels', (.76, .77, .64))
black = material('Inset void', (.012, .02, .018)); glass = material('Clear barrel', (.78, .86, .9))
green_liquid = material('Antidote liquid', (.45, .85, .35))
mats = {k: material(k, c, .15) for k, c in PALETTE.items()}
skins = {}
parts = []


def finish(o, name, mat):
    o.name = name; o.data.materials.append(mat); parts.append(o); return o


def mesh_object(name, verts, faces, mat):
    mesh = bpy.data.meshes.new(name); mesh.from_pydata(verts, [], faces); mesh.update()
    bm = bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
    o = bpy.data.objects.new(name, mesh); scene.collection.objects.link(o)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.smart_project(island_margin=.02); bpy.ops.object.mode_set(mode='OBJECT')
    return finish(o, name, mat)


def box(name, loc, size, mat, bevel=.004):
    # Soft, rounded manufactured shapes: every box gets a generous multi-segment bevel (about a third of its
    # thinnest side), so nothing reads as a plain cube.
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc); o = bpy.context.object; o.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    thin = min(size)
    mod = o.modifiers.new('Bevel', 'BEVEL'); mod.width = min(max(bevel, thin * .32), thin * .45); mod.segments = 5
    mod.limit_method = 'NONE'; bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(o, name, mat)


def cylinder(name, loc, r, depth, mat, axis='Z', verts=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc); o = bpy.context.object
    if axis == 'Y': o.rotation_euler.x = math.pi / 2
    if axis == 'X': o.rotation_euler.y = math.pi / 2
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    mod = o.modifiers.new('Bevel', 'BEVEL'); mod.width = min(r * .3, depth * .4); mod.segments = 4; bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(o, name, mat)


def torus(name, loc, major, minor, mat, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=28, minor_segments=10, location=loc, rotation=rot)
    o = bpy.context.object; bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish(o, name, mat)


def shell(name, rings, mat, sides=32):
    """Elliptical sections along Z: (z, half-width, half-depth)."""
    verts = [(w * math.cos(j * 2 * math.pi / sides), d * math.sin(j * 2 * math.pi / sides), z) for z, w, d in rings for j in range(sides)]
    faces = []
    for i in range(len(rings) - 1):
        for j in range(sides):
            a = i * sides + j; b = i * sides + (j + 1) % sides; faces.append((a, b, b + sides, a + sides))
    faces.extend([tuple(reversed(range(sides))), tuple(range((len(rings) - 1) * sides, len(rings) * sides))])
    return mesh_object(name, verts, faces, mat)


def label(key, loc, size, facing='Y'):
    # A round badge rather than a square plate (circle UVs fill the 0-1 square, so the emblem stays centred).
    bpy.ops.mesh.primitive_circle_add(vertices=32, radius=.5, fill_type='NGON', calc_uvs=True, location=loc); o = bpy.context.object
    o.rotation_euler = (math.pi / 2, 0, 0 if facing == 'Y' else math.pi / 2); o.scale = (size, size, 1)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish(o, 'Specification plate', skins[key])


def keys(o, frames):
    """frames: (frame, offset, euler offset, scale)."""
    base = o.location.copy(); rot = o.rotation_euler.copy()
    for frame, offset, euler, scale in frames:
        o.location = base + Vector(offset); o.rotation_euler = (rot.x + euler[0], rot.y + euler[1], rot.z + euler[2]); o.scale = scale
        for channel in ('location', 'rotation_euler', 'scale'): o.keyframe_insert(data_path=channel, frame=frame)
    scene.frame_set(1)


ONE = (1, 1, 1)


def pivot(o, point):
    """Moves the object's origin to point (world) so it can swing about a hinge."""
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    scene.cursor.location = point; bpy.ops.object.origin_set(type='ORIGIN_CURSOR'); scene.cursor.location = (0, 0, 0)


# ------------------------------------------------------------------ grenades (body centre origin)

def smoke():
    shell('Smoke canister', [(-.13, .062, .062), (-.12, .072, .072), (.09, .072, .072), (.11, .066, .066), (.13, .05, .05)], mats['smoke'])
    for z in (-.10, .06): cylinder('Canister band', (0, 0, z), .076, .018, dark)
    for i in range(8):
        a = i * math.pi / 4; cylinder('Vent hole', (.055 * math.cos(a), .055 * math.sin(a), .118), .011, .03, black, verts=8)
    cap = cylinder('Fuse head', (0, 0, .15), .035, .04, steel)
    lever = box('Spoon lever', (.045, 0, .085), (.016, .03, .17), steel, .003); pivot(lever, (.03, 0, .165))
    ring = torus('Pull ring', (-.05, 0, .165), .026, .005, steel, (math.pi / 2, 0, 0))
    label('smoke', (0, -.073, -.02), .085)
    keys(lever, [(1, (0, 0, 0), (0, 0, 0), ONE), (4, (.02, 0, .01), (0, .9, 0), ONE), (8, (.06, 0, .02), (0, 1.6, 0), (.001,) * 3), (30, (.06, 0, .02), (0, 1.6, 0), (.001,) * 3)])
    keys(ring, [(1, (0, 0, 0), (0, 0, 0), ONE), (3, (-.04, 0, .02), (0, 0, 0), ONE), (5, (-.06, 0, .03), (0, 0, 0), (.001,) * 3), (30, (-.06, 0, .03), (0, 0, 0), (.001,) * 3)])
    keys(cap, [(1, (0, 0, 0), (0, 0, 0), ONE), (8, (0, 0, .012), (0, 0, 0), ONE), (14, (0, 0, .018), (0, 0, 1.2), ONE), (30, (0, 0, .018), (0, 0, 3.0), ONE)])


def cryo():
    shell('Cryo capsule', [(-.13, .03, .03), (-.11, .058, .058), (-.06, .07, .07), (.06, .07, .07), (.11, .058, .058), (.13, .03, .03)], mats['cryo'])
    core = cylinder('Frost core window', (0, 0, 0), .074, .07, glass, verts=24)
    core.data.materials[0] = bone
    for i in range(4):
        a = i * math.pi / 2 + math.pi / 4
        fin = box('Cooling fin', (.07 * math.cos(a), .07 * math.sin(a), .07), (.016, .05, .09), steel, .006)
        fin.rotation_euler.z = a + math.pi / 2; pivot(fin, (.06 * math.cos(a), .06 * math.sin(a), .115))
        keys(fin, [(1, (0, 0, 0), (0, 0, 0), ONE), (6, (.012 * math.cos(a), .012 * math.sin(a), 0), (.5 * math.cos(a + math.pi / 2), .5 * math.sin(a + math.pi / 2), 0), ONE),
                   (30, (.012 * math.cos(a), .012 * math.sin(a), 0), (.5 * math.cos(a + math.pi / 2), .5 * math.sin(a + math.pi / 2), 0), ONE)])
    for z in (-.04, .04): cylinder('Core collar', (0, 0, z), .076, .012, dark)
    cylinder('Arming cap', (0, 0, .14), .028, .03, steel)
    label('cryo', (0, -.071, -.08), .06)
    keys(core, [(1, (0, 0, 0), (0, 0, 0), ONE), (8, (0, 0, 0), (0, 0, 0), (1.08, 1.08, 1)), (16, (0, 0, 0), (0, 0, 0), (1, 1, 1)), (24, (0, 0, 0), (0, 0, 0), (1.08, 1.08, 1)), (30, (0, 0, 0), (0, 0, 0), ONE)])


def glue():
    shell('Glue drum', [(-.10, .07, .07), (-.09, .088, .088), (.06, .088, .088), (.08, .07, .07)], mats['glue'])
    for z in (-.075, .045): cylinder('Drum hoop', (0, 0, z), .092, .016, steel)
    nozzle = cylinder('Nozzle', (0, 0, .105), .032, .05, dark)
    cap = cylinder('Nozzle cap', (0, 0, .137), .036, .016, mats['glue'])
    valve = cylinder('Pressure valve', (.05, 0, .095), .014, .04, steel, verts=10)
    gauge = cylinder('Pressure gauge', (0, -.09, .0), .026, .012, bone, 'Y')
    label('glue', (0, -.09, -.045), .05)
    keys(cap, [(1, (0, 0, 0), (0, 0, 0), ONE), (4, (0, 0, .03), (.6, 0, 0), ONE), (8, (0, .04, .05), (1.4, 0, 0), (.001,) * 3), (30, (0, .04, .05), (1.4, 0, 0), (.001,) * 3)])
    keys(valve, [(1, (0, 0, 0), (0, 0, 0), ONE), (6, (0, 0, .025), (0, 0, 0), ONE), (12, (0, 0, .01), (0, 0, 0), ONE), (18, (0, 0, .025), (0, 0, 0), ONE), (30, (0, 0, .02), (0, 0, 0), ONE)])
    keys(nozzle, [(1, (0, 0, 0), (0, 0, 0), ONE), (8, (0, 0, .008), (0, 0, 0), (1.1, 1.1, 1)), (30, (0, 0, .008), (0, 0, 0), (1.1, 1.1, 1))])
    _ = gauge


# ------------------------------------------------------------------ consumables (body centre origin, used = animation)

def antidote():
    shell('Injector body', [(-.11, .02, .02), (-.10, .034, .034), (.07, .034, .034), (.09, .028, .028)], mats['antidote'])
    window = box('Antidote window', (0, -.031, -.01), (.03, .012, .1), green_liquid, .002)
    cap = cylinder('Safety cap', (0, 0, .11), .03, .05, mats['antidote'])
    cylinder('Grip ring', (0, 0, .055), .037, .014, dark)
    tip = cylinder('Needle guard', (0, 0, -.12), .016, .03, steel, verts=12)
    label('antidote', (.035, 0, .01), .05, 'X')
    keys(cap, [(1, (0, 0, 0), (0, 0, 0), ONE), (5, (0, 0, .03), (0, 0, 0), ONE), (9, (0, 0, .04), (.8, 0, 0), (.001,) * 3), (30, (0, 0, .04), (.8, 0, 0), (.001,) * 3)])
    keys(window, [(1, (0, 0, 0), (0, 0, 0), ONE), (10, (0, 0, 0), (0, 0, 0), ONE), (24, (0, 0, -.045), (0, 0, 0), (1, 1, .1)), (30, (0, 0, -.045), (0, 0, 0), (1, 1, .1))])
    keys(tip, [(1, (0, 0, 0), (0, 0, 0), ONE), (10, (0, 0, 0), (0, 0, 0), ONE), (12, (0, 0, -.015), (0, 0, 0), ONE), (30, (0, 0, -.015), (0, 0, 0), ONE)])


def adrenaline():
    shell('Syringe barrel', [(-.10, .022, .022), (-.09, .03, .03), (.07, .03, .03), (.075, .045, .03), (.085, .045, .03)], glass)
    liquid = cylinder('Adrenaline dose', (0, 0, -.02), .024, .12, mats['adrenaline'])
    plunger = cylinder('Plunger rod', (0, 0, .13), .008, .1, steel, verts=8)
    thumb = cylinder('Thumb press', (0, 0, .18), .026, .012, mats['adrenaline'])
    needle = cylinder('Needle', (0, 0, -.13), .003, .06, bright, verts=6)
    cylinder('Needle hub', (0, 0, -.1), .012, .02, steel, verts=10)
    label('adrenaline', (.031, 0, -.03), .045, 'X')
    for o, depth in ((plunger, -.08), (thumb, -.08)):
        keys(o, [(1, (0, 0, 0), (0, 0, 0), ONE), (6, (0, 0, 0), (0, 0, 0), ONE), (18, (0, 0, depth), (0, 0, 0), ONE), (30, (0, 0, depth), (0, 0, 0), ONE)])
    keys(liquid, [(1, (0, 0, 0), (0, 0, 0), ONE), (6, (0, 0, 0), (0, 0, 0), ONE), (18, (0, 0, -.055), (0, 0, 0), (1, 1, .08)), (30, (0, 0, -.055), (0, 0, 0), (1, 1, .08))])
    keys(needle, [(1, (0, 0, 0), (0, 0, 0), ONE), (4, (0, 0, -.01), (0, 0, 0), ONE), (30, (0, 0, -.01), (0, 0, 0), ONE)])


def repairkit():
    case = shell('Kit case', [(-.075, .085, .05), (-.07, .1, .06), (.0, .1, .06), (.015, .098, .058)], mats['repairkit'])
    lid = shell('Kit lid', [(.015, .098, .058), (.03, .1, .06), (.045, .094, .055), (.052, .075, .042)], mats['repairkit']); pivot(lid, (0, .06, .03))
    _ = case
    box('Latch', (0, -.062, .01), (.04, .008, .03), steel, .002)
    handle = torus('Carry handle', (0, 0, .055), .035, .008, dark, (math.pi / 2, 0, 0))
    wrench = box('Wrench', (-.03, 0, .002), (.13, .022, .01), steel, .002); wrench.rotation_euler.z = .3
    gear_ = cylinder('Spare gear', (.05, -.01, .004), .024, .012, bright, verts=10)
    label('repairkit', (0, -.063, -.03), .05)
    keys(lid, [(1, (0, 0, 0), (0, 0, 0), ONE), (7, (0, 0, 0), (-1.1, 0, 0), ONE), (20, (0, 0, 0), (-1.1, 0, 0), ONE), (28, (0, 0, 0), (0, 0, 0), ONE), (30, (0, 0, 0), (0, 0, 0), ONE)])
    keys(handle, [(1, (0, 0, 0), (0, 0, 0), ONE), (7, (0, .05, .02), (-1.1, 0, 0), ONE), (20, (0, .05, .02), (-1.1, 0, 0), ONE), (28, (0, 0, 0), (0, 0, 0), ONE), (30, (0, 0, 0), (0, 0, 0), ONE)])
    keys(wrench, [(1, (0, 0, 0), (0, 0, 0), ONE), (8, (0, 0, .04), (0, 0, 1.0), ONE), (14, (0, 0, .045), (0, 0, 2.6), ONE), (20, (0, 0, .04), (0, 0, 4.0), ONE), (27, (0, 0, 0), (0, 0, 6.28), ONE), (30, (0, 0, 0), (0, 0, 6.28), ONE)])
    keys(gear_, [(1, (0, 0, 0), (0, 0, 0), ONE), (20, (0, 0, 0), (0, 0, 3.14), ONE), (30, (0, 0, 0), (0, 0, 3.14), ONE)])


# ------------------------------------------------------------------ sickle (grip origin, blade up +Z, swept to -X)

def sickle_blade(broken):
    """Flat crescent ribbon: points along an arc, inner and outer edge, thin cross-section."""
    arc = []
    steps = 9 if broken else 22
    end = .45 if broken else 1.0
    for i in range(steps + 1):
        t = i / steps * end
        ang = math.radians(-10 + 200 * t)
        cx, cz, r = -.16, .10, .175
        x = cx + r * math.cos(ang); z = cz + r * math.sin(ang)
        width = .055 * (1 - t) ** .6 + .004
        arc.append((x, z, ang, width))
    verts = []; faces = []
    for x, z, ang, w in arc:
        nx, nz = math.cos(ang), math.sin(ang)
        for off, y in ((0, .006), (w, .0015), (0, -.006)):
            verts.append((x - nx * off, y, z - nz * off))
    for i in range(len(arc) - 1):
        a, b = i * 3, (i + 1) * 3
        for j in range(2):
            faces.append((a + j, b + j, b + j + 1, a + j + 1))
        faces.append((a + 2, b + 2, b, a))
    faces.append((0, 1, 2)); n = len(arc) - 1; faces.append((n * 3 + 2, n * 3 + 1, n * 3))
    return mesh_object('Crescent blade', verts, faces, bright if not broken else steel)


def sickle(broken=False):
    shell('Wooden grip', [(-.36, .018, .018), (-.34, .024, .024), (-.12, .022, .022), (-.08, .026, .026)], mats['sickle'], 12)
    for z in (-.30, -.22, -.15): cylinder('Grip wrap', (0, 0, z), .027, .02, dark, verts=12)
    cylinder('Ferrule', (0, 0, -.06), .028, .05, steel, verts=12)
    box('Blade tang', (.012, 0, .03), (.036, .016, .16), steel, .003)
    sickle_blade(broken)
    torus('Throwing loop', (0, 0, -.385), .022, .005, steel, (math.pi / 2, 0, 0))


# ------------------------------------------------------------------ armored cart plating (cart origin)

def armored():
    # Bolt-on plates over the big C.A.R.T.'s walls (about 1.0 x 1.55 x .55 m tub). Thin, so the native colliders rule.
    for side in (-1, 1):
        box('Side plate', (side * .635, 0, .0), (.035, 1.62, .38), mats['armored'], .015)
        for y in (-.6, -.2, .2, .6):
            for z in (-.13, .13): cylinder('Plate rivet', (side * .655, y, z), .016, .02, steel, 'X', 16)
    box('Front plate', (0, 1.0, .0), (1.18, .035, .38), mats['armored'], .015)
    for x in (-.4, 0, .4):
        for z in (-.13, .13): cylinder('Plate rivet', (x, 1.02, z), .016, .02, steel, 'Y', 16)
    for side in (-1, 1): cylinder('Corner bumper', (side * .62, .98, 0), .045, .40, dark, 'Z', 24)


C = Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))


def vec(v): return {'x': round(v[0], 6), 'y': round(v[1], 6), 'z': round(v[2], 6)}


def pose(o):
    p, q, s = o.matrix_local.decompose(); q = (C @ q.to_matrix() @ C).to_quaternion()
    return {'position': vec(C @ p), 'rotation': {'x': q.x, 'y': q.y, 'z': q.z, 'w': q.w}, 'scale': vec(C @ s)}


def export(key, label_key, frames):
    data = {'version': 1, 'fps': 30, 'frames': frames, 'parts': []}
    for o in parts:
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
        if not o.name.startswith('Specification plate'):
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(50), keep_sharp_edges=False)
        if not o.data.uv_layers:
            bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.smart_project(island_margin=.02); bpy.ops.object.mode_set(mode='OBJECT')
        mesh = o.data; mesh.calc_loop_triangles(); vertices = []; normals = []; uv = []; indices = []
        for tri in mesh.loop_triangles:
            for li in reversed(tri.loops):
                loop = mesh.loops[li]; vertices.append(vec(C @ mesh.vertices[loop.vertex_index].co))
                normals.append(vec(C @ mesh.corner_normals[li].vector)); p = mesh.uv_layers.active.data[li].uv
                uv.append({'x': float(p[0]), 'y': float(p[1])}); indices.append(len(indices))
        mat = mesh.materials[0]
        texture = label_key + '_skin.png' if label_key and mat == skins.get(label_key) else 'workshop_wear.png'
        col = mat.diffuse_color
        record = {'name': o.name, 'vertices': vertices, 'normals': normals, 'uv': uv, 'triangles': indices,
                  'color': {'r': col[0], 'g': col[1], 'b': col[2], 'a': 1}, 'texture': texture, 'rest': pose(o), 'samples': []}
        if o.animation_data:
            for frame in range(1, frames + 1): scene.frame_set(frame); record['samples'].append(pose(o))
        data['parts'].append(record); scene.frame_set(1)
    (OUT / (key + '.mesh.json')).write_text(json.dumps(data, separators=(',', ':')), encoding='utf8')


def area(name, loc, power, size):
    bpy.ops.object.light_add(type='AREA', location=loc); o = bpy.context.object; o.name = name
    o.data.energy = power; o.data.shape = 'DISK'; o.data.size = size; o.rotation_euler = (-o.location).to_track_quat('-Z', 'Y').to_euler()


area('Softbox', (1, -2, 4), 420, 4); area('Fill', (-3, 0, 2), 270, 3); area('Edge', (0, 3, 3), 420, 2)
bpy.ops.object.camera_add(location=(1, -1.5, 1)); camera = bpy.context.object; camera.data.type = 'ORTHO'; scene.camera = camera
scene.render.film_transparent = True

BUILDS = [('smoke', smoke, 'smoke', .42), ('cryo', cryo, 'cryo', .42), ('glue', glue, 'glue', .42),
          ('antidote', antidote, 'antidote', .38), ('adrenaline', adrenaline, 'adrenaline', .42), ('repairkit', repairkit, 'repairkit', .34),
          ('sickle', lambda: sickle(False), None, 1.0), ('sickle_broken', lambda: sickle(True), None, 1.0), ('armored', armored, None, 2.4)]
for k in ('smoke', 'cryo', 'glue', 'antidote', 'adrenaline', 'repairkit'):
    skins[k] = material(k + '_label', (1, 1, 1), 0, skin(k))

lineup = []
for i, (key, builder, label_key, ortho) in enumerate(BUILDS):
    parts = []; builder(); scene.frame_set(1)
    export(key, label_key, 30)
    group = bpy.data.objects.new(key.upper() + ' / animation 1-30', None); scene.collection.objects.link(group)
    for o in parts: o.parent = group
    group.location = (i * 3.0, 0, 0)
    lineup.append((key, group, list(parts), ortho))

# Inventory renders (transparent, 256 px), one model at a time.
scene.render.resolution_x = 256; scene.render.resolution_y = 256
meshes = [o for o in scene.objects if o.type == 'MESH']
for key, group, objects, ortho in lineup:
    if key in ('sickle_broken', 'armored'): continue
    for o in meshes: o.hide_render = o not in objects
    center = group.location + Vector((0, 0, 0 if key != 'sickle' else .0))
    camera.location = center + Vector((1, -1.5, .8)); camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.ortho_scale = ortho
    scene.render.filepath = str(OUT / (key + '_inventory.png')); bpy.ops.render.render(write_still=True)

# Review lineup render and the editable scene.
for o in meshes: o.hide_render = False
scene.render.resolution_x = 2400; scene.render.resolution_y = 700; scene.render.film_transparent = False
camera.location = Vector((12, -14, 6)); camera.rotation_euler = (Vector((12, 0, .1)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
camera.data.ortho_scale = 28
scene.render.filepath = str(ART / 'gear15_lineup.png'); bpy.ops.render.render(write_still=True)
scene.frame_end = 30; scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(ART / 'gear15.blend'))
print('IHM_GEAR15_ART_OK', len(BUILDS), 'models')
