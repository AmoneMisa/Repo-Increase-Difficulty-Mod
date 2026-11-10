"""Blender 5.2: --background --factory-startup --python tools/build_wings.py -- --root PATH [--preview]

Three original feathered back-wing cosmetics (Rare / purple tier, Body Top slot). Inspired by the user's
reference sheets, not copies of them:
  * wings_astral  - Astral Night: navy-black feathers, blue coverts, silver arch, layered feather roots and attached cyan crystals.
  * wings_solar   - Solar Seraph: white-to-gold feathers, gold filigree arch, floating star shards.
  * wings_prism   - Prism Chime: pink -> lilac -> cyan feathers, pearl arch, hanging chains with
                    glowing star charms.

Coordinates are the cosmetic prefab's own space (the same as the game's Body Top cosmetics: the player
faces +Z in Unity, Y up, the upper back is at about z -0.3, y 0.2). Modelled in Blender with the player
facing +Y, exported with Unity (x, y, z) = Blender (x, z, y).

Writes src/Art/v11_<key>.mesh.json (ValuableVisuals format with 'pivots' for the two wing roots),
src/Art/v11_<key>_feather.png (feather gradient), src/Art/cosmetic_<key>_icon.png (menu icon, transparent)
and, with --preview, art/cosmetics/wings/<key>.png renders from behind and from the side.
Size budget: about 0.85 m total span, 0.55 m tall, close to the body so it does not block other players' view.
"""
import argparse
import json
import math
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import cosmetic_kit as K

import bmesh
import bpy
from mathutils import Matrix, Vector

parser = argparse.ArgumentParser(); parser.add_argument('--root', type=Path, required=True); parser.add_argument('--preview', action='store_true')
ARGS = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
ROOT = ARGS.root.resolve()
SRC = ROOT / 'src' / 'Art'
ART = ROOT / 'art' / 'cosmetics' / 'wings'; ART.mkdir(parents=True, exist_ok=True)
C = Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))

ROOT_POS = Vector((.07, -.29, .27))      # right wing root between the shoulder blades (Blender coords)
SWEEP = math.radians(25)                 # wings angle backwards from the shoulder line
LIFT = math.radians(8)                   # slight upward dihedral


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 48
    scene.view_settings.view_transform = 'Standard'


def material(name, color, rough=.5, metal=0., emit=0., image=None):
    m = bpy.data.materials.new(name); m.use_nodes = True
    p = m.node_tree.nodes['Principled BSDF']
    p.inputs['Base Color'].default_value = (*color, 1); p.inputs['Roughness'].default_value = rough
    p.inputs['Metallic'].default_value = metal
    p.inputs['Emission Color'].default_value = (*color, 1); p.inputs['Emission Strength'].default_value = emit
    if image:
        tex = m.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(str(SRC / image)); tex.interpolation = 'Linear'
        m.node_tree.links.new(tex.outputs['Color'], p.inputs['Base Color'])
        if emit > 0: m.node_tree.links.new(tex.outputs['Color'], p.inputs['Emission Color'])
        m['texture'] = image
    return m


def feather_texture(name, stops, quill=(1, 1, 1), quill_mix=.55, barbs=.07, edge=.35):
    """Feather map: u across the vane (quill at u=.5), v from the quill base (0) to the tip (1).
    Colour gradient along the length, a bright quill, fine diagonal barbs and darker vane edges."""
    w, h = 64, 256
    img = bpy.data.images.new(name, w, h, alpha=False)
    px = []
    for y in range(h):
        t = y / (h - 1)
        for i in range(len(stops) - 1):
            (t0, c0), (t1, c1) = stops[i], stops[i + 1]
            if t0 <= t <= t1:
                k = (t - t0) / max(t1 - t0, 1e-6); base = [c0[j] + (c1[j] - c0[j]) * k for j in range(3)]; break
        for x in range(w):
            c = abs(x + .5 - w / 2) / (w / 2)                        # 0 at the quill, 1 at the vane edge
            shade = 1 - edge * c ** 3
            shade *= 1 + barbs * math.sin((y * .55 + c * 26) * math.pi)   # barbs slanting out from the quill
            col = [min(1., ch * shade) for ch in base]
            q = max(0., 1 - c / .07) * quill_mix * (1 - .6 * t)
            col = [col[j] * (1 - q) + quill[j] * q for j in range(3)]
            px.extend((*col, 1))
    img.pixels[:] = px
    img.filepath_raw = str(SRC / name); img.file_format = 'PNG'; img.save()
    return name


