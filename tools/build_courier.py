"""Courier pets: twelve original chibi animals and spirits with a delivery backpack.

Run:
  blender --background --factory-startup --python tools/build_courier.py -- --root . [--only cat,owl] [--no-render]

Writes, per pet, src/Art/courier_<key>.mesh.json, courier_<key>_atlas.png and courier_<key>_normal.png, then renders
art/courier/<key>.png and art/courier/courier_lineup.png from those shipped files (tools/render_courier.py).

Every part is named "group|channel|label":
  group   - what animates it in game (src/Courier/CourierVisual.cs): body, arm_l, arm_r, foot_l, foot_r, tail, the
            expression sets eyes_open, eyes_happy, eyes_wide, eyes_sleepy, brows_sad, brows_focus (one shown at a time)
            and the swaying extras ear_l, ear_r, wing_l, wing_r, scarf
  channel - which recolour slot tints it: main, second, accent; fixed parts keep their colour
The model also carries "pivots": where each animated group turns (Unity space, metres, feet on y = 0).
Front is Blender +Y (Unity +Z).

The atlas is a COLOUR MULTIPLIER, not a finished picture: the game multiplies it by the part's colour (the animal's
own, or the swatch the player picked), so stripes, feather scallops, scales and spots survive a recolour. Each part
picks its pattern with pat='...' (see pattern()); parts without one use the species default for their channel.
"""
import argparse, json, math, sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

args = argparse.ArgumentParser()
args.add_argument('--root', type=Path, required=True)
args.add_argument('--only', default='')
args.add_argument('--no-render', action='store_true')
args.add_argument('--preview', action='store_true')   # accepted for old command lines; rendering is on by default
opts = args.parse_args(sys.argv[sys.argv.index('--') + 1:])
ROOT = opts.root.resolve()
OUT = ROOT / 'src' / 'Art'
ART = ROOT / 'art' / 'courier'; ART.mkdir(parents=True, exist_ok=True)
C = Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))  # Blender (x, y, z) -> Unity (x, z, y)
ATLAS = 768


def clear():
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.curves):
        for item in list(block):
            if item.users == 0: block.remove(item)


# ------------------------------------------------------------------ primitives

PARTS = []
PIV = {}     # pivots of the pet being built
F = {}       # its frame: head and body objects and sizes


def add(o, group, channel, color, label, pat=None):
    o.name = f'{group}|{channel}|{label}'
    o['ihm_color'] = list(color)
    o['ihm_texture'] = ''
    o['ihm_pat'] = pat or ''
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.shade_smooth()
    PARTS.append(o)
    return o


def mesh_obj(label, verts, faces):
    """A mesh from raw data: welded, without collapsed faces, facing outwards."""
    me = bpy.data.meshes.new(label); me.from_pydata([tuple(v) for v in verts], [], faces); me.update()
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=.00002)
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=.00001)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(label, me); bpy.context.collection.objects.link(o)
    return o


def ellipsoid(group, channel, color, label, loc, radii, rot=(0, 0, 0), segs=16, rings=10, pat=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=rings, radius=1, location=loc, rotation=rot)
    o = bpy.context.object; o.scale = radii
    return add(o, group, channel, color, label, pat)


def orb(group, channel, color, label, center, radii, segs=28, rings=14, mod=None, pat=None):
    """A rounded body. mod(t, a) scales the radius: t runs 0 (bottom) to 1 (top), a is the angle (pi/2 = front)."""
    cx, cy, cz = center; rx, ry, rz = radii
    verts = [(cx, cy, cz - rz)]; faces = []
    for i in range(1, rings):
        t = i / rings; rad = math.sin(math.pi * t); h = -math.cos(math.pi * t)
        for k in range(segs):
            a = math.tau * k / segs; m = mod(t, a) if mod else 1.0
            verts.append((cx + rx * rad * math.cos(a) * m, cy + ry * rad * math.sin(a) * m, cz + rz * h))
    verts.append((cx, cy, cz + rz)); top = len(verts) - 1
    for k in range(segs):
        faces.append((0, 1 + (k + 1) % segs, 1 + k))
        last = 1 + (rings - 2) * segs
        faces.append((top, last + k, last + (k + 1) % segs))
    for i in range(rings - 2):
        for k in range(segs):
            a = 1 + i * segs + k; b = 1 + i * segs + (k + 1) % segs
            faces.append((a, b, b + segs, a + segs))
    return add(mesh_obj(label, verts, faces), group, channel, color, label, pat)


def loft(group, channel, color, label, rings, segs=12, side=(1, 0, 0), shape=None, pat=None):
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
            verts.append(pts[i] + u * (max(rx, .0015) * math.cos(a) * s) + v * (max(ry, .0015) * math.sin(a) * s))
    for i in range(n - 1):
        for k in range(segs):
            a = i * segs + k; b = i * segs + (k + 1) % segs
            faces.append((a, b, b + segs, a + segs))
    for ring_i in (0, n - 1):
        verts.append(pts[ring_i]); c = len(verts) - 1
        for k in range(segs): faces.append((c, ring_i * segs + k, ring_i * segs + (k + 1) % segs))
    return add(mesh_obj(label, verts, faces), group, channel, color, label, pat)


def spline(pts, per=4):
    """Catmull-Rom resample of a polyline."""
    pts = [Vector(q) for q in pts]; res = []
    for i in range(len(pts) - 1):
        p0 = pts[max(i - 1, 0)]; p1 = pts[i]; p2 = pts[i + 1]; p3 = pts[min(i + 2, len(pts) - 1)]
        for k in range(per):
            t = k / per
            res.append(.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    res.append(pts[-1]); return res


def tube(group, channel, color, label, pts, r0, r1, segs=10, per=4, flatten=1.0, side=(1, 0, 0), profile=None, pat=None):
    """A smooth tapered tube through the points, rounded off at both ends. profile(f) overrides the radius along 0..1."""
    q = spline(pts, per) if len(pts) > 2 and per > 1 else [Vector(a) for a in pts]
    rings = []; n = len(q)
    for i, c in enumerate(q):
        f = i / (n - 1); r = profile(f) if profile else r0 + (r1 - r0) * f
        rings.append((c, r, r * flatten))
    # Dome the ends instead of leaving flat caps.
    a, b = rings[0], rings[-1]
    d0 = (q[0] - q[1]).normalized(); d1 = (q[-1] - q[-2]).normalized()
    head = [(q[0] + d0 * a[1] * k, a[1] * w, a[2] * w) for k, w in ((.85, .45), (.55, .82))]
    tail = [(q[-1] + d1 * b[1] * k, b[1] * w, b[2] * w) for k, w in ((.55, .82), (.85, .45))]
    return loft(group, channel, color, label, head + rings + tail, segs, side, pat=pat)


def cone(group, channel, color, label, base, tip, r, verts=10, r2=0.0, pat=None):
    base, tip = Vector(base), Vector(tip)
    return loft(group, channel, color, label, [(base, r), (base.lerp(tip, .5), r * .62 + r2 * .38), (tip, max(r2, .0015))], verts, pat=pat)


def box(group, channel, color, label, loc, size, bevel=.012, rot=(0, 0, 0), pat=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.object; o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel > 0:
        mod = o.modifiers.new('bevel', 'BEVEL'); mod.width = bevel; mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return add(o, group, channel, color, label, pat)


def ring(group, channel, color, label, loc, major, minor, scale=(1, 1, 1), rot=(0, 0, 0), arc=None, pat=None):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=32, minor_segments=6,
                                     location=(0, 0, 0))
    o = bpy.context.object
    if arc is not None:  # keep only the upper half (local +Y), for the closed "happy" eyes
        bm = bmesh.new(); bm.from_mesh(o.data)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y < -minor * .2], context='VERTS')
        bm.to_mesh(o.data); bm.free()
    o.scale = scale; o.rotation_euler = rot; o.location = loc
    return add(o, group, channel, color, label, pat)


