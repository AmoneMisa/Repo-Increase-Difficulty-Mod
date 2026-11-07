"""Blender 5.2: --background --factory-startup --python tools/build_starlight.py -- --root PATH [--base JSON] [--preview]

The Starlight Gown set: seven original cosmetics after the user's starry evening-gown concept (deep-blue galaxy
fabric with glowing stars, blue lace, silver piping, a choker with a ring brooch, a belted high-low skirt with
lace hems, lace sleeves, thigh-high galaxy boots with gems, drop earrings), fitted to the R.E.P.O. robot:

  starlight_bodice   Body Top     galaxy bodice, lace side panels, silver piping, choker and ring brooch
  starlight_skirt    Body Bottom  high-low galaxy underskirt + open-front overskirt, lace hems, belt and buckle
  starlight_sleeve_r Arm Right    lace sleeve with a lace cuff
  starlight_sleeve_l Arm Left
  starlight_boot_r   Leg Right    thigh-high galaxy boot, lace top, silver seam, front gem
  starlight_boot_l   Leg Left
  starlight_earrings Ears         silver studs, short chains, drop gems

Galaxy, lace and gems are tintable (the colour picked for the slot); the galaxy's stars glow in that colour too
(materials with 'tintEmission' take the tint on their emission). Lining, belt leather and silver keep their colours.
Same conventions as tools/build_shark.py: parts are built in their slot's own frame in Unity coordinates and
exported with Unity (x, y, z) = Blender (x, z, y).

Writes src/Art/v11_<key>.mesh.json, src/Art/cosmetic_<key>_icon.png, src/Art/starlight_*.png textures and, with
--preview (needs --base, the game's base meshes as a grey stand-in), art/cosmetics/starlight/*.png.
"""
import argparse
import json
import math
import random
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, required=True)
parser.add_argument('--base', type=Path)
parser.add_argument('--preview', action='store_true')
ARGS = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
ROOT = ARGS.root.resolve()
SRC = ROOT / 'src' / 'Art'
ART = ROOT / 'art' / 'cosmetics' / 'starlight'; ART.mkdir(parents=True, exist_ok=True)
C = Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))
TAU = 2 * math.pi


def B(x, y, z):
    return Vector((x, z, y))


GALAXY = (.16, .24, .9)
LACE = (.3, .42, 1.)
LINING = (.025, .03, .1)
SILVER = (.78, .8, .87)
LEATHER = (.035, .035, .05)
GEM = (.25, .45, 1.)


# ---------------------------------------------------------------- textures (tileable, grey so the tint colours them)
def save_png(name, arr):
    h, w = arr.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=False)
    rgba = np.ones((h, w, 4), np.float32); rgba[..., :3] = np.clip(arr, 0, 1)[..., None] if arr.ndim == 2 else np.clip(arr, 0, 1)
    img.pixels[:] = rgba.ravel()
    img.filepath_raw = str(SRC / name); img.file_format = 'PNG'; img.save()


def periodic_noise(n, octaves, rng):
    y, x = np.mgrid[0:n, 0:n] / n
    out = np.zeros((n, n))
    for o in range(octaves):
        f = 2 ** o
        for _ in range(6):
            kx, ky = rng.randint(-f, f), rng.randint(-f, f)
            if kx == 0 and ky == 0: continue
            out += np.sin(TAU * (kx * x + ky * y) + rng.uniform(0, TAU)) / (f ** .9)
    out -= out.min(); out /= out.max()
    return out