def obj(name, verts, faces, mat, group, uvs=None, smooth=True):
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update()
    if uvs:
        layer = me.uv_layers.new(name='UVMap')
        for poly in me.polygons:
            for li in poly.loop_indices: layer.data[li].uv = uvs[me.loops[li].vertex_index]
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    for poly in me.polygons: poly.use_smooth = smooth
    me.materials.append(mat); ob['group'] = group
    return ob


class Frame:
    """Wing plane: u outward, v up, w backward (towards a viewer behind the player)."""
    def __init__(self, side):
        self.side = side
        rot = Matrix.Rotation(-side * SWEEP, 3, 'Z') @ Matrix.Rotation(side * LIFT, 3, 'Y')
        self.u = rot @ Vector((side, 0, 0)); self.v = rot @ Vector((0, 0, 1)); self.w = rot @ Vector((0, -1, 0))
        self.o = Vector((side * ROOT_POS.x, ROOT_POS.y, ROOT_POS.z))

    def p(self, u, v, w=0.):
        return self.o + self.u * u + self.v * v + self.w * w


def arm_curve(s):
    """Leading-edge arch of the wing in (u, v), s from the root (0) to the tip (1)."""
    pts = [(0, 0), (.04, .1), (.1, .2), (.165, .265), (.225, .295), (.27, .3)]
    x = s * (len(pts) - 1); i = min(int(x), len(pts) - 2); t = x - i
    a, b = pts[i], pts[i + 1]
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def feather(f, name, base, angle, length, width, mat, group, layer=0., bend=.12, rows=14, cup=.006):
    """Two-sided feather: widest a little below the middle, rounded to a soft point, slightly cupped
    across the vane, with a raised quill. From base (u, v) pointing at `angle` degrees."""
    a = math.radians(angle); d = Vector((math.cos(a), math.sin(a))); n = Vector((-d.y, d.x))
    cols = (-1, -.5, 0, .5, 1)
    verts, faces, uvs = [], [], []
    for side_w, flip in ((.002, False), (-.002, True)):
        start = len(verts)
        for i in range(rows + 1):
            t = i / rows
            half = width * .5 * max(0., math.sin(math.pi * t ** .8)) ** .55 + .0015 * (1 - t)
            if i == rows: half = 0
            c = Vector(base) + d * (length * t) + n * (bend * length * t * t)
            for k, off in enumerate(cols):
                q = c + n * (off * half)
                lift = cup * off * off * min(1., t * 4) + (.003 * (1 - t) if off == 0 else 0)
                verts.append(f.p(q.x, q.y, layer + side_w + (lift if not flip else -lift)))
                uvs.append(((off + 1) / 2, t))
        m = len(cols)
        for i in range(rows):
            for k in range(m - 1):
                a0 = start + i * m + k; quad = (a0, a0 + 1, a0 + m + 1, a0 + m)
                faces.append(quad[::-1] if (flip == (f.side > 0)) else quad)
    return obj(name, verts, faces, mat, group, uvs)


def feather_point(base, angle, length, bend, t):
    a = math.radians(angle); d = Vector((math.cos(a), math.sin(a))); n = Vector((-d.y, d.x))
    return Vector(base) + d * (length * t) + n * (bend * length * t * t)


def crescent(f, name, u, v, size, mat, group, layer, turn=0.):
    """Small crescent moon (an outer disc minus an offset inner disc), as a flat strip."""
    verts, faces = [], []
    steps = 14
    ct, st_ = math.cos(turn), math.sin(turn)
    for i in range(steps + 1):
        a = math.radians(-150 + 300 * i / steps)
        for q in (Vector((math.cos(a), math.sin(a))), Vector((math.cos(a) * .72 + .38, math.sin(a) * .72))):
            q = q * size; q = Vector((q.x * ct - q.y * st_, q.x * st_ + q.y * ct))
            verts.append(f.p(u + q.x, v + q.y, layer))
    for i in range(steps):
        quad = (2 * i, 2 * i + 2, 2 * i + 3, 2 * i + 1)
        faces.append(quad if f.side > 0 else quad[::-1])
        faces.append(quad[::-1] if f.side > 0 else quad)
    return obj(name, verts, faces, mat, group, smooth=False)