def leaf(group, channel, color, label, root, tip, width, normal=(0, 1, 0), camber=.01, thick=.005, prof=None,
         rows=8, cols=4, curl=0.0, pat=None):
    """A thin lens-shaped blade (feather, petal, fin) from root to tip; `normal` is the way its face looks."""
    root, tip = Vector(root), Vector(tip); axis = tip - root
    n = Vector(normal); n = (n - axis * (n.dot(axis) / axis.length_squared)).normalized()
    across = axis.cross(n).normalized()
    prof = prof or (lambda t: math.sin(math.pi * t) ** .8)
    verts = []; faces = []
    for layer in (1, -1):
        for i in range(rows + 1):
            t = i / rows; w = prof(t)
            for j in range(cols + 1):
                u = 2 * j / cols - 1
                dome = math.sqrt(max(0.0, 1 - u * u)) * min(1.0, w * 3) * (1 if 0 < i < rows else 0)
                verts.append(root + axis * t + across * (width * w * u)
                             + n * (camber * w * (1 - u * u) + curl * t * t * axis.length + layer * thick * dome))
    stride = (rows + 1) * (cols + 1)
    for layer in range(2):
        for i in range(rows):
            for j in range(cols):
                k = layer * stride + i * (cols + 1) + j
                faces.append((k, k + 1, k + cols + 2, k + cols + 1))
    return add(mesh_obj(label, verts, faces), group, channel, color, label, pat)


def tuft(group, channel, color, label, base, tip, r, pat=None):
    """A soft tapered lock of fur or down."""
    base, tip = Vector(base), Vector(tip)
    return loft(group, channel, color, label, [(base, r * .7), (base.lerp(tip, .35), r), (base.lerp(tip, .75), r * .55), (tip, .002)], 7, pat=pat)


# ------------------------------------------------------------------ measuring the surface (so parts sit on it)

_BVH = {}


def _tree(o):
    if o.name not in _BVH:
        me = o.data
        _BVH[o.name] = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [tuple(p.vertices) for p in me.polygons])
    return _BVH[o.name]


def _cast(objects, origin, direction, axis, default):
    best = None
    for o in objects:
        hit = _tree(o).ray_cast(Vector(origin), Vector(direction))[0]
        if hit is None: continue
        v = hit[axis]
        if best is None or v * direction[axis] < best * direction[axis]: best = v
    return default if best is None else best


def front(x, z, objects=None, default=0.0):
    """Y of the frontmost surface at (x, z)."""
    return _cast(objects or [F['head'], F['body']], (x, 2, z), (0, -1, 0), 1, default)


def back(x, z, objects=None, default=0.0):
    return _cast(objects or [F['head'], F['body']], (x, -2, z), (0, 1, 0), 1, default)


def right(y, z, objects=None, default=0.0):
    """X of the outermost surface on the +X side at (y, z)."""
    return _cast(objects or [F['head'], F['body']], (2, y, z), (-1, 0, 0), 0, default)


def body_parts():
    return [o for o in PARTS if o.name.startswith('body|') and max(o.dimensions) > .15]   # head, body, belly, face, robe


# ------------------------------------------------------------------ shared pieces

WHITE, BLACK = (.95, .95, .93), (.035, .035, .04)
CREAM, PINK, DARK = (.99, .9, .72), (.98, .56, .62), (.07, .04, .035)
HEAD_Z = .39
EYE_Z = HEAD_Z + .015


def frame(main, head=(.2, .18, .172), body=(.165, .15, .165), cheek=.10, pear=.16, pat=None):
    """The shared chibi build: a big head with full cheeks on a small pear-shaped body. Sets F and default pivots."""
    PIV.clear(); F.clear(); _BVH.clear()
    b = orb('body', 'main', main, 'body', (0, 0, body[2] + .002), body, 28, 14,
            mod=lambda t, a: 1 + pear * (1 - 2 * t) * (1 - t), pat=pat)
    h = orb('body', 'main', main, 'head', (0, 0, HEAD_Z), head, 32, 16,
            mod=lambda t, a: 1 + cheek * math.exp(-((t - .34) / .17) ** 2) * abs(math.cos(a)) ** 1.5, pat=pat)
    F.update(head=h, body=b, hx=head[0], hy=head[1], hz=head[2], bx=body[0], by=body[1], bz=body[2], top=HEAD_Z + head[2])
    PIV.update({'foot_l': (.08, .03, .055), 'foot_r': (-.08, .03, .055), 'tail': (0, -body[1] * .9, .08)})
    return h, b


def eyes(x, z, r=.055, iris=None, on=None):
    """Big glossy eyes and every expression set. The game shows one set at a time:
    eyes_open (neutral), eyes_happy (^ ^), eyes_wide (surprised: tiny pupils), eyes_sleepy (closed lines),
    plus brows_sad and brows_focus drawn over the open eyes."""
    iris = iris or (.36, .2, .09)
    y = front(x, z, on) - r * .34
    lid = (.19, .11, .07)
    for s, side in ((1, 'l'), (-1, 'r')):
        ex = s * x
        # Open: a glossy dome in a rim, a big coloured iris with a lighter lower crescent, pupil and two shines
        ring('eyes_open', 'fixed', lid, 'eyelid_' + side, (ex, y - r * .08, z), r * 1.02, r * .06, rot=(math.pi / 2, 0, 0))
        ellipsoid('eyes_open', 'fixed', WHITE, 'sclera_' + side, (ex, y, z), (r * 1.06, r * .6, r * 1.14), segs=20, rings=12)
        ellipsoid('eyes_open', 'fixed', iris, 'iris_' + side, (ex, y + r * .50, z - r * .06), (r * .62, r * .20, r * .68))
        ellipsoid('eyes_open', 'fixed', tuple(min(1, c * 1.5 + .12) for c in iris), 'iris_glow_' + side,
                  (ex, y + r * .535, z - r * .3), (r * .44, r * .185, r * .36), segs=14, rings=8)
        ellipsoid('eyes_open', 'fixed', BLACK, 'pupil_' + side, (ex, y + r * .565, z - r * .02), (r * .40, r * .18, r * .46))
        ellipsoid('eyes_open', 'fixed', (1, 1, 1), 'shine_' + side, (ex + s * r * .2, y + r * .68, z + r * .26), (r * .22, r * .08, r * .22), segs=12, rings=8)
        ellipsoid('eyes_open', 'fixed', (1, 1, 1), 'shine2_' + side, (ex - s * r * .22, y + r * .66, z - r * .34), (r * .1, r * .06, r * .1), segs=10, rings=6)
        # Soft blush under the eye (every mood)
        bx, bz = s * (x + r * .55), z - r * 1.3
        ellipsoid('body', 'fixed', (1.0, .5, .56), 'blush_' + side, (bx, front(bx, bz, on, y) - r * .04, bz), (r * .5, r * .1, r * .28), segs=12, rings=8)
        # Surprised: wider white, small pupils
        ring('eyes_wide', 'fixed', lid, 'eyelid_' + side, (ex, y - r * .08, z + r * .06), r * 1.18, r * .06, rot=(math.pi / 2, 0, 0))
        ellipsoid('eyes_wide', 'fixed', WHITE, 'sclera_' + side, (ex, y + .002, z + r * .06), (r * 1.18, r * .6, r * 1.28), segs=20, rings=12)
        ellipsoid('eyes_wide', 'fixed', iris, 'iris_' + side, (ex, y + r * .50, z), (r * .36, r * .2, r * .4))
        ellipsoid('eyes_wide', 'fixed', BLACK, 'pupil_' + side, (ex, y + r * .54, z), (r * .22, r * .2, r * .26))
        ellipsoid('eyes_wide', 'fixed', (1, 1, 1), 'shine_' + side, (ex + s * r * .1, y + r * .62, z + r * .12), (r * .09, r * .06, r * .09), segs=10, rings=6)
        # Happy ^ ^
        ring('eyes_happy', 'fixed', BLACK, 'smile_' + side, (ex, y + r * .45, z - r * .1), r * .62, r * .13, rot=(math.pi / 2, 0, 0), arc=True)
        # Sleepy: a soft downward curve
        ring('eyes_sleepy', 'fixed', BLACK, 'sleep_' + side, (ex, y + r * .45, z - r * .25), r * .55, r * .11, rot=(math.pi / 2, math.pi, 0), arc=True)
        # Brows: sad (inner ends up) and focused (inner ends down)
        for name, angle in (('brows_sad', .38), ('brows_focus', -.42)):
            box(name, 'fixed', BLACK, 'brow_' + side, (ex, y + r * .5, z + r * 1.35), (r * 1.05, r * .2, r * .2), bevel=r * .08,
                rot=(0, s * angle, 0))


def smile(z=.32, width=.04, on=None, tongue=True):
    """An open little mouth with a tongue, set into whatever is in front at that height."""
    y = front(0, z, on) - .004
    ellipsoid('body', 'fixed', (.2, .03, .05), 'mouth', (0, y, z), (width, .014, width * .78))
    if tongue: ellipsoid('body', 'fixed', (.98, .36, .44), 'tongue', (0, y + .009, z - width * .3), (width * .6, .008, width * .36), segs=12, rings=8)