def textures():
    rng = random.Random(7); n = 512
    cloud = periodic_noise(n, 6, rng)
    wisps = periodic_noise(n, 5, rng)
    nebula = np.clip((cloud - .45) * 2.2, 0, 1) ** 1.4 * .55 + np.clip((wisps - .55) * 3, 0, 1) * .25
    stars = np.zeros((n, n))
    yy, xx = np.mgrid[-4:5, -4:5]
    for k in range(420):
        cx, cy = rng.randrange(n), rng.randrange(n)
        big = rng.random() < .06
        s = rng.uniform(.6, 1.0) if not big else 1.6
        b = rng.uniform(.45, 1.) * (1.6 if big else 1)
        spot = b * np.exp(-(xx ** 2 + yy ** 2) / (2 * (s * .55) ** 2))
        if big:  # four-point sparkle
            spot += .8 * np.exp(-(yy ** 2) / .3) * np.exp(-np.abs(xx) / 1.6) + .8 * np.exp(-(xx ** 2) / .3) * np.exp(-np.abs(yy) / 1.6)
        ys = (np.arange(cy - 4, cy + 5) % n)[:, None]; xs = (np.arange(cx - 4, cx + 5) % n)[None, :]
        stars[ys, xs] = np.maximum(stars[ys, xs], spot)
    base = .42 + .5 * nebula
    save_png('starlight_galaxy.png', np.clip(base + stars * 1.2, 0, 1))
    save_png('starlight_stars.png', np.clip(stars * 1.4 + nebula * .1, 0, 1))
    # Lace: a diamond net with floral rosettes, light threads on a dark ground.
    m = 256
    y, x = (np.mgrid[0:m, 0:m] + .5) / m
    def line(d, w): return np.exp(-(d / w) ** 2)
    net = np.maximum(line(np.abs(((x + y) * 4) % 1 - .5) - .5 + .5, .9), 0)
    u, v = (x * 4) % 1 - .5, (y * 4) % 1 - .5
    net = np.maximum(line(np.abs(np.abs(u) + np.abs(v) - .5), .05), 0)
    r = np.hypot((x * 2) % 1 - .5, (y * 2) % 1 - .5)
    ang = np.arctan2((y * 2) % 1 - .5, (x * 2) % 1 - .5)
    petal = line(r - (.14 + .06 * np.cos(6 * ang)), .025) + line(r - .04, .025)
    vine = line(np.abs(r - .3) * (1 + .5 * np.cos(3 * ang)), .02)
    lace = np.clip(np.maximum.reduce([net * .8, petal, vine * .7]), 0, 1)
    save_png('starlight_lace.png', .14 + .8 * lace)
    # Lace trim: a ribbon (v 0 = hem side): scallops along the hem, net and dots above, a solid edge line on top.
    w, h = 256, 64
    y, x = (np.mgrid[0:h, 0:w] + .5)
    xv, yv = x / w, y / h
    sc = (xv * 8) % 1 - .5
    scallop_edge = .18 + .14 * np.sqrt(np.clip(.25 - sc ** 2, 0, 1)) * 2
    inside = yv > scallop_edge - .12
    arc = line(yv - scallop_edge, .03)
    holes = line(np.hypot(sc * 1.0, (yv - .33) * 2.2), .05)
    netb = line(np.abs(np.abs((xv * 16) % 1 - .5) + np.abs((yv * 4) % 1 - .5) - .5), .06) * (yv > .42) * (yv < .86)
    top = line(yv - .92, .035)
    trim = np.where(inside, .22, .05) + .75 * np.clip(np.maximum.reduce([arc, holes, netb * .8, top]), 0, 1)
    save_png('starlight_trim.png', trim)


# ---------------------------------------------------------------- materials
MATS = {}


def material(name, color, rough=.6, metal=0., emit=0., image=None, emit_image=None, tint=False, tint_emission=False):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; p = nt.nodes['Principled BSDF']
    p.inputs['Base Color'].default_value = (*color, 1); p.inputs['Roughness'].default_value = rough
    p.inputs['Metallic'].default_value = metal
    p.inputs['Emission Color'].default_value = (*color, 1); p.inputs['Emission Strength'].default_value = emit
    if image:
        tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(str(SRC / image))
        mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'; mix.inputs['Factor'].default_value = 1
        nt.links.new(tex.outputs['Color'], mix.inputs['A']); mix.inputs['B'].default_value = (*color, 1)
        nt.links.new(mix.outputs['Result'], p.inputs['Base Color'])
        m['texture'] = image
    if emit_image:
        tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(str(SRC / emit_image))
        mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'; mix.inputs['Factor'].default_value = 1
        nt.links.new(tex.outputs['Color'], mix.inputs['A']); mix.inputs['B'].default_value = (*color, 1)
        nt.links.new(mix.outputs['Result'], p.inputs['Emission Color'])
        m['emissionTexture'] = emit_image
    m['tint'] = tint; m['tintEmission'] = tint_emission; m['color'] = list(color)
    return m


def make_mats():
    return {
        'galaxy': material('Starlight galaxy', GALAXY, .45, emit=1.6, image='starlight_galaxy.png', emit_image='starlight_stars.png', tint=True, tint_emission=True),
        'lace': material('Starlight lace', LACE, .7, image='starlight_lace.png', tint=True),
        'trim': material('Starlight lace trim', LACE, .7, image='starlight_trim.png', tint=True),
        'lining': material('Starlight lining', LINING, .8),
        'silver': material('Starlight silver', SILVER, .25, metal=.85),
        'leather': material('Starlight leather', LEATHER, .45),
        'gem': material('Starlight gem', GEM, .08, emit=.9, tint=True, tint_emission=True),
    }


# ---------------------------------------------------------------- geometry
def mesh_object(name, verts, faces, mat, group='static', loop_uvs=None, smooth=True, inside=None, flip=False):
    me = bpy.data.meshes.new(name); me.from_pydata([tuple(v) for v in verts], [], [tuple(f) for f in faces]); me.update()
    if loop_uvs:
        layer = me.uv_layers.new(name='UVMap')
        for poly, uvs in zip(me.polygons, loop_uvs):
            for li, uv in zip(poly.loop_indices, uvs): layer.data[li].uv = uv
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.normal_update()
    if inside is not None:
        score = sum((f.calc_center_median() - inside).dot(f.normal) * f.calc_area() for f in bm.faces)
        if score < 0:
            for f in bm.faces: f.normal_flip()
    else:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if flip:
        for f in bm.faces: f.normal_flip()
    bm.to_mesh(me); bm.free(); me.update()
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    for poly in me.polygons: poly.use_smooth = smooth
    me.materials.append(mat); ob['group'] = group
    return ob


