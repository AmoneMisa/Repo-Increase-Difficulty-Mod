"""Blender 5.2: --background --factory-startup --python tools/build_shark.py -- --root PATH [--base JSON] [--preview]

The Plush Shark set: six original cosmetics that each fill their own slot, so they can be worn alone or together.
Inspired by a cuddly blue plush shark (white belly, stitched seams, small black eyes, a toothy grin). The blue
"skin" of every part is tintable, so it takes the colour the player picks in the cosmetics menu; the white belly,
teeth, eyes and headphone details keep their own colours.

  shark_hood    Hat         hood with a rounded snout over the face, a row of teeth, side eyes, gill marks
  shark_phones  Ears        over-ear headphones (tintable cups, silver plates)
  shark_suit    Body Top    plush top with a white belly, stitched seams and a dorsal fin
  shark_tail    Body Bottom plush shorts with a swaying tail and caudal fin (pivot 'tail')
  shark_fin_r   Arm Right   sleeve with a pectoral fin
  shark_fin_l   Arm Left    the mirrored sleeve

All geometry is parametric: every part is built in its slot's own frame (Unity coordinates, measured from the
game's cosmetic parents: head top ellipsoid centre (0, 0, .254) radii (.253, .374, .254); body top cylinder r .287
y 0..361; body bottom cylinder r .294 y 0..282; arm capsule r .071 along +x). Nothing is copied from game meshes.
Built with the player facing +Y in Blender; exported with Unity (x, y, z) = Blender (x, z, y).

Writes src/Art/v11_<key>.mesh.json (ValuableVisuals format; materials carry 'tint'), src/Art/cosmetic_<key>_icon.png,
src/Art/shark_phone_plate.png and, with --preview (needs --base: the game's base meshes exported to JSON, used only
as a grey stand-in body), art/cosmetics/shark/*.png.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, required=True)
parser.add_argument('--base', type=Path)
parser.add_argument('--preview', action='store_true')
parser.add_argument('--only', default='')
ARGS = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
ROOT = ARGS.root.resolve()
SRC = ROOT / 'src' / 'Art'
ART = ROOT / 'art' / 'cosmetics' / 'shark'; ART.mkdir(parents=True, exist_ok=True)
C = Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))


def B(x, y, z):
    """Unity (x, y, z) -> Blender."""
    return Vector((x, z, y))


def U(v):
    return Vector((v[0], v[2], v[1]))


# ---------------------------------------------------------------- palette
SKIN = (.20, .45, .78)          # default plush blue (tinted in game)
BELLY = (.93, .93, .90)
NAVY = (.06, .11, .24)
TOOTH = (.97, .96, .92)
GUM = (.85, .47, .45)
EYE = (.015, .015, .02)
PHONE = (.13, .13, .15)
CUSHION = (.07, .07, .08)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 48
    scene.view_settings.view_transform = 'Standard'


MATS = {}


def material(name, color, rough=.75, metal=0., emit=0., image=None, tint=False):
    if name in MATS: return MATS[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    p = m.node_tree.nodes['Principled BSDF']
    p.inputs['Base Color'].default_value = (*color, 1); p.inputs['Roughness'].default_value = rough
    p.inputs['Metallic'].default_value = metal
    p.inputs['Emission Color'].default_value = (*color, 1); p.inputs['Emission Strength'].default_value = emit
    if image:
        tex = m.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(str(SRC / image)); tex.interpolation = 'Linear'
        m.node_tree.links.new(tex.outputs['Color'], p.inputs['Base Color'])
        m['texture'] = image
    m['tint'] = tint
    MATS[name] = m
    return m


def mats():
    MATS.clear()
    return {
        'skin': material('Shark skin', SKIN, .85, tint=True),
        'belly': material('Shark belly', BELLY, .85),
        'navy': material('Shark stitch', NAVY, .8),
        'tooth': material('Shark tooth', TOOTH, .35),
        'gum': material('Shark gum', GUM, .6),
        'eye': material('Shark eye', EYE, .12),
        'glint': material('Shark eye glint', (1, 1, 1), .2, emit=.6),
        'phone': material('Phone cup', PHONE, .45, tint=True),
        'cushion': material('Phone cushion', CUSHION, .9),
        'band': material('Phone band', (.09, .09, .1), .4, metal=.3),
        'plate': material('Phone plate', (1, 1, 1), .25, metal=.6, image='shark_phone_plate.png'),
    }


def mesh_object(name, verts, faces, mat, group='static', uvs=None, smooth=True, inside=None):
    """verts in Blender coords. inside: a point the surface should face away from (fixes winding)."""
    me = bpy.data.meshes.new(name); me.from_pydata([tuple(v) for v in verts], [], faces); me.update()
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    if inside is not None:
        bm.normal_update()
        score = sum((f.calc_center_median() - inside).dot(f.normal) * f.calc_area() for f in bm.faces)
        if score < 0:
            for f in bm.faces: f.normal_flip()
    else:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(me); bm.free(); me.update()
    if uvs:
        layer = me.uv_layers.new(name='UVMap')
        for poly in me.polygons:
            for li in poly.loop_indices: layer.data[li].uv = uvs[me.loops[li].vertex_index]
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    for poly in me.polygons: poly.use_smooth = smooth
    me.materials.append(mat); ob['group'] = group
    return ob


def grid_faces(rows, cols, wrap=False, pick=None):
    """Quads over a rows x cols vertex grid (index r*cols + c). pick(r, c) -> bool keeps a face."""
    faces = []
    span = cols if wrap else cols - 1
    for r in range(rows - 1):
        for c in range(span):
            c2 = (c + 1) % cols
            if pick and not pick(r, c): continue
            faces.append((r * cols + c, r * cols + c2, (r + 1) * cols + c2, (r + 1) * cols + c))
    return faces


def ellipsoid(name, center, radii, mat, segs=(12, 8), group='static'):
    """center/radii in Unity coords."""
    cx, cy, cz = center; rx, ry, rz = radii
    n, m = segs
    verts = []
    for i in range(m + 1):
        lat = -math.pi / 2 + math.pi * i / m
        for j in range(n):
            a = 2 * math.pi * j / n
            verts.append(B(cx + rx * math.cos(lat) * math.cos(a), cy + ry * math.sin(lat), cz + rz * math.cos(lat) * math.sin(a)))
    faces = grid_faces(m + 1, n, wrap=True)
    return mesh_object(name, verts, faces, mat, group, inside=B(cx, cy, cz))


def bezier(p, t):
    """Quadratic or cubic Bezier through control points p (tuples) at t."""
    pts = [Vector(q) for q in p]
    while len(pts) > 1:
        pts = [pts[i].lerp(pts[i + 1], t) for i in range(len(pts) - 1)]
    return pts[0]


def tube(name, path, radius, mat, group='static', segs=10, flat=1.0, up=None, cap=True):
    """Tube along Unity-space points 'path' (list of 3-tuples); radius a number or a function of s in [0,1].
    flat < 1 squashes the cross-section along the 'up' side vector."""
    pts = [Vector(p) for p in path]
    n = len(pts)
    rad = radius if callable(radius) else (lambda s, r=radius: r)
    verts = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        side = Vector(up) if up else (Vector((0, 1, 0)) if abs(t.y) < .9 else Vector((1, 0, 0)))
        a = t.cross(side).normalized(); b = t.cross(a).normalized()
        r = rad(i / (n - 1))
        for j in range(segs):
            ang = 2 * math.pi * j / segs
            q = p + a * (r * math.cos(ang)) + b * (r * flat * math.sin(ang))
            verts.append(B(*q))
    faces = grid_faces(n, segs, wrap=True)
    if cap:
        for idx, end in ((0, pts[0]), (n - 1, pts[-1])):
            c = len(verts); verts.append(B(*end))
            for j in range(segs):
                faces.append((idx * segs + j, idx * segs + (j + 1) % segs, c))
    centre = sum(pts, Vector()) / n
    return mesh_object(name, verts, faces, mat, group)


def fin(name, lead, trail, axis_a, axis_b, origin, thick, mat, group='static', rows=9, cols=12):
    """A tapered fin. lead/trail: Bezier control points in the fin's 2D plane (a, b); the fin lies in the plane
    spanned by axis_a, axis_b (Unity vectors) through origin; thickness along their cross product, thick(v) at
    height v (0 base, 1 tip), thinning towards both edges."""
    A = Vector(axis_a).normalized(); Bv = Vector(axis_b).normalized(); N = A.cross(Bv).normalized(); O = Vector(origin)
    verts = []
    for side in (1, -1):
        for r in range(rows):
            v = r / (rows - 1)
            L = bezier(lead, v); T = bezier(trail, v)
            for c in range(cols):
                s = c / (cols - 1)
                q = L.lerp(T, s)
                w = thick(v) * (math.sin(math.pi * s) ** .6 if 0 < s < 1 else 0)
                p = O + A * q[0] + Bv * q[1] + N * (side * w * .5)
                verts.append(B(*p))
    faces = []
    for half in (0, 1):
        off = half * rows * cols
        for f in grid_faces(rows, cols):
            faces.append(tuple(i + off for i in (f if half == 0 else f[::-1])))
    # base cap between the two halves
    for c in range(cols - 1):
        faces.append((c, rows * cols + c, rows * cols + c + 1, c + 1))
    return mesh_object(name, verts, faces, mat, group)


def lathe(name, profile, mat, segs=48, axis='y', pick=None, offset=(0, 0, 0), group='static', inside=None):
    """Revolve (r, h) profile points around a Unity axis. pick(row, angle) keeps faces (angle 0 = front +z)."""
    verts = []
    ox, oy, oz = offset
    for r, h in profile:
        for j in range(segs):
            a = 2 * math.pi * j / segs
            if axis == 'y':
                verts.append(B(ox + r * math.sin(a), oy + h, oz + r * math.cos(a)))
            else:  # around x: angle 0 = +z
                verts.append(B(ox + h, oy + r * math.sin(a), oz + r * math.cos(a)))
    def keep(row, col):
        if pick is None: return True
        a = 2 * math.pi * (col + .5) / segs
        if a > math.pi: a -= 2 * math.pi
        return pick(row, a)
    faces = grid_faces(len(profile), segs, wrap=True, pick=keep)
    if inside is None:
        inside = B(ox, oy + sum(h for _, h in profile) / len(profile), oz) if axis == 'y' else B(ox + sum(h for _, h in profile) / len(profile), oy, oz)
    return mesh_object(name, verts, faces, mat, group, inside=inside)


def dashes(name, points, mat, length=.022, radius=.0045, group='static'):
    """Stitch dashes: small capsules at Unity points along a seam, each aligned to the seam direction."""
    obs = []
    for i in range(len(points) - 1):
        if i % 2: continue
        a = Vector(points[i]); b = Vector(points[i + 1])
        mid = (a + b) / 2; d = (b - a).normalized()
        obs.append(tube(name, [mid - d * length / 2, mid, mid + d * length / 2], lambda s: radius * (.55 + .45 * math.sin(math.pi * s)), mat, group, segs=6))
    return obs


# ---------------------------------------------------------------- head frame (Hat, Ears)
HC = Vector((0, 0, .254)); HR = Vector((.283, .404, .284))      # hood ellipsoid: head top + 3 cm


def hood_edge(alpha):
    """Latitude (degrees) of the hood's lower edge at azimuth alpha (0 = front)."""
    f = (1 + math.cos(alpha)) / 2
    return -4 + 54 * f ** 1.6