def arm(s, color, channel='main', tip=None, tip_channel='second', shoulder=None, hand=None, r=.04, claws=None, pat=None):
    """A chubby arm with a rounded paw and three toes. s = 1 is the pet's left (+X)."""
    side = 'l' if s > 0 else 'r'; g = 'arm_' + side
    sx = right(.02, .235, [F['body']], F['bx'])
    shoulder = shoulder or (sx - .012, .02, .245); hand = hand or (sx + .03, .075, .125)
    S = Vector((s * shoulder[0], shoulder[1], shoulder[2])); H = Vector((s * hand[0], hand[1], hand[2]))
    mid = S.lerp(H, .5) + Vector((s * .022, 0, 0))
    tube(g, channel, color, 'arm', [S, mid, H], r * .9, r, segs=12, per=3, pat=pat)
    tc = tip or color; tch = tip_channel if tip else channel
    ellipsoid(g, tch, tc, 'paw', H + Vector((0, .006, -.012)), (r * 1.18, r * 1.25, r * 1.05), pat=pat if not tip else None)
    for k in (-1, 0, 1):
        t = H + Vector((k * r * .62, r * .95, -r * .55))
        ellipsoid(g, tch, tc, f'toe{k}', t, (r * .42, r * .5, r * .42), segs=10, rings=6, pat=pat if not tip else None)
        if claws: cone(g, 'fixed', claws, f'claw{k}', t + Vector((0, r * .25, 0)), t + Vector((0, r * .95, -r * .25)), r * .2, verts=6)
    PIV[g] = tuple(Vector((s * shoulder[0], shoulder[1], shoulder[2])))


def foot(s, color, channel='main', toes=None, toe_channel='second', x=.08, claws=None, pat=None):
    side = 'l' if s > 0 else 'r'; g = 'foot_' + side
    ellipsoid(g, channel, color, 'foot', (s * x, .045, .03), (.052, .078, .032), pat=pat)
    tc = toes or color; tch = toe_channel if toes else channel
    for k in (-1, 0, 1):
        t = Vector((s * x + k * .03, .112 - abs(k) * .008, .024))
        ellipsoid(g, tch, tc, f'toe{k}', t, (.021, .026, .022), segs=10, rings=6, pat=pat if not toes else None)
        if claws: cone(g, 'fixed', claws, f'claw{k}', t + Vector((0, .014, 0)), t + Vector((0, .045, -.012)), .009, verts=6)
    PIV[g] = (s * x, .03, .055)


def hoof_leg(s, color, channel, x=.08, hoof=DARK, pat=None):
    side = 'l' if s > 0 else 'r'; g = 'foot_' + side
    tube(g, channel, color, 'leg', [(s * x, .01, .1), (s * x, .02, .05)], .048, .05, segs=12, pat=pat)
    loft(g, 'fixed', hoof, 'hoof', [((s * x, .025, .052), .05, .058), ((s * x, .03, .03), .056, .066), ((s * x, .032, .004), .058, .07), ((s * x, .032, .0), .04, .05)], 14)
    box(g, 'fixed', (.015, .01, .01), 'hoof split', (s * x, .1, .02), (.006, .012, .034), bevel=0)
    PIV[g] = (s * x, .02, .07)


def hoof_arm(s, color, channel, hoof=DARK, pat=None):
    side = 'l' if s > 0 else 'r'; g = 'arm_' + side
    sx = right(.02, .235, [F['body']], F['bx'])
    S = Vector((s * (sx - .012), .02, .245)); H = Vector((s * (sx + .03), .07, .13))
    tube(g, channel, color, 'arm', [S, S.lerp(H, .5) + Vector((s * .022, 0, 0)), H], .037, .04, segs=12, per=3, pat=pat)
    d = (H - S).normalized()
    loft(g, 'fixed', hoof, 'hoof', [(H - d * .012, .042), (H + d * .02, .046), (H + d * .045, .044), (H + d * .052, .03)], 12)
    PIV[g] = tuple(S)


def bird_wing(s, color, feathers=4, length=.2, tip_pat='flight', pat=None, channel='main'):
    """A folded wing hugging the body with layered flight feathers along its lower edge."""
    side = 'l' if s > 0 else 'r'; g = 'arm_' + side
    sx = right(0, .24, [F['body']], F['bx'])
    S = Vector((s * (sx - .008), .0, .262))
    pts = [S, S + Vector((s * .03, -.012, -length * .32)), S + Vector((s * .034, -.035, -length * .68)), S + Vector((s * .02, -.06, -length))]
    widths = (.034, .07, .074, .05, .016)
    q = spline(pts, 2)
    rings = [(c, .028 - .012 * i / (len(q) - 1), widths[min(len(widths) - 1, round(i * (len(widths) - 1) / (len(q) - 1)))]) for i, c in enumerate(q)]
    rings = [(q[0] + Vector((0, 0, .018)), .012, .016)] + rings
    loft(g, channel, color, 'wing', rings, 14, side=(s, 0, 0), pat=pat)
    for k in range(feathers):
        f = k / max(1, feathers - 1)
        root = S + Vector((s * .036, .02 - .05 * f, -length * (.42 + .16 * f)))
        tip = S + Vector((s * (.03 - .012 * f), -.02 - .085 * f, -length * (1.0 + .13 * f)))
        leaf(g, channel, color, f'flight feather{k}', root, tip, .042, normal=(s, 0, 0), camber=.006, thick=.006,
             prof=lambda t: (.5 + .5 * math.sin(math.pi * min(1, t * 1.15))) * (1 - t ** 4), rows=6, cols=4, pat=tip_pat)
    PIV[g] = tuple(S)


def bird_foot(s, color, x=.065, claw=None):
    side = 'l' if s > 0 else 'r'; g = 'foot_' + side
    tube(g, 'fixed', color, 'leg', [(s * x, .02, .07), (s * x, .03, .016)], .014, .013, segs=8, pat='plain')
    for k in (-1, 0, 1):
        tip = (s * x + k * .042, .105 - abs(k) * .012, .01)
        tube(g, 'fixed', color, f'toe{k}', [(s * x, .03, .014), tip], .013, .009, segs=8, pat='plain')
        if claw: cone(g, 'fixed', claw, f'claw{k}', tip, (tip[0] + k * .008, tip[1] + .024, .004), .007, verts=6)
    tube(g, 'fixed', color, 'back toe', [(s * x, .03, .014), (s * x, -.012, .01)], .011, .008, segs=8, pat='plain')
    PIV[g] = (s * x, .025, .05)


def cheek_fluff(color, channel='main', n=3, z=.345, pat=None):
    for s in (1, -1):
        for k in range(n):
            zz = z + (k - (n - 1) / 2) * .034
            x = right(.03, zz, [F['head']], F['hx'])
            tuft('body', channel, color, 'cheek fluff', (s * (x - .03), .03, zz + .01), (s * (x + .026 - abs(k - (n - 1) / 2) * .012), .02, zz - .03), .03, pat=pat)


def backpack(accent, z=.225):
    """Delivery backpack on the back: parcel badge, amber status strip, side pocket and a bedroll in the accent colour."""
    by = back(0, z, [F['body']], -F['by'])
    y = by - .045
    box('body', 'accent', (.56, .38, .2), 'pack', (0, y, z), (.2, .11, .2), bevel=.022, pat='canvas')
    box('body', 'accent', (.46, .3, .16), 'pack_lid', (0, y, z + .1), (.212, .122, .034), bevel=.012, pat='canvas')
    tube('body', 'accent', accent, 'bedroll', [(-.105, y, z + .143), (.105, y, z + .143)], .03, .03, segs=12, pat='cloth')
    for sx in (-.062, .062):
        ring('body', 'fixed', (.2, .12, .07), 'bedroll strap', (sx, y, z + .143), .032, .006, rot=(0, math.pi / 2, 0), pat='leather')
    yb = y - .056
    for k, (dx, dz, sx, sz) in enumerate(((0, .032, .064, .008), (0, -.032, .064, .008), (.028, 0, .008, .064), (-.028, 0, .008, .064), (0, .01, .064, .006))):
        box('body', 'fixed', (.2, .13, .08), f'pack_mark{k}', (dx, yb, z + .005 + dz), (sx, .006, sz), bevel=0)
    box('body', 'fixed', (1.0, .66, .1), 'pack_strip', (0, yb, z - .062), (.11, .008, .018), bevel=.003)
    box('body', 'accent', (.5, .33, .18), 'pack pocket', (.112, y, z - .03), (.03, .08, .1), bevel=.01, pat='canvas')


