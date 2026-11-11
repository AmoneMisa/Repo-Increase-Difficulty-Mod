"""Original articulated monster meshes. Blender background script; no third-party models.

Run:  blender --background --factory-startup --python tools/build_monsters.py -- --root . [--only witch,naga] [--no-render]

Output: src/Art/monster_<key>.mesh.json (read by src/Visuals/ArtModelReader.cs, built by src/Monsters/MonsterVisual.cs)
        art/monsters/<key>.png (concept render, posed).

Contract with MonsterVisual (group = part.name before '|'; every group turns around its pivot):
  wing_l / wing_r        flapping wings
  leg<side><y>           legs; side is '-1' (left) or '1', a rear leg has a negative y ("-0." in the name)
  tail0..N               snake chain. Basilisk: rooted at tail6, the neck (tail5..0) hangs on tail6 and carries the
                         head. Naga: rooted at the torso, tail0 first.
  head, jaw              jaw hangs on head
  arm_l / arm_r          witch (and fairy) arms; naga arm-10, arm-11 (left upper/lower), arm10, arm11
  pivots without parts   eye_l, eye_r, hand_l, hand_r: anchors for glow / spell effects
The export is the REST pose (chains straight where the code bends them); the render applies a pose on top.
Axes: Blender x right, y forward, z up -> Unity x, z, y.
"""
import argparse, json, math, sys
from pathlib import Path
import bpy, bmesh
from mathutils import Vector, Matrix, Euler

p = argparse.ArgumentParser()
p.add_argument('--root', type=Path, required=True)
p.add_argument('--only', default='')
p.add_argument('--no-render', action='store_true')
p.add_argument('--closeup', default='')   # debug: also render head close-ups into this folder
args = p.parse_args(sys.argv[sys.argv.index('--') + 1:])
root = args.root.resolve()
out = root / 'src' / 'Art'
preview = root / 'art' / 'monsters'
preview.mkdir(parents=True, exist_ok=True)
C = Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))

gold = (.72, .42, .06); bronze = (.25, .14, .06); steel = (.34, .38, .42); darksteel = (.1, .11, .13)
dark = (.035, .03, .04); white = (.88, .84, .72); bone = (.72, .64, .46); wood = (.2, .11, .05)
parts = []; pivots = {}; parent = {}; pose = {}


# ---------------------------------------------------------------------------------------------- primitives

def material(color, metal=False, glow=0.0):
    key = str((color, metal, glow)); m = bpy.data.materials.get(key)
    if m: return m
    m = bpy.data.materials.new(key); m.diffuse_color = (*color, 1); m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF'); b.inputs['Base Color'].default_value = (*color, 1)
    b.inputs['Metallic'].default_value = .8 if metal else .0
    b.inputs['Roughness'].default_value = .32 if metal else .6
    if glow > 0:
        b.inputs['Emission Color'].default_value = (*color, 1); b.inputs['Emission Strength'].default_value = glow
    return m


def finish(o, name, group, color, metal=False, flat=False, glow=0.0):
    o.name = group + '|fixed|' + name; o['color'] = color; o['metal'] = metal
    o.data.materials.append(material(color, metal, glow)); parts.append(o)
    if not flat:
        for f in o.data.polygons: f.use_smooth = True
    return o


def ball(name, pos, scale, color, group='body', metal=False, rot=None, quat=None, glow=0.0, seg=None):
    if isinstance(scale, (int, float)): scale = (scale, scale, scale)
    big = max(scale)
    if seg is None: seg = (8, 5) if big < .045 else (12, 7) if big < .13 else (16, 10) if big < .4 else (24, 14)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg[0], ring_count=seg[1], location=pos)
    o = bpy.context.object; o.scale = scale
    if rot is not None: o.rotation_euler = rot
    if quat is not None: o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = quat
    return finish(o, name, group, color, metal, glow=glow)


def torus(name, pos, major, minor, color, group='body', metal=True, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_segments=20, minor_segments=8, major_radius=major, minor_radius=minor, location=pos, rotation=rot)
    return finish(bpy.context.object, name, group, color, metal)


def loft(name, rings, color, group='body', metal=False, segs=12, side=(1, 0, 0), shape=None, flat=False, glow=0.0):
    """A closed tube through rings [(centre, rx, ry)]: rx lies along `side`, ry across. shape(i, angle) scales a vertex."""
    pts = [Vector(r[0]) for r in rings]; n = len(pts); verts = []; faces = []
    u = None
    for i, r in enumerate(rings):
        t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        base = Vector(side) if u is None else u
        u = base - t * base.dot(t)
        if u.length < 1e-4:
            u = Vector((0, 1, 0)) - t * t.y
            if u.length < 1e-4: u = Vector((0, 0, 1)) - t * t.z
        u.normalize(); v = t.cross(u)
        rx = r[1]; ry = r[2] if len(r) > 2 else r[1]
        for k in range(segs):
            a = k * math.tau / segs; s = shape(i, a) if shape else 1
            verts.append(pts[i] + u * (max(rx, .003) * math.cos(a) * s) + v * (max(ry, .003) * math.sin(a) * s))
    for i in range(n - 1):
        for k in range(segs):
            a = i * segs + k; b = i * segs + (k + 1) % segs
            faces.append((a, b, b + segs, a + segs))
    for end, ring in ((0, 0), (1, n - 1)):
        verts.append(pts[ring]); c = len(verts) - 1
        for k in range(segs): faces.append((c, ring * segs + k, ring * segs + (k + 1) % segs))
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update()
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o)
    return finish(o, name, group, color, metal, flat, glow)