def hood_point(alpha, lat, inflate=0.):
    """Point on the hood surface (Unity), with the snout pushed forward above the face."""
    la = math.radians(lat)
    d = Vector((math.cos(la) * math.sin(alpha), math.sin(la), math.cos(la) * math.cos(alpha)))
    p = HC + Vector((HR.x * d.x, HR.y * d.y, HR.z * d.z))
    front = max(0., math.cos(alpha)) ** 3
    edge = hood_edge(alpha)
    bump = math.exp(-((lat - (edge + 12)) / 14) ** 2)
    p += Vector((0, -.03 * front * bump, .16 * front * bump))
    if inflate: p += d * inflate
    return p


def build_hood(M):
    parts = []
    n, m = 56, 16
    verts_out, verts_in = [], []
    belly = set()
    for i in range(m + 1):
        t = i / m
        for j in range(n):
            a = -math.pi + 2 * math.pi * j / n
            e = hood_edge(a)
            lat = e + (89.5 - e) * (t ** .85)
            verts_out.append(B(*hood_point(a, lat)))
            verts_in.append(B(*hood_point(a, lat, -.014)))
    def is_belly(r, c):
        a = -math.pi + 2 * math.pi * (c + .5) / n
        return math.cos(a) > .45 and r < 3
    out_skin = grid_faces(m + 1, n, wrap=True)   # all plush blue: the belly white belongs below the mouth, not on the snout
    centre = B(*HC)
    parts.append(mesh_object('Hood', verts_out, out_skin, M['skin'], inside=centre))
    inner = mesh_object('Hood lining', verts_in, grid_faces(m + 1, n, wrap=True), M['skin'], inside=centre)
    for p in inner.data.polygons: p.flip()
    parts.append(inner)
    # Rim: gum along the mouth, skin piping round the back.
    def rim(a0, a1, count, inflate):
        return [hood_point(a, hood_edge(a), inflate - .007) for a in (a0 + (a1 - a0) * k / count for k in range(count + 1))]
    mouth = math.radians(78)
    parts.append(tube('Hood gum', rim(-mouth, mouth, 30, 0), .017, M['gum'], segs=8, flat=.75))
    parts.append(tube('Hood piping', rim(mouth - .05, 2 * math.pi - mouth + .05, 40, 0), .009, M['skin'], segs=8))
    # Teeth hanging from the gum.
    teeth = 19
    span = math.radians(70)
    for k in range(teeth):
        a = -span + 2 * span * k / (teeth - 1)
        root = hood_point(a, hood_edge(a), -.012)
        nrm = Vector((math.sin(a), 0, math.cos(a)))
        side = Vector((math.cos(a), 0, -math.sin(a)))
        size = .017 + .003 * math.cos(a * 1.4)
        tip = root + Vector((0, -size * 1.6, 0)) - nrm * .004
        base = [root + side * size - nrm * .004, root + nrm * .006, root - side * size - nrm * .004, root - nrm * .012]
        verts = [B(*v) for v in base] + [B(*tip)]
        faces = [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4), (3, 2, 1, 0)]
        parts.append(mesh_object('Tooth', verts, faces, M['tooth'], smooth=False))
    # Eyes on the sides of the snout, with a glint.
    for s in (1, -1):
        a = s * math.radians(63)
        lat = hood_edge(a) + 21
        p = hood_point(a, lat, .004)
        parts.append(ellipsoid('Eye', p, (.026, .03, .026), M['eye'], (14, 8)))
        g = hood_point(a - s * .03, lat + 1.8, .027)
        parts.append(ellipsoid('Eye glint', g, (.007, .008, .007), M['glint'], (8, 5)))
        # Three cheek marks behind the eye.
        for k in range(3):
            la = lat - 9 - k * 5.5
            a0, a1 = s * math.radians(78), s * math.radians(98)
            path = [hood_point(a0 + (a1 - a0) * q / 6, la, .003) for q in range(7)]
            parts.append(tube('Cheek mark', path, .0055, M['navy'], segs=6))
    # Stitched seam over the crown, front to back.
    seam = [hood_point(0 if lat < 89 else 0, lat, .002) for lat in range(int(hood_edge(0) + 22), 89, 3)]
    seam += [hood_point(math.pi, lat, .002) for lat in range(89, 10, -3)]
    parts += dashes('Seam', seam, M['navy'])
    crown = B(*(HC + Vector((0, HR.y + .02, 0))))
    return parts, {}, crown