def harness(z=.178):
    """Chest belt with a glowing buckle in front, harness band round the back."""
    solid = body_parts()
    rx = right(0, z, solid, F['bx']) + .004; ry = front(0, z, solid, F['by']) + .004
    rb = -back(0, z, [F['body']], -F['by']) + .004
    ring('body', 'accent', (.46, .3, .16), 'harness', (0, 0, z), 1.0, .011, scale=(rx, rb, 1.6), rot=(0, 0, math.pi), arc=True, pat='leather')
    ring('body', 'fixed', (.24, .2, .2), 'belt', (0, 0, z), 1.0, .013, scale=(rx, ry, 1.5), arc=True, pat='leather')
    box('body', 'fixed', (.16, .14, .13), 'buckle', (0, ry + .008, z), (.08, .016, .034), bevel=.006)
    box('body', 'fixed', (1.0, .7, .15), 'buckle_glow', (0, ry + .017, z), (.06, .006, .014), bevel=.002)


def cap(accent):
    """Courier cap (accent colour): crown, button, forward brim and a small glowing badge."""
    top = F['top'] + .02
    ellipsoid('body', 'accent', accent, 'cap', (0, -.01, top - .04), (.13, .125, .08), segs=20, rings=12, pat='cloth')
    ellipsoid('body', 'accent', accent, 'cap_button', (0, -.01, top + .038), (.018, .018, .012), segs=12, rings=8, pat='cloth')
    ellipsoid('body', 'accent', accent, 'cap_brim', (0, .095, top - .05), (.1, .075, .013), rot=(-.32, 0, 0), pat='cloth')
    ring('body', 'fixed', (.99, .93, .8), 'cap_band', (0, -.01, top - .062), .122, .008, scale=(1, .96, 1))
    box('body', 'fixed', (1.0, .7, .15), 'cap_badge', (0, .088, top - .015), (.05, .02, .022), bevel=.004, rot=(-.6, 0, 0))


def scarf(accent):
    """A neckerchief that follows the chest: a curved band with its point hanging in front, and a parcel badge."""
    solid = body_parts(); cols = 11; half = .135; z0 = .292
    verts = []
    for layer in (0, 1):
        for row in (0, 1):
            for j in range(cols):
                u = 2 * j / (cols - 1) - 1; x = u * half
                if row == 0: px, pz = x, z0
                else: px, pz = x * .8, z0 - .1 * (1 - abs(u) ** 1.4) - .012
                y = front(px, pz, solid, .1) + .007 - layer * .007
                verts.append((px, y, pz))
    faces = []; stride = 2 * cols
    for layer in (0, 1):
        for j in range(cols - 1):
            a = layer * stride + j; faces.append((a, a + 1, a + cols + 1, a + cols))
    rim = list(range(cols)) + list(range(2 * cols - 1, cols - 1, -1))
    for k, i in enumerate(rim):
        j = rim[(k + 1) % len(rim)]; faces.append((i, j, j + stride, i + stride))
    add(mesh_obj('scarf', verts, faces), 'scarf', 'accent', accent, 'neckerchief', 'cloth')
    by = front(0, .238, solid, .1)
    box('scarf', 'fixed', (.99, .9, .62), 'parcel badge', (0, by + .012, .238), (.036, .012, .036), bevel=.002, rot=(0, math.pi / 4, 0))
    PIV['scarf'] = (0, front(0, z0, solid, .1), z0)


def gear(accent=(.95, .46, .08), with_cap=True):
    harness(); backpack(accent)
    if with_cap: cap(accent)
    scarf(accent)
    sx = -right(.07, .115, [F['body']], F['bx']) + .012
    box('body', 'fixed', (.34, .17, .07), 'satchel', (sx, .075, .115), (.09, .075, .09), bevel=.009, pat='leather')
    box('body', 'fixed', (.58, .31, .12), 'satchel flap', (sx, .119, .139), (.10, .012, .05), bevel=.006, pat='leather')
    box('body', 'fixed', (.95, .7, .25), 'satchel buckle', (sx, .128, .12), (.025, .006, .024), bevel=.002)


# ------------------------------------------------------------------ animals

def penguin():
    main, belly, orange = (.13, .2, .4), (.98, .97, .93), (1.0, .55, .1)
    frame(main, head=(.2, .185, .178), body=(.172, .158, .168), cheek=.06, pear=.2)
    ellipsoid('body', 'second', belly, 'belly', (0, .04, .152), (.138, .132, .142), segs=24, rings=14)
    fy = front(.07, EYE_Z)
    for s, side in ((1, 'l'), (-1, 'r')):
        ellipsoid('body', 'second', belly, 'mask_' + side, (s * .068, fy - .068, EYE_Z - .005), (.092, .082, .104), segs=20, rings=12)
        cx = right(.06, .365, [F['head']])
        ellipsoid('body', 'fixed', (1.0, .7, .16), 'cheek patch', (s * (cx - .01), .06, .365), (.014, .034, .042), pat='plain')
    eyes(.07, EYE_Z, .058, iris=(.16, .45, .8), on=body_parts())
    f0 = front(0, EYE_Z - .06, body_parts())
    loft('body', 'fixed', orange, 'beak', [((0, f0 - .01, EYE_Z - .052), .04, .026), ((0, f0 + .03, EYE_Z - .058), .03, .02), ((0, f0 + .068, EYE_Z - .068), .006, .005)], 12, pat='beak')
    loft('body', 'fixed', (.86, .34, .08), 'beak lower', [((0, f0 - .01, EYE_Z - .074), .032, .016), ((0, f0 + .025, EYE_Z - .076), .022, .011), ((0, f0 + .052, EYE_Z - .076), .005, .004)], 10, pat='beak')
    sx = right(0, .24, [F['body']])
    for s, side in ((1, 'l'), (-1, 'r')):
        S = Vector((s * (sx - .006), .0, .26))
        pts = spline([S, S + Vector((s * .034, -.005, -.06)), S + Vector((s * .05, -.012, -.13)), S + Vector((s * .04, -.02, -.19))], 3)
        w = lambda f: .024 + .05 * math.sin(math.pi * min(1, f * 1.2 + .08)) ** .8 * (1 - f ** 3)
        loft('arm_' + side, 'main', main, 'flipper', [(c, .017, w(i / (len(pts) - 1))) for i, c in enumerate(pts)], 12, side=(s, 0, 0))
        PIV['arm_' + side] = tuple(S)
        g = 'foot_' + side
        ellipsoid(g, 'fixed', orange, 'foot', (s * .068, .07, .016), (.052, .07, .018), pat='beak')
        for k in (-1, 0, 1):
            ellipsoid(g, 'fixed', orange, f'toe{k}', (s * .068 + k * .03, .128, .015), (.019, .03, .014), segs=10, rings=6, pat='beak')
        PIV[g] = (s * .068, .03, .05)
    by = back(0, .06, [F['body']])
    loft('tail', 'main', main, 'tail', [((0, by + .03, .075), .05, .03), ((0, by - .02, .05), .042, .02), ((0, by - .06, .03), .008, .006)], 10)
    PIV['tail'] = (0, by + .02, .07)
    gear()


def chicken():
    main, red, yellow = (1.0, .96, .88), (.92, .12, .1), (1.0, .72, .1)
    frame(main, head=(.19, .175, .17), body=(.176, .166, .168), cheek=.05, pear=.22)
    eyes(.07, EYE_Z, .054, iris=(.9, .42, .08))
    f0 = front(0, EYE_Z - .06)
    loft('body', 'fixed', yellow, 'beak', [((0, f0 - .01, EYE_Z - .05), .034, .024), ((0, f0 + .03, EYE_Z - .058), .022, .016), ((0, f0 + .062, EYE_Z - .068), .004, .004)], 12, pat='beak')
    loft('body', 'fixed', (.95, .5, .08), 'beak lower', [((0, f0 - .01, EYE_Z - .07), .026, .013), ((0, f0 + .022, EYE_Z - .073), .017, .009), ((0, f0 + .044, EYE_Z - .074), .004, .003)], 10, pat='beak')
    for s in (1, -1):
        ellipsoid('body', 'second', red, 'wattle', (s * .017, f0 + .002, EYE_Z - .108), (.017, .014, .034), pat='comb')
    for k, (y, z, r) in enumerate(((.07, .0, .03), (.025, .028, .04), (-.03, .034, .044), (-.085, .012, .036))):
        ellipsoid('body', 'second', red, f'comb{k}', (0, y, F['top'] + .055 + z), (.02, r, r * 1.1), pat='comb')
    for s in (1, -1):
        bird_wing(s, main, feathers=4, pat=None)
        bird_foot(s, yellow, .062)
    by = back(0, .2, [F['body']])
    for k, a in enumerate((-1.45, -1.1, -.75, .75, 1.1, 1.45)):   # two fans, either side of the backpack
        leaf('tail', 'main', main, f'tail feather{k}', (a * .085, by + .03, .12), (a * .21, by - .075, .33 - (abs(a) - .75) * .14), .05,
             normal=(0, -1, .3), camber=.012, thick=.007, curl=.12, rows=8, cols=4,
             prof=lambda t: math.sin(math.pi * min(1, t * .8 + .12)) ** .6 * (1 - t ** 6), pat='tailfeather')
    PIV['tail'] = (0, by + .02, .1)
    gear(accent=(.16, .5, .92))


