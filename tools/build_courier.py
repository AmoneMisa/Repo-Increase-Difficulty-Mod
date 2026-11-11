"""Courier pets: five original chibi animals (penguin, chicken, cat, owl, beaver) with a delivery backpack.

Run:
  blender --background --factory-startup --python tools/build_courier.py -- --root . [--preview]

Writes src/Art/courier_<animal>.mesh.json (one model per animal) plus the shared greyscale textures
src/Art/courier_fur.png and courier_cloth.png, and previews under art/courier/.

Every part is named "group|channel|label":
  group   - what animates it in game: body, arm_l, arm_r, foot_l, foot_r, tail, and the expression sets
            eyes_open, eyes_happy, eyes_wide, eyes_sleepy, brows_sad, brows_focus (one shown at a time)
  channel - which recolour slot tints it: main, second, accent; fixed parts keep their colour
The model also carries "pivots": where each animated group turns (Unity space, metres, feet on y = 0).
Front is Blender +Y (Unity +Z).
"""
import argparse, json, math, sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

args = argparse.ArgumentParser()
args.add_argument('--root', type=Path, required=True)
args.add_argument('--preview', action='store_true')
opts = args.parse_args(sys.argv[sys.argv.index('--') + 1:])
ROOT = opts.root.resolve()
OUT = ROOT / 'src' / 'Art'
ART = ROOT / 'art' / 'courier'; ART.mkdir(parents=True, exist_ok=True)
C = Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))  # Blender (x, y, z) -> Unity (x, z, y)


def clear():
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
        for item in list(block):
            if item.users == 0: block.remove(item)


# ------------------------------------------------------------------ textures (greyscale, tinted in game)

def save_texture(name, pixels):
    h, w = pixels.shape
    img = bpy.data.images.new(name, w, h, alpha=False)
    rgba = np.ones((h, w, 4), dtype=np.float32)
    rgba[..., 0] = rgba[..., 1] = rgba[..., 2] = pixels
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = str(OUT / (name + '.png')); img.file_format = 'PNG'; img.save()
    return img


def smooth_noise(size, cells, seed):
    rng = np.random.default_rng(seed)
    grid = rng.random((cells + 1, cells + 1)); grid[-1, :] = grid[0, :]; grid[:, -1] = grid[:, 0]
    t = np.linspace(0, cells, size, endpoint=False)
    i = t.astype(int); f = t - i; f = f * f * (3 - 2 * f)
    a = grid[i][:, i]; b = grid[i][:, i + 1]; c = grid[i + 1][:, i]; d = grid[i + 1][:, i + 1]
    fx = f[None, :]; fy = f[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def make_textures():
    size = 256
    # Fur/feather: soft blotches plus fine short strands, kept light so the tint stays true.
    fur = .55 * smooth_noise(size, 6, 1) + .3 * smooth_noise(size, 24, 2) + .15 * smooth_noise(size, 64, 3)
    rng = np.random.default_rng(4)
    strands = np.zeros((size, size))
    for _ in range(2600):
        x, y = rng.integers(0, size, 2); length = rng.integers(3, 7); v = rng.uniform(-1, 1)
        for k in range(length): strands[(y + k) % size, (x + k // 3) % size] += v
    fur = .84 + .1 * (fur - fur.mean()) / (fur.std() + 1e-6) * .5 + .035 * np.clip(strands, -1, 1)
    save_texture('courier_fur', np.clip(fur, .62, 1.0).astype(np.float32))
    # Canvas weave for the backpack and harness.
    yy, xx = np.mgrid[0:size, 0:size]
    weave = .5 + .5 * np.sin(xx * math.pi / 2) * np.sign(np.sin(yy * math.pi / 8 + .3))
    cloth = .86 + .06 * weave + .06 * (smooth_noise(size, 16, 5) - .5) - .05 * (smooth_noise(size, 4, 6) > .78)
    save_texture('courier_cloth', np.clip(cloth, .6, 1.0).astype(np.float32))


# ------------------------------------------------------------------ primitives

PREVIEW_MATS = {}


def preview_material(color):
    key = tuple(round(c, 3) for c in color)
    if key in PREVIEW_MATS: return PREVIEW_MATS[key]
    m = bpy.data.materials.new('pv %s' % (key,)); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*color, 1); b.inputs['Roughness'].default_value = .87
    PREVIEW_MATS[key] = m
    return m


PARTS = []


def add(o, group, channel, color, label):
    o.name = f'{group}|{channel}|{label}'
    o['ihm_color'] = list(color)
    o['ihm_texture'] = {'main': 'courier_fur.png', 'second': 'courier_fur.png', 'accent': 'courier_cloth.png'}.get(channel, '')
    o.data.materials.clear(); o.data.materials.append(preview_material(color))
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.shade_smooth()
    PARTS.append(o)
    return o


def ellipsoid(group, channel, color, label, loc, radii, rot=(0, 0, 0), segs=20, rings=10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=rings, radius=1, location=loc, rotation=rot)
    o = bpy.context.object; o.scale = radii
    return add(o, group, channel, color, label)


def cone(group, channel, color, label, base, tip, r, verts=16, r2=0.0):
    base, tip = Vector(base), Vector(tip)
    d = tip - base
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=d.length, location=(base + tip) / 2)
    o = bpy.context.object; o.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
    return add(o, group, channel, color, label)


def box(group, channel, color, label, loc, size, bevel=.012, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.object; o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel > 0:
        mod = o.modifiers.new('bevel', 'BEVEL'); mod.width = bevel; mod.segments = 3
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return add(o, group, channel, color, label)


def ring(group, channel, color, label, loc, major, minor, scale=(1, 1, 1), rot=(0, 0, 0), arc=None):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=40, minor_segments=8,
                                     location=(0, 0, 0))
    o = bpy.context.object
    if arc is not None:  # keep only the upper half (local +Y), for the closed "happy" eyes
        bm = bmesh.new(); bm.from_mesh(o.data)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y < -minor * .2], context='VERTS')
        bm.to_mesh(o.data); bm.free()
    o.scale = scale; o.rotation_euler = rot; o.location = loc
    return add(o, group, channel, color, label)