def tube(f, name, path, r0, r1, mat, group, layer=.006, segs=10):
    """Tapered tube along (u, v) points (the wing arch)."""
    verts, faces = [], []
    pts = [f.p(u, v, layer) for u, v in path]
    for i, p in enumerate(pts):
        t = i / (len(pts) - 1); r = r0 + (r1 - r0) * t
        tan = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        a = tan.cross(f.w).normalized(); b = tan.cross(a).normalized()
        for j in range(segs):
            ang = 2 * math.pi * j / segs; verts.append(p + (a * math.cos(ang) + b * math.sin(ang)) * r)
    for i in range(len(pts) - 1):
        for j in range(segs):
            q0 = i * segs + j; q1 = i * segs + (j + 1) % segs
            quad = (q0, q1, q1 + segs, q0 + segs)
            faces.append(quad if f.side > 0 else quad[::-1])
    for cap, idx in ((0, 0), (len(pts) - 1, (len(pts) - 1) * segs)):
        c = len(verts); verts.append(pts[cap])
        for j in range(segs):
            tri = (c, idx + j, idx + (j + 1) % segs)
            faces.append(tri[::-1] if (cap == 0) == (f.side > 0) else tri)
    return obj(name, verts, faces, mat, group)


def gem(f, name, u, v, size, mat, group, layer=.01, stretch=1.6):
    """Floating diamond crystal (elongated octahedron)."""
    c = f.p(u, v, layer)
    verts = [c + f.v * size * stretch, c - f.v * size * stretch, c + f.u * size, c - f.u * size, c + f.w * size * .6, c - f.w * size * .6]
    faces = [(0, 2, 4), (0, 4, 3), (0, 3, 5), (0, 5, 2), (1, 4, 2), (1, 3, 4), (1, 5, 3), (1, 2, 5)]
    if f.side < 0: faces = [t[::-1] for t in faces]
    return obj(name, verts, faces, mat, group, smooth=False)


def star(f, name, u, v, size, mat, group, layer=.012, points=4, inner=.38, thick=.008):
    """Flat star charm with a little thickness."""
    c = f.p(u, v, layer); verts = []; faces = []
    ring = []
    for k in range(points * 2):
        a = math.pi / 2 + math.pi * k / points; r = size if k % 2 == 0 else size * inner
        ring.append(Vector((math.cos(a) * r, math.sin(a) * r)))
    for sgn in (1, -1):
        base = len(verts); verts.append(c + f.w * thick * sgn)
        for q in ring: verts.append(c + f.u * q.x + f.v * q.y)
        for k in range(len(ring)):
            tri = (base, base + 1 + k, base + 1 + (k + 1) % len(ring))
            faces.append(tri if (sgn > 0) == (f.side > 0) else tri[::-1])
    return obj(name, verts, faces, mat, group, smooth=False)


def chain(f, name, u, v, length, mat, group, layer=.012):
    """Hanging chain: small alternating rings."""
    verts, faces = [], []
    links = int(length / .018)
    for k in range(links):
        c = f.p(u, v - .009 - k * .018, layer); axis_a = f.v; axis_b = f.u if k % 2 == 0 else f.w
        base = len(verts); segs, rings, R, r = 8, 4, .008, .0022
        for i in range(segs):
            a = 2 * math.pi * i / segs
            for j in range(rings):
                b = 2 * math.pi * j / rings
                radial = axis_a * math.cos(a) * 1.2 + axis_b * math.sin(a)
                verts.append(c + radial * (R + r * math.cos(b)) + (axis_a.cross(axis_b).normalized()) * r * math.sin(b))
        for i in range(segs):
            for j in range(rings):
                q = (base + i * rings + j, base + ((i + 1) % segs) * rings + j, base + ((i + 1) % segs) * rings + (j + 1) % rings, base + i * rings + (j + 1) % rings)
                faces.append(q)
    return obj(name, verts, faces, mat, group)


STYLES = {
    'wings_astral': dict(
        title='Astral Night Wings',
        grad=[(0, (.10, .12, .20)), (.6, (.15, .19, .32)), (1, (.22, .39, .72))],
        covert=[(0, (.05, .07, .22)), (.55, (.12, .3, .85)), (1, (.4, .7, 1.))], covert_emit=.18, quill=(.75, .85, 1.),
        arch=(.78, .8, .88), arch_metal=.8, accent=(.42, .14, .95), extra='astral'),
    'wings_solar': dict(
        title='Solar Seraph Wings',
        grad=[(0, (.98, .96, .9)), (.45, (1., .86, .55)), (1, (1., .62, .16))],
        covert=[(0, (1., 1., .98)), (1, (.97, .9, .74))], covert_emit=.0, quill=(1., .85, .45),
        arch=(1., .72, .25), arch_metal=.9, accent=(1., .78, .3), extra='solar'),
    'wings_prism': dict(
        title='Prism Chime Wings',
        grad=[(0, (1., .62, .92)), (.45, (.62, .5, 1.)), (1, (.32, .85, 1.))],
        covert=[(0, (1., .86, .98)), (1, (.95, .58, .96))], covert_emit=.1, quill=(1., 1., 1.),
        arch=(.96, .92, 1.), arch_metal=.4, accent=(.75, .6, 1.), extra='prism'),
}