def surface(name, rows, cols, point, mat, wrap=False, uv=(1., 1.), inside=None, flip=False, group='static'):
    """Grid surface: point(t, s) -> Unity Vector, t in [0,1] over rows, s over columns ([0,1); wraps if wrap).
    UV u = s * uv[0], v = t * uv[1], continuous across the wrap seam."""
    ncol = cols if wrap else cols + 1
    verts = []
    for r in range(rows + 1):
        t = r / rows
        for c in range(ncol):
            verts.append(B(*point(t, c / cols)))
    faces, uvs = [], []
    for r in range(rows):
        for c in range(cols):
            c2 = (c + 1) % ncol if wrap else c + 1
            faces.append((r * ncol + c, r * ncol + c2, (r + 1) * ncol + c2, (r + 1) * ncol + c))
            u0, u1 = c / cols * uv[0], (c + 1) / cols * uv[0]; v0, v1 = r / rows * uv[1], (r + 1) / rows * uv[1]
            uvs.append(((u0, v0), (u1, v0), (u1, v1), (u0, v1)))
    return mesh_object(name, verts, faces, mat, group, loop_uvs=uvs, inside=inside, flip=flip)


def tube(name, path, radius, mat, group='static', segs=8, closed=False, cap=True, flat=1., up=None):
    pts = [Vector(p) for p in path]
    n = len(pts)
    rad = radius if callable(radius) else (lambda s, r=radius: r)
    verts = []
    for i, p in enumerate(pts):
        if closed:
            t = (pts[(i + 1) % n] - pts[(i - 1) % n]).normalized()
        else:
            t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        side = Vector(up) if up else (Vector((0, 1, 0)) if abs(t.y) < .9 else Vector((1, 0, 0)))
        a = t.cross(side).normalized(); b = t.cross(a).normalized()
        r = rad(i / max(n - 1, 1))
        for j in range(segs):
            ang = TAU * j / segs
            verts.append(B(*(p + a * (r * math.cos(ang)) + b * (r * flat * math.sin(ang)))))
    faces = []
    for i in range(n if closed else n - 1):
        i2 = (i + 1) % n
        for j in range(segs):
            j2 = (j + 1) % segs
            faces.append((i * segs + j, i * segs + j2, i2 * segs + j2, i2 * segs + j))
    if cap and not closed:
        for idx, end in ((0, pts[0]), (n - 1, pts[-1])):
            c = len(verts); verts.append(B(*end))
            for j in range(segs): faces.append((idx * segs + j, idx * segs + (j + 1) % segs, c))
    return mesh_object(name, verts, faces, mat, group)


def ring(name, centre, normal, radius, thick, mat, segs=28, group='static'):
    nrm = Vector(normal).normalized()
    a = nrm.cross(Vector((0, 1, 0)) if abs(nrm.y) < .9 else Vector((1, 0, 0))).normalized(); b = nrm.cross(a)
    path = [Vector(centre) + (a * math.cos(TAU * k / segs) + b * math.sin(TAU * k / segs)) * radius for k in range(segs)]
    return tube(name, path, thick, mat, group, segs=8, closed=True)


def ellipsoid(name, centre, radii, mat, segs=(14, 8), group='static'):
    cx, cy, cz = centre
    return surface(name, segs[1], segs[0], lambda t, s: Vector((cx + radii[0] * math.cos(math.pi * (t - .5)) * math.cos(TAU * s),
                                                               cy + radii[1] * math.sin(math.pi * (t - .5)),
                                                               cz + radii[2] * math.cos(math.pi * (t - .5)) * math.sin(TAU * s))),
                   mat, wrap=True, inside=B(cx, cy, cz), group=group)


def gem(name, centre, half, mat, facing=(0, 0, 1), group='static'):
    """Faceted diamond (octahedron with a girdle), long axis up; 'facing' is the side that sits flat-ish outwards."""
    c = Vector(centre); hx, hy, hz = half
    f = Vector(facing).normalized(); side = Vector((0, 1, 0)).cross(f).normalized()
    pts = [c + Vector((0, hy, 0)), c - Vector((0, hy, 0))]
    for k in range(8):
        a = TAU * k / 8
        pts.append(c + side * (hx * math.cos(a)) + f * (hz * math.sin(a)) + Vector((0, hy * .12, 0)))
    verts = [B(*p) for p in pts]
    faces = [(0, 2 + k, 2 + (k + 1) % 8) for k in range(8)] + [(1, 2 + (k + 1) % 8, 2 + k) for k in range(8)]
    return mesh_object(name, verts, faces, mat, group, smooth=False, inside=B(*c))