def build_phones(M):
    parts = []
    z = .235
    rx, ry = .335, .432
    band = []
    for k in range(25):
        th = math.radians(-62 + 124 * k / 24)
        band.append((rx * math.sin(th), ry * math.cos(th), z))
    parts.append(tube('Band', band, .016, M['band'], segs=10, flat=.55, up=(0, 0, 1)))
    pad = [p for p in band[8:17]]
    parts.append(tube('Band pad', [(x, y - .012, zz) for x, y, zz in pad], .02, M['cushion'], segs=10, flat=.6, up=(0, 0, 1)))
    for s in (1, -1):
        cx, cy = s * .322, .1
        # slider from band end to cup
        end = band[0] if s < 0 else band[-1]
        parts.append(tube('Slider', [end, (cx, cy + .095, z), (cx, cy + .06, z)], .009, M['band'], segs=8))
        cup = [(.0, s * .284), (.075, s * .284), (.088, s * .294), (.09, s * .322), (.084, s * .348), (.0, s * .35)]
        parts.append(lathe('Cup', [(r, h) for r, h in cup], M['phone'], segs=32, axis='x', offset=(0, cy, z)))
        cush = [(.0, s * .27), (.06, s * .266), (.08, s * .274), (.084, s * .286), (.0, s * .288)]
        parts.append(lathe('Cushion', cush, M['cushion'], segs=32, axis='x', offset=(0, cy, z)))
        # silver plate with UVs for the glare texture
        verts, uvs = [], []
        segs = 32
        x = s * .3515
        centre = len(verts); verts.append(B(x, cy, z)); uvs.append((.5, .5))
        for j in range(segs):
            a = 2 * math.pi * j / segs
            verts.append(B(x, cy + .066 * math.sin(a), z + .066 * math.cos(a)))
            uvs.append((.5 + .5 * math.cos(a) * s, .5 + .5 * math.sin(a)))
        faces = [(0, 1 + j, 1 + (j + 1) % segs) for j in range(segs)]
        plate = mesh_object('Plate', verts, faces, M['plate'], uvs=uvs, inside=B(0, cy, z))
        parts.append(plate)
        ring = [(s * .3515, cy + .068 * math.sin(2 * math.pi * k / 32), z + .068 * math.cos(2 * math.pi * k / 32)) for k in range(33)]
        parts.append(tube('Plate ring', ring, .006, M['band'], segs=6, cap=False))
    return parts, {}, None