def build(key):
    st = STYLES[key]
    grad = feather_texture('v11_' + key + '_feather.png', st['grad'], quill=st['quill'])
    cov = feather_texture('v11_' + key + '_covert.png', [(0, (.72,) * 3), (1, (1.,) * 3)], quill_mix=.35, barbs=.05, edge=.45)
    feather_mat = material(st['title'] + ' feathers', (1, 1, 1), .55, image=grad, emit=.1)
    covert_mat = material(st['title'] + ' coverts', st['covert'][-1][1], .5, image=cov, emit=st['covert_emit'])
    covert_mat['tint'] = True
    arch_mat = material(st['title'] + ' arch', st['arch'], .3, metal=st['arch_metal'])
    accent_mat = material(st['title'] + ' glow', st['accent'], .2, emit=.9)
    moon_mat = material(st['title'] + ' moons', (.85, .92, 1.), .2, emit=1.2)
    parts, pivots = [], {}
    for side in (1, -1):
        f = Frame(side); g = 'wing_r' if side > 0 else 'wing_l'
        pivots[g] = f.o.copy()
        # Primaries: the long flight feathers fanning down and out from the wrist to the tip.
        for k in range(8):
            s = .58 + .42 * k / 7; bu, bv = arm_curve(s)
            parts.append(feather(f, 'Primary', (bu - .012, bv - .02), -106 + 40 * k / 7, .24 + .1 * k / 7, .095, feather_mat, g, layer=-.0035 * k, bend=.08))
        # Secondaries along the inner arm, pointing down, overlapping.
        for k in range(7):
            s = .06 + .5 * k / 6; bu, bv = arm_curve(s)
            parts.append(feather(f, 'Secondary', (bu, bv - .03), -100 - 6 * k / 6, .18 + .06 * k / 6, .095, feather_mat, g, layer=.004 + .001 * k, bend=.06))
        # Two rows of coverts on top in the accent colour: greater coverts, then small scapulars at the arch.
        for k in range(9):
            s = .04 + .9 * k / 8; bu, bv = arm_curve(s)
            parts.append(feather(f, 'Covert', (bu, bv - .012), -98 + 28 * k / 8, .12 + .03 * math.sin(math.pi * k / 8), .065, covert_mat, g, layer=.012, rows=6))
        for k in range(8):
            s = .08 + .84 * k / 7; bu, bv = arm_curve(s)
            parts.append(feather(f, 'Scapular', (bu, bv - .002), -95 + 26 * k / 7, .065, .038, covert_mat, g, layer=.018, rows=5, bend=.04))
        # Arch with an upturned crescent horn at the tip, and a jewel where it meets the back.
        arch = [arm_curve(i / 16) for i in range(17)] + [(.29, .312), (.305, .332), (.312, .356), (.31, .378)]
        parts.append(tube(f, 'Arch', arch, .021, .003, arch_mat, g))
        parts.append(gem(f, 'Root jewel', .015, .035, .02, accent_mat, g, layer=.026, stretch=1.3))
        if st['extra'] == 'astral':
            for k in (2, 4, 6):                                    # crescent moons near the flight feather tips
                sk = .58 + .42 * k / 7; bu, bv = arm_curve(sk)
                tip = feather_point((bu - .012, bv - .02), -106 + 40 * k / 7, .24 + .1 * k / 7, .08, .72)
                parts.append(crescent(f, 'Moon', tip.x, tip.y, .011, moon_mat, g, layer=-.0035 * k + .0045, turn=math.radians(90)))
            for u, v in ((.30, .16), (.28, .02)):
                parts.append(tube(f, 'Crystal setting', [(.22, .24), (u, v)], .003, .003, arch_mat, g))
                parts.append(gem(f, 'Crystal', u, v, .014, accent_mat, g))
        elif st['extra'] == 'solar':
            for u, v, s in ((.165, .265, .018), (.225, .295, .014)):   # filigree studs on the arch
                parts.append(gem(f, 'Stud', u, v, s, accent_mat, g, layer=.02, stretch=1.0))
            for u, v in ((.30, .18), (.29, .04)):
                parts.append(tube(f, 'Star setting', [(.225, .295), (u, v)], .003, .003, arch_mat, g))
                parts.append(star(f, 'Star shard', u, v, .018, accent_mat, g))
        else:
            for u, v, ln in ((.1, .2, .17), (.2, .28, .22)):
                parts.append(chain(f, 'Chain', u, v, ln, arch_mat, g))
                parts.append(star(f, 'Charm', u, v - ln - .02, .02, accent_mat, g, layer=.012))
            parts.append(tube(f, 'Charm setting', [(.27, .30), (.30, .2)], .003, .003, arch_mat, g))
            parts.append(star(f, 'Charm', .30, .2, .016, accent_mat, g))
    return K.merge(parts), pivots, st['title']


