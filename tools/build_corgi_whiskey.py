"""Blender 5.2: --background --factory-startup --python tools/build_corgi_whiskey.py -- --root PATH [--preview]

Original models for two collection valuables (no game or third-party assets):
  * plush_corgi    - soft plush toy corgi (metaball fur, white chest/muzzle/paws, felt nose and eyes).
  * whiskey_bottle - big square-shouldered whiskey bottle with an invented "OLD TRUCK No. 9" label.
    The liquid inside is generated and animated at runtime (WhiskeyLiquid.cs), so only the glass, cap and
    labels are exported here; the interior box the liquid fills is written to the model as 'liquid' info.

Writes src/Art/v11_<key>.mesh.json (ValuableVisuals format) and, with --preview, a Cycles preview to
art/valuables/corgi-whiskey-16/<key>.png. Axes: Blender X/right Y/forward Z/up -> Unity X/right Y/up Z/forward.
Sizes follow the game's scale: the bottle matches Swiftbroom's big potion bottles (about 1.4 m tall).
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Quaternion, Vector

parser = argparse.ArgumentParser(); parser.add_argument('--root', type=Path, required=True); parser.add_argument('--preview', action='store_true')
ARGS = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
ROOT = ARGS.root.resolve()
SRC = ROOT / 'src' / 'Art'
ART = ROOT / 'art' / 'valuables' / 'corgi-whiskey-16'; ART.mkdir(parents=True, exist_ok=True)

# Interior volume the runtime liquid fills (Unity local space of the visual, before layout offset).
BOTTLE_HALF = .27
LIQUID = {'halfWidth': .238, 'bottom': .035, 'top': .80, 'fill': .78}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 64
    scene.view_settings.view_transform = 'AgX'


def material(name, color, rough=.6, metal=0., trans=0., emit=0., image=None):
    m = bpy.data.materials.new(name); m.use_nodes = True
    p = m.node_tree.nodes['Principled BSDF']
    p.inputs['Base Color'].default_value = (*color, 1); p.inputs['Roughness'].default_value = rough
    p.inputs['Metallic'].default_value = metal; p.inputs['Transmission Weight'].default_value = trans
    p.inputs['IOR'].default_value = 1.5
    p.inputs['Emission Color'].default_value = (*color, 1); p.inputs['Emission Strength'].default_value = emit
    if image:
        tex = m.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(str(SRC / image)); tex.interpolation = 'Linear'
        m.node_tree.links.new(tex.outputs['Color'], p.inputs['Base Color'])
        m['texture'] = image
    return m


def assign(ob, mat, group='static'):
    ob.data.materials.clear(); ob.data.materials.append(mat); ob['group'] = group; return ob


def from_data(name, verts, faces, mat, uvs=None, smooth=True, group='static'):
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update()
    if uvs:
        layer = me.uv_layers.new(name='UVMap')
        for poly in me.polygons:
            for li in poly.loop_indices:
                layer.data[li].uv = uvs[me.loops[li].vertex_index]
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    for poly in me.polygons: poly.use_smooth = smooth
    return assign(ob, mat, group)


def sphere(name, loc, scale, mat, rot=(0, 0, 0), segs=20, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=rings, location=loc, rotation=rot)
    ob = bpy.context.object; ob.name = name; ob.scale = scale; bpy.ops.object.shade_smooth()
    return assign(ob, mat)


def apply_transforms(ob):
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)


# ------------------------------------------------------------------ corgi

def corgi():
    fur = material('Corgi fur', (.55, .17, .03), .95)
    white = material('Corgi cream', (.85, .78, .66), .97)
    pink = material('Corgi felt pink', (.72, .22, .26), .9)
    black = material('Corgi felt black', (.015, .013, .012), .45)
    tilt = math.radians(18)

    def blobs(name, mat, elements, resolution=.009):
        mb = bpy.data.metaballs.new(name); mb.resolution = mb.render_resolution = resolution; mb.threshold = .6
        ob = bpy.data.objects.new(name, mb); bpy.context.collection.objects.link(ob)
        for co, radius, size, rot in elements:
            e = mb.elements.new(); e.type = 'ELLIPSOID'; e.co = co; e.radius = radius; e.stiffness = 2
            e.size_x, e.size_y, e.size_z = size
            if rot: e.rotation = Quaternion((0, 1, 0), rot)
        bpy.context.view_layer.update()
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
        bpy.ops.object.convert(target='MESH')
        mesh_ob = bpy.context.object; mesh_ob.name = name
        dec = mesh_ob.modifiers.new('Plush decimate', 'DECIMATE'); dec.ratio = .35
        bpy.ops.object.modifier_apply(modifier=dec.name)
        bpy.ops.object.shade_smooth()
        return assign(mesh_ob, mat)

    def E(center, half, rot=0):
        # Element from the visible half extents (metaball surfaces sit at ~0.76 of the influence size).
        radius = max(half) / .76
        return (center, radius, tuple(h / (radius * .76) for h in half), rot)

    fur_parts = [
        E((0, -.01, .2), (.16, .26, .14)),                       # long loaf body
        E((0, -.2, .21), (.15, .13, .13)),                       # round rump
        E((0, .2, .29), (.11, .09, .1)),                         # neck
        E((0, .29, .36), (.13, .12, .115)),                      # head
        E((.07, .33, .325), (.07, .07, .06)), E((-.07, .33, .325), (.07, .07, .06)),        # cheeks
        E((.085, .27, .49), (.065, .024, .065), tilt), E((-.085, .27, .49), (.065, .024, .065), -tilt),  # ear bases
        E((.11, .27, .575), (.032, .017, .05), tilt), E((-.11, .27, .575), (.032, .017, .05), -tilt),    # ear tips
        E((.1, .17, .07), (.055, .055, .07)), E((-.1, .17, .07), (.055, .055, .07)),         # stubby front legs
        E((.1, -.2, .07), (.058, .058, .07)), E((-.1, -.2, .07), (.058, .058, .07)),         # back legs
        E((0, -.35, .25), (.05, .05, .05)),                      # tail nub
    ]
    white_parts = [
        E((0, .22, .22), (.11, .075, .12)),                      # chest bib
        E((0, -.02, .1), (.12, .22, .06)),                       # belly
        E((0, .40, .31), (.07, .08, .055)),                      # tapered muzzle
        E((0, .37, .445), (.028, .06, .055)),                    # forehead blaze
        E((.1, .185, .028), (.05, .06, .03)), E((-.1, .185, .028), (.05, .06, .03)),
        E((.1, -.19, .028), (.052, .06, .03)), E((-.1, -.19, .028), (.052, .06, .03)),
        E((0, -.39, .26), (.03, .03, .03)),                      # tail tip
    ]
    parts = [blobs('Corgi fur', fur, fur_parts), blobs('Corgi cream patches', white, white_parts)]
    # Felt details: small solid shapes (too thin for metaballs).
    for side in (1, -1):
        ear = sphere('Corgi inner ear', (side * .092, .292, .515), (.038, .006, .07), pink, rot=(0, side * tilt, 0), segs=16, rings=10)
        parts.append(ear)
        parts.append(sphere('Corgi eye', (side * .06, .397, .39), (.02, .012, .022), black, segs=16, rings=10))
    parts.append(sphere('Corgi nose', (0, .476, .33), (.028, .02, .02), black, segs=16, rings=10))
    parts.append(sphere('Corgi tongue', (0, .452, .268), (.026, .016, .011), pink, segs=16, rings=10))
    for ob in parts: apply_transforms(ob)
    return parts


# ------------------------------------------------------------------ whiskey bottle

def superellipse(hw, p, n=48):
    pts = []
    for j in range(n):
        a = 2 * math.pi * j / n
        c, s = math.cos(a), math.sin(a)
        pts.append((hw * math.copysign(abs(c) ** (2 / p), c), hw * math.copysign(abs(s) ** (2 / p), s)))
    return pts


def loft(name, rings, mat, n=48, close_bottom=True):
    verts = []; faces = []
    for z, hw, p in rings:
        verts.extend((x, y, z) for x, y in superellipse(hw, p, n))
    for k in range(len(rings) - 1):
        for j in range(n):
            a = k * n + j; b = k * n + (j + 1) % n
            faces.append((a, b, b + n, a + n))
    if close_bottom:
        c = len(verts); verts.append((0, 0, rings[0][0]))
        for j in range(n): faces.append((c, (j + 1) % n, j))
    ob = from_data(name, verts, faces, mat, smooth=True)
    return ob


def face_y(x, hw, p):
    """Front face of the rounded-square body (Blender -Y) at horizontal position x."""
    u = min(.999, abs(x) / hw)
    return -hw * (1 - u ** p) ** (1 / p)


def whiskey():
    glass = material('Whiskey glass', (.80, .78, .70), .04, trans=.95)
    cap = material('Whiskey cap', (.018, .016, .015), .5)
    label = material('Whiskey label', (1, 1, 1), .7, image='v11_whiskey_label.png')
    neck = material('Whiskey neck band', (1, 1, 1), .7, image='v11_whiskey_neck.png')
    hw = BOTTLE_HALF
    rings = [(0, .232, 5), (.012, .258, 6), (.035, hw, 6), (.80, hw, 6), (.86, .258, 5), (.925, .205, 3.6),
             (.985, .135, 2.5), (1.035, .088, 2), (1.10, .077, 2), (1.235, .074, 2), (1.25, .083, 2), (1.265, .078, 2)]
    parts = [loft('Whiskey glass', rings, glass)]
    # Ribbed screw cap.
    n = 40; verts = []; faces = []
    for k, z in enumerate((1.225, 1.235, 1.405, 1.42)):
        for j in range(n):
            a = 2 * math.pi * j / n; r = (.088 if k in (1, 2) else .084) + (.0035 if j % 2 and k in (1, 2) else 0)
            verts.append((r * math.cos(a), r * math.sin(a), z))
    for k in range(3):
        for j in range(n):
            a = k * n + j; b = k * n + (j + 1) % n; faces.append((a, b, b + n, a + n))
    top = len(verts); verts.append((0, 0, 1.426))
    for j in range(n): faces.append((top, 3 * n + j, 3 * n + (j + 1) % n))
    bottom = len(verts); verts.append((0, 0, 1.225))
    for j in range(n): faces.append((bottom, (j + 1) % n, j))
    parts.append(from_data('Whiskey cap', verts, faces, cap, smooth=False))
    # Front label conforming to the flat-ish face, plus a matching back label.
    for side, (z0, z1, half) in ((-1, (.22, .69, .21)), (1, (.30, .60, .15))):
        cols, rows = 16, 2; verts = []; uvs = []; faces = []
        for r in range(rows + 1):
            for c in range(cols + 1):
                x = -half + 2 * half * c / cols; z = z0 + (z1 - z0) * r / rows
                y = (face_y(x, hw, 6) - .0025) * (1 if side < 0 else -1)
                verts.append((x * (1 if side < 0 else -1), y, z)); uvs.append((c / cols, r / rows))
        for r in range(rows):
            for c in range(cols):
                a = r * (cols + 1) + c; faces.append((a, a + 1, a + cols + 2, a + cols + 1))
        parts.append(from_data('Whiskey label' + (' back' if side > 0 else ''), verts, faces, label, uvs, smooth=True))
    # Neck band wrapping the neck (texture repeats twice around).
    n = 40; verts = []; uvs = []; faces = []
    for k, z in enumerate((1.045, 1.105)):
        for j in range(n + 1):
            a = -math.pi / 2 + 2 * math.pi * j / n; r = .0835
            verts.append((r * math.cos(a), r * math.sin(a), z)); uvs.append((2 * j / n, k))
    for j in range(n): faces.append((j, j + 1, j + n + 2, j + n + 1))
    parts.append(from_data('Whiskey neck band', verts, faces, neck, uvs, smooth=True))
    return parts


# ------------------------------------------------------------------ export

C = Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))


def vec(v): return {'x': round(float(v.x), 6), 'y': round(float(v.y), 6), 'z': round(float(v.z), 6)}


def export(key, parts, extra=None):
    mats = []; index = {}; records = []
    lo = Vector((1e9, 1e9, 1e9)); hi = -lo
    for ob in parts:
        me = ob.data
        if any(len(p.vertices) > 3 for p in me.polygons):
            bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free(); me.update()
        pts = [ob.matrix_world @ v.co for v in me.vertices]
        a = Vector(tuple(min(p[i] for p in pts) for i in range(3))); b = Vector(tuple(max(p[i] for p in pts) for i in range(3)))
        lo = Vector(tuple(min(lo[i], a[i]) for i in range(3))); hi = Vector(tuple(max(hi[i], b[i]) for i in range(3)))
        center = (a + b) / 2
        m = me.materials[0]
        if m.name not in index:
            p = m.node_tree.nodes['Principled BSDF']; base = p.inputs['Base Color'].default_value
            index[m.name] = len(mats)
            mats.append({'name': m.name, 'texture': m.get('texture', ''), 'color': {'r': base[0], 'g': base[1], 'b': base[2], 'a': 1},
                         'transmission': p.inputs['Transmission Weight'].default_value, 'emission': p.inputs['Emission Strength'].default_value,
                         'roughness': p.inputs['Roughness'].default_value, 'metallic': p.inputs['Metallic'].default_value})
        uv_layer = me.uv_layers.active
        verts = []; normals = []; uv = []; tri = []
        for poly in me.polygons:
            start = len(verts)
            for li in poly.loop_indices:
                vi = me.loops[li].vertex_index
                verts.append(vec(C @ (ob.matrix_world @ me.vertices[vi].co - center)))
                n = me.corner_normals[li].vector if poly.use_smooth else poly.normal
                normals.append(vec((C @ n).normalized()))
                u = uv_layer.data[li].uv if uv_layer else (0, 0)
                uv.append({'x': round(float(u[0]), 6), 'y': round(float(u[1]), 6)})
            tri.extend([start, start + 2, start + 1])
        mi = index[m.name]
        color = {'r': 1, 'g': 1, 'b': 1, 'a': 1} if mats[mi]['texture'] else mats[mi]['color']
        records.append({'name': ob.name, 'group': ob.get('group', 'static'), 'phase': 0, 'material': mi, 'vertices': verts, 'normals': normals,
                        'uv': uv, 'triangles': tri, 'color': color, 'texture': mats[mi]['texture'],
                        'rest': {'position': vec(C @ center), 'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'scale': {'x': 1, 'y': 1, 'z': 1}}, 'samples': []})
    data = {'version': 1, 'fps': 24, 'frames': 1, 'materials': mats, 'parts': records}
    if extra: data.update(extra)
    (SRC / ('v11_' + key + '.mesh.json')).write_text(json.dumps(data, separators=(',', ':')))
    size = C @ (hi - lo); mid = C @ ((hi + lo) / 2)
    print(f'EXPORT {key}: parts {len(records)}, triangles {sum(len(r["triangles"]) // 3 for r in records)}, '
          f'size (Unity) {size.x:.3f} x {size.y:.3f} x {size.z:.3f}, center {mid.x:.3f},{mid.y:.3f},{mid.z:.3f}', flush=True)


def preview(key, parts, liquid=False, view=(1.05, -2.3, .75)):
    scene = bpy.context.scene
    if liquid:  # preview stand-in for the runtime liquid
        amber = material('Preview whiskey', (.42, .14, .025), .12, trans=.55)
        h = LIQUID['halfWidth']; z1 = LIQUID['bottom'] + (LIQUID['top'] - LIQUID['bottom']) * LIQUID['fill']
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, (LIQUID['bottom'] + z1) / 2)); ob = bpy.context.object
        ob.scale = (2 * h, 2 * h, z1 - LIQUID['bottom']); assign(ob, amber)
    lo = Vector((1e9, 1e9, 1e9)); hi = -lo
    for ob in parts:
        for v in ob.data.vertices:
            p = ob.matrix_world @ v.co; lo = Vector(tuple(min(lo[i], p[i]) for i in range(3))); hi = Vector(tuple(max(hi[i], p[i]) for i in range(3)))
    mid = (lo + hi) / 2; size = max(hi - lo)
    bpy.ops.mesh.primitive_plane_add(size=size * 12, location=(0, 0, lo.z)); assign(bpy.context.object, material('Floor', (.09, .085, .08), .8))
    world = bpy.data.worlds.new('World'); scene.world = world; world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.05, .055, .06, 1)
    for loc, energy, sz in (((-1.6, -2.2, 2.4), 900, 2.5), ((2.2, -.8, 1.6), 350, 2), ((0, 2.5, 2.0), 500, 2)):
        bpy.ops.object.light_add(type='AREA', location=Vector(loc) * size * 1.4 + mid); light = bpy.context.object
        light.data.energy = energy * size * size; light.data.size = sz * size
        light.rotation_euler = (mid - light.location).to_track_quat('-Z', 'Y').to_euler()
    cam_loc = mid + Vector(view).normalized() * size * 2.5
    bpy.ops.object.camera_add(location=cam_loc); cam = bpy.context.object
    cam.rotation_euler = (mid - cam_loc).to_track_quat('-Z', 'Y').to_euler(); cam.data.lens = 55; scene.camera = cam
    scene.render.resolution_x = 900; scene.render.resolution_y = 900
    scene.render.filepath = str(ART / (key + '.png')); bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(ART / (key + '.blend')))


for key, build in (('plush_corgi', corgi), ('whiskey_bottle', whiskey)):
    reset()
    parts = build()
    extra = {'liquid': LIQUID} if key == 'whiskey_bottle' else None
    if ARGS.preview:
        # Export first (export triangulates in place; previews use the same geometry).
        export(key, parts, extra); preview(key, parts, liquid=key == 'whiskey_bottle', view=(1.3, 2.1, .8) if key == 'plush_corgi' else (1.05, -2.3, .75))
    else:
        export(key, parts, extra)
print('CORGI_WHISKEY_COMPLETE', flush=True)