def plate_texture():
    """Silver headphone plate: brushed radial gradient with two diagonal glare bands."""
    w = h = 128
    img = bpy.data.images.new('shark_phone_plate.png', w, h, alpha=False)
    px = []
    for y in range(h):
        for x in range(w):
            u, v = (x + .5) / w - .5, (y + .5) / h - .5
            r = math.hypot(u, v) * 2
            base = .78 - .22 * r
            d = (u + v) * 1.4142
            glare = .2 * math.exp(-((d - .05) / .05) ** 2) + .16 * math.exp(-((d + .18) / .035) ** 2)
            c = min(1, base + glare)
            px.extend((c * .96, c * .98, c, 1))
    img.pixels[:] = px
    img.filepath_raw = str(SRC / 'shark_phone_plate.png'); img.file_format = 'PNG'; img.save()


# ---------------------------------------------------------------- body
BELLY_HALF = math.radians(50)


def body_shell(name_prefix, profile, M, seam_rows):
    parts = []
    belly = lambda row, a: abs(a) < BELLY_HALF
    parts.append(lathe(name_prefix, profile, M['skin'], pick=lambda r, a: not belly(r, a)))
    parts.append(lathe(name_prefix + ' belly', profile, M['belly'], pick=belly))
    # stitches down both belly seams
    for s in (1, -1):
        a = s * (BELLY_HALF + .035)
        pts = []
        for k in range(seam_rows[2]):
            h = seam_rows[0] + (seam_rows[1] - seam_rows[0]) * k / (seam_rows[2] - 1)
            r = profile_radius(profile, h) + .003
            pts.append((r * math.sin(a), h, r * math.cos(a)))
        parts += dashes('Seam', pts, M['navy'])
    return parts


