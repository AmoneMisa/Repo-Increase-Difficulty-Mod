"""Shared Blender helpers for the mod's cosmetic sets (tools/build_legate.py and later sets).

Import from a build script run as: blender --background --factory-startup --python tools/build_<set>.py -- --root PATH [--base JSON] [--preview]
Call configure(root, base) first. Everything is built in a cosmetic slot's own frame in Unity coordinates and exported with
Unity (x, y, z) = Blender (x, z, y); run(...) exports each part to src/Art/v11_<key>.mesh.json, renders its menu icon and,
with previews, renders the whole set on a grey stand-in robot (the game's base meshes, never exported).
"""
import json
import math
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

C = Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))
TAU = 2 * math.pi
SRC = None
BASE = None


def configure(root, base=None):
    global SRC, BASE
    SRC = Path(root).resolve() / 'src' / 'Art'
    BASE = Path(base) if base else None

def B(x, y, z):
    return Vector((x, z, y))


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
    if not BASE or not BASE.exists(): return []
    grey = material('Preview body', (.42, .44, .47), .55)
    data = json.loads(BASE.read_text())
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


def run(styles, views, art_dir, prefix, preview, distance=3.0, preview_views=None, setup=None):
    """styles: key -> (slot, build(M) -> (parts, pivots, crown)); setup() -> M (materials, after textures)."""
    setup_scene()
    M = setup()
    built = {}
    for key, (slot, fn) in styles.items():
        parts, pivots, crown = fn(M)
        if crown is not None: pivots = dict(pivots, crown=crown)
        export(key, parts, pivots)
        built[key] = (slot, parts)
    cam = lights_and_camera()
    scene = bpy.context.scene
    everything = [ob for _, parts in built.values() for ob in parts]
    scene.render.film_transparent = True; scene.render.resolution_x = scene.render.resolution_y = 256
    for key, (slot, parts) in built.items():
        for ob in everything: ob.hide_render = ob not in parts
        lo = Vector((1e9,) * 3); hi = -lo
        for ob in parts:
            for v in ob.data.vertices:
                lo = Vector(tuple(min(lo[i], v.co[i]) for i in range(3))); hi = Vector(tuple(max(hi[i], v.co[i]) for i in range(3)))
        c = (lo + hi) / 2; r = (hi - lo).length / 2
        look(cam, c + Vector(views.get(key, (.7, 1., .3))).normalized() * r * distance, c)
        render(SRC / ('cosmetic_' + key + '_icon.png'))
    if preview:
        art_dir = Path(art_dir); art_dir.mkdir(parents=True, exist_ok=True)
        for ob in everything: ob.hide_render = False
        for key, (slot, parts) in built.items():
            for ob in parts: ob.matrix_world = frame_matrix(FRAMES[slot])
        stand_in_body()
        scene.render.film_transparent = False; scene.render.resolution_x = 760; scene.render.resolution_y = 900
        target = Vector((0, -.05, .1))
        views = preview_views or (('front', (1.0, 2.6, .8)), ('back', (-1.2, -2.5, .8)), ('side', (2.8, -.3, .45)))
        for suffix, eye in views:
            look(cam, target + Vector(eye), target); render(art_dir / (prefix + '_' + suffix + '.png'))
    return built