def ribbon(name, curve, down, height, mat, closed=False, uv_per_m=6., group='static', inside=None, both=True):
    """A band hanging from 'curve' (list of Unity points) along 'down(p, i)' (unit vector) for 'height' (lace trim).
    v = 1 at the curve (the trim's solid edge), 0 at the free edge (scallops). Two-sided unless both=False."""
    n = len(curve)
    pts = [Vector(p) for p in curve]
    length = [0.]
    for i in range(1, n + (1 if closed else 0)):
        length.append(length[-1] + (pts[i % n] - pts[i - 1]).length)
    obs = []
    for side in ((1, -1) if both else (1,)):
        verts, faces, uvs = [], [], []
        for i in range(n):
            d = Vector(down(pts[i], i))
            verts.append(B(*pts[i])); verts.append(B(*(pts[i] + d * height)))
        count = n if closed else n - 1
        for i in range(count):
            i2 = (i + 1) % n
            q = (2 * i, 2 * i2, 2 * i2 + 1, 2 * i + 1)
            u0, u1 = length[i] * uv_per_m, length[i + 1] * uv_per_m
            uv = ((u0, 1), (u1, 1), (u1, 0), (u0, 0))
            if side < 0: q, uv = q[::-1], uv[::-1]
            faces.append(q); uvs.append(uv)
        ob = mesh_object(name, verts, faces, mat, group, loop_uvs=uvs)
        if inside is not None:
            # orient each side explicitly: side 1 faces away from 'inside', side -1 towards it
            bm = bmesh.new(); bm.from_mesh(ob.data); bm.normal_update()
            score = sum((f.calc_center_median() - inside).dot(f.normal) for f in bm.faces)
            if (score < 0) == (side > 0):
                for f in bm.faces: f.normal_flip()
            bm.to_mesh(ob.data); bm.free()
        obs.append(ob)
    return obs


def bez(p, t):
    pts = [Vector(q) for q in p]
    while len(pts) > 1: pts = [pts[i].lerp(pts[i + 1], t) for i in range(len(pts) - 1)]
    return pts[0]


def lerp(a, b, t): return a + (b - a) * t


# ---------------------------------------------------------------- parts
def bodice_r(h):
    prof = [(.296, -.02), (.303, .0), (.306, .04), (.306, .2), (.304, .3), (.296, .343), (.28, .37), (.258, .382)]
    for (r0, h0), (r1, h1) in zip(prof, prof[1:]):
        if h0 <= h <= h1: return r0 + (r1 - r0) * (h - h0) / (h1 - h0)
    return prof[-1][0] if h > .382 else prof[0][0]


def build_bodice(M):
    parts = []
    hs = [-.02, .0, .04, .12, .2, .3, .343, .37, .382]
    def pt(t, s, extra=0.):
        h = lerp(hs[0], hs[-1], t)
        r = bodice_r(h) + extra; a = TAU * s
        return Vector((r * math.sin(a), h, r * math.cos(a)))
    parts.append(surface('Bodice', 18, 64, pt, M['galaxy'], wrap=True, uv=(3, 1.3), inside=B(0, .18, 0)))
    # lace side panels with silver borders
    for sd in (1, -1):
        a0, a1 = math.radians(62), math.radians(104)
        h0, h1 = .05, .27
        def panel(t, s, sd=sd):
            h = lerp(h0, h1, t); a = sd * lerp(a0, a1, s); r = bodice_r(h) + .004
            return Vector((r * math.sin(a), h, r * math.cos(a)))
        parts.append(surface('Lace panel', 8, 10, panel, M['lace'], uv=(1.2, 1.2), inside=B(0, .16, 0)))
    # silver piping: from the collar over the bust, curving in to a V at the waist
    brooch = Vector((0, .384, .282))
    for sd in (1, -1):
        path = []
        for t in np.linspace(0, 1, 16):
            h = lerp(.36, .005, t)
            a = sd * math.radians(lerp(44, 9, t ** 1.6) + 6 * math.sin(math.pi * t))
            r = bodice_r(h) + .004; path.append(Vector((r * math.sin(a), h, r * math.cos(a))))
        parts.append(tube('Piping', path, .0055, M['silver'], segs=6))
        # side piping framing the lace panel
        side = []
        for t in np.linspace(0, 1, 10):
            h = lerp(.33, .03, t); a = sd * math.radians(lerp(56, 60, t)); r = bodice_r(h) + .004
            side.append(Vector((r * math.sin(a), h, r * math.cos(a))))
        parts.append(tube('Side piping', side, .0045, M['silver'], segs=6))
    # choker collar, silver-edged, and the ring brooch with a gem
    def collar(t, s):
        h = lerp(.364, .4, t); a = TAU * s; r = .266 + .006 * math.sin(math.pi * t)
        return Vector((r * math.sin(a), h, r * math.cos(a)))
    parts.append(surface('Choker', 3, 48, collar, M['leather'], wrap=True, inside=B(0, .38, 0)))
    for h, r in ((.364, .267), (.4, .267)):
        parts.append(tube('Choker edge', [Vector((r * math.sin(TAU * k / 40), h, r * math.cos(TAU * k / 40))) for k in range(40)], .0042, M['silver'], closed=True, segs=6))
    parts.append(ring('Brooch', brooch, (0, 0, 1), .03, .0085, M['silver']))
    parts.append(gem('Brooch gem', brooch + Vector((0, 0, .006)), (.02, .026, .012), M['gem']))
    # small gem studs on the waist piping ends
    return parts, {}, None