def profile_radius(profile, h):
    for (r0, h0), (r1, h1) in zip(profile, profile[1:]):
        if min(h0, h1) <= h <= max(h0, h1) and h0 != h1:
            return r0 + (r1 - r0) * (h - h0) / (h1 - h0)
    return profile[len(profile) // 2][0]


def build_suit(M):
    profile = [(.296, -.02), (.305, .0), (.309, .04), (.31, .2), (.309, .3), (.3, .343), (.283, .37), (.258, .382)]
    parts = body_shell('Suit', profile, M, (.03, .33, 14))
    # Dorsal fin on the back.
    parts.append(fin('Dorsal fin', [(.31, -.285), (.42, -.34), (.47, -.52)], [(.035, -.29), (.21, -.44), (.47, -.52)],
                     (0, 1, 0), (0, 0, 1), (0, 0, 0), lambda v: .055 * (1 - v) + .01, M['skin']))
    # a white tip line on the fin? keep it plain plush; a seam along the fin base instead
    base = [(0., .04 + .26 * k / 9, -.306 - .002) for k in range(10)]
    return parts, {}, None


def build_tail(M):
    profile = [(.27, -.035), (.306, -.022), (.316, .0), (.319, .14), (.318, .25), (.31, .285), (.29, .298)]
    parts = body_shell('Shorts', profile, M, (.0, .27, 12))
    root = Vector((0, .1, -.29))
    path = [bezier([(0, .10, -.27), (0, .03, -.5), (0, .1, -.69)], k / 14) for k in range(15)]
    parts.append(tube('Tail', path, lambda s: .1 * (1 - s) ** .9 + .03, M['skin'], 'tail', segs=14, flat=.72, up=(1, 0, 0)))
    tip = Vector(path[-1])
    thick = lambda v: .04 * (1 - v) + .008
    parts.append(fin('Tail fin upper', [(tip.z + .03, tip.y + .02), (tip.z - .02, tip.y + .14), (tip.z - .13, tip.y + .25)],
                     [(tip.z - .03, tip.y - .02), (tip.z - .08, tip.y + .07), (tip.z - .13, tip.y + .25)],
                     (0, 0, 1), (0, 1, 0), (0, 0, 0), thick, M['skin'], 'tail'))
    parts.append(fin('Tail fin lower', [(tip.z + .02, tip.y - .0), (tip.z - .03, tip.y - .08), (tip.z - .1, tip.y - .14)],
                     [(tip.z - .03, tip.y + .02), (tip.z - .07, tip.y - .03), (tip.z - .1, tip.y - .14)],
                     (0, 0, 1), (0, 1, 0), (0, 0, 0), thick, M['skin'], 'tail'))
    # little dorsal ridge stitches along the tail top
    top = [Vector(p) + Vector((0, .1 * (1 - k / 14) ** .9 * .72 + .025, 0)) for k, p in enumerate(path)][2:12]
    parts += dashes('Seam', [tuple(p) for p in top], M['navy'], group='tail')
    return parts, {'tail': B(*root)}, None


def build_fin(M, side):
    """side 1 = right arm (arm along +x), -1 = left arm (along -x)."""
    sx = side
    parts = []
    sleeve = [(.0, .015), (.086, .02), (.09, .05), (.09, .33), (.096, .37), (.098, .4), (.0, .405)]
    prof = [(r, sx * h) for r, h in sleeve]
    parts.append(lathe('Sleeve', prof, M['skin'], segs=28, axis='x', inside=B(sx * .2, 0, 0)))
    cuff = [(.0, .372), (.1, .374), (.104, .392), (.1, .41), (.0, .412)]
    parts.append(lathe('Cuff', [(r, sx * h) for r, h in cuff], M['belly'], segs=28, axis='x', inside=B(sx * .39, 0, 0)))
    d = Vector((0, .5, -1)).normalized()
    parts.append(fin('Pectoral fin', [(.1, .07), (.16, .21), (.36, .3)], [(.33, .07), (.36, .17), (.36, .3)],
                     (sx, 0, 0), tuple(d), (0, 0, 0), lambda v: .04 * (1 - v) + .008, M['skin']))
    seam = [(sx * (.06 + .3 * k / 12), .0915 * .5, .0915 * .866) for k in range(13)]
    parts += dashes('Seam', seam, M['navy'])
    return parts, {}, None


STYLES = {
    'shark_hood': ('Plush Shark Hood', 'Hat', lambda M: build_hood(M)),
    'shark_phones': ('Plush Shark Headphones', 'Ears', lambda M: build_phones(M)),
    'shark_suit': ('Plush Shark Suit', 'Body Top', lambda M: build_suit(M)),
    'shark_tail': ('Plush Shark Tail', 'Body Bottom', lambda M: build_tail(M)),
    'shark_fin_r': ('Plush Shark Fin Right', 'Arm Right', lambda M: build_fin(M, 1)),
    'shark_fin_l': ('Plush Shark Fin Left', 'Arm Left', lambda M: build_fin(M, -1)),
}

# Slot frames on the player (Unity avatar space, from the game's cosmetic parents): position, local x axis, local y axis.
FRAMES = {
    'Hat': ((0, .6681, -.254), (1, 0, 0), (0, 1, 0)), 'Ears': ((0, .6681, -.254), (1, 0, 0), (0, 1, 0)),
    'Body Top': ((0, .19, 0), (1, 0, 0), (0, 1, 0)), 'Body Bottom': ((0, -.102, 0), (1, 0, 0), (0, 1, 0)),
    'Arm Right': ((.2626, .4448, 0), (.259, -.966, 0), (.966, .259, 0)), 'Arm Left': ((-.2626, .4448, 0), (.259, .966, 0), (-.966, .259, 0)),
}
BASE_FRAMES = {'head_top': 'Hat', 'body_top': 'Body Top', 'body_bot': 'Body Bottom', 'arm_r': 'Arm Right', 'arm_l': 'Arm Left',
               'head_bot': ((0, .5488, 0), (1, 0, 0), (0, 1, 0)), 'leg_r': ((.1292, -.1377, 0), (1, 0, 0), (0, 1, 0)), 'leg_l': ((-.1292, -.1377, 0), (1, 0, 0), (0, 1, 0))}


def frame_matrix(frame):
    pos, ax, ay = frame
    X = Vector(ax); Y = Vector(ay); Z = X.cross(Y)   # Unity is left-handed: z = x cross y with this convention flipped below
    Z = Vector((0, 0, 1)) if abs(X.z) < 1e-6 and abs(Y.z) < 1e-6 else Z
    m = Matrix.Identity(4)
    for i in range(3): m[i][0], m[i][1], m[i][2], m[i][3] = X[i], Y[i], Z[i], pos[i]
    return Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1))) @ m @ Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def vec(v): return {'x': round(float(v.x), 6), 'y': round(float(v.y), 6), 'z': round(float(v.z), 6)}