def blob(group, channel, color, label, elements, resolution=.012):
    """A soft one-piece shape from metaball ellipsoids: the head flows into the body with no seam."""
    mb = bpy.data.metaballs.new(label); mb.resolution = resolution; mb.render_resolution = resolution; mb.threshold = .6
    o = bpy.data.objects.new(label, mb); bpy.context.collection.objects.link(o)
    for co, radii, stiff in elements:
        e = mb.elements.new(type='ELLIPSOID'); e.co = co; e.radius = 1.72; e.stiffness = stiff  # surface at ~1x size for stiffness 2, threshold .6
        e.size_x, e.size_y, e.size_z = radii
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.convert(target='MESH')
    o = bpy.context.object
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=.0005); bpy.ops.object.mode_set(mode='OBJECT')
    return add(o, group, channel, color, label)


def tail_curve(group, channel, color, label, points, root_r, tip_r):
    """A smooth tapered tail along a curve (one mesh, not beads)."""
    curve = bpy.data.curves.new(label, 'CURVE'); curve.dimensions = '3D'
    curve.bevel_depth = 1.0; curve.bevel_resolution = 6; curve.resolution_u = 14; curve.use_fill_caps = True
    spline = curve.splines.new('BEZIER'); spline.bezier_points.add(len(points) - 1)
    n = len(points)
    for k, (bp, p) in enumerate(zip(spline.bezier_points, points)):
        bp.co = p; bp.handle_left_type = bp.handle_right_type = 'AUTO'
        f = k / (n - 1); bp.radius = root_r + (tip_r - root_r) * f ** 1.3
    o = bpy.data.objects.new(label, curve); bpy.context.collection.objects.link(o)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.convert(target='MESH')
    o = bpy.context.object
    # A round tip instead of a flat cap.
    ellipsoid(group, channel, color, label + '_tip', points[-1], (tip_r * 1.05,) * 3, segs=16, rings=8)
    return add(o, group, channel, color, label)


# ------------------------------------------------------------------ shared pieces

WHITE, BLACK = (.95, .95, .93), (.035, .035, .04)

# Every pet shares one chibi frame, like the concept: a big round head flowing into a small round body.
HEAD_Z, BODY_Z = .39, .155


HEAD_SCALE = 1.1


def chibi(main, head=(.185, .165, .16), body=(.165, .15, .165), head_y=.0):
    """A big round head with chubby cheeks sitting on a smaller round body: two shapes, so the neck line shows."""
    hx, hy, hz = (v * HEAD_SCALE for v in head)
    blob('body', 'main', main, 'body', [((0, 0, BODY_Z), body, 2.0)])
    cheeks = [((s * hx * .6, hy * .35, HEAD_Z - hz * .38), (hx * .45, hy * .45, hz * .42), 1.3) for s in (1, -1)]
    blob('body', 'main', main, 'head', [((0, head_y, HEAD_Z), (hx, hy, hz), 2.0)] + cheeks)


def eyes(x, y, z, r=.05, iris=None):
    """Big glossy eyes and every expression set. The game shows one set at a time:
    eyes_open (neutral), eyes_happy (^ ^), eyes_wide (surprised: tiny pupils), eyes_sleepy (closed lines),
    plus brows_sad and brows_focus drawn over the open eyes."""
    for s, side in ((1, 'l'), (-1, 'r')):
        ex = s * x
        # Open: a glossy dome in a goggle rim, a big iris, pupil and two shines
        ring('eyes_open', 'fixed', (.19, .11, .07), 'eyelid_' + side, (ex, y - r * .08, z), r * 1.02, r * .06, rot=(math.pi / 2, 0, 0))
        ellipsoid('eyes_open', 'fixed', WHITE, 'sclera_' + side, (ex, y, z), (r * 1.06, r * .6, r * 1.14))
        ellipsoid('eyes_open', 'fixed', iris or (.25, .16, .09), 'iris_' + side, (ex, y + r * .52, z - r * .06), (r * .56, r * .20, r * .62))
        ellipsoid('eyes_open', 'fixed', BLACK, 'pupil_' + side, (ex, y + r * .55, z - r * .06), (r * .46, r * .18, r * .52))
        ellipsoid('eyes_open', 'fixed', (1, 1, 1), 'shine_' + side, (ex + s * r * .2, y + r * .66, z + r * .26), (r * .22, r * .08, r * .22), segs=12, rings=8)
        ellipsoid('eyes_open', 'fixed', (1, 1, 1), 'shine2_' + side, (ex - s * r * .22, y + r * .64, z - r * .34), (r * .1, r * .06, r * .1), segs=10, rings=6)
        # Soft blush under the eye (every mood)
        ellipsoid('body', 'fixed', (.98, .62, .64), 'blush_' + side, (s * (x + r * .5), y - r * .1, z - r * 1.25), (r * .5, r * .12, r * .28))
        # Surprised: wider white, small pupils
        ring('eyes_wide', 'fixed', (.19, .11, .07), 'eyelid_' + side, (ex, y - r * .08, z + r * .06), r * 1.18, r * .06, rot=(math.pi / 2, 0, 0))
        ellipsoid('eyes_wide', 'fixed', WHITE, 'sclera_' + side, (ex, y + .002, z + r * .06), (r * 1.18, r * .6, r * 1.28))
        ellipsoid('eyes_wide', 'fixed', BLACK, 'pupil_' + side, (ex, y + r * .52, z), (r * .26, r * .2, r * .3))
        ellipsoid('eyes_wide', 'fixed', (1, 1, 1), 'shine_' + side, (ex + s * r * .1, y + r * .6, z + r * .12), (r * .09, r * .06, r * .09), segs=10, rings=6)
        # Happy ^ ^
        ring('eyes_happy', 'fixed', BLACK, 'smile_' + side, (ex, y + r * .45, z - r * .1), r * .62, r * .13, rot=(math.pi / 2, 0, 0), arc=True)
        # Sleepy: a soft downward curve
        ring('eyes_sleepy', 'fixed', BLACK, 'sleep_' + side, (ex, y + r * .45, z - r * .25), r * .55, r * .11, rot=(math.pi / 2, math.pi, 0), arc=True)
        # Brows: sad (inner ends up) and focused (inner ends down)
        for name, angle in (('brows_sad', .38), ('brows_focus', -.42)):
            box(name, 'fixed', BLACK, 'brow_' + side, (ex, y + r * .5, z + r * 1.35), (r * 1.05, r * .2, r * .2), bevel=r * .08,
                rot=(0, s * angle, 0))