def skirt_point(t, a, layer, inflate=0.):
    """t 0 waist -> 1 hem, a angle (0 = front)."""
    front = ((1 + math.cos(a)) / 2)
    if layer == 0:
        hem = -.55 + .47 * front ** 1.6; R = .5 - .1 * front ** 1.6
        r0, fold, k, ph = .322, .028, 10, .3
    else:
        hem = -.575 + .36 * front ** 1.3; R = .55 - .06 * front
        r0, fold, k, ph = .33, .04, 7, 1.1
    y = lerp(.27, hem, t)
    r = r0 + (R - r0) * t ** 1.25 + fold * t ** 1.6 * math.sin(k * a + ph) + inflate
    return Vector((r * math.sin(a), y, r * math.cos(a)))


OPEN = math.radians(38)


def build_skirt(M):
    parts = []
    inside = B(0, -.1, 0)
    under = lambda t, s, d=0.: skirt_point(t, TAU * s, 0, d)
    parts.append(surface('Underskirt', 16, 72, under, M['galaxy'], wrap=True, uv=(5, 2.2), inside=inside))
    parts.append(surface('Underskirt lining', 16, 72, lambda t, s: under(t, s, -.007), M['lining'], wrap=True, inside=inside, flip=True))
    over = lambda t, s, d=0.: skirt_point(t, lerp(OPEN, TAU - OPEN, s), 1, d)
    parts.append(surface('Overskirt', 16, 60, over, M['galaxy'], uv=(4, 2.2), inside=inside))
    parts.append(surface('Overskirt lining', 16, 60, lambda t, s: over(t, s, -.007), M['lining'], inside=inside, flip=True))
    # lace trims: round the underskirt hem, along the overskirt hem and down both open edges
    hem0 = [skirt_point(1, TAU * k / 96, 0) for k in range(96)]
    def outward_down(p, i, pts):
        a = pts[(i + 1) % len(pts)] - pts[i - 1]
        radial = Vector((p.x, 0, p.z)).normalized()
        return (Vector((0, -1, 0)) * .8 + radial * .45).normalized()
    parts += ribbon('Lace hem', hem0, lambda p, i: outward_down(p, i, hem0), .05, M['trim'], closed=True, uv_per_m=7, inside=inside)
    hem1 = [skirt_point(1, lerp(OPEN, TAU - OPEN, k / 80), 1) for k in range(81)]
    parts += ribbon('Lace hem', hem1, lambda p, i: outward_down(p, i, hem1), .055, M['trim'], uv_per_m=7, inside=inside)
    for a in (OPEN, TAU - OPEN):
        edge = [skirt_point(t, a, 1) for t in np.linspace(.12, 1, 18)]
        side = 1 if a < math.pi else -1
        def inward(p, i, a=a, side=side):
            tangent = Vector((math.cos(a), 0, -math.sin(a))) * side
            return (-tangent).normalized()
        parts += ribbon('Lace edge', edge, inward, .04, M['trim'], uv_per_m=7, inside=inside)
    for pts, closed in ((hem0, True), (hem1, False)):
        parts.append(tube('Hem piping', pts, .004, M['silver'], segs=6, closed=closed))
    # belt with a ring buckle and a hanging strap
    def belt(t, s):
        a = TAU * s; r = .338; return Vector((r * math.sin(a), lerp(.215, .27, t), r * math.cos(a)))
    parts.append(surface('Belt', 2, 64, belt, M['leather'], wrap=True, inside=B(0, .24, 0)))
    for h in (.218, .267):
        parts.append(tube('Belt stitch', [Vector((.341 * math.sin(TAU * k / 64), h, .341 * math.cos(TAU * k / 64))) for k in range(64)], .0028, M['silver'], closed=True, segs=5))
    ab = math.radians(-24)
    centre = Vector((.35 * math.sin(ab), .243, .35 * math.cos(ab)))
    facing = Vector((math.sin(ab), 0, math.cos(ab)))
    parts.append(ring('Buckle', centre, facing, .034, .0085, M['silver']))
    parts.append(gem('Buckle gem', centre + facing * .006, (.018, .024, .011), M['gem'], facing=tuple(facing)))
    strap = [centre + facing * .004 + Vector((0, -.03 - .2 * t, 0)) + facing * (.02 * t * t) for t in np.linspace(0, 1, 8)]
    tang = Vector((math.cos(ab), 0, -math.sin(ab)))
    def strap_pt(t, s):
        p = strap[0].lerp(strap[-1], t)
        w = .022 * (1 if t < .9 else (1 - t) / .1 * .9 + .1)
        return p + tang * (w * (s * 2 - 1)) + facing * (.02 * t * t)
    parts.append(surface('Strap', 8, 2, strap_pt, M['leather'], inside=B(0, .1, 0)))
    parts.append(surface('Strap back', 8, 2, strap_pt, M['leather'], inside=B(0, .1, 0), flip=True))
    parts.append(gem('Strap tip', strap_pt(1, .5) + Vector((0, -.012, 0)), (.008, .014, .006), M['silver'], facing=tuple(facing)))
    return parts, {}, None