def cat():
    main, light = (.96, .56, .16), (1.0, .93, .8)
    frame(main, head=(.205, .176, .165), body=(.156, .143, .16), cheek=.16, pear=.16)
    ellipsoid('body', 'second', light, 'belly', (0, .05, .145), (.112, .105, .125), segs=20, rings=12)
    f0 = front(0, EYE_Z - .068)
    for s in (1, -1):
        ellipsoid('body', 'second', light, 'muzzle', (s * .03, f0 - .016, EYE_Z - .066), (.046, .036, .036))
    ellipsoid('body', 'second', light, 'chin', (0, f0 - .02, EYE_Z - .098), (.04, .03, .026))
    eyes(.078, EYE_Z + .005, .057, iris=(.42, .82, .2))
    ellipsoid('body', 'fixed', PINK, 'nose', (0, f0 + .018, EYE_Z - .05), (.018, .011, .012))
    smile(EYE_Z - .09, .02, body_parts())
    top = F['top']
    for s, side in ((1, 'l'), (-1, 'r')):
        base = Vector((s * .118, -.005, top - .06)); tip = Vector((s * .172, .0, top + .095))
        loft('body', 'main', main, 'ear_' + side, [(base, .068, .04), (base.lerp(tip, .45), .046, .026), (base.lerp(tip, .82), .018, .011), (tip, .003, .003)], 12, side=(1, 0, 0))
        leaf('body', 'fixed', PINK, 'ear_in_' + side, base + Vector((0, .03, .012)), tip + Vector((-s * .01, .012, -.022)), .034, normal=(0, 1, 0), camber=.002, thick=.004, rows=6, cols=3)
        tuft('body', 'second', light, 'ear tuft', base + Vector((-s * .012, .03, .02)), base + Vector((s * .012, .045, .075)), .014)
        for k in (-1, 0, 1):
            cone('body', 'fixed', (.98, .97, .92), f'whisker_{side}{k}', (s * .055, f0 + .004, EYE_Z - .066 + k * .008),
                 (s * .215, f0 - .02, EYE_Z - .06 + k * .034), .0032, verts=5, r2=.001)
        arm(s, main, tip=light)
        foot(s, main, toes=light)
    cheek_fluff(main)
    by = back(0, .07, [F['body']])
    tube('tail', 'main', main, 'tail', [(.03, by + .03, .06), (.14, by - .06, .075), (.215, by - .05, .17), (.2, by + .01, .265)], .033, .028, segs=12, per=5)
    ellipsoid('tail', 'second', light, 'tail tip', (.192, by + .028, .288), (.034, .034, .04))
    PIV['tail'] = (.03, by + .03, .06)
    gear(accent=(.08, .62, .58))


def owl():
    main, disc, amber, rim = (.6, .37, .18), (1.0, .88, .68), (1.0, .6, .03), (.8, .42, .14)
    frame(main, head=(.215, .186, .17), body=(.178, .163, .17), cheek=.08, pear=.2)
    ellipsoid('body', 'second', disc, 'belly', (0, .045, .15), (.128, .13, .138), segs=24, rings=14)
    fy = front(.078, EYE_Z)
    for s, side in ((1, 'l'), (-1, 'r')):
        ellipsoid('body', 'second', disc, 'disc_' + side, (s * .078, fy - .05, EYE_Z), (.096, .06, .104), segs=20, rings=12, pat='disc')
        ring('body', 'fixed', rim, 'disc rim', (s * .078, fy - .034, EYE_Z), .097, .011, scale=(1, 1.07, 1), rot=(math.pi / 2, 0, 0), pat='plain')
    eyes(.078, EYE_Z, .064, iris=amber, on=body_parts())
    f0 = front(0, EYE_Z - .05, body_parts())
    tube('body', 'fixed', (.36, .3, .22), 'beak', [(0, f0 - .008, EYE_Z - .03), (0, f0 + .022, EYE_Z - .062), (0, f0 + .01, EYE_Z - .1)], .02, .004, segs=10, per=3, pat='beak')
    top = F['top']
    for s, side in ((1, 'l'), (-1, 'r')):
        base = Vector((s * .1, .02, top - .05))
        for k, (dx, dz, r) in enumerate(((.105, .1, .042), (.075, .115, .03), (.125, .07, .03))):
            tuft('body', 'main', main, f'ear tuft{k}', base + Vector((s * k * .012, -k * .012, 0)), base + Vector((s * dx, -.03 - k * .01, dz)), r)
        bird_wing(s, main, feathers=5, length=.215, tip_pat='owlflight')
        bird_foot(s, (1.0, .74, .16), .066, claw=(.25, .2, .16))
    by = back(0, .09, [F['body']])
    for k, a in enumerate((-.7, 0, .7)):
        leaf('tail', 'main', main, f'tail feather{k}', (a * .025, by + .035, .11), (a * .075, by - .075, .012 + abs(a) * .015), .036,
             normal=(0, -.5, 1), camber=.008, thick=.006, prof=lambda t: (.5 + .5 * math.sin(math.pi * min(1, t * 1.1))) * (1 - t ** 5), rows=6, pat='owlflight')
    PIV['tail'] = (0, by + .03, .1)
    for k in range(7):
        a = (k - 3) * .26
        x = math.sin(a) * .135
        tuft('body', 'main', main, 'ruff', (x, front(x, .262, [F['body']]) - .02, .268), (x * 1.16, front(x, .225, [F['body']]) + .012, .222), .026)
    gear(accent=(.2, .58, .28))


def beaver():
    main, light, dark = (.66, .36, .13), (.96, .76, .5), (.27, .19, .15)
    frame(main, head=(.2, .18, .165), body=(.186, .17, .172), cheek=.15, pear=.22)
    ellipsoid('body', 'second', light, 'belly', (0, .05, .152), (.135, .13, .138), segs=22, rings=12)
    f0 = front(0, EYE_Z - .07)
    for s in (1, -1):
        ellipsoid('body', 'second', light, 'muzzle', (s * .034, f0 - .014, EYE_Z - .068), (.054, .042, .044))
    eyes(.074, EYE_Z + .01, .054, iris=(.3, .17, .07))
    ellipsoid('body', 'fixed', (.16, .09, .08), 'nose', (0, f0 + .024, EYE_Z - .044), (.032, .018, .022))
    for s in (1, -1):
        box('body', 'fixed', (1.0, .62, .16), 'tooth', (s * .0125, f0 + .012, EYE_Z - .122), (.023, .012, .05), bevel=.005)
    top = F['top']
    for s, side in ((1, 'l'), (-1, 'r')):
        ex = right(-.01, top - .05, [F['head']])
        ellipsoid('body', 'main', main, 'ear_' + side, (s * (ex - .005), -.01, top - .03), (.04, .024, .04))
        ellipsoid('body', 'fixed', (.4, .2, .16), 'ear_in_' + side, (s * (ex - .003), .008, top - .032), (.024, .012, .025), segs=12, rings=8)
        arm(s, main, tip=dark, tip_channel='fixed', hand=(right(.02, .235, [F['body']]) + .02, .095, .14))
        g = 'foot_' + side
        ellipsoid(g, 'fixed', dark, 'foot', (s * .085, .05, .022), (.058, .085, .024), pat='beavertail')
        for k in (-1, 0, 1):
            ellipsoid(g, 'fixed', dark, f'toe{k}', (s * .085 + k * .036, .128, .018), (.021, .03, .016), segs=10, rings=6, pat='beavertail')
        PIV[g] = (s * .085, .04, .05)
    cheek_fluff(main)
    by = back(0, .06, [F['body']])
    path = spline([(0, by + .04, .07), (0, by - .06, .04), (0, by - .16, .03), (0, by - .25, .034)], 4)
    w = lambda f: .03 + .095 * math.sin(math.pi * min(1, f * .95 + .08)) ** .7
    loft('tail', 'fixed', dark, 'tail', [(c, w(i / (len(path) - 1)) * (1 - (i / (len(path) - 1)) ** 6 * .7), .02) for i, c in enumerate(path)], 14, side=(1, 0, 0), pat='beavertail')
    PIV['tail'] = (0, by + .03, .06)
    gear(accent=(.88, .18, .14))