def backpack(back_y, z=.22, wide=.2):
    """Delivery backpack on the back with a parcel badge and an amber status strip."""
    back = back_y
    box('body', 'accent', (.5, .36, .2), 'pack', (0, back - .06, z), (wide + .03, .13, .24), bevel=.024)
    box('body', 'accent', (.44, .31, .17), 'pack_lid', (0, back - .06, z + .125), (wide + .044, .144, .035), bevel=.012)
    for k, (dx, dz, sx, sz) in enumerate(((0, .035, .07, .009), (0, -.035, .07, .009), (.031, 0, .009, .07), (-.031, 0, .009, .07), (0, .012, .07, .006))):
        box('body', 'fixed', (.16, .12, .08), f'pack_mark{k}', (dx, back - .127, z - .005 + dz), (sx, .006, sz), bevel=0)
    box('body', 'fixed', (1.0, .62, .12), 'pack_strip', (0, back - .127, z - .075), (.12, .008, .018), bevel=.003)


def harness(r_x, r_y, z):
    """Chest belt with a glowing buckle in front, harness band round the back."""
    ring('body', 'accent', (.4, .29, .16), 'harness', (0, 0, z), 1.0, .011, scale=(r_x + .004, r_y + .004, 1.6), rot=(0, 0, math.pi), arc=True)
    ring('body', 'accent', (.2, .19, .18), 'belt', (0, 0, z), 1.0, .013, scale=(r_x + .006, r_y + .006, 1.5), arc=True)
    box('body', 'fixed', (.14, .13, .12), 'buckle', (0, r_y + .012, z), (.08, .016, .034), bevel=.006)
    box('body', 'fixed', (1.0, .7, .15), 'buckle_glow', (0, r_y + .021, z), (.06, .006, .014), bevel=.002)


def cap(top, front=.0):
    """Courier cap (accent colour): crown, button, forward brim and a small glowing badge."""
    ellipsoid('body', 'accent', (.2, .19, .18), 'cap', (0, front - .01, top - .04), (.13, .125, .08))
    ellipsoid('body', 'accent', (.2, .19, .18), 'cap_button', (0, front - .01, top + .038), (.018, .018, .012), segs=12, rings=8)
    ellipsoid('body', 'accent', (.2, .19, .18), 'cap_brim', (0, front + .095, top - .05), (.1, .075, .013), rot=(-.32, 0, 0))
    box('body', 'fixed', (1.0, .7, .15), 'cap_badge', (0, front + .088, top - .015), (.05, .02, .022), bevel=.004, rot=(-.6, 0, 0))


def gear(head=(.185, .165, .16), body=(.165, .15, .165), accent=(.85,.44,.10), with_cap=True):
    harness(body[0] + .005, body[1] + .005, BODY_Z + .02)
    backpack(-body[1] - .005, z=BODY_Z + .07)
    if with_cap:
        start=len(PARTS)
        cap(HEAD_Z + head[2] * HEAD_SCALE + .02)
        for o in PARTS[start:]:
            if o.name.split('|')[1]=='accent':
                o['ihm_color']=list(accent);o.data.materials.clear();o.data.materials.append(preview_material(accent))
    # A triangular courier neckerchief, its badge and a side satchel.
    mesh=bpy.data.meshes.new('scarf')
    mesh.from_pydata([(-.13,.135,.29),(.13,.135,.29),(0,.175,.195),(-.13,.129,.29),(.13,.129,.29),(0,.169,.195)],
                     [],[(0,1,2),(5,4,3),(0,3,4,1),(1,4,5,2),(2,5,3,0)])
    mesh.update();o=bpy.data.objects.new('scarf',mesh);bpy.context.collection.objects.link(o)
    add(o,'scarf','accent',accent,'neckerchief')
    box('scarf','fixed',(.94,.84,.62),'parcel badge',(0,.164,.25),(.036,.012,.036),bevel=.002,rot=(0,math.pi/4,0))
    box('body','fixed',(.21,.105,.045),'satchel',(-.15,.075,.115),(.09,.075,.09),bevel=.009)
    box('body','fixed',(.44,.25,.105),'satchel flap',(-.15,.119,.139),(.10,.012,.05),bevel=.006)
    box('body','fixed',(.76,.56,.25),'satchel buckle',(-.15,.128,.12),(.025,.006,.024),bevel=.002)


# Eye placement on the front of the head
EYE_Z = HEAD_Z + .015


def face_y(head):
    return head[1] * HEAD_SCALE * .86


# ------------------------------------------------------------------ animals