def export(key, parts, pivots, crown):
    mats_out = []; index = {}; records = []
    lo = Vector((1e9, 1e9, 1e9)); hi = -lo
    tris = 0
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
            index[m.name] = len(mats_out)
            mats_out.append({'name': m.name, 'texture': m.get('texture', ''), 'color': {'r': base[0], 'g': base[1], 'b': base[2], 'a': 1},
                             'transmission': 0, 'emission': p.inputs['Emission Strength'].default_value,
                             'roughness': p.inputs['Roughness'].default_value, 'metallic': p.inputs['Metallic'].default_value,
                             'tint': bool(m.get('tint', False))})
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
        tris += len(tri) // 3
        mi = index[m.name]
        color = {'r': 1, 'g': 1, 'b': 1, 'a': 1} if mats_out[mi]['texture'] else mats_out[mi]['color']
        records.append({'name': ob.name, 'group': ob.get('group', 'static'), 'phase': 0, 'material': mi, 'vertices': verts, 'normals': normals,
                        'uv': uv, 'triangles': tri, 'color': color, 'texture': mats_out[mi]['texture'],
                        'rest': {'position': vec(C @ center), 'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'scale': {'x': 1, 'y': 1, 'z': 1}}, 'samples': []})
    data = {'version': 1, 'fps': 24, 'frames': 1, 'materials': mats_out, 'parts': records,
            'pivots': [{'group': g, 'position': vec(C @ p)} for g, p in pivots.items()]}
    if crown is not None: data['pivots'].append({'group': 'crown', 'position': vec(C @ crown)})  # leader-crown anchor for hats
    (SRC / ('v11_' + key + '.mesh.json')).write_text(json.dumps(data, separators=(',', ':')))
    size = C @ (hi - lo)
    print(f'EXPORT {key}: parts {len(records)}, triangles {tris}, size (Unity) {size.x:.3f} x {size.y:.3f} x {size.z:.3f}', flush=True)


def stand_in_body():
    """The game's base body as a grey stand-in, for previews only (never exported)."""
    if not ARGS.base or not ARGS.base.exists(): return []
    grey = material('Preview body', (.42, .44, .47), .55)
    data = json.loads(ARGS.base.read_text())
    obs = []
    for key, parts in data.items():
        frame = BASE_FRAMES.get(key)
        if frame is None: continue
        if isinstance(frame, str): frame = FRAMES[frame]
        M = frame_matrix(frame)
        for p in parts:
            if 'flat' in p['mesh'] and key == 'body_top': continue
            verts = [B(*v) for v in p['v']]
            faces = [tuple(f) for f in p['f']]
            me = bpy.data.meshes.new(key); me.from_pydata([tuple(v) for v in verts], [], faces); me.update()
            ob = bpy.data.objects.new(key, me); bpy.context.collection.objects.link(ob)
            for poly in me.polygons: poly.use_smooth = True
            me.materials.append(grey); ob.matrix_world = M
            obs.append(ob)
    return obs


def lights_and_camera():
    scene = bpy.context.scene
    world = bpy.data.worlds.new('World'); scene.world = world; world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.035, .04, .05, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 1.0
    for loc, energy in (((-1.4, 2.0, 1.8), 90), ((1.8, 1.2, 1.0), 45), ((0, -2.2, 1.6), 60), ((0, 0, 3), 30)):
        bpy.ops.object.light_add(type='AREA', location=loc); light = bpy.context.object
        light.data.energy = energy; light.data.size = 1.6
        light.rotation_euler = (Vector((0, 0, .3)) - light.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.ops.object.camera_add(); cam = bpy.context.object; scene.camera = cam; cam.data.lens = 50
    return cam


def look(cam, eye, target):
    cam.location = eye; cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()


def icon(key, parts, cam):
    scene = bpy.context.scene
    scene.render.film_transparent = True; scene.render.resolution_x = scene.render.resolution_y = 256
    lo = Vector((1e9,) * 3); hi = -lo
    for ob in parts:
        for v in ob.data.vertices:
            p = ob.matrix_world @ v.co
            lo = Vector(tuple(min(lo[i], p[i]) for i in range(3))); hi = Vector(tuple(max(hi[i], p[i]) for i in range(3)))
    c = (lo + hi) / 2; r = (hi - lo).length / 2
    view = {'shark_tail': Vector((1.0, -.45, .35)), 'shark_suit': Vector((1.0, .45, .35)), 'shark_fin_r': Vector((.25, -1.0, .75)),
            'shark_fin_l': Vector((-.25, -1.0, .75)), 'shark_phones': Vector((.6, 1.0, .3))}.get(key, Vector((.85, 1.0, .35)))
    look(cam, c + view.normalized() * r * 2.35, c)
    scene.render.filepath = str(SRC / ('cosmetic_' + key + '_icon.png')); bpy.render_render()


def bpy_render():
    bpy.ops.render.render(write_still=True)


bpy.render_render = bpy_render


def build_all():
    reset()
    plate_texture()
    M = mats()
    built = {}
    for key, (title, slot, fn) in STYLES.items():
        if ARGS.only and key not in ARGS.only.split(','): continue
        parts, pivots, crown = fn(M)
        export(key, parts, pivots, crown)
        built[key] = (slot, parts)
    return built


built = build_all()
cam = lights_and_camera()
# icons: each part alone
everything = [ob for _, parts in built.values() for ob in parts]
for key, (slot, parts) in built.items():
    for ob in everything: ob.hide_render = ob not in parts
    for ob in parts: ob.matrix_world = Matrix.Identity(4)
    icon(key, parts, cam)
if ARGS.preview:
    for ob in everything: ob.hide_render = False
    for key, (slot, parts) in built.items():
        Mf = frame_matrix(FRAMES[slot])
        for ob in parts: ob.matrix_world = Mf
    stand_in_body()
    scene = bpy.context.scene
    scene.render.film_transparent = False; scene.render.resolution_x = 760; scene.render.resolution_y = 900
    target = Vector((0, -.05, .22))
    for suffix, eye in (('front', Vector((1.0, 2.6, .9))), ('back', Vector((-1.2, -2.5, .9))), ('side', Vector((2.8, -.3, .55))), ('head', None)):
        if eye is None:
            scene.render.resolution_x = scene.render.resolution_y = 700
            t = Vector((0, .05, .82)); look(cam, t + Vector((.75, 1.05, .3)), t)
        else:
            look(cam, target + eye, target)
        scene.render.filepath = str(ART / ('shark_' + suffix + '.png')); bpy_render()
print('SHARK_COMPLETE', flush=True)