ANIMALS = {'penguin': penguin, 'chicken': chicken, 'cat': cat, 'owl': owl, 'beaver': beaver}
# Species default patterns for the (main, second) recolour slots.
DEFAULT_PAT = {'penguin': ('pfeather', 'pbelly'), 'chicken': ('cfeather', 'comb'), 'cat': ('tabby', 'fur'),
               'owl': ('ofeather', 'obelly'), 'beaver': ('bfur', 'fur')}

from courier_species import install
install(globals())


# ------------------------------------------------------------------ bake: colour patterns x occlusion

CLEAN_GROUPS = ('eyes_open', 'eyes_happy', 'eyes_wide', 'eyes_sleepy', 'brows_sad', 'brows_focus')
W = (1.0, 1.0, 1.0)


def _out(node, *names):
    for n in names:
        if n in node.outputs: return node.outputs[n]
    return node.outputs[0]


class Pat:
    """Small helper over a shader node tree: scalar masks and colour mixes in model space (metres)."""
    def __init__(self, nt):
        self.nt = nt
        geo = nt.nodes.new('ShaderNodeNewGeometry'); self.pos = geo.outputs['Position']; self.pointy = geo.outputs['Pointiness']
        sep = nt.nodes.new('ShaderNodeSeparateXYZ'); nt.links.new(self.pos, sep.inputs[0])
        self.x, self.y, self.z = sep.outputs[0], sep.outputs[1], sep.outputs[2]
        self.absx = self.m('ABSOLUTE', self.x)

    def _set(self, socket, value):
        if isinstance(value, (int, float)): socket.default_value = float(value)
        elif isinstance(value, tuple): socket.default_value = (*value, 1.0) if len(value) == 3 else value
        else: self.nt.links.new(value, socket)

    def m(self, op, a, b=0.0):
        n = self.nt.nodes.new('ShaderNodeMath'); n.operation = op; n.use_clamp = False
        self._set(n.inputs[0], a); self._set(n.inputs[1], b)
        return n.outputs[0]

    def rng(self, v, a, b, c=0.0, d=1.0):
        """v mapped from a..b to c..d, clamped (a may be above b for a falling ramp)."""
        n = self.nt.nodes.new('ShaderNodeMapRange'); n.clamp = True; n.interpolation_type = 'SMOOTHSTEP' if a < b else 'LINEAR'
        if a > b: a, b, c, d = b, a, d, c
        self._set(n.inputs['Value'], v)
        n.inputs['From Min'].default_value = a; n.inputs['From Max'].default_value = b
        n.inputs['To Min'].default_value = c; n.inputs['To Max'].default_value = d
        return n.outputs['Result']

    def _vec(self, stretch, offset=(0, 0, 0)):
        mp = self.nt.nodes.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value = stretch; mp.inputs['Location'].default_value = offset
        self.nt.links.new(self.pos, mp.inputs['Vector']); return mp.outputs[0]

    def noise(self, scale, stretch=(1, 1, 1), detail=2.0, rough=.5, offset=(0, 0, 0)):
        n = self.nt.nodes.new('ShaderNodeTexNoise')
        n.inputs['Scale'].default_value = scale; n.inputs['Detail'].default_value = detail; n.inputs['Roughness'].default_value = rough
        self.nt.links.new(self._vec(stretch, offset), n.inputs['Vector'])
        return _out(n, 'Fac', 'Factor')

    def vor(self, scale, stretch=(1, 1, 1), feature='F1', color=False):
        n = self.nt.nodes.new('ShaderNodeTexVoronoi'); n.feature = feature; n.inputs['Scale'].default_value = scale
        self.nt.links.new(self._vec(stretch), n.inputs['Vector'])
        return n.outputs['Color'] if color else n.outputs['Distance']

    def wave(self, scale, axis='Z', distortion=0.0, dscale=1.0, stretch=(1, 1, 1)):
        n = self.nt.nodes.new('ShaderNodeTexWave'); n.wave_type = 'BANDS'; n.bands_direction = axis
        n.inputs['Scale'].default_value = scale; n.inputs['Distortion'].default_value = distortion; n.inputs['Detail Scale'].default_value = dscale
        self.nt.links.new(self._vec(stretch), n.inputs['Vector'])
        return _out(n, 'Fac', 'Factor')

    def mix(self, fac, a, b, blend='MIX'):
        n = self.nt.nodes.new('ShaderNodeMix'); n.data_type = 'RGBA'; n.blend_type = blend; n.clamp_factor = True
        self._set(n.inputs[0], fac); self._set(n.inputs[6], a); self._set(n.inputs[7], b)
        return n.outputs[2]

    def mul(self, *cols):
        res = cols[0]
        for c in cols[1:]: res = self.mix(1.0, res, c, 'MULTIPLY')
        return res

    def both(self, a, b): return self.m('MULTIPLY', a, b)

    def either(self, a, b): return self.m('MAXIMUM', a, b)