def penguin():
    main, belly, orange = (.12, .14, .2), (.93, .92, .88), (.98, .55, .12)
    head = (.19, .17, .165)
    chibi(main, head)
    ellipsoid('body', 'second', belly, 'belly', (0, .055, BODY_Z - .01), (.13, .11, .14))
    for s, side in ((1, 'l'), (-1, 'r')):
        ellipsoid('body', 'second', belly, 'mask_' + side, (s * .07, .125, EYE_Z), (.088, .075, .098))
    eyes(.07, face_y(head) + .02, EYE_Z, .062)
    cone('body', 'fixed', orange, 'beak', (0, .19, EYE_Z - .055), (0, .26, EYE_Z - .065), .036, r2=.006)
    for s, side in ((1, 'l'), (-1, 'r')):
        ellipsoid('arm_' + side, 'main', main, 'flipper', (s * .175, -.005, .17), (.03, .07, .11), rot=(0, s * .3, 0))
        ellipsoid('foot_' + side, 'fixed', orange, 'foot', (s * .065, .07, .016), (.05, .07, .018))
        for k in (-1, 0, 1):
            ellipsoid('foot_' + side, 'fixed', orange, f'toe{k}', (s * .065 + k * .028, .125, .016), (.017, .028, .014), segs=12, rings=8)
    ellipsoid('tail', 'main', main, 'tail', (0, -.15, .05), (.055, .05, .03), rot=(-.5, 0, 0))
    gear(head)


def chicken():
    main, red, yellow = (.95, .93, .88), (.86, .14, .12), (.98, .76, .18)
    head = (.175, .16, .155)
    chibi(main, head, body=(.17, .16, .165))
    eyes(.07, face_y(head) + .015, EYE_Z, .052)
    cone('body', 'fixed', yellow, 'beak', (0, .18, EYE_Z - .055), (0, .245, EYE_Z - .065), .032, r2=.004)
    ellipsoid('body', 'second', red, 'wattle', (0, .19, EYE_Z - .1), (.02, .018, .035))
    for k, (y, z, r) in enumerate(((.02, .02, .036), (-.03, .04, .044), (-.08, .02, .036))):
        ellipsoid('body', 'second', red, f'comb{k}', (0, y, HEAD_Z + head[2] + .06 + z), (.022, r, r))
    for s, side in ((1, 'l'), (-1, 'r')):
        ellipsoid('arm_' + side, 'main', main, 'wing', (s * .17, -.02, .17), (.045, .095, .1), rot=(.2, s * .3, 0))
        ellipsoid('arm_' + side, 'main', main, 'wingtip', (s * .18, -.085, .12), (.03, .055, .055), rot=(.4, s * .3, 0))
        cone('foot_' + side, 'fixed', yellow, 'leg', (s * .06, .02, .05), (s * .06, .03, .0), .012)
        for k in (-1, 0, 1):
            cone('foot_' + side, 'fixed', yellow, f'toe{k}', (s * .06, .03, .008), (s * .06 + k * .033, .095, .008), .01, r2=.004)
    for k, a in enumerate((-.35, 0, .35)):
        ellipsoid('tail', 'main', main, f'feather{k}', (a * .1, -.165, .25 - abs(a) * .05), (.03, .05, .09), rot=(-.6, a, 0))
    gear(head, (.17, .16, .165))


def cat():
    main, light, pink = (.55, .53, .5), (.9, .86, .8), (.95, .6, .62)
    head = (.19, .16, .15)
    chibi(main, head, body=(.155, .14, .16))
    ellipsoid('body', 'second', light, 'belly', (0, .07, BODY_Z - .02), (.11, .09, .12))
    ellipsoid('body', 'second', light, 'muzzle', (0, .165, EYE_Z - .065), (.078, .045, .045))
    eyes(.075, face_y(head) + .01, EYE_Z + .005, .056, iris=(.55, .78, .25))
    ellipsoid('body', 'fixed', pink, 'nose', (0, .207, EYE_Z - .05), (.017, .011, .012))
    top = HEAD_Z + head[2]
    for s, side in ((1, 'l'), (-1, 'r')):
        cone('body', 'main', main, 'ear_' + side, (s * .12, 0, top - .04), (s * .15, .005, top + .07), .055, r2=.006)
        cone('body', 'fixed', pink, 'ear_in_' + side, (s * .12, .025, top - .035), (s * .145, .03, top + .05), .032, r2=.004)
        for k in (-1, 1):
            cone('body', 'fixed', (.2, .19, .18), f'whisker_{side}{k}', (s * .05, .2, EYE_Z - .065 + k * .01),
                 (s * .2, .19, EYE_Z - .06 + k * .03), .003, verts=6, r2=.001)
        ellipsoid('arm_' + side, 'main', main, 'paw', (s * .11, .085, .09), (.042, .05, .08), rot=(.3, 0, 0))
        ellipsoid('arm_' + side, 'second', light, 'paw_tip', (s * .11, .115, .025), (.038, .042, .028))
        ellipsoid('foot_' + side, 'main', main, 'foot', (s * .08, .03, .028), (.048, .068, .033))
    # A smooth tail that curls up behind the backpack, off to one side.
    tail_curve('tail', 'main', main, 'tail', [(.05, -.13, .06), (.15, -.2, .08), (.22, -.19, .17), (.21, -.13, .27), (.16, -.1, .31)], .03, .022)
    gear(head, (.155, .14, .16))