def vec(v): return {'x': round(float(v.x), 6), 'y': round(float(v.y), 6), 'z': round(float(v.z), 6)}


def export(key, parts, pivots):
    mats = []; index = {}; records = []
    lo = Vector((1e9, 1e9, 1e9)); hi = -lo
    for ob in parts:
        me = ob.data
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
                         'transmission': 0, 'emission': p.inputs['Emission Strength'].default_value,
                         'roughness': p.inputs['Roughness'].default_value, 'metallic': p.inputs['Metallic'].default_value,
                         'tint': bool(m.get('tint', False)), 'multiply': bool(m.get('tint', False))})
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
    data = {'version': 1, 'fps': 24, 'frames': 1, 'materials': mats, 'parts': records,
            'pivots': [{'group': g, 'position': vec(C @ p)} for g, p in pivots.items()]}
    (SRC / ('v11_' + key + '.mesh.json')).write_text(json.dumps(data, separators=(',', ':')))
    size = C @ (hi - lo)
    print(f'EXPORT {key}: parts {len(records)}, triangles {sum(len(r["triangles"]) // 3 for r in records)}, '
          f'size (Unity) {size.x:.3f} x {size.y:.3f} x {size.z:.3f}', flush=True)


def stand_in_body():
    """Grey player-sized body for previews only (not exported): torso bean and head."""
    grey = material('Preview body', (.45, .47, .5), .6)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, location=(0, 0, .12)); body = bpy.context.object
    body.scale = (.31, .3, .32); body.data.materials.append(grey)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, location=(0, 0, .64)); head = bpy.context.object
    head.scale = (.25, .24, .2); head.data.materials.append(grey)
    for o in (body, head): bpy.ops.object.shade_smooth()
    return [body, head]


def render(key, parts, title):
    scene = bpy.context.scene
    world = bpy.data.worlds.new('World'); scene.world = world; world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.03, .03, .05, 1)
    for loc, energy in (((-1.2, -2.0, 1.6), 70), ((1.5, -1.2, 1.0), 30), ((0, 2.0, 1.4), 45)):
        bpy.ops.object.light_add(type='AREA', location=loc); light = bpy.context.object
        light.data.energy = energy; light.data.size = 1.5
        light.rotation_euler = (Vector((0, -.2, .25)) - light.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.ops.object.camera_add(); cam = bpy.context.object; scene.camera = cam; cam.data.lens = 50
    scene.render.resolution_x = scene.render.resolution_y = 256
    # Menu icon: both wings from behind on a transparent background.
    scene.render.film_transparent = True
    target = Vector((0, -.29, .3))
    cam.location = target + Vector((0, -1.75, .25)); cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = str(SRC / ('cosmetic_' + key + '_icon.png')); bpy.ops.render.render(write_still=True)
    if not ARGS.preview: return
    stand_in_body()
    scene.render.film_transparent = False; scene.render.resolution_x = scene.render.resolution_y = 700
    for suffix, offset in (('', Vector((.55, -1.75, .45))), ('_side', Vector((2.6, -.6, .5))), ('_front', Vector((.5, 2.6, .5)))):
        cam.location = Vector((0, -.15, .25)) + offset; cam.rotation_euler = (Vector((0, -.15, .25)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = str(ART / (key + suffix + '.png')); bpy.ops.render.render(write_still=True)


for key in STYLES:
    reset()
    parts, pivots, title = build(key)
    export(key, parts, pivots)
    render(key, parts, title)
print('WINGS_COMPLETE', flush=True)