def pattern(p, pat):
    """The colour multiplier (white = the part's own colour) for one named surface."""
    def fur(dark=(.68, .58, .52), scale=105.0, clump=(.84, .8, .78)):
        strands = p.rng(p.noise(scale, (2, 2, .45), 3.0, .7), .3, .7)
        return p.mul(p.mix(strands, dark, W), p.mix(p.rng(p.noise(16.0, detail=2.0), .35, .7), clump, W))

    def barbs(dark=(.82, .82, .85)):
        return p.mix(p.rng(p.noise(150.0, (2, 2, .4), 2.0), .3, .7), dark, W)

    def scallop(scale, edge, inner=W, stretch=(1, 1, 1.5)):
        d = p.vor(scale, stretch, 'DISTANCE_TO_EDGE')   # outlines round each feather/scale
        soft = p.vor(scale, stretch)                    # and a soft shade away from its centre
        return p.mul(p.mix(p.rng(d, .0, .13), edge, inner), p.mix(p.m('MULTIPLY', p.rng(soft, .25, .75), .45), W, edge), barbs())

    if pat == 'fur': return fur()
    if pat == 'tabby':   # ginger tabby: dark rings round the body, limbs and tail, forehead stripes running back
        body = p.rng(p.wave(8.5, 'Z', 4.0, 1.4), .6, .78)
        body = p.both(body, p.rng(p.y, .1, .03))                       # none on the chest and face
        brow = p.both(p.rng(p.wave(9.0, 'X', 1.5, 1.0), .62, .8), p.both(p.both(p.rng(p.z, .475, .52), p.rng(p.z, .57, .55)), p.rng(p.y, -.12, -.04)))
        return p.mul(fur((.74, .6, .5)), p.mix(p.either(body, brow), W, (.5, .3, .2)))
    if pat == 'cow':     # big irregular dark patches; the face stays clean
        mask = p.both(p.rng(p.noise(6.2, detail=1.0, offset=(.3, .1, .7)), .52, .56), p.rng(p.y, .13, .08))
        return p.mul(fur((.8, .74, .7)), p.mix(mask, W, (.2, .12, .1)))
    if pat == 'dog':     # a darker saddle over the back
        ragged = p.m('ADD', p.y, p.m('MULTIPLY', p.m('SUBTRACT', p.noise(14.0), .5), .06))
        saddle = p.both(p.rng(ragged, -.03, -.075), p.both(p.rng(p.z, .07, .13), p.rng(p.z, .36, .31)))
        return p.mul(fur((.74, .62, .52)), p.mix(saddle, W, (.52, .3, .18)))
    if pat == 'bfur':    # coarse wet beaver coat
        return p.mul(fur((.6, .47, .38), 70.0), p.mix(p.rng(p.noise(40.0, (1, 1, 3), 2.0), .3, .7), (.8, .72, .66), W))
    if pat == 'beavertail':
        d = p.vor(52.0, (1, 1, 1), 'DISTANCE_TO_EDGE')
        return p.mul(p.mix(p.rng(d, 0.0, .09), (.42, .36, .36), W), p.mix(p.noise(9.0), (.82, .8, .84), W))
    if pat == 'wool':    # tight curls with a hint of cotton-candy colour
        d = p.vor(40.0, (1, 1, 1), 'DISTANCE_TO_EDGE')
        return p.mul(p.mix(p.rng(d, .02, .17), (.68, .6, .66), W), p.mix(p.noise(4.5, detail=1.0), (1.0, .86, .9), (.88, .94, 1.0)),
                     p.mix(p.rng(p.noise(160.0), .25, .75), (.9, .88, .9), W))
    if pat == 'pfeather':  # sleek penguin: fine scallops and a blue sheen low down
        return p.mul(scallop(62.0, (.66, .74, .96)), p.mix(p.rng(p.z, .02, .4), (.72, .84, 1.0), W))
    if pat == 'pbelly':    # white front with a warm yellow bib under the chin
        bib = p.both(p.rng(p.z, .2, .262), p.rng(p.z, .33, .29))
        return p.mul(scallop(62.0, (.86, .88, .95)), p.mix(bib, W, (1.0, .84, .36)))
    if pat == 'cfeather':  # hen: cream scallops, a golden hackle round the neck, rusty speckles on the back
        hackle = p.both(p.rng(p.z, .215, .262), p.rng(p.z, .35, .3))
        speck = p.both(p.rng(p.vor(46.0, (1, 1, 1.5)), .22, .1), p.rng(p.y, -.02, -.1))
        return p.mul(scallop(46.0, (.8, .68, .54)), p.mix(hackle, W, (1.0, .78, .42)), p.mix(p.m('MULTIPLY', speck, .7), W, (.72, .4, .22)))
    if pat == 'flight':    # wing feathers with rust tips
        return p.mul(barbs((.78, .76, .74)), p.mix(p.rng(p.z, .15, .07), W, (.8, .44, .2)))
    if pat == 'tailfeather':  # tail fan: rust shafts shading to teal tips
        return p.mul(barbs((.74, .74, .76)), p.mix(p.rng(p.z, .13, .21), (.86, .46, .2), W), p.mix(p.rng(p.z, .22, .31), W, (.16, .62, .58)))
    if pat == 'ofeather':  # owl: bold scallops with pale centres and darker bars
        bars = p.rng(p.wave(15.0, 'Z', 1.5, 2.0), .62, .82)
        return p.mul(scallop(33.0, (.42, .3, .24), (1.0, .95, .84)), p.mix(p.m('MULTIPLY', bars, .55), W, (.5, .38, .3)))
    if pat == 'owlflight':  # barred flight feathers
        bars = p.rng(p.wave(13.0, 'Z', .6, 1.0), .55, .7)
        return p.mul(barbs((.76, .72, .7)), p.mix(bars, W, (.42, .3, .22)), p.mix(p.rng(p.z, .09, .02), W, (1.0, .9, .72)))
    if pat == 'obelly':    # pale belly with dark arrow streaks
        streak = p.both(p.rng(p.vor(27.0, (1, 1, .42)), .26, .15), p.rng(p.z, .3, .26))
        return p.mul(barbs((.86, .84, .82)), p.mix(streak, W, (.5, .32, .2)))
    if pat == 'disc':      # facial disc: fine rays, darker towards the rim
        return p.mul(barbs((.86, .82, .78)), p.mix(p.rng(p.absx, .11, .17), W, (.86, .68, .5)))
    if pat == 'comb': return p.mix(p.rng(p.noise(60.0, detail=3.0), .3, .7), (.8, .72, .74), W)
    if pat == 'beak': return p.mul(p.mix(p.rng(p.noise(25.0, (1, .3, 1)), .3, .7), (.86, .8, .76), W), p.mix(p.rng(p.z, .0, .03), (.8, .66, .6), W))
    if pat == 'skin':      # soft skin with a warm flush
        return p.mul(p.mix(p.rng(p.noise(9.0), .35, .7), (1.0, .88, .86), W), p.mix(p.rng(p.z, .0, .12), (.92, .8, .8), W))
    if pat == 'devilskin':  # hot red shading to plum on the limbs, mottled
        return p.mul(p.mix(p.rng(p.z, .0, .2), (.58, .3, .52), W), p.mix(p.rng(p.noise(24.0, detail=3.0), .35, .7), (.78, .7, .72), W),
                     p.mix(p.rng(p.z, .44, .58), W, (1.0, .82, .7)))
    if pat == 'hair':      # fairy hair: strands, pink at the tips
        strands = p.rng(p.noise(90.0, (3, 3, .3), 2.0), .3, .7)
        return p.mul(p.mix(strands, (.68, .64, .78), W), p.mix(p.rng(p.z, .5, .42), W, (1.0, .62, .8)))
    if pat == 'goldhair':
        strands = p.rng(p.noise(80.0, (3, 3, .4), 2.0), .3, .7)
        return p.mul(p.mix(strands, (.78, .62, .44), W), p.mix(p.rng(p.noise(12.0), .35, .7), (1.0, .86, .7), W))
    if pat == 'horn':      # ringed horn
        return p.mul(p.mix(p.rng(p.wave(34.0, 'Z', .5, 1.0), .5, .9), W, (.66, .56, .5)), p.mix(p.rng(p.z, .52, .64), (.82, .76, .76), W))
    if pat == 'membrane':  # bat/dragon wing skin: dark at the arm, lighter and warmer towards the trailing edge
        streak = p.rng(p.noise(46.0, (.25, 1, 1), 2.0), .3, .7)
        return p.mul(p.mix(streak, (.72, .68, .74), W), p.mix(p.rng(p.absx, .13, .36), (.6, .52, .68), W), p.mix(p.rng(p.z, .34, .18), W, (1.0, .84, .72)))
    if pat == 'fairywing':  # dragonfly cells, pink low and cyan high, golden tips
        d = p.vor(40.0, (1, 1, 1), 'DISTANCE_TO_EDGE')
        return p.mul(p.mix(p.rng(d, 0.0, .07), (.46, .38, .74), W), p.mix(p.rng(p.z, .16, .46), (1.0, .6, .82), (.6, .92, 1.0)),
                     p.mix(p.rng(p.absx, .31, .4), W, (1.0, .92, .5)))
    if pat == 'angelwing':  # white feathers: cool at the root, gold-dipped tips
        return p.mul(barbs((.84, .84, .88)), p.mix(p.rng(p.absx, .22, .13), W, (.78, .86, 1.0)), p.mix(p.rng(p.z, .25, .15), W, (1.0, .8, .42)))
    if pat == 'robe':       # soft folds, a gold hem
        folds = p.rng(p.wave(9.0, 'X', 1.0, 1.0), .2, .8)
        return p.mul(p.mix(folds, (.84, .86, .96), W), p.mix(p.rng(p.z, .075, .05), W, (1.0, .76, .26)))
    if pat == 'scales':     # dragon hide: overlapping scales with a teal shimmer
        return p.mul(scallop(31.0, (.42, .32, .66), stretch=(1, 1, 1.3)), p.mix(p.rng(p.noise(7.0, detail=1.0), .4, .65), (.62, 1.0, .96), (1.0, .86, 1.0)),
                     p.mix(p.rng(p.z, .0, .16), (.7, .62, .86), W))
    if pat == 'plates':     # belly plates: ridged bands
        return p.mul(p.mix(p.rng(p.wave(7.6, 'Z'), .0, .22), (.56, .34, .2), W), p.mix(p.rng(p.noise(30.0), .3, .7), (.88, .84, .8), W))
    if pat == 'leafskirt':
        veins = p.rng(p.noise(70.0, (3, 3, .3), 2.0), .3, .7)
        return p.mul(p.mix(veins, (.66, .8, .56), W), p.mix(p.rng(p.z, .04, .16), (1.0, .96, .5), W))
    if pat == 'canvas' or pat == 'cloth':   # weave and scuffs
        w1 = p.wave(60.0, 'X'); w2 = p.wave(60.0, 'Z')
        weave = p.mix(p.both(w1, w2), (.86, .85, .84), W)
        return p.mul(weave, p.mix(p.rng(p.noise(14.0, detail=6.0), .35, .75), (.8, .78, .76) if pat == 'canvas' else (.88, .86, .86), W))
    if pat == 'leather':
        return p.mix(p.rng(p.noise(38.0, detail=4.0), .3, .7), (.72, .68, .66), W)
    return p.mix(p.noise(30.0), (.9, .9, .9), W)   # 'plain'