def build_sleeve(M, sx):
    parts = []
    prof = [(.0, .02), (.074, .024), (.081, .05), (.082, .36), (.086, .40)]
    def pt(t, s):
        # t along the sleeve profile, s around the arm
        k = t * (len(prof) - 1); i = min(int(k), len(prof) - 2); f = k - i
        r = lerp(prof[i][0], prof[i + 1][0], f); h = lerp(prof[i][1], prof[i + 1][1], f); a = TAU * s
        return Vector((sx * h, r * math.sin(a), r * math.cos(a)))
    parts.append(surface('Lace sleeve', 16, 28, pt, M['lace'], wrap=True, uv=(2, 4), inside=B(sx * .2, 0, 0)))
    parts.append(surface('Sleeve lining', 16, 28, lambda t, s: (lambda p: Vector((p.x, p.y * .93, p.z * .93)))(pt(t, s)), M['lining'], wrap=True, inside=B(sx * .2, 0, 0), flip=True))
    cuff = [Vector((sx * .40, .088 * math.sin(TAU * k / 40), .088 * math.cos(TAU * k / 40))) for k in range(40)]
    parts += ribbon('Lace cuff', cuff, lambda p, i: (Vector((sx, 0, 0)) * .85 + Vector((0, p.y, p.z)).normalized() * .5).normalized(), .055,
                    M['trim'], closed=True, uv_per_m=9, inside=B(sx * .4, 0, 0))
    parts.append(tube('Cuff ring', cuff, .0045, M['silver'], closed=True, segs=6))
    shoulder = [Vector((sx * .05, .083 * math.sin(TAU * k / 40), .083 * math.cos(TAU * k / 40))) for k in range(40)]
    parts.append(tube('Shoulder ring', shoulder, .004, M['silver'], closed=True, segs=6))
    return parts, {}, None


def leg_r(y):
    return .106 if y > 0 else .106 * max(0., (y + .549) / .549)


def build_boot(M):
    parts = []
    top, tip = .0, -.566
    def pt(t, s, extra=.013):
        y = lerp(top, tip, t); a = TAU * s
        r = (leg_r(y) + extra) * (1 - t ** 8)
        dz = .022 * t ** 3
        return Vector((r * math.sin(a), y, r * math.cos(a) + dz))
    parts.append(surface('Boot', 20, 36, pt, M['galaxy'], wrap=True, uv=(2, 3), inside=B(0, -.25, 0)))
    topring = [pt(0, k / 48, .016) for k in range(48)]
    parts += ribbon('Lace top', [p + Vector((0, .012, 0)) for p in topring], lambda p, i: Vector((p.x, -1.5, p.z)).normalized(), .07, M['trim'],
                    closed=True, uv_per_m=8, inside=B(0, -.03, 0))
    parts.append(tube('Boot top', [p + Vector((0, .012, 0)) for p in topring], .005, M['silver'], closed=True, segs=6))
    seam = [pt(t, 0, .0145) for t in np.linspace(.13, .93, 14)]
    parts.append(tube('Boot seam', seam, .0035, M['silver'], segs=5))
    gp = pt(.8, 0, .02)
    parts.append(gem('Boot gem', gp, (.015, .02, .008), M['gem']))
    parts.append(ring('Gem setting', gp - Vector((0, 0, .002)), (0, .25, 1), .021, .0035, M['silver'], segs=20))
    return parts, {}, None