def spline(pts, per=4):
    """Catmull-Rom resample of a polyline."""
    pts = [Vector(q) for q in pts]; res = []
    for i in range(len(pts) - 1):
        p0 = pts[max(i - 1, 0)]; p1 = pts[i]; p2 = pts[i + 1]; p3 = pts[min(i + 2, len(pts) - 1)]
        for k in range(per):
            t = k / per
            res.append(.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    res.append(pts[-1]); return res


def tube(name, pts, r0, r1, color, group='body', metal=False, segs=10, per=4, flatten=1.0, side=(1, 0, 0), profile=None, glow=0.0):
    """A smooth tapered tube through the points. profile(f) overrides the radius along 0..1."""
    q = spline(pts, per) if len(pts) > 2 and per > 1 else [Vector(a) for a in pts]
    rings = []
    for i, c in enumerate(q):
        f = i / (len(q) - 1); r = profile(f) if profile else r0 + (r1 - r0) * f
        rings.append((c, r, r * flatten))
    return loft(name, rings, color, group, metal, segs, side, glow=glow)


def cone(name, base, tip, r, color, group='body', metal=False, segs=8, glow=0.0):
    base = Vector(base); tip = Vector(tip)
    return loft(name, [(base, r), (base.lerp(tip, .5), r * .55), (tip, .004)], color, group, metal, segs, glow=glow)


def blade(name, base, direction, side, length, width, color, group, curve=.0, thick=.012):
    """A flat diamond-section blade from base along direction, bending towards side."""
    d = Vector(direction).normalized(); s = Vector(side); s = (s - d * s.dot(d)).normalized(); rings = []
    for i in range(9):
        f = i / 8
        w = width * (.75 + .55 * f) if f < .75 else width * 1.16 * (1 - (f - .75) / .25) ** .7
        rings.append((Vector(base) + d * (length * f) + s * (curve * length * f * f), max(w, .004), thick * (1 - .6 * f)))
    return loft(name, rings, color, group, True, 4, s, flat=True)


def sword(group, hand, direction, side, length=.75, curve=.16, steelc=steel):
    d = Vector(direction).normalized(); h = Vector(hand); s = Vector(side); s = (s - d * s.dot(d)).normalized()
    tube('grip', [h - d * .1, h + d * .09], .022, .022, dark, group, segs=8)
    ball('pommel', h - d * .12, .034, gold, group, True)
    tube('guard', [h + d * .1 - s * .11, h + d * .1 + s * .11], .02, .02, gold, group, True, segs=6)
    ball('guard gem', h + d * .1, (.03, .03, .03), (.6, .03, .05), group, True)
    blade('scimitar', h + d * .11, d, s, length, .07, steelc, group, curve, .016)


def face(c, s, skin, iris, lips, group='head', lid=None, glow=1.5, brow=dark, wide=1.0):
    """An adult face on an oval head; c = head centre, s = scale (head is about .31*s tall). Sets eye pivots."""
    c = Vector(c); lid = lid or tuple(x * .55 for x in skin)
    P = lambda x, y, z: c + Vector((x, y, z)) * s
    loft('skull', [(P(0, .028, -.158), .03 * s, .034 * s), (P(0, .016, -.125), .066 * s, .075 * s), (P(0, .006, -.07), .092 * s, .1 * s),
                   (P(0, 0, 0), .104 * s * wide, .112 * s), (P(0, -.004, .07), .104 * s * wide, .116 * s), (P(0, -.008, .125), .078 * s, .092 * s),
                   (P(0, -.01, .152), .03 * s, .036 * s)], skin, group, segs=16)
    for sx in (-1, 1):
        e = P(sx * .043, .096, .018)
        pivots['eye_l' if sx < 0 else 'eye_r'] = tuple(e)
        ball('eye white', e, (.027 * s, .012 * s, .013 * s), (.9, .88, .84), group, rot=(0, -sx * .22, 0))
        ball('iris glow', e + Vector((sx * .002, .008, .0)) * s, (.0115 * s, .006 * s, .0125 * s), iris, group, glow=glow)
        ball('pupil', e + Vector((sx * .002, .0125, 0)) * s, (.0045 * s, .003 * s, .009 * s), dark, group)
        ball('eyelid', e + Vector((0, .003, .0095)) * s, (.031 * s, .0135 * s, .0095 * s), lid, group, rot=(0, -sx * .3, 0))
        tube('brow', [P(sx * .014, .108, .04), P(sx * .05, .105, .056), P(sx * .08, .088, .05)], .0055 * s, .003 * s, brow, group, segs=6, per=3)
        tube('lash', [P(sx * .066, .094, .024), P(sx * .084, .084, .036)], .004 * s, .0015 * s, dark, group, segs=5)
    tube('nose', [P(0, .104, .03), P(0, .124, -.012), P(0, .128, -.03)], .01 * s, .0145 * s, skin, group, segs=8, per=2)
    ball('upper lip', P(0, .101, -.07), (.029 * s, .013 * s, .0075 * s), lips, group)
    ball('lower lip', P(0, .099, -.083), (.023 * s, .013 * s, .009 * s), lips, group)
    for sx in (-1, 1): ball('lip corner', P(sx * .029, .094, -.071), (.008 * s, .008 * s, .005 * s), lips, group)
    return P


def hand(group, at, toward, skin, size=1.0, nails=None):
    at = Vector(at); d = Vector(toward).normalized()
    sidev = d.cross(Vector((0, 0, 1)));
    if sidev.length < .01: sidev = Vector((1, 0, 0))
    sidev.normalize()
    ball('palm', at, (.036 * size, .036 * size, .042 * size), skin, group)
    for k in range(4):
        b = at + sidev * ((k - 1.5) * .017 * size) + d * .028 * size; tip = b + d * .06 * size + Vector((0, 0, -.012 * size))
        tube('finger', [b, tip], .0085 * size, .005 * size, skin, group, segs=5)
        if nails: cone('nail', tip, tip + d * .028 * size, .005 * size, nails, group, segs=5)
    tube('thumb', [at + sidev * .03 * size, at + sidev * .05 * size + d * .04 * size], .009 * size, .006 * size, skin, group, segs=5)


def feather_wing(group, base, s, span, color, shade, metal=True):
    """A feathered wing spreading sideways and up from base; s = side."""
    base = Vector(base); pivots[group] = tuple(base); n = 10; bones = []
    for i in range(n):
        f = i / (n - 1)
        b = base + Vector((s * span * .5 * f, -.05 * f, span * .22 * math.sin(f * math.pi * .8)))
        bones.append(b)
        for layer, (scale_l, c, dy) in enumerate(((1.0, color, 0.0), (.55, shade, .028))):
            th = math.radians(18 + 118 * f ** 1.15); ln = span * (.42 + .5 * f) * scale_l
            d = Vector((s * math.sin(th), -.22, -math.cos(th))).normalized(); w = Vector((s * math.cos(th), 0, math.sin(th)))
            rings = [(b + Vector((0, dy, 0)), .018 * span, .012), (b + d * ln * .45 + Vector((0, dy, 0)), .1 * span * scale_l + .02, .013),
                     (b + d * ln * .85 + Vector((0, dy, 0)), .075 * span * scale_l + .014, .01), (b + d * ln + Vector((0, dy, 0)), .008, .005)]
            loft('feather', rings, c, group, metal, 6, w)
    tube('wing arm', bones, .05 * span + .015, .02, shade, group, metal, segs=8, per=1)


def crown(c, r, group='head', points=7, height=.16, gem=(.6, .03, .05), front_only=False):
    c = Vector(c)
    loft('crown band', [(c + Vector((0, 0, -.012)), r * .98), (c + Vector((0, 0, 0)), r), (c + Vector((0, 0, .05)), r * 1.04), (c + Vector((0, 0, .062)), r * 1.0)], gold, group, True, 16)
    for i in range(points):
        a = math.pi / 2 + (i - (points - 1) / 2) * (math.pi * (1.0 if front_only else 2.0) / points)
        h = height * (1.0 if not front_only else 1.25 - .55 * abs(i - (points - 1) / 2) / max(1, (points - 1) / 2))
        b = c + Vector((math.cos(a) * r, math.sin(a) * r, .05))
        cone('crown point', b, b + Vector((math.cos(a) * r * .15, math.sin(a) * r * .15, h)), r * .2, gold, group, True, 6)
        ball('crown gem', b + Vector((math.cos(a) * r * .06, math.sin(a) * r * .06, -.02)), r * .11, gem, group, True)


# ---------------------------------------------------------------------------------------------- monsters

def pig():
    """Golden Pig: a fat golden piggy bank on feathered wings."""
    ball('golden body', (0, 0, .62), (.5, .6, .46), gold, metal=True)
    ball('belly', (0, .02, .5), (.42, .5, .36), (.8, .5, .1), metal=True)
    # Coin slot and a coin dropping in.
    ball('coin slot', (0, -.08, 1.065), (.035, .15, .02), dark)
    loft('coin', [((-.012, -.08, 1.12), .09, .09), ((.012, -.08, 1.12), .09, .09)], (.95, .7, .15), metal=True, segs=14, side=(0, 1, 0))
    pivots['head'] = (0, .38, .74)
    ball('head', (0, .46, .74), (.36, .32, .33), gold, 'head', True)
    loft('snout', [((0, .7, .7), .14, .1), ((0, .76, .7), .2, .14), ((0, .86, .7), .21, .15), ((0, .875, .7), .16, .11)], (.78, .34, .3), 'head', segs=14, side=(1, 0, 0))
    for x in (-.08, .08): ball('nostril', (x, .878, .71), (.036, .014, .05), dark, 'head')
    ball('smile', (0, .74, .575), (.13, .05, .025), (.3, .12, .05), 'head')
    for s in (-1, 1):
        e = (s * .19, .7, .88)
        pivots['eye_l' if s < 0 else 'eye_r'] = e
        ball('eye', e, (.075, .05, .085), dark, 'head')
        ball('glint', (s * .19 - .02, .745, .915), (.022, .012, .024), (1, 1, 1), 'head')
        ball('cheek', (s * .27, .62, .7), (.07, .05, .05), (.85, .4, .25), 'head')
        # Floppy ear.
        loft('ear', [((s * .2, .42, 1.0), .1, .03), ((s * .3, .5, 1.12), .12, .03), ((s * .4, .62, 1.1), .08, .025), ((s * .44, .72, 1.0), .01, .01)], gold, 'head', True, 8, side=(1, 0, 0))
        for y in (-.28, .26):
            g = 'leg' + str(s) + str(y); pivots[g] = (s * .28, y, .42)
            tube('leg', [(s * .28, y, .44), (s * .3, y + .03, .2)], .13, .1, gold, g, True)
            loft('hoof', [((s * .3, y + .03, .2), .1), ((s * .3, y + .04, .1), .125), ((s * .3, y + .04, .04), .115)], bronze, g, True, 10)
        feather_wing('wing_l' if s < 0 else 'wing_r', (s * .36, -.08, .86), s, .66, white, (.95, .75, .3))
    tube('curled tail', [(0, -.56, .68), (.1, -.68, .72), (.14, -.7, .84), (.04, -.68, .9), (-.04, -.72, .83), (.02, -.78, .78)], .035, .015, gold, metal=True, segs=8)
    crown((0, .5, 1.05), .11, 'head', 6, .1)
    pose.update({'wing_l': (0, 22, 0), 'wing_r': (0, -22, 0), 'head': (-6, 0, 0), 'leg-1-0.28': (-25, 0, 0), 'leg1-0.28': (-25, 0, 0), 'leg-10.26': (25, 0, 0), 'leg10.26': (25, 0, 0)})


def gorgon():
    """Gorgon: an armoured bull, head carried low, heavy forward horns."""
    hide = (.1, .06, .045); plate = (.17, .19, .22)
    loft('torso', [((0, -1.08, 1.12), .1, .12), ((0, -.98, 1.13), .42, .46), ((0, -.6, 1.12), .56, .57), ((0, -.2, 1.15), .6, .6),
                   ((0, .2, 1.24), .68, .72), ((0, .5, 1.26), .64, .7), ((0, .75, 1.16), .46, .5), ((0, .9, 1.06), .22, .26)], hide, segs=20)
    ball('shoulder hump', (0, .3, 1.72), (.5, .5, .36), hide)
    for i, y in enumerate((-.82, -.47, -.12)):
        ball('back plate', (0, y, 1.36), (.62, .22, .5), plate, metal=True)
        ball('plate rim', (0, y - .17, 1.36), (.6, .05, .48), gold, metal=True)
        cone('spine spike', (0, y, 1.82), (0, y - .1, 2.12), .09, bone)
    ball('hump plate', (0, .3, 1.5), (.72, .34, .62), plate, metal=True)
    ball('hump rim', (0, .04, 1.48), (.69, .06, .58), gold, metal=True)
    for k in (-1, 0, 1): cone('hump spike', (k * .28, .3, 2.06 - abs(k) * .08), (k * .4, .22, 2.42 - abs(k) * .16), .11, bone)
    ball('rump plate', (0, -.98, 1.2), (.44, .2, .42), plate, metal=True)
    pivots['head'] = (0, .8, 1.1)
    loft('low head', [((0, .76, 1.06), .3, .33), ((0, .98, 1.0), .42, .37), ((0, 1.2, .84), .38, .31), ((0, 1.4, .69), .3, .23),
                      ((0, 1.54, .585), .27, .2), ((0, 1.62, .54), .15, .11)], hide, 'head', segs=16)
    ball('muzzle', (0, 1.56, .56), (.25, .12, .17), (.2, .13, .12), 'head')
    for s in (-1, 1): ball('nostril', (s * .1, 1.665, .59), (.05, .025, .035), dark, 'head')
    torus('nose ring', (0, 1.68, .45), .1, .022, gold, 'head', rot=(math.pi / 2, 0, 0))
    nrm = Vector((0, .54, .84))
    loft('chamfron', [(Vector((0, .9, 1.03)) + nrm * .1, .2, .3), (Vector((0, .98, 1.0)) + nrm * .1, .3, .36), (Vector((0, 1.2, .84)) + nrm * .09, .27, .3),
                      (Vector((0, 1.4, .69)) + nrm * .08, .2, .22), (Vector((0, 1.52, .6)) + nrm * .07, .1, .16)], steel, 'head', True, 14)
    tube('chamfron ridge', [(0, 1.08, 1.4), (0, 1.36, 1.24), (0, 1.6, 1.03), (0, 1.7, .9)], .04, .02, gold, 'head', True, segs=6)
    ball('chamfron stud', (0, 1.2, 1.36), .065, (.6, .03, .05), 'head', True)
    pivots['jaw'] = (0, 1.1, .7); parent['jaw'] = 'head'
    loft('jaw', [((0, 1.04, .72), .2, .08), ((0, 1.25, .62), .2, .08), ((0, 1.46, .49), .16, .07), ((0, 1.53, .46), .07, .04)], (.06, .04, .035), 'jaw', segs=10)
    for s in (-1, 1): cone('tusk', (s * .12, 1.44, .52), (s * .14, 1.47, .64), .025, bone, 'jaw', segs=6)
    for s in (-1, 1):
        e = (s * .33, 1.3, .96); pivots['eye_l' if s < 0 else 'eye_r'] = e
        ball('eye glow', e, (.065, .075, .06), (.95, .16, .04), 'head', glow=3)
        ball('brow plate', (s * .33, 1.27, 1.05), (.12, .16, .05), plate, 'head', True, rot=(-.55, s * .7, 0))
        tube('horn', [(s * .3, .95, 1.1), (s * .66, .92, 1.2), (s * .98, 1.02, 1.3), (s * 1.12, 1.3, 1.44), (s * 1.04, 1.62, 1.6)], .15, .05, bone, 'head', segs=10)
        cone('horn tip', (s * 1.04, 1.62, 1.6), (s * .96, 1.84, 1.72), .05, dark, 'head')
        for f, r in ((.36, .165), (.46, .15)): torus('horn band', (s * f, .945, 1.115), r * .92, .028, gold, 'head', rot=(0, math.pi / 2 - s * .3, 0))
        loft('ear', [((s * .34, .84, 1.02), .05, .02), ((s * .52, .8, 1.0), .09, .03), ((s * .7, .78, .94), .01, .01)], hide, 'head', False, 6, side=(0, 0, 1))
        ball('pauldron', (s * .6, .42, 1.42), (.24, .32, .32), plate, metal=True)
        ball('pauldron rim', (s * .62, .42, 1.2), (.22, .3, .06), gold, metal=True)
        cone('pauldron spike', (s * .74, .42, 1.5), (s * 1.0, .4, 1.72), .09, bone)
        for y in (-.65, .48):
            g = 'leg' + str(s) + str(y); pivots[g] = (s * .42, y, 1.02)
            tube('thigh', [(s * .42, y, 1.05), (s * .46, y + (.06 if y > 0 else -.04), .58)], .23 if y > 0 else .21, .14, hide, g)
            tube('shin', [(s * .46, y + (.06 if y > 0 else -.04), .58), (s * .46, y, .2)], .13, .105, hide, g)
            loft('hoof', [((s * .46, y, .22), .12), ((s * .46, y + .02, .1), .17), ((s * .46, y + .03, .0), .18)], dark, g, False, 10)
            ball('greave', (s * .47, y + .08, .4), (.11, .07, .2), plate, g, True)
            ball('knee cap', (s * .47, y + .13, .6), (.1, .08, .1), gold, g, True)
    tube('tail', [(0, -1.02, 1.22), (0, -1.3, 1.08), (0, -1.4, .7)], .05, .028, hide, segs=8)
    ball('tail tuft', (0, -1.4, .6), (.07, .07, .14), dark)
    pose.update({'head': (-6, 0, 8), 'jaw': (-12, 0, 0), 'leg-10.48': (18, 0, 0), 'leg1-0.65': (12, 0, 0), 'leg10.48': (-10, 0, 0), 'leg-1-0.65': (-12, 0, 0)})


BASILISK_ROOT = 6


def basilisk():
    """Basilisk: flat viper head with small horn crests and glowing green eyes. Modelled straight; the code bends it."""
    scale_c = (.1, .24, .08); back = (.05, .12, .05); belly = (.62, .6, .34); crest = (.3, .22, .08)
    n = 18; d = .2
    for i in range(n):
        f = i / (n - 1); r = .2 * (1 - .8 * f ** 1.5); g = 'tail' + str(i); z = r * .85; y = -i * d - d * .5
        pivots[g] = (0, -(i + 1) * d if i < BASILISK_ROOT else -i * d, z)
        if i < BASILISK_ROOT: parent[g] = 'tail' + str(i + 1)
        elif i > BASILISK_ROOT: parent[g] = 'tail' + str(i - 1)
        ball('body segment', (0, y, z), (r * 1.2, d * .78, r * .85), scale_c, g, True, seg=(14, 8))
        ball('belly plate', (0, y, z * .55), (r * .95, d * .7, r * .5), belly, g, seg=(10, 6))
        ball('back diamond', (0, y, z + r * .62), (r * .62, d * .5, r * .28), back, g, True, seg=(8, 5))
        for s in (-1, 1): ball('side scale', (s * r * .95, y, z + r * .25), (r * .3, d * .4, r * .28), (.3, .42, .1), g, True, seg=(8, 5))
        if i < 12: cone('dorsal crest', (0, y + .03, z + r * .8), (0, y - .09, z + r * (1.55 - .5 * f)), r * .28, crest, g, segs=6)
        if i >= n - 3: ball('rattle bead', (0, y, z), (r * 1.35, d * .4, r * 1.2), (.5, .42, .26), g, seg=(10, 6))
    pivots['head'] = (0, 0, .17); parent['head'] = 'tail0'
    loft('flat head', [((0, -.04, .17), .19, .14), ((0, .12, .18), .28, .13), ((0, .28, .175), .33, .115), ((0, .45, .16), .25, .095),
                       ((0, .6, .14), .16, .07), ((0, .7, .13), .07, .04)], scale_c, 'head', True, 16)
    ball('head plate', (0, .3, .255), (.2, .26, .05), back, 'head', True)
    pivots['jaw'] = (0, .08, .1); parent['jaw'] = 'head'
    loft('lower jaw', [((0, .06, .085), .2, .045), ((0, .3, .075), .26, .045), ((0, .55, .07), .15, .035), ((0, .67, .07), .06, .02)], belly, 'jaw', segs=12)
    ball('mouth lining', (0, .36, .1), (.18, .24, .02), (.45, .05, .08), 'jaw')
    tube('tongue', [(0, .5, .1), (0, .76, .09), (0, .88, .1)], .018, .012, (.7, .08, .14), 'jaw', segs=6)
    for s in (-1, 1):
        tube('tongue fork', [(0, .88, .1), (s * .05, .98, .11)], .011, .003, (.7, .08, .14), 'jaw', segs=5)
        e = (s * .225, .44, .21); pivots['eye_l' if s < 0 else 'eye_r'] = e
        ball('eye glow', e, (.065, .085, .055), (.25, 1.0, .12), 'head', glow=5)
        ball('slit pupil', (s * .276, .46, .212), (.012, .016, .045), dark, 'head')
        ball('brow ridge', (s * .19, .36, .268), (.075, .1, .028), back, 'head', True, rot=(0, s * .5, 0))
        for k in range(4):
            b = (s * (.24 - k * .012), .34 - k * .085, .262)
            cone('brow horn', b, (b[0] + s * .04, b[1] - .06, b[2] + .06 + k * .014), .026, crest, 'head', segs=6)
        cone('fang', (s * .1, .6, .1), (s * .1, .62, -.04), .022, white, 'head', segs=6)
        ball('nostril', (s * .045, .68, .16), (.014, .012, .01), dark, 'head')
    for k in range(4): cone('crown crest', (0, .16 - k * .07, .29), (0, .11 - k * .07, .345 + k * .01), .028, crest, 'head', segs=6)
    # Render pose: raised hissing neck, S-curved body.
    rise = {5: 26, 4: 26, 3: 20, 2: -4, 1: -26, 0: -28}
    for i in range(n):
        if i < BASILISK_ROOT: pose['tail' + str(i)] = (rise[i], 0, 6 * math.sin(i))
        elif i > BASILISK_ROOT: pose['tail' + str(i)] = (0, 0, 30 * math.sin((i - BASILISK_ROOT) * .75 + .4))
    pose['tail' + str(BASILISK_ROOT)] = (0, 0, -35)
    pose['head'] = (-6, 0, 0); pose['jaw'] = (-28, 0, 0)


def naga():
    """Naga Queen: crowned, armoured, four arms with four scimitars, a long serpent tail."""
    skin = (.62, .42, .32); scales = (.03, .2, .17); fin = gold
    # Tail: a column from the hips to a coil on the floor, trailing behind.
    path = spline([(0, 0, 1.0), (0, .1, .74), (.02, .24, .5), (.12, .36, .3), (.34, .36, .22), (.54, .18, .2), (.56, -.12, .19), (.36, -.42, .18),
                   (0, -.62, .17), (-.36, -.86, .15), (-.46, -1.2, .13), (-.22, -1.56, .11), (.14, -1.82, .09), (.3, -2.14, .07)], 8)
    lengths = [0]
    for a, b in zip(path, path[1:]): lengths.append(lengths[-1] + (b - a).length)
    n = 24; total = lengths[-1]; step = total / n
    def at(dist):
        for k in range(1, len(path)):
            if lengths[k] >= dist:
                f = (dist - lengths[k - 1]) / max(1e-6, lengths[k] - lengths[k - 1]); return path[k - 1].lerp(path[k], f)
        return path[-1]
    for i in range(n):
        f = i / (n - 1); r = .25 * (1 - .82 * f ** 1.7) + .012; c = at((i + .5) * step); t = (at(min(total, (i + 1) * step)) - at(i * step)).normalized()
        c.z = max(c.z, r * .86); g = 'tail' + str(i); j = at(i * step); j.z = max(j.z, r * .86)
        pivots[g] = tuple(j); parent[g] = 'tail' + str(i - 1) if i else 'body'
        q = t.to_track_quat('Y', 'Z')
        ball('tail segment', c, (r * 1.05, step * 1.05, r * .9), scales, g, True, quat=q, seg=(14, 8))
        up = q @ Vector((0, 0, 1)); sd = q @ Vector((1, 0, 0))
        if t.z > -.6:
            cone('dorsal fin', c + up * r * .8 + t * .03, c + up * r * (1.32 - .3 * f) - t * .07, r * .22, fin, g, True, 6)
            ball('belly plate', c - up * r * .4, (r * .85, step * .7, r * .5), (.7, .6, .34), g, quat=q, seg=(10, 6))
        if i % 3 == 1: ball('gold band', c, (r * 1.09, step * .2, r * .94), gold, g, True, quat=q, seg=(14, 6))
    # Torso.
    loft('torso', [((0, 0, .98), .26, .2), ((0, 0, 1.1), .265, .195), ((0, 0, 1.25), .18, .135), ((0, .005, 1.4), .205, .15), ((0, .01, 1.55), .245, .168),
                   ((0, 0, 1.7), .27, .15), ((0, 0, 1.78), .19, .115), ((0, 0, 1.835), .068, .068), ((0, .005, 1.96), .058, .058)], skin, segs=18)
    loft('belt', [((0, 0, 1.0), .285, .215), ((0, 0, 1.06), .29, .22), ((0, 0, 1.13), .262, .2)], gold, metal=True, segs=18)
    ball('belt gem', (0, .215, 1.07), (.05, .025, .05), (.05, .6, .45), metal=True)
    for k in range(-2, 3):
        a = math.pi / 2 + k * .5
        loft('tasset', [((math.cos(a) * .27, math.sin(a) * .21, 1.02), .07, .02), ((math.cos(a) * .31, math.sin(a) * .25, .84), .06, .018),
                        ((math.cos(a) * .32, math.sin(a) * .26, .7), .008, .008)], gold if k % 2 == 0 else darksteel, metal=True, segs=6, side=(-math.sin(a), math.cos(a), 0))
    for s in (-1, 1):
        ball('breast cup', (s * .1, .115, 1.56), (.1, .085, .095), gold, metal=True)
        tube('strap', [(s * .1, .16, 1.64), (s * .2, .08, 1.76)], .016, .016, gold, metal=True, segs=6)
    ball('gorget', (0, .02, 1.79), (.2, .13, .06), gold, metal=True)
    torus('collar', (0, .005, 1.87), .068, .018, gold)
    ball('navel gem', (0, .135, 1.27), .02, (.05, .6, .45), metal=True)
    ball('back plate', (0, -.09, 1.56), (.22, .08, .2), darksteel, metal=True)
    # Head, hair, crown.
    hc = (0, .01, 2.1)
    face(hc, 1.0, skin, (1.0, .78, .1), (.42, .05, .1), glow=2.5, brow=(.05, .03, .02))
    pivots['head'] = (0, 0, 1.95)
    hair = (.03, .025, .035)
    ball('hair cap', (0, -.03, 2.14), (.112, .118, .118), hair, 'head')
    loft('hair fall', [((0, -.1, 2.2), .08, .04), ((0, -.15, 2.05), .14, .06), ((0, -.19, 1.8), .19, .06), ((0, -.2, 1.5), .17, .05),
                       ((0, -.19, 1.25), .11, .035), ((0, -.18, 1.12), .01, .01)], hair, 'head', segs=12)
    for s in (-1, 1):
        tube('front lock', [(s * .09, .04, 2.18), (s * .125, .06, 2.0), (s * .15, .1, 1.78), (s * .17, .12, 1.58)], .032, .01, hair, 'head', segs=8)
        ball('earring', (s * .11, .02, 2.03), (.016, .016, .03), gold, 'head', True)
    crown((0, -.005, 2.2), .1, 'head', 7, .15, (.05, .6, .45), front_only=True)
    for k in range(-3, 4):   # a fan crest behind the crown, like a cobra hood in gold
        a = k * .3; ln = .34 - abs(k) * .035
        b = Vector((math.sin(a) * .07, -.09, 2.2 + math.cos(a) * .03))
        loft('crest blade', [(b, .018, .008), (b + Vector((math.sin(a), 0, math.cos(a))) * ln * .6, .045, .01), (b + Vector((math.sin(a), 0, math.cos(a))) * ln, .006, .004)],
             gold, 'head', True, 6, side=(math.cos(a), 0, -math.sin(a)))
    # Four arms, four scimitars.
    for s in (-1, 1):
        for row in (0, 1):
            g = 'arm' + str(s) + str(row)
            if row == 0:
                a = (s * .29, 0, 1.73); b = (s * .6, .04, 1.72); c = (s * .78, .16, 2.0); sd = (s * .22, .18, 1); sside = (s, 0, -.2)
            else:
                a = (s * .25, .02, 1.48); b = (s * .5, .1, 1.3); c = (s * .7, .34, 1.36); sd = (s * .5, .62, .6); sside = (s * .6, -.5, 0)
            pivots[g] = a
            tube('upper arm', [a, b], .07, .055, skin, g); tube('forearm', [b, c], .055, .042, skin, g)
            ball('elbow', b, .058, skin, g)
            ball('pauldron', (a[0] + s * .03, a[1], a[2] + .03 - row * .04), (.13, .12, .1) if row == 0 else (.09, .09, .08), gold, g, True)
            if row == 0: cone('pauldron spike', (a[0] + s * .08, a[1], a[2] + .1), (a[0] + s * .22, a[1], a[2] + .26), .04, gold, g, True, 6)
            tube('bracer', [Vector(b).lerp(Vector(c), .4), Vector(b).lerp(Vector(c), .92)], .062, .052, gold, g, True)
            torus('armband', Vector(a).lerp(Vector(b), .55), .068, .014, gold, g, rot=(0, math.pi / 2, 0))
            ball('fist', c, (.05, .05, .055), skin, g)
            sword(g, c, sd, sside, .78 if row == 0 else .7)
    pose.update({'head': (4, 0, -8), 'arm-10': (0, -10, 0), 'arm10': (0, 10, 0)})


def witch():
    """Witch: an adult, elegant and sinister woman riding an old broom; tall crooked hat, long hair, long dress."""
    skin = (.8, .7, .68); dress = (.15, .035, .22); bodice = (.045, .02, .06); hat = (.06, .025, .085); hair = (.035, .03, .06)
    # Old broom.
    tube('broom handle', [(0, -1.3, .98), (.01, -.6, 1.0), (-.012, .2, 1.05), (.01, .75, 1.07), (0, 1.12, 1.13)], .03, .024, wood, segs=8)
    ball('handle knot', (0, .5, 1.065), (.036, .05, .036), wood)
    for i in range(22):
        a = i * 2.4; rr = .045 + .09 * ((i * 37) % 10) / 10
        tube('broom straw', [(math.cos(a) * .03, -1.22, .98 + math.sin(a) * .03), (math.cos(a) * rr * 1.3, -1.6, .96 + math.sin(a) * rr * 1.3),
                             (math.cos(a) * rr * 1.7, -1.95 - .08 * (i % 3), .93 + math.sin(a) * rr * 1.5)], .02, .008, (.5, .33, .12), segs=5, per=2)
    for y in (-1.28, -1.36): torus('twine', (0, y, .98), .058 + (y + 1.28) * -.2, .014, (.3, .08, .06), metal=False, rot=(math.pi / 2, 0, 0))
    tube('lantern cord', [(0, 1.08, 1.12), (0, 1.1, .98)], .006, .006, dark, segs=5)
    ball('lantern', (0, 1.1, .9), (.05, .05, .07), (.45, 1.0, .35), glow=3)
    torus('lantern cage', (0, 1.1, .9), .052, .008, bronze)
    # Skirt: over the lap, then a long train flowing back.
    folds = lambda i, a: 1 + (.02 + .012 * i) * math.sin(a * 7 + i)
    loft('dress', [((0, -.14, 1.32), .105, .085), ((0, -.1, 1.2), .2, .2), ((0, .02, 1.09), .24, .3), ((0, -.06, .86), .29, .36), ((0, -.26, .56), .33, .36),
                   ((0, -.52, .27), .35, .3), ((0, -.82, .03), .29, .2), ((0, -1.1, -.1), .14, .08), ((0, -1.22, -.14), .02, .02)], dress, segs=22, shape=folds)
    loft('sash', [((0, -.14, 1.3), .118, .096), ((0, -.135, 1.345), .11, .09), ((0, -.13, 1.37), .105, .086)], (.4, .05, .1), segs=16)
    ball('sash clasp', (0, -.04, 1.335), (.03, .016, .036), gold, metal=True)
    for s in (-1, 1):
        tube('shin', [(s * .075, .3, 1.0), (s * .05 - .03, .42, .58)], .05, .034, (.05, .03, .06))
        tube('boot', [(s * .05 - .03, .42, .6), (s * .05 - .03, .46, .5), (s * .05 - .03, .62, .47)], .042, .01, dark, segs=8, per=3)
        cone('heel', (s * .05 - .03, .41, .52), (s * .05 - .03, .4, .43), .016, dark, segs=5)
    # Torso.
    loft('bodice', [((0, -.15, 1.24), .16, .13), ((0, -.135, 1.34), .103, .083), ((0, -.11, 1.5), .145, .112), ((0, -.09, 1.62), .16, .115),
                    ((0, -.09, 1.7), .17, .085), ((0, -.09, 1.735), .1, .06)], bodice, segs=16)
    for s in (-1, 1): ball('bust', (s * .066, .0, 1.6), (.073, .07, .068), bodice)
    ball('neckline', (0, -.045, 1.7), (.1, .06, .05), skin)
    loft('neck', [((0, -.09, 1.7), .055, .05), ((0, -.085, 1.78), .042, .042), ((0, -.075, 1.9), .04, .04)], skin, segs=10)
    torus('choker', (0, -.082, 1.83), .043, .009, dark, metal=False)
    ball('choker gem', (0, -.04, 1.825), (.014, .008, .018), (.45, 1.0, .35), glow=2)
    for k in range(5): tube('lacing', [(-.045, .012 - k * .004, 1.38 + k * .04), (.045, .012 - k * .004, 1.4 + k * .04)], .005, .005, (.4, .05, .1), segs=4)
    # Arms: long bell sleeves, pale hands with dark nails. Modelled hanging; the code poses them.
    for s in (-1, 1):
        g = 'arm_l' if s < 0 else 'arm_r'; a = (s * .185, -.09, 1.68); b = (s * .27, -.06, 1.42); c = (s * .3, .08, 1.2)
        pivots[g] = a
        ball('shoulder', a, (.062, .06, .06), bodice, g)
        tube('sleeve', [a, b, c], .056, .1, dress, g, segs=12, profile=lambda f: .056 + .01 * math.sin(f * 3.14) + .06 * max(0, f - .55) ** 1.3 * 3)
        ball('cuff lining', (c[0], c[1] + .012, c[2] - .012), (.085, .085, .03), (.4, .05, .1), g, rot=(.9, 0, 0))
        h = (c[0], c[1] + .07, c[2] - .07); pivots['hand_l' if s < 0 else 'hand_r'] = h
        tube('wrist', [c, h], .024, .02, skin, g, segs=6)
        hand(g, h, (0, .7, -.6), skin, .9, dark)
    # Head.
    hc = Vector((0, -.065, 2.045)); pivots['head'] = (0, -.08, 1.88)
    face(hc, .98, skin, (.55, 1.0, .3), (.35, .02, .08), lid=(.2, .08, .26), glow=2.5)
    ball('beauty mark', hc + Vector((.045, .096, -.045)), .004, dark, 'head')
    ball('hair cap', hc + Vector((0, -.035, .03)), (.112, .118, .12), hair, 'head')
    loft('hair fall', [(hc + Vector((0, -.1, .1)), .085, .04), (hc + Vector((0, -.15, -.08)), .15, .06), (hc + Vector((0, -.2, -.38)), .2, .06),
                       (hc + Vector((0, -.25, -.7)), .2, .055), (hc + Vector((0, -.32, -.98)), .13, .04), (hc + Vector((0, -.4, -1.15)), .012, .01)], hair, 'head', segs=14,
         shape=lambda i, a: 1 + .05 * math.sin(a * 5 + i * 1.3))
    for s in (-1, 1):
        tube('front lock', [hc + Vector((s * .085, .045, .1)), hc + Vector((s * .125, .06, -.08)), hc + Vector((s * .15, .1, -.32)), hc + Vector((s * .13, .13, -.56))], .034, .008, hair, 'head', segs=8)
        ball('earring', hc + Vector((s * .108, .0, -.07)), (.012, .012, .03), gold, 'head', True)
    # Hat: wide brim, tall crooked crown.
    ball('hat brim', hc + Vector((0, -.01, .135)), (.43, .44, .022), hat, 'head', rot=(.1, .04, 0))
    tube('tall hat', [hc + Vector((0, -.01, .12)), hc + Vector((0, -.03, .36)), hc + Vector((.02, -.09, .58)), hc + Vector((.08, -.2, .76)), hc + Vector((.17, -.33, .8)),
                      hc + Vector((.22, -.4, .74))], .155, .006, hat, 'head', segs=16, profile=lambda f: .155 * (1 - f) ** .8 + .006)
    loft('hat band', [(hc + Vector((0, -.012, .15)), .158), (hc + Vector((0, -.016, .205)), .147)], (.4, .05, .1), 'head', segs=16)
    torus('buckle', hc + Vector((0, .135, .178)), .026, .007, gold, 'head', rot=(math.pi / 2, 0, 0))
    pose.update({'arm_r': (72, 0, -28), 'arm_l': (38, 0, 0), 'head': (4, 0, 10)})
    # Fire in the raised palm (render only: in game it is MonsterVisual's spell effect).
    render_only.append(lambda: (ball('flame core', (.3, .3, 1.13), (.07, .1, .07), (1.0, .5, .08), 'arm_r', glow=9),
                                cone('flame tongue', (.3, .33, 1.13), (.3, .56, 1.15), .05, (1.0, .75, .2), 'arm_r', glow=6)))


def fairy():
    """Mischief Fairy (Tier I concept): a tiny imp of a fairy who lifts things out of carts and extractors."""
    skin = (.86, .62, .52); leaf = (.12, .42, .1); hair = (.03, .46, .48); cap = (.62, .08, .38); wing = (.6, .92, .95)
    loft('torso', [((0, 0, .4), .07, .06), ((0, 0, .46), .085, .075), ((0, .005, .56), .075, .065), ((0, 0, .64), .085, .06), ((0, 0, .67), .035, .035)], leaf, segs=12)
    for k in range(8):   # petal skirt
        a = k * math.tau / 8; d = Vector((math.cos(a), math.sin(a), 0))
        loft('petal', [(d * .05 + Vector((0, 0, .47)), .05, .012), (d * .14 + Vector((0, 0, .4)), .075, .014), (d * .2 + Vector((0, 0, .3)), .05, .01),
                       (d * .22 + Vector((0, 0, .22)), .006, .004)], leaf if k % 2 else (.2, .55, .12), segs=6, side=(-d.y, d.x, 0))
    torus('belt', (0, 0, .47), .082, .014, (.5, .3, .1), metal=False)
    # Loot sack with a stolen coin poking out.
    ball('loot sack', (-.14, -.06, .4), (.075, .075, .09), (.45, .3, .16)); torus('sack tie', (-.14, -.06, .485), .03, .01, (.3, .08, .06), metal=False)
    loft('stolen coin', [((-.15, -.066, .52), .04, .04), ((-.14, -.06, .525), .04, .04)], (.95, .7, .15), metal=True, segs=12, side=(0, 1, 0))
    for s in (-1, 1):   # legs: striped stockings, curled shoes with bells
        hip = Vector((s * .04, 0, .4)); knee = Vector((s * .07, .1, .27)); foot = Vector((s * .065, .04, .1))
        for k in range(4):
            tube('stocking', [hip.lerp(knee, k / 4), hip.lerp(knee, (k + 1) / 4)], .03, .028, (.9, .88, .8) if k % 2 else cap, segs=8)
            tube('stocking', [knee.lerp(foot, k / 4), knee.lerp(foot, (k + 1) / 4)], .027, .022, cap if k % 2 else (.9, .88, .8), segs=8)
        tube('curled shoe', [foot, foot + Vector((0, .06, -.03)), foot + Vector((0, .13, -.01)), foot + Vector((0, .15, .04)), foot + Vector((0, .12, .06))], .03, .008, leaf, segs=8)
        ball('bell', foot + Vector((0, .115, .075)), .016, gold, metal=True)
    # Big head, impish face.
    hc = Vector((0, .01, .86)); pivots['head'] = (0, 0, .68)
    ball('head', hc, (.17, .16, .16), skin, 'head')
    for s in (-1, 1):
        e = hc + Vector((s * .068, .135, .02)); pivots['eye_l' if s < 0 else 'eye_r'] = tuple(e)
        ball('eye white', e, (.047, .022, .055), (.95, .95, .92), 'head')
        ball('iris', e + Vector((s * -.006, .017, -.004)), (.03, .012, .036), (.6, .25, .9), 'head', glow=1.2)
        ball('pupil', e + Vector((s * -.008, .026, -.004)), (.014, .006, .02), dark, 'head')
        ball('glint', e + Vector((-.014, .03, .016)), .008, (1, 1, 1), 'head')
        tube('brow', [e + Vector((-s * .04, .02, .05 + (.03 if s > 0 else 0))), e + Vector((s * .035, .01, .075 + (.03 if s > 0 else -.012)))], .008, .005, hair, 'head', segs=5)
        ball('blush', hc + Vector((s * .115, .1, -.045)), (.035, .02, .022), (.95, .45, .45), 'head')
        loft('pointed ear', [(hc + Vector((s * .15, -.01, 0)), .03, .05), (hc + Vector((s * .24, -.03, .05)), .018, .045), (hc + Vector((s * .36, -.07, .14)), .004, .006)], skin, 'head', segs=8, side=(0, 1, 0))
    ball('nose', hc + Vector((0, .162, -.02)), (.018, .016, .014), skin, 'head')
    tube('grin', [hc + Vector((-.07, .125, -.06)), hc + Vector((-.03, .148, -.09)), hc + Vector((.035, .146, -.085)), hc + Vector((.08, .12, -.045))], .011, .008, (.35, .05, .1), 'head', segs=6)
    cone('fang', hc + Vector((.045, .146, -.078)), hc + Vector((.047, .15, -.105)), .008, (1, 1, .95), 'head', segs=5)
    for k in range(9):   # wild tufts of hair
        a = -.2 + k * .42; b = hc + Vector((math.cos(a) * .12, -.02 + .02 * math.sin(k), .09 + .04 * math.sin(a)))
        tube('hair tuft', [b, b + Vector((math.cos(a) * .1, -.04, .07)), b + Vector((math.cos(a) * .15, -.02, .16 + .02 * (k % 3))), b + Vector((math.cos(a) * .12, .04, .2))], .04, .006, hair, 'head', segs=7)
    ball('hair cap', hc + Vector((0, -.025, .03)), (.172, .16, .155), hair, 'head')
    for k in (-1, 0, 1): tube('fringe', [hc + Vector((k * .07, .12, .12)), hc + Vector((k * .08 + .02, .16, .07)), hc + Vector((k * .09 + .03, .155, .03))], .03, .006, hair, 'head', segs=6)
    # Bluebell cap, tipped to one side.
    tube('bluebell cap', [hc + Vector((.04, -.01, .13)), hc + Vector((.07, -.02, .21)), hc + Vector((.12, -.04, .27)), hc + Vector((.2, -.06, .28))], .1, .012, cap, 'head', segs=12,
         profile=lambda f: .105 * (1 - f) ** .6 + .012)
    for k in range(6):
        a = k * math.tau / 6; b = hc + Vector((.035 + math.cos(a) * .1, -.01 + math.sin(a) * .1, .125))
        cone('cap petal', b, b + Vector((math.cos(a) * .06, math.sin(a) * .06, -.04)), .035, cap, 'head', segs=6)
    ball('cap bell', hc + Vector((.215, -.065, .27)), .02, gold, 'head', True)
    # Arms: thin, with oversized grabby mittens. The right one waves a star wand.
    for s in (-1, 1):
        g = 'arm_l' if s < 0 else 'arm_r'; a = Vector((s * .085, 0, .63)); b = Vector((s * .17, .06, .56)); c = Vector((s * .2, .17, .62))
        pivots[g] = tuple(a); tube('arm', [a, b, c], .026, .02, skin, g, segs=8, per=3)
        pivots['hand_l' if s < 0 else 'hand_r'] = tuple(c)
        ball('mitten', c + Vector((0, .02, .01)), (.045, .045, .05), (.95, .8, .25), g)
        ball('mitten thumb', c + Vector((-s * .04, .02, .03)), (.018, .018, .026), (.95, .8, .25), g)
        torus('mitten cuff', c + Vector((0, -.015, -.02)), .03, .012, cap, g, False, rot=(.9, 0, 0))
    w = Vector((.2, .19, .66))
    tube('wand', [w + Vector((0, 0, -.06)), w + Vector((.03, .03, .2))], .008, .006, wood, 'arm_r', segs=5)
    for k in range(5):
        a = k * math.tau / 5
        cone('wand star', w + Vector((.03, .03, .22)), w + Vector((.03 + math.sin(a) * .06, .03, .22 + math.cos(a) * .06)), .022, (1.0, .85, .3), 'arm_r', segs=5, glow=4)
    ball('star core', w + Vector((.03, .03, .22)), .026, (1.0, .9, .4), 'arm_r', glow=5)
    # Two pairs of dragonfly wings.
    for s in (-1, 1):
        g = 'wing_l' if s < 0 else 'wing_r'; r = Vector((s * .035, -.075, .6)); pivots[g] = tuple(r)
        for (dx, dz, ln, wd) in ((1.0, .75, .46, .12), (1.0, -.35, .32, .09)):
            d = Vector((s * dx, -.25, dz)).normalized(); wv = Vector((-s * dz, 0, dx)).normalized()
            loft('wing', [(r, .012, .006), (r + d * ln * .3, wd * .7, .006), (r + d * ln * .65, wd, .006), (r + d * ln * .92, wd * .6, .005), (r + d * ln, .01, .004)], wing, g, True, 8, wv, glow=.35)
            tube('wing vein', [r, r + d * ln * .95], .008, .003, (.3, .7, .8), g, segs=5)
    pose.update({'head': (0, 0, -12), 'arm_r': (-25, 0, 0), 'wing_l': (0, 12, 0), 'wing_r': (0, -12, 0)})
    for k in range(14):   # fairy dust (render only)
        render_only.append(lambda k=k: ball('dust', (-.25 + .04 * math.sin(k * 2.1), -.2 - k * .035, .5 - k * .028 + .05 * math.sin(k * 1.3)), .012 - k * .0005, (1.0, .85, .4), 'fx', glow=6))


def twins():
    """Hansel and Gretel (Tier II concept): pale, hollow-eyed twin children who never let go of each other's hand."""
    porcelain = (.84, .8, .76); stitch = (.12, .02, .03)

    def child(x, girl):
        s = 1 if x > 0 else -1   # outer side
        tag = 'g' if girl else 'h'; P = lambda a, b, c: Vector((x + a, b, c))
        cloth = (.36, .09, .1) if girl else (.1, .2, .12); shirt = (.78, .74, .66)
        for sx in (-1, 1):
            tube('leg', [P(sx * .065, 0, .5), P(sx * .07, .01, .1)], .045, .036, (.8, .78, .72), segs=8)
            tube('sock', [P(sx * .068, .005, .3), P(sx * .07, .01, .1)], .041, .04, (.5, .48, .46) if not girl else (.75, .72, .7), segs=8)
            ball('shoe', P(sx * .07, .04, .05), (.055, .095, .05), dark)
        if girl:
            loft('dress', [(P(0, 0, .86), .1, .08), (P(0, 0, .7), .12, .1), (P(0, 0, .52), .2, .18), (P(0, 0, .34), .26, .23), (P(0, 0, .32), .2, .18)], cloth, segs=16,
                 shape=lambda i, a: 1 + .012 * i * math.sin(a * 8))
            ball('apron', P(0, .13, .5), (.12, .05, .17), shirt); ball('apron bib', P(0, .085, .74), (.07, .03, .08), shirt)
            ball('apron stain', P(.03, .176, .46), (.035, .008, .05), (.3, .04, .04))
            torus('waist tie', P(0, 0, .66), .115, .014, shirt, metal=False)
        else:
            loft('shirt', [(P(0, 0, .6), .12, .095), (P(0, 0, .72), .125, .1), (P(0, 0, .86), .11, .085)], shirt, segs=14)
            loft('lederhosen', [(P(0, 0, .42), .12, .1), (P(0, 0, .5), .135, .11), (P(0, 0, .62), .128, .1), (P(0, 0, .64), .11, .085)], cloth, segs=14)
            for sx in (-1, 1):
                tube('short leg', [P(sx * .065, 0, .5), P(sx * .07, 0, .36)], .07, .062, cloth, segs=10)
                tube('suspender', [P(sx * .07, .095, .62), P(sx * .06, .085, .86)], .013, .013, (.25, .12, .05), segs=5)
            tube('chest strap', [P(-.07, .1, .75), P(.07, .1, .75)], .014, .014, (.25, .12, .05), segs=5)
            ball('strap heart', P(0, .108, .75), (.025, .012, .025), stitch)
        loft('collar', [(P(0, 0, .85), .11, .09), (P(0, 0, .885), .06, .055)], shirt, segs=12)
        tube('neck', [P(0, 0, .86), P(0, 0, .95)], .035, .032, porcelain, segs=8)
        # Head: an oversized porcelain doll head.
        hg = 'head_' + tag; hc = P(0, 0, 1.08); pivots[hg] = tuple(P(0, 0, .93))
        ball('head', hc, (.16, .15, .165), porcelain, hg)
        for sx in (-1, 1):
            e = hc + Vector((sx * .062, .128, .01))
            ball('hollow eye', e, (.04, .02, .05), dark, hg)
            ball('pin pupil', e + Vector((0, .02, 0)), .007, (1.0, .95, .8), hg, glow=6)
            ball('eye shadow', e + Vector((0, -.008, -.01)), (.052, .02, .066), (.5, .42, .45), hg)
            ball('doll cheek', hc + Vector((sx * .108, .095, -.05)), (.03, .016, .026), (.72, .4, .4), hg)
        ball('nose', hc + Vector((0, .15, -.025)), (.014, .012, .012), porcelain, hg)
        tube('stitched smile', [hc + Vector((-.075, .115, -.065)), hc + Vector((-.035, .138, -.088)), hc + Vector((.035, .138, -.088)), hc + Vector((.075, .115, -.065))], .006, .006, stitch, hg, segs=5)
        for k in range(-3, 4):
            m = hc + Vector((k * .022, .14 - abs(k) * .006, -.086 + abs(k) * .005)); tube('stitch', [m + Vector((0, .003, .014)), m + Vector((0, .003, -.014))], .0035, .0035, stitch, hg, segs=4, per=1)
        crack = [hc + Vector((s * .04, .12, .15)), hc + Vector((s * .06, .14, .1)), hc + Vector((s * .045, .145, .07)), hc + Vector((s * .075, .135, .04))]
        tube('crack', crack, .004, .002, dark, hg, segs=4, per=1)
        blond = (.62, .52, .3)
        ball('hair', hc + Vector((0, -.022, .035)), (.168, .158, .155), blond, hg)
        for k in range(-3, 4): tube('fringe', [hc + Vector((k * .035, .1, .14)), hc + Vector((k * .04, .15, .075))], .022, .008, blond, hg, segs=6)
        if girl:
            for sx in (-1, 1):
                for k in range(7): ball('braid', hc + Vector((sx * (.15 + k * .008), -.02, -.06 - k * .055)), (.03, .03, .034), blond, hg)
                for b in (-1, 1): ball('braid bow', hc + Vector((sx * .2 + b * .028, -.02, -.44)), (.03, .014, .02), stitch, hg, rot=(0, b * .5, 0))
            for b in (-1, 1): ball('head bow', hc + Vector((b * .055, .02, .185)), (.06, .02, .035), stitch, hg, rot=(0, -b * .45, 0))
            ball('bow knot', hc + Vector((0, .02, .18)), .022, stitch, hg)
        else:
            ball('felt cap', hc + Vector((0, -.01, .135)), (.13, .13, .06), (.1, .2, .12), hg)
            ball('cap brim', hc + Vector((0, .02, .11)), (.15, .16, .016), (.1, .2, .12), hg)
            loft('cap feather', [(hc + Vector((s * .1, -.02, .15)), .008, .004), (hc + Vector((s * .14, -.08, .25)), .03, .005), (hc + Vector((s * .15, -.16, .33)), .004, .003)], (.6, .08, .06), hg, segs=6, side=(0, 1, 1))
        # Arms: the inner one reaches for the twin, the outer one carries a treat.
        ia = 'arm_' + tag + '_in'; oa = 'arm_' + tag + '_out'
        a = P(-s * .12, 0, .82); pivots[ia] = tuple(a); grip = Vector((0, .02, .6))
        tube('inner arm', [a, a.lerp(grip, .5) + Vector((0, .03, -.03)), grip + Vector((s * .03, 0, 0))], .034, .026, shirt if not girl else cloth, ia, segs=8, per=3)
        ball('inner hand', grip + Vector((s * .02, 0, 0)), (.036, .04, .04), porcelain, ia)
        a = P(s * .12, 0, .82); pivots[oa] = tuple(a); h = P(s * .2, .06, .5)
        tube('outer arm', [a, P(s * .18, .0, .66), h], .034, .026, shirt if not girl else cloth, oa, segs=8, per=3)
        ball('outer hand', h, (.036, .04, .04), porcelain, oa)
        if girl:   # a lollipop with a bite taken out
            tube('lolly stick', [h + Vector((0, .02, -.08)), h + Vector((0, .04, .16))], .008, .008, (.9, .88, .8), oa, segs=5)
            loft('lollipop', [(h + Vector((0, .03, .22)), .085, .085), (h + Vector((0, .055, .225)), .085, .085)], (.75, .1, .14), oa, segs=16, side=(1, 0, 0))
            torus('lolly swirl', h + Vector((0, .058, .225)), .045, .012, (.92, .88, .82), oa, False, rot=(math.pi / 2 - .12, 0, 0))
            ball('lolly centre', h + Vector((0, .06, .225)), .018, (.92, .88, .82), oa)
        else:      # a gingerbread man, one leg already eaten
            gb = (.4, .2, .07); c0 = h + Vector((0, .03, -.02))
            ball('gingerbread body', c0 + Vector((0, 0, -.1)), (.05, .018, .065), gb, oa); ball('gingerbread head', c0 + Vector((0, 0, .0)), (.04, .018, .04), gb, oa)
            for b in (-1, 1): ball('gingerbread arm', c0 + Vector((b * .06, 0, -.08)), (.035, .016, .018), gb, oa)
            ball('gingerbread leg', c0 + Vector((-.03, 0, -.18)), (.02, .016, .04), gb, oa)
            for b in (-1, 1): ball('icing eye', c0 + Vector((b * .014, .018, .008)), .006, (.95, .95, .9), oa)
            for k in range(2): ball('icing button', c0 + Vector((0, .018, -.075 - k * .035)), .007, (.75, .1, .14), oa)

    child(-.3, False); child(.3, True)
    ball('breadcrumb pouch', (-.42, -.11, .52), (.06, .05, .075), (.45, .3, .16))
    for k in range(9):
        render_only.append(lambda k=k: ball('breadcrumb', (-.5 + .13 * math.sin(k * 2.3), -.25 - k * .12, .012), (.02, .02, .012), (.75, .62, .38), 'fx'))
    pose.update({'head_h': (0, 16, 0), 'head_g': (0, -16, 0)})


# ---------------------------------------------------------------------------------------------- export / render

def vec(v): return dict(zip(('x', 'y', 'z'), [round(float(x), 5) for x in v]))


def export(key):
    data = {'version': 1, 'fps': 30, 'frames': 1, 'parts': [], 'pivots': [{'name': n, 'position': vec(C @ Vector(v))} for n, v in pivots.items()]}
    total = 0; bad = 0; tris = 0; lo = Vector((1e9,) * 3); hi = -lo
    for o in parts:
        mesh = o.data; mesh.calc_loop_triangles(); vs = []; ns = []; uv = []; ts = []; raw = []; rawn = []
        normal = o.matrix_world.to_3x3().inverted().transposed(); indices = {}
        for tri in mesh.loop_triangles:
            for li in reversed(tri.loops):
                loop = mesh.loops[li]; v = mesh.vertices[loop.vertex_index]
                n = (C @ (normal @ mesh.corner_normals[li].vector)).normalized(); co = C @ (o.matrix_world @ v.co)
                if n.length < .5: n = Vector((0, 1, 0))
                keyv = (loop.vertex_index, round(n.x, 3), round(n.y, 3), round(n.z, 3))
                if keyv not in indices:
                    indices[keyv] = len(vs); vs.append(vec(co)); ns.append(vec(n)); uv.append({'x': round(co.x * 2, 4), 'y': round(co.y * 2, 4)})
                    raw.append(co.copy()); rawn.append(n.copy())
                    if not all(math.isfinite(c) for c in (*co, *n)): raise SystemExit('non-finite vertex in ' + o.name)
                    lo = Vector(map(min, lo, co)); hi = Vector(map(max, hi, co))
                ts.append(indices[keyv])
        # Unity front faces are clockwise: cross(b-a, c-a) must agree with the vertex normals.
        for i in range(0, len(ts), 3):
            a, b, c = (raw[ts[i + k]] for k in range(3)); fn = (b - a).cross(c - a)
            if fn.length > 1e-9 and fn.dot(rawn[ts[i]] + rawn[ts[i + 1]] + rawn[ts[i + 2]]) < 0: bad += 1
        total += len(vs); tris += len(ts) // 3
        color = o['color']; data['parts'].append({'name': o.name, 'vertices': vs, 'normals': ns, 'uv': uv, 'triangles': ts,
            'color': dict(zip(('r', 'g', 'b', 'a'), [*color, 1])), 'texture': 'workshop_wear.png' if o['metal'] else '',
            'rest': {'position': vec((0, 0, 0)), 'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'scale': vec((1, 1, 1))}, 'samples': []})
    if bad > tris * .002: raise SystemExit(f'{key}: {bad} of {tris} triangles wound against their normals')
    if total > 60000: raise SystemExit(f'{key}: {total} vertices is too many')
    (out / ('monster_' + key + '.mesh.json')).write_text(json.dumps(data, separators=(',', ':')), encoding='utf8')
    groups = sorted({o.name.split('|')[0] for o in parts})
    print('MONSTER_EXPORTED', key, 'parts', len(parts), 'verts', total, 'tris', tris, 'flipped', bad, 'size', [round(a, 2) for a in hi - lo], 'groups', len(groups), flush=True)


def apply_pose():
    cache = {}
    def M(g):
        if g in cache: return cache[g]
        piv = Vector(pivots.get(g, (0, 0, 0)))
        local = Matrix.Translation(piv) @ Euler([math.radians(a) for a in pose.get(g, (0, 0, 0))], 'XYZ').to_matrix().to_4x4() @ Matrix.Translation(-piv)
        cache[g] = (M(parent[g]) @ local) if g in parent and parent[g] != 'body' else local
        return cache[g]
    for o in parts: o.matrix_world = M(o.name.split('|')[0]) @ o.matrix_world
    for g in list(pivots): posed[g] = M(parent.get(g, g) if g not in pose and g in parent else g) @ Vector(pivots[g])
    bpy.context.view_layer.update()


def render(key, hover, closeup=None):
    scene = bpy.context.scene; scene.render.engine = 'CYCLES'; scene.cycles.samples = 48; scene.cycles.use_denoising = True
    scene.view_settings.view_transform = 'Standard'
    scene.render.resolution_x = 800; scene.render.resolution_y = 800; scene.render.resolution_percentage = 100
    lo = Vector((1e9,) * 3); hi = -lo
    for o in parts:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co; lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    floor = lo.z - hover * (hi.z - lo.z)
    toward = Vector((4.2, 6.4, 3.4)).normalized(); right = Vector((0, 0, 1)).cross(toward).normalized(); up = toward.cross(right)
    if closeup:
        c = Vector(posed[closeup[0]]) + Vector((0, 0, closeup[1] * .45)); toward = Vector((2.5, 6.4, 1.2)).normalized()
        corners = [c + Vector((a, b, d)) * closeup[1] * .5 for a in (-1, 1) for b in (-1, 1) for d in (-1, 1)]
        right = Vector((0, 0, 1)).cross(toward).normalized(); up = toward.cross(right)
    else:
        corners = [Vector((x, y, z)) for x in (lo.x, hi.x) for y in (lo.y, hi.y) for z in (floor, hi.z)]
    xs = [q.dot(right) for q in corners]; ys = [q.dot(up) for q in corners]; ds = [q.dot(toward) for q in corners]
    look = right * (min(xs) + max(xs)) / 2 + up * (min(ys) + max(ys)) / 2 + toward * (min(ds) + max(ds)) / 2
    size = max(max(xs) - min(xs), max(ys) - min(ys)) * (1.0 if closeup else .95); k = max(size, (hi - lo).length * .5) / 3.2
    if not closeup:
        bpy.ops.mesh.primitive_plane_add(size=400, location=(0, 0, floor - .002)); bpy.context.object.data.materials.append(material((.05, .055, .06)))
    bpy.ops.object.camera_add(location=look + toward * 30); cam = bpy.context.object
    cam.rotation_euler = (look - cam.location).to_track_quat('-Z', 'Y').to_euler(); cam.data.type = 'ORTHO'; cam.data.ortho_scale = size; cam.data.clip_end = 200; scene.camera = cam
    if not closeup:
        for pos, power, sz in (((3, 4, 6), 520, 5), ((-4, 1, 3), 300, 4), ((1, -4, 5), 650, 3)):
            bpy.ops.object.light_add(type='AREA', location=look + Vector(pos) * k); l = bpy.context.object
            l.data.energy = power * k * k; l.data.shape = 'DISK'; l.data.size = sz * k
            l.rotation_euler = (look - l.location).to_track_quat('-Z', 'Y').to_euler()
    scene.world.color = (.1, .1, .11)
    scene.render.filepath = str((Path(args.closeup) / (key + '_' + closeup[0] + '.png')) if closeup else (preview / (key + '.png'))); bpy.ops.render.render(write_still=True)


CLOSEUPS = {'witch': [('head', .75)], 'naga': [('head', .8)], 'gorgon': [('head', 1.6)], 'basilisk': [('head', 1.2)], 'twins': [('head_g', .7)], 'fairy': [('head', .7)]}
posed = {}
render_only = []
only = [k for k in args.only.split(',') if k]
for key, make, hover in (('pig', pig, .3), ('gorgon', gorgon, 0), ('basilisk', basilisk, 0), ('naga', naga, 0), ('witch', witch, .1), ('fairy', fairy, .3), ('twins', twins, 0)):
    if only and key not in only: continue
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    parts = []; pivots = {'body': (0, 0, 0)}; parent = {}; pose = {}; render_only = []
    make(); bpy.context.view_layer.update(); export(key)
    if not args.no_render:
        for extra in render_only: extra()
        bpy.context.view_layer.update(); apply_pose(); render(key, hover)
        if args.closeup:
            for name, sz in CLOSEUPS.get(key, ()): render(key, hover, (name, sz))