def owl():
    main, disc, amber, beak = (.42, .3, .2), (.86, .76, .6), (.96, .62, .1), (.3, .27, .22)
    head = (.2, .175, .165)
    chibi(main, head, body=(.175, .16, .17))
    ellipsoid('body', 'second', disc, 'belly', (0, .075, BODY_Z - .01), (.12, .1, .13))
    for s, side in ((1, 'l'), (-1, 'r')):
        ellipsoid('body', 'second', disc, 'disc_' + side, (s * .076, .14, EYE_Z), (.092, .06, .1))
    for k in range(6):
        a = (k % 3 - 1) * .045; zz = BODY_Z - .06 + (k // 3) * .05
        ellipsoid('body', 'main', main, f'speck{k}', (a + (k // 3 % 2) * .02, .165, zz), (.012, .006, .016), segs=10, rings=6)
    eyes(.074, face_y(head) + .02, EYE_Z, .066, iris=amber)
    cone('body', 'fixed', beak, 'beak', (0, .2, EYE_Z - .045), (0, .215, EYE_Z - .1), .022, r2=.002)
    top = HEAD_Z + head[2]
    for s, side in ((1, 'l'), (-1, 'r')):
        cone('body', 'main', main, 'tuft_' + side, (s * .12, -.01, top - .03), (s * .18, -.02, top + .07), .05, r2=.004)
        ellipsoid('arm_' + side, 'main', main, 'wing', (s * .18, -.03, .17), (.045, .1, .13), rot=(.15, s * .25, 0))
        ellipsoid('arm_' + side, 'second', disc, 'wing_bar', (s * .19, -.02, .13), (.035, .075, .028), rot=(.15, s * .25, 0))
        for k in (-1, 0, 1):
            cone('foot_' + side, 'fixed', (.96, .74, .22), f'talon{k}', (s * .065, .05, .012), (s * .065 + k * .025, .105, .006), .014, r2=.003)
    ellipsoid('tail', 'main', main, 'tail', (0, -.16, .06), (.065, .05, .04), rot=(-.4, 0, 0))
    gear(head, (.175, .16, .17))


def beaver():
    main, light, tail = (.48, .3, .17), (.82, .66, .48), (.23, .17, .13)
    head = (.185, .165, .155)
    chibi(main, head, body=(.18, .165, .17))
    ellipsoid('body', 'second', light, 'belly', (0, .075, BODY_Z - .01), (.13, .1, .13))
    ellipsoid('body', 'second', light, 'muzzle', (0, .16, EYE_Z - .07), (.088, .055, .052))
    eyes(.072, face_y(head) + .01, EYE_Z + .01, .054)
    ellipsoid('body', 'fixed', (.12, .08, .07), 'nose', (0, .21, EYE_Z - .045), (.03, .018, .02))
    box('body', 'fixed', (.98, .96, .9), 'teeth', (0, .2, EYE_Z - .12), (.042, .012, .045), bevel=.006)
    box('body', 'fixed', (.6, .55, .5), 'teeth_gap', (0, .207, EYE_Z - .12), (.003, .003, .044), bevel=0)
    top = HEAD_Z + head[2]
    for s, side in ((1, 'l'), (-1, 'r')):
        ellipsoid('body', 'main', main, 'ear_' + side, (s * .14, -.01, top - .03), (.035, .022, .035))
        ellipsoid('arm_' + side, 'main', main, 'paw', (s * .13, .1, .12), (.038, .048, .065), rot=(.6, 0, 0))
        ellipsoid('foot_' + side, 'main', tail, 'foot', (s * .08, .05, .022), (.052, .078, .025))
    ellipsoid('tail', 'fixed', tail, 'tail', (0, -.26, .035), (.1, .14, .022), rot=(.15, 0, 0))
    for k in range(4):
        box('tail', 'fixed', (.15, .11, .09), f'hatch{k}', (0, -.2 - k * .04, .058 - k * .004), (.17, .004, .004), bevel=0, rot=(.15, 0, 0))
    gear(head, (.18, .165, .17))


ANIMALS = {'penguin': penguin, 'chicken': chicken, 'cat': cat, 'owl': owl, 'beaver': beaver}

PIVOTS = {
    'penguin': {'arm_l': (.165, 0, .25), 'arm_r': (-.165, 0, .25), 'foot_l': (.065, .03, .05), 'foot_r': (-.065, .03, .05), 'tail': (0, -.13, .08)},
    'chicken': {'arm_l': (.16, 0, .25), 'arm_r': (-.16, 0, .25), 'foot_l': (.06, .02, .06), 'foot_r': (-.06, .02, .06), 'tail': (0, -.14, .2)},
    'cat': {'arm_l': (.11, .07, .16), 'arm_r': (-.11, .07, .16), 'foot_l': (.08, .02, .055), 'foot_r': (-.08, .02, .055), 'tail': (.05, -.12, .06)},
    'owl': {'arm_l': (.17, -.02, .28), 'arm_r': (-.17, -.02, .28), 'foot_l': (.065, .04, .04), 'foot_r': (-.065, .04, .04), 'tail': (0, -.14, .09)},
    'beaver': {'arm_l': (.13, .08, .18), 'arm_r': (-.13, .08, .18), 'foot_l': (.08, .04, .05), 'foot_r': (-.08, .04, .05), 'tail': (0, -.15, .05)},
}

from courier_species import install
install(globals())
for _p in PIVOTS.values(): _p.setdefault('scarf',(0,.135,.29))


def surface_detail(animal):
    """Silhouette-level fur/feather layers; the atlas supplies the finer fibres."""
    import random
    rng=random.Random(71)
    base=next(tuple(o['ihm_color']) for o in PARTS if o.name.split('|')[1]=='main')
    if animal in ('cat','beaver','dog'):
        # Small overlapping cheek and chest tufts, tapered instead of soft spheres.
        for s in (-1,1):
            for k in range(6):
                z=.31+k*.025; x=s*(.154+.013*math.sin(k))
                cone('body','main',base,'cheek fur',(x,.06,z),(x+s*.035,.055,z-.025),.018,verts=7,r2=.001)
        for row in range(3):
            for k in range(32):
                a=math.tau*(k+row*.4)/32;z=.19-row*.022
                cone('body','main',base,'chest fur',(math.sin(a)*.15,math.cos(a)*.135,z),
                     (math.sin(a)*.155,math.cos(a)*.14,z-.015-rng.random()*.007),.009,verts=5,r2=.001)
    # Break the perfect sphere highlight with small fibre clumps on fur-bearing surfaces.
    if animal in ('cat','beaver','dog','sheep','cow'):
        for o in PARTS:
            group,channel,label=o.name.split('|',2)
            if channel not in ('main','second') or group.startswith('eyes_'):continue
            for v in o.data.vertices:
                p=v.co
                amount=.0007*(math.sin(p.x*613+p.z*211)*math.sin(p.y*389-p.z*71))
                v.co += v.normal*amount
            o.data.update()
    if animal in ('owl','chicken','penguin'):
        for s,side in ((1,'l'),(-1,'r')):
            for k in range(5):
                ellipsoid('arm_'+side,'main',base,'feather overlap',(s*(.176+k*.002),-.07+k*.027,.13),
                          (.022,.026,.075),rot=(.23,s*.15,0),segs=12,rings=8)
    # Existing cat and beaver have muzzles but no smile; give the mouth a dark interior and a tongue.
    if animal in ('cat','beaver'):
        z=.305 if animal=='cat' else .29
        ellipsoid('body','fixed',(.14,.025,.022),'mouth',(0,.194,z),(.033,.014,.022),segs=16,rings=10)
        ellipsoid('body','fixed',(.84,.25,.30),'tongue',(0,.206,z-.009),(.019,.005,.009),segs=12,rings=8)


def merge_parts(parts):
    """One renderer per expression/group/material, preserving the original palette and UVs."""
    buckets={}
    for o in parts:
        group,channel,_=o.name.split('|',2)
        key=(group,channel,tuple(round(v,5) for v in o['ihm_color']),o['ihm_texture'])
        buckets.setdefault(key,[]).append(o)
    out=[]
    for (group,channel,color,tex),obs in buckets.items():
        bpy.ops.object.select_all(action='DESELECT')
        for o in obs:o.select_set(True)
        bpy.context.view_layer.objects.active=obs[0]
        if len(obs)>1:bpy.ops.object.join()
        o=bpy.context.object;o.name=f'{group}|{channel}|surface_{len(out)}'
        out.append(o)
    return out


# ------------------------------------------------------------------ bake: R.E.P.O.-style worn, living surfaces

CLEAN_GROUPS = ('eyes_open', 'eyes_happy', 'eyes_wide', 'eyes_sleepy', 'brows_sad', 'brows_focus')


def _node(nt, kind, **inputs):
    n = nt.nodes.new(kind)
    for k, v in inputs.items():
        if k.startswith('_'): setattr(n, k[1:], v)
        else: n.inputs[k].default_value = v
    return n


def _mul(nt, a, b):
    m = _node(nt, 'ShaderNodeMath', _operation='MULTIPLY'); nt.links.new(a, m.inputs[0])
    if isinstance(b, float): m.inputs[1].default_value = b
    else: nt.links.new(b, m.inputs[1])
    return m.outputs[0]


def _range(nt, value, a, b, c, d):
    r = _node(nt, 'ShaderNodeMapRange'); nt.links.new(value, r.inputs['Value'])
    r.inputs['From Min'].default_value = a; r.inputs['From Max'].default_value = b
    r.inputs['To Min'].default_value = c; r.inputs['To Max'].default_value = d
    return r.outputs['Result']


def _pattern(nt, pos, animal, channel):
    """Greyscale surface detail (about 0.6-1.0) for one animal and colour slot, in model space."""
    def noise(scale, stretch=(1, 1, 1), detail=4.0, rough=.55):
        mp = _node(nt, 'ShaderNodeMapping'); mp.inputs['Scale'].default_value = stretch
        nt.links.new(pos, mp.inputs['Vector'])
        n = _node(nt, 'ShaderNodeTexNoise', Scale=scale, Detail=detail, Roughness=rough); nt.links.new(mp.outputs[0], n.inputs['Vector'])
        return n.outputs['Fac']
    if channel == 'accent':  # worn canvas: weave, scuffs and darker seams
        w1 = _node(nt, 'ShaderNodeTexWave', Scale=180.0, _wave_type='BANDS', _bands_direction='X'); nt.links.new(pos, w1.inputs['Vector'])
        w2 = _node(nt, 'ShaderNodeTexWave', Scale=180.0, _wave_type='BANDS', _bands_direction='Z'); nt.links.new(pos, w2.inputs['Vector'])
        weave = _range(nt, _mul(nt, w1.outputs['Fac'], w2.outputs['Fac']), 0, 1, .86, 1.0)
        scuff = _range(nt, noise(14.0, detail=6.0), .35, .75, .8, 1.04)
        return _mul(nt, weave, scuff)
    if channel == 'fixed':
        return _range(nt, noise(30.0), 0, 1, .9, 1.0)
    # Fur and feathers: fine vertical strands everywhere
    strands = _range(nt, noise(105.0, (2, 2, .45), 3.0, .7), .3, .7, .66, 1.04)
    detail = strands
    if animal in ('cat', 'dragon') and channel == 'main':  # tabby stripes or mottled scales
        wave = _node(nt, 'ShaderNodeTexWave', Scale=7.0, Distortion=9.0, _wave_type='BANDS', _bands_direction='Z')
        nt.links.new(pos, wave.inputs['Vector'])
        detail = _mul(nt, detail, _range(nt, wave.outputs['Fac'], .55, .9, 1.0, .68))
    if animal in ('owl', 'chicken', 'dragon', 'angel') and channel in ('main', 'second'):
        mp = _node(nt, 'ShaderNodeMapping'); mp.inputs['Scale'].default_value = (1, 1, 1.6); nt.links.new(pos, mp.inputs['Vector'])
        scale = {'owl': 33.0, 'chicken': 49.0, 'dragon': 24.0, 'angel': 40.0}[animal]
        v = _node(nt, 'ShaderNodeTexVoronoi', Scale=scale, _feature='DISTANCE_TO_EDGE')
        nt.links.new(mp.outputs[0], v.inputs['Vector'])
        detail = _mul(nt, detail, _range(nt, v.outputs['Distance'], 0, .16, .72 if animal in ('dragon', 'owl') else .89, 1.0))
    if animal == 'sheep' and channel == 'main':  # tightly curled fleece
        curls = _node(nt, 'ShaderNodeTexVoronoi', Scale=48.0, _feature='DISTANCE_TO_EDGE')
        nt.links.new(pos, curls.inputs['Vector'])
        detail = _mul(nt, _range(nt, curls.outputs['Distance'], .035, .17, .71, 1.04),
                      _range(nt, noise(160.0), .25, .75, .86, 1.0))
    if animal in ('cow', 'dog') and channel == 'main':  # irregular patches and short coat
        spots = noise(11.0 if animal == 'cow' else 18.0, (1, 1.6, 1), 2.0)
        detail = _mul(nt, detail, _range(nt, spots, .48, .58, .61 if animal == 'cow' else .78, 1.03))
    if animal in ('fairy', 'devil'):
        flecks = noise(42.0 if animal == 'fairy' else 22.0, (1.4, 1, .65), 3.0)
        detail = _range(nt, flecks, .38, .7, .82 if animal == 'fairy' else .67, 1.03)
    if animal == 'beaver':  # coarse, clumpy fur
        detail = _mul(nt, detail, _range(nt, noise(40.0, (1, 1, 3), 2.0), .3, .7, .82, 1.04))
    if animal == 'penguin' and channel == 'main':  # sleek, with a faint sheen streak
        detail = _mul(nt, _range(nt, noise(60.0, (1, 1, 6), 2.0), .3, .7, .9, 1.03), 1.0)
    return detail


def bake_atlas(key, animal, parts, size=768):
    """Packs the surface parts into one atlas and bakes greyscale detail x occlusion x grime into it.
    The game tints it per colour slot, so recolouring keeps all the wear."""
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
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=.004, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=.003, rotate=True)
    bpy.ops.object.mode_set(mode='OBJECT')

    keep = {}
    mats = {}
    for o in targets:
        channel = o.name.split('|')[1]
        if channel not in mats:
            m = bpy.data.materials.new(f'{key} bake {channel}'); m.use_nodes = True; nt = m.node_tree
            for n in list(nt.nodes): nt.nodes.remove(n)
            out = _node(nt, 'ShaderNodeOutputMaterial'); emit = _node(nt, 'ShaderNodeEmission', Strength=1.0)
            geo = _node(nt, 'ShaderNodeNewGeometry'); pos = geo.outputs['Position']
            value = _pattern(nt, pos, animal, channel)
            ao = _node(nt, 'ShaderNodeAmbientOcclusion', Distance=.055, _samples=8, _only_local=False)
            value = _mul(nt, value, _range(nt, ao.outputs['AO'], 0, 1, .5, 1.0))
            sep = _node(nt, 'ShaderNodeSeparateXYZ'); nt.links.new(pos, sep.inputs[0])
            value = _mul(nt, value, _range(nt, sep.outputs['Z'], 0, .14, .74, 1.0))          # floor grime on the lower body
            cav = _range(nt, geo.outputs['Pointiness'], .44, .52, .82, 1.0)                 # dirt in creases
            value = _mul(nt, value, cav)
            mp = _node(nt, 'ShaderNodeMapping'); nt.links.new(pos, mp.inputs['Vector'])
            dust = _node(nt, 'ShaderNodeTexNoise', Scale=55.0, Detail=8.0, Roughness=.75); nt.links.new(mp.outputs[0], dust.inputs['Vector'])
            value = _mul(nt, value, _range(nt, dust.outputs['Fac'], .62, .72, 1.0, .78))   # dust speckles
            nt.links.new(value, emit.inputs['Color'])
            nt.links.new(emit.outputs[0], out.inputs['Surface'])
            tex = _node(nt, 'ShaderNodeTexImage'); tex.image = img; nt.nodes.active = tex
            mats[channel] = m
        keep[o.name] = list(o.data.materials)
        o.data.materials.clear(); o.data.materials.append(mats[channel])
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 12
    scene.render.bake.margin = 4; scene.render.bake.use_clear = True
    bpy.ops.object.bake(type='EMIT')
    img.filepath_raw = str(OUT / f'{key}_atlas.png'); img.file_format = 'PNG'; img.save()
    # Small surface-normal relief, separate from the colour map; keep UV island borders neutral.
    pixels=np.array(img.pixels[:],dtype=np.float32).reshape(size,size,4)[...,:3].mean(axis=2)
    dx=np.clip((np.roll(pixels,-1,1)-np.roll(pixels,1,1))*1.2,-.20,.20)
    dy=np.clip((np.roll(pixels,-1,0)-np.roll(pixels,1,0))*1.2,-.20,.20)
    normal=np.ones((size,size,4),dtype=np.float32)
    normal[...,0]=.5-dx;normal[...,1]=.5-dy;normal[...,2]=np.sqrt(np.maximum(0,1-4*dx*dx-4*dy*dy))*.5+.5
    ni=bpy.data.images.new(key+'_normal',size,size,alpha=False)
    ni.colorspace_settings.name='Non-Color';ni.pixels.foreach_set(normal.ravel())
    ni.filepath_raw=str(OUT/f'{key}_normal.png');ni.file_format='PNG';ni.save()
    # Preview what ships: the atlas times each part's colour, nearest filtering like in game.
    for o in targets:
        c = o['ihm_color']
        m = bpy.data.materials.new(f'{o.name} shown'); m.use_nodes = True; nt = m.node_tree
        b = nt.nodes['Principled BSDF']; b.inputs['Roughness'].default_value = .91 if o.name.split('|')[1]!='fixed' else .78
        tex = _node(nt, 'ShaderNodeTexImage'); tex.image = img; tex.interpolation = 'Linear'
        mix = _node(nt, 'ShaderNodeMixRGB', _blend_type='MULTIPLY', Fac=1.0); mix.inputs['Color2'].default_value = (*c, 1)
        nt.links.new(tex.outputs['Color'], mix.inputs['Color1']); nt.links.new(mix.outputs[0], b.inputs['Base Color'])
        normal_tex=_node(nt,'ShaderNodeTexImage');normal_tex.image=ni
        normal_node=_node(nt,'ShaderNodeNormalMap',Strength=.55)
        nt.links.new(normal_tex.outputs['Color'],normal_node.inputs['Color']);nt.links.new(normal_node.outputs['Normal'],b.inputs['Normal'])
        o.data.materials.clear(); o.data.materials.append(m)
    for m in mats.values(): bpy.data.materials.remove(m)


# ------------------------------------------------------------------ export

def vec(v): return {'x': round(v[0], 5), 'y': round(v[1], 5), 'z': round(v[2], 5)}


def export(key, parts, pivots):
    data = {'version': 1, 'fps': 30, 'frames': 1, 'parts': [], 'pivots': []}
    for name, p in pivots.items():
        data['pivots'].append({'name': name, 'position': vec(C @ Vector(p))})
    for o in parts:
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
        atlas = o.get('ihm_atlas', False)
        if o['ihm_texture'] and not atlas:
            bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
            bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.01, scale_to_bounds=False)
            bpy.ops.object.mode_set(mode='OBJECT')
        mesh = o.data; mesh.calc_loop_triangles()
        uv_layer = mesh.uv_layers.active
        verts, normals, uv, tris = [], [], [], []
        index = {}
        for tri in mesh.loop_triangles:
            for li in reversed(tri.loops):
                loop = mesh.loops[li]
                co = C @ mesh.vertices[loop.vertex_index].co
                n = C @ mesh.corner_normals[li].vector
                t = uv_layer.data[li].uv if uv_layer else (0, 0)
                k = (loop.vertex_index, round(n.x, 3), round(n.y, 3), round(n.z, 3), round(t[0], 4), round(t[1], 4))
                if k not in index:
                    index[k] = len(verts); verts.append(vec(co)); normals.append(vec(n))
                    tile = 1 if atlas else 3
                    uv.append({'x': round(float(t[0]) * tile, 5), 'y': round(float(t[1]) * tile, 5)})
                tris.append(index[k])
        c = o['ihm_color']
        data['parts'].append({'name': o.name, 'vertices': verts, 'normals': normals, 'uv': uv, 'triangles': tris,
                              'color': {'r': c[0], 'g': c[1], 'b': c[2], 'a': 1}, 'texture': o['ihm_texture'],
                              'rest': {'position': vec((0, 0, 0)), 'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'scale': vec((1, 1, 1))},
                              'samples': []})
    (OUT / f'{key}.mesh.json').write_text(json.dumps(data, separators=(',', ':')), encoding='utf8')
    return sum(len(p['triangles']) // 3 for p in data['parts'])


def stage():
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 40
    scene.view_settings.view_transform = 'AgX'
    bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0, 0)); floor = bpy.context.object
    m = bpy.data.materials.new('floor'); m.use_nodes = True
    m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.07, .07, .08, 1)
    floor.data.materials.append(m)
    for name, loc, power in (('Key', (1.6, 2.2, 2.0), 260), ('Fill', (-2.2, 1.0, 1.2), 110), ('Rim', (0, -2.4, 1.6), 200)):
        bpy.ops.object.light_add(type='AREA', location=loc); light = bpy.context.object
        light.data.energy = power; light.data.size = 2
        light.rotation_euler = (Vector((0, 0, .25)) - light.location).to_track_quat('-Z', 'Y').to_euler()
    return scene


clear()
make_textures()
totals = {}
BY_ANIMAL = {}
lineup = []
for i, (key, build) in enumerate(ANIMALS.items()):
    PARTS.clear()
    build()
    surface_detail(key)
    merged=merge_parts(list(PARTS))
    bake_atlas('courier_' + key, key, merged)
    totals[key] = export('courier_' + key, merged, PIVOTS[key])
    BY_ANIMAL[key] = set(merged)
    offset = Vector(((i%6 - 2.5) * .82, -(i//6)*1.0, 0))
    for o in merged: o.location += offset
    lineup.extend(merged)
    print('COURIER_EXPORTED',key,totals[key],flush=True)

if opts.preview:
    EXTRA = ('eyes_happy', 'eyes_wide', 'eyes_sleepy', 'brows_sad', 'brows_focus')
    for o in lineup:
        if o.name.split('|')[0] in EXTRA: o.hide_render = True
    scene = stage()
    bpy.ops.object.camera_add(location=(.4, 5.5, 3.1)); cam = bpy.context.object; scene.camera = cam
    cam.rotation_euler = (Vector((0, -.45, .24)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 5.35
    scene.render.resolution_x, scene.render.resolution_y = 2000, 1050
    scene.render.filepath = str(ART / 'courier_lineup.png'); bpy.ops.render.render(write_still=True)
    # Back view: the backpacks.
    cam.location = (-.4, -6.4, 3.1)
    cam.rotation_euler = (Vector((0, -.45, .24)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = str(ART / 'courier_lineup_back.png'); bpy.ops.render.render(write_still=True)
    # Petted: closed eyes instead of open ones.
    for o in lineup:
        g = o.name.split('|')[0]
        if g == 'eyes_open': o.hide_render = True
        if g == 'eyes_happy': o.hide_render = False
    cam.location = (.4, 5.5, 3.1)
    cam.rotation_euler = (Vector((0, -.45, .24)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = str(ART / 'courier_lineup_happy.png'); bpy.ops.render.render(write_still=True)

if opts.preview:
    # Expression sheet: each pet shows a different mood (neutral, happy, surprised, sleepy, sad, focused).
    moods = [('eyes_wide',), ('eyes_sleepy',), ('eyes_open', 'brows_sad'), ('eyes_open', 'brows_focus'), ('eyes_happy',)]
    keys = list(ANIMALS)
    for o in lineup:
        animal = next(i for i, k in enumerate(keys) if o in BY_ANIMAL[k])
        g = o.name.split('|')[0]
        if g.startswith('eyes_') or g.startswith('brows_'):
            o.hide_render = g not in moods[animal % len(moods)]
    scene.render.filepath = str(ART / 'courier_expressions.png'); bpy.ops.render.render(write_still=True)

print('IHM_COURIER_OK', totals)