def build_earrings(M):
    parts = []
    for sd in (1, -1):
        x = sd * .262
        parts.append(ellipsoid('Stud', (x, .045, .25), (.011, .011, .011), M['silver'], (10, 6)))
        chain = [Vector((x + sd * .002 * math.sin(k), .04 - .012 * k, .25)) for k in range(7)]
        parts.append(tube('Chain', chain, .0028, M['silver'], segs=5))
        parts.append(ellipsoid('Cap', (x + sd * .003, -.036, .25), (.008, .006, .008), M['silver'], (8, 5)))
        parts.append(gem('Drop gem', (x + sd * .003, -.072, .25), (.015, .034, .015), M['gem'], facing=(sd, 0, 0)))
    return parts, {}, None


STYLES = {
    'starlight_bodice': ('Body Top', build_bodice),
    'starlight_skirt': ('Body Bottom', build_skirt),
    'starlight_sleeve_r': ('Arm Right', lambda M: build_sleeve(M, 1)),
    'starlight_sleeve_l': ('Arm Left', lambda M: build_sleeve(M, -1)),
    'starlight_boot_r': ('Leg Right', build_boot),
    'starlight_boot_l': ('Leg Left', build_boot),
    'starlight_earrings': ('Ears', build_earrings),
}

FRAMES = {
    'Hat': ((0, .6681, -.254), (1, 0, 0), (0, 1, 0)), 'Ears': ((0, .6681, -.254), (1, 0, 0), (0, 1, 0)),
    'Body Top': ((0, .19, 0), (1, 0, 0), (0, 1, 0)), 'Body Bottom': ((0, -.102, 0), (1, 0, 0), (0, 1, 0)),
    'Arm Right': ((.2626, .4448, 0), (.259, -.966, 0), (.966, .259, 0)), 'Arm Left': ((-.2626, .4448, 0), (.259, .966, 0), (-.966, .259, 0)),
    'Leg Right': ((.1292, -.1377, 0), (1, 0, 0), (0, 1, 0)), 'Leg Left': ((-.1292, -.1377, 0), (1, 0, 0), (0, 1, 0)),
}
BASE_FRAMES = {'head_top': 'Hat', 'body_top': 'Body Top', 'body_bot': 'Body Bottom', 'arm_r': 'Arm Right', 'arm_l': 'Arm Left',
               'head_bot': ((0, .5488, 0), (1, 0, 0), (0, 1, 0)), 'leg_r': 'Leg Right', 'leg_l': 'Leg Left'}
S4 = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def frame_matrix(frame):
    pos, ax, ay = frame
    X = Vector(ax); Y = Vector(ay); Z = Vector((0, 0, 1))
    m = Matrix.Identity(4)
    for i in range(3): m[i][0], m[i][1], m[i][2], m[i][3] = X[i], Y[i], Z[i], pos[i]
    return S4 @ m @ S4


def vec(v): return {'x': round(float(v.x), 6), 'y': round(float(v.y), 6), 'z': round(float(v.z), 6)}