def merge_parts(parts):
    """One renderer per group / colour slot / colour / pattern."""
    buckets = {}
    for o in parts:
        group, channel, _ = o.name.split('|', 2)
        key = (group, channel, tuple(round(v, 5) for v in o['ihm_color']), o['ihm_pat'])
        buckets.setdefault(key, []).append(o)
    out = []
    for (group, channel, color, pat), obs in buckets.items():
        bpy.ops.object.select_all(action='DESELECT')
        for o in obs: o.select_set(True)
        bpy.context.view_layer.objects.active = obs[0]
        if len(obs) > 1: bpy.ops.object.join()
        o = bpy.context.object; o.name = f'{group}|{channel}|surface_{len(out)}'
        out.append(o)
    return out


def save_image(img, tries=6):
    """Saves a texture, waiting out a short lock (a build or a viewer reading src/Art at the same moment)."""
    import time
    for attempt in range(tries):
        try: img.save(); return
        except RuntimeError:
            if attempt == tries - 1: raise
            time.sleep(1.5)


def bake_atlas(key, animal, parts, size=ATLAS):
    """Packs the surface parts into one atlas and bakes pattern x occlusion x a little floor dust into it."""
    targets = [o for o in parts if o.name.split('|')[0] not in CLEAN_GROUPS]
    for o in targets: o['ihm_texture'] = f'{key}_atlas.png'; o['ihm_atlas'] = True
    img = bpy.data.images.new(key + '_atlas', size, size, alpha=False)
    bpy.ops.object.select_all(action='DESELECT')
    for o in targets:
        o.select_set(True)
        for layer in list(o.data.uv_layers): o.data.uv_layers.remove(layer)
        o.data.uv_layers.new(name='Atlas')
    bpy.context.view_layer.objects.active = targets[0]
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.004, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=.004, rotate=True)
    bpy.ops.object.mode_set(mode='OBJECT')

    mats = {}
    defaults = DEFAULT_PAT[animal]
    for o in targets:
        channel = o.name.split('|')[1]
        pat = o['ihm_pat'] or {'main': defaults[0], 'second': defaults[1], 'accent': 'cloth'}.get(channel, 'plain')
        if pat not in mats:
            m = bpy.data.materials.new(f'{key} bake {pat}'); m.use_nodes = True; nt = m.node_tree
            for n in list(nt.nodes): nt.nodes.remove(n)
            out = nt.nodes.new('ShaderNodeOutputMaterial'); emit = nt.nodes.new('ShaderNodeEmission')
            p = Pat(nt)
            value = pattern(p, pat)
            ao = nt.nodes.new('ShaderNodeAmbientOcclusion'); ao.inputs['Distance'].default_value = .05; ao.samples = 8
            value = p.mul(value, p.mix(p.rng(ao.outputs['AO'], .15, 1.0), (.54, .5, .62), W),          # cool, coloured shade in the folds
                          p.mix(p.rng(p.z, .0, .09), (.84, .8, .76), W),                               # a little floor dust
                          p.mix(p.rng(p.pointy, .44, .5), (.86, .84, .86), W))                         # creases
            nt.links.new(value, emit.inputs['Color']); nt.links.new(emit.outputs[0], out.inputs['Surface'])
            tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = img; nt.nodes.active = tex
            mats[pat] = m
        o.data.materials.clear(); o.data.materials.append(mats[pat])
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 16
    scene.render.bake.margin = 6; scene.render.bake.use_clear = True
    bpy.ops.object.bake(type='EMIT')
    img.filepath_raw = str(OUT / f'{key}_atlas.png'); img.file_format = 'PNG'; save_image(img)
    # Small surface-normal relief from the pattern's brightness; flat where there is no detail.
    pixels = np.array(img.pixels[:], dtype=np.float32).reshape(size, size, 4)[..., :3].mean(axis=2)
    dx = np.clip((np.roll(pixels, -1, 1) - np.roll(pixels, 1, 1)) * 1.2, -.20, .20)
    dy = np.clip((np.roll(pixels, -1, 0) - np.roll(pixels, 1, 0)) * 1.2, -.20, .20)
    normal = np.ones((size, size, 4), dtype=np.float32)
    normal[..., 0] = .5 - dx; normal[..., 1] = .5 - dy; normal[..., 2] = np.sqrt(np.maximum(0, 1 - 4 * dx * dx - 4 * dy * dy)) * .5 + .5
    ni = bpy.data.images.new(key + '_normal', size, size, alpha=False)
    ni.colorspace_settings.name = 'Non-Color'; ni.pixels.foreach_set(normal.ravel())
    ni.filepath_raw = str(OUT / f'{key}_normal.png'); ni.file_format = 'PNG'; save_image(ni)
    for m in mats.values(): bpy.data.materials.remove(m)
    bpy.data.images.remove(img); bpy.data.images.remove(ni)


# ------------------------------------------------------------------ export

def vec(v): return {'x': round(v[0], 5), 'y': round(v[1], 5), 'z': round(v[2], 5)}


def export(key, parts, pivots):
    """Writes the model and checks it: finite numbers, and every triangle's winding agreeing with its normals."""
    data = {'version': 1, 'fps': 30, 'frames': 1, 'parts': [], 'pivots': []}
    for name, p in pivots.items():
        data['pivots'].append({'name': name, 'position': vec(C @ Vector(p))})
    flipped = total = nverts = 0
    for o in parts:
        mesh = o.data; mesh.calc_loop_triangles()
        uv_layer = mesh.uv_layers.active
        verts, normals, uv, tris = [], [], [], []
        index = {}
        for tri in mesh.loop_triangles:
            corner = []
            for li in reversed(tri.loops):
                loop = mesh.loops[li]
                co = C @ mesh.vertices[loop.vertex_index].co
                n = C @ mesh.corner_normals[li].vector
                t = uv_layer.data[li].uv if uv_layer else (0, 0)
                k = (loop.vertex_index, round(n.x, 3), round(n.y, 3), round(n.z, 3), round(t[0], 4), round(t[1], 4))
                if k not in index:
                    index[k] = len(verts); verts.append(vec(co)); normals.append(vec(n))
                    uv.append({'x': round(float(t[0]), 5), 'y': round(float(t[1]), 5)})
                tris.append(index[k]); corner.append((co, n))
            # Unity draws clockwise faces: (b - a) x (c - a) of the exported order must point along the normals.
            (a, na), (b, nb), (c, nc) = corner
            face = (b - a).cross(c - a)
            if face.length > 1e-12:
                total += 1
                if face.normalized().dot((na + nb + nc).normalized()) < -.15: flipped += 1
        for v in verts + normals + uv:
            if not all(math.isfinite(c) for c in v.values()): raise SystemExit(f'{key}/{o.name}: non-finite geometry')
        if len(verts) > 65535: raise SystemExit(f'{key}/{o.name}: more than 65535 vertices in one part')
        nverts += len(verts)
        c = o['ihm_color']
        data['parts'].append({'name': o.name, 'vertices': verts, 'normals': normals, 'uv': uv, 'triangles': tris,
                              'color': {'r': c[0], 'g': c[1], 'b': c[2], 'a': 1}, 'texture': o['ihm_texture'],
                              'rest': {'position': vec((0, 0, 0)), 'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'scale': vec((1, 1, 1))},
                              'samples': []})
    if flipped > total * .002: raise SystemExit(f'{key}: {flipped} of {total} triangles wind against their normals')
    if nverts > 30000: raise SystemExit(f'{key}: {nverts} vertices is too heavy for a pet')
    (OUT / f'{key}.mesh.json').write_text(json.dumps(data, separators=(',', ':')), encoding='utf8')
    return {'parts': len(parts), 'vertices': nverts, 'triangles': total, 'flipped': flipped}


only = [k for k in opts.only.split(',') if k]
unknown = [k for k in only if k not in ANIMALS]
if unknown: raise SystemExit(f'Unknown pets: {unknown}; known: {list(ANIMALS)}')
clear()
totals = {}
for key, build in ANIMALS.items():
    if only and key not in only: continue
    PARTS.clear()
    build()
    for name in ('arm_l', 'arm_r', 'foot_l', 'foot_r', 'tail', 'scarf'):
        if name not in PIV: raise SystemExit(f'{key}: no pivot for {name}')
    merged = merge_parts(list(PARTS))
    bake_atlas('courier_' + key, key, merged)
    totals[key] = export('courier_' + key, merged, dict(PIV))
    clear()
    print('COURIER_EXPORTED', key, totals[key], flush=True)

if not opts.no_render:
    from render_courier import render
    render(ROOT, list(totals), ART)

print('IHM_COURIER_OK', totals)