def export(key, parts, pivots):
    mats_out = []; index = {}; records = []; tris = 0
    lo = Vector((1e9,) * 3); hi = -lo
    for ob in parts:
        me = ob.data
        bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free(); me.update()
        pts = [ob.matrix_world @ v.co for v in me.vertices]
        a = Vector(tuple(min(p[i] for p in pts) for i in range(3))); b = Vector(tuple(max(p[i] for p in pts) for i in range(3)))
        lo = Vector(tuple(min(lo[i], a[i]) for i in range(3))); hi = Vector(tuple(max(hi[i], b[i]) for i in range(3)))
        center = (a + b) / 2
        m = me.materials[0]
        if m.name not in index:
            p = m.node_tree.nodes['Principled BSDF']; col = m['color']
            index[m.name] = len(mats_out)
            mats_out.append({'name': m.name, 'texture': m.get('texture', ''), 'emissionTexture': m.get('emissionTexture', ''),
                             'color': {'r': col[0], 'g': col[1], 'b': col[2], 'a': 1}, 'transmission': 0,
                             'emission': p.inputs['Emission Strength'].default_value, 'roughness': p.inputs['Roughness'].default_value,
                             'metallic': p.inputs['Metallic'].default_value, 'tint': bool(m.get('tint', False)),
                             'tintEmission': bool(m.get('tintEmission', False))})
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
                uv.append({'x': round(float(u[0]), 5), 'y': round(float(u[1]), 5)})
            tri.extend([start, start + 2, start + 1])
        tris += len(tri) // 3
        mi = index[m.name]
        records.append({'name': ob.name, 'group': ob.get('group', 'static'), 'phase': 0, 'material': mi, 'vertices': verts, 'normals': normals,
                        'uv': uv, 'triangles': tri, 'color': mats_out[mi]['color'], 'texture': mats_out[mi]['texture'],
                        'rest': {'position': vec(C @ center), 'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'scale': {'x': 1, 'y': 1, 'z': 1}}, 'samples': []})
    data = {'version': 1, 'fps': 24, 'frames': 1, 'materials': mats_out, 'parts': records,
            'pivots': [{'group': g, 'position': vec(C @ p)} for g, p in pivots.items()]}
    (SRC / ('v11_' + key + '.mesh.json')).write_text(json.dumps(data, separators=(',', ':')))
    size = C @ (hi - lo)
    print(f'EXPORT {key}: parts {len(records)}, triangles {tris}, size (Unity) {size.x:.3f} x {size.y:.3f} x {size.z:.3f}', flush=True)


def stand_in_body():
    if not ARGS.base or not ARGS.base.exists(): return []
    grey = material('Preview body', (.42, .44, .47), .55)
    data = json.loads(ARGS.base.read_text())
    for key, parts in data.items():
        frame = BASE_FRAMES.get(key)
        if frame is None: continue
        if isinstance(frame, str): frame = FRAMES[frame]
        for p in parts:
            if 'flat' in p['mesh'] and key == 'body_top': continue
            me = bpy.data.meshes.new(key); me.from_pydata([tuple(B(*v)) for v in p['v']], [], [tuple(f) for f in p['f']]); me.update()
            ob = bpy.data.objects.new(key, me); bpy.context.collection.objects.link(ob)
            for poly in me.polygons: poly.use_smooth = True
            me.materials.append(grey); ob.matrix_world = frame_matrix(frame)


def setup_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 64
    scene.view_settings.view_transform = 'Standard'


def lights_and_camera():
    scene = bpy.context.scene
    world = bpy.data.worlds.new('World'); scene.world = world; world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.035, .035, .05, 1)
    for loc, energy in (((-1.4, 2.0, 1.8), 80), ((1.8, 1.2, 1.0), 40), ((0, -2.2, 1.6), 55), ((0, 0, 3), 25)):
        bpy.ops.object.light_add(type='AREA', location=loc); light = bpy.context.object
        light.data.energy = energy; light.data.size = 1.6
        light.rotation_euler = (Vector((0, 0, .3)) - light.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.ops.object.camera_add(); cam = bpy.context.object; scene.camera = cam; cam.data.lens = 50
    return cam


def look(cam, eye, target):
    cam.location = eye; cam.rotation_euler = (target - eye).to_track_quat('-Z', 'Y').to_euler()


def render(path):
    bpy.context.scene.render.filepath = str(path); bpy.ops.render.render(write_still=True)


setup_scene()
textures()
M = make_mats()
built = {}
for key, (slot, fn) in STYLES.items():
    parts, pivots, _ = fn(M)
    export(key, parts, pivots)
    built[key] = (slot, parts)
cam = lights_and_camera()
scene = bpy.context.scene
everything = [ob for _, parts in built.values() for ob in parts]
VIEWS = {'starlight_skirt': (.55, 1.0, .2), 'starlight_bodice': (.7, 1.0, .3), 'starlight_boot_r': (.8, 1.0, .1), 'starlight_boot_l': (-.8, 1.0, .1),
         'starlight_sleeve_r': (.3, 1.0, .5), 'starlight_sleeve_l': (-.3, 1.0, .5), 'starlight_earrings': (.25, 1.0, .1)}
scene.render.film_transparent = True; scene.render.resolution_x = scene.render.resolution_y = 256
for key, (slot, parts) in built.items():
    for ob in everything: ob.hide_render = ob not in parts
    lo = Vector((1e9,) * 3); hi = -lo
    framed = [ob for ob in parts if key != 'starlight_earrings' or ob.data.vertices[0].co.x > 0]   # one earring fills the icon
    for ob in framed:
        for v in ob.data.vertices:
            lo = Vector(tuple(min(lo[i], v.co[i]) for i in range(3))); hi = Vector(tuple(max(hi[i], v.co[i]) for i in range(3)))
    c = (lo + hi) / 2; r = (hi - lo).length / 2
    look(cam, c + Vector(VIEWS[key]).normalized() * r * 3.0, c)
    render(SRC / ('cosmetic_' + key + '_icon.png'))
if ARGS.preview:
    for ob in everything: ob.hide_render = False
    for key, (slot, parts) in built.items():
        for ob in parts: ob.matrix_world = frame_matrix(FRAMES[slot])
    stand_in_body()
    scene.render.film_transparent = False; scene.render.resolution_x = 760; scene.render.resolution_y = 900
    target = Vector((0, -.05, .1))
    for suffix, eye in (('front', Vector((1.0, 2.6, .8))), ('back', Vector((-1.2, -2.5, .8))), ('side', Vector((2.8, -.3, .45))), ('low', Vector((.5, 2.4, -.35)))):
        look(cam, target + eye, target); render(ART / ('starlight_' + suffix + '.png'))
    scene.render.resolution_x = scene.render.resolution_y = 700
    t = Vector((0, .02, .55)); look(cam, t + Vector((.7, 1.1, .2)), t); render(ART / 'starlight_bust.png')
print('STARLIGHT_COMPLETE', flush=True)
