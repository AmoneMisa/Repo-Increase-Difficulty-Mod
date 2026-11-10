"""Blender 5.2: --background --factory-startup --python tools/build_fairy.py -- --root PATH [--base JSON] [--preview]

The Petal Fairy set: an original magical-fairy outfit (pink crop top and wrap skirt, glowing petal wings, striped
stockings, slouch boots), after the general look of the user's reference (#3), not any particular show's design.

  fairy_top        Body Top     pink sparkle crop top with a striped under-band, choker with a flower charm, chest flower
  fairy_top_wings  Body Top     the same top with four glowing petal wings that flutter (pivots wing_r / wing_l)
  fairy_skirt      Body Bottom  striped shorts under an asymmetric sparkle wrap skirt
  fairy_sleeve_r   Arm Right    off-shoulder sparkle sleeve with a flared cuff
  fairy_sleeve_l   Arm Left
  fairy_leg_r      Leg Right    striped stocking with a pink slouch boot
  fairy_leg_l      Leg Left

Tintable: the pink sparkle fabric and wings (top/skirt/sleeves), the coloured stocking stripes (legs). Fixed: light
stripes, boots, flowers, wing frames. Built with tools/cosmetic_kit.py.
"""
import argparse
import math
import random
import sys
from pathlib import Path

import numpy as np
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import cosmetic_kit as K  # noqa: E402
from cosmetic_kit import TAU, lerp, surface, tube, ellipsoid, bez  # noqa: E402

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, required=True)
parser.add_argument('--base', type=Path)
parser.add_argument('--preview', action='store_true')
ARGS = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
ARGS.root = ARGS.root.resolve()
K.configure(ARGS.root, ARGS.base)

PINK = (1., .42, .72)
GREEN = (.38, .8, .52)
MINT = (.8, .95, .84)
BOOT = (1., .48, .74)
WING = (1., .62, .86)
FRAME = (.78, 1., .86)


def textures():
    rng = random.Random(5)
    n = 256
    mott = K.periodic_noise(n, 4, rng)
    spark = np.zeros((n, n))
    for _ in range(260):
        x, y = rng.randrange(n), rng.randrange(n); b = rng.uniform(.4, 1)
        for dx, dy, w in ((0, 0, 1), (1, 0, .4), (-1, 0, .4), (0, 1, .4), (0, -1, .4)):
            spark[(y + dy) % n, (x + dx) % n] = max(spark[(y + dy) % n, (x + dx) % n], b * w)
    K.save_png('fairy_sparkle.png', np.clip(.8 + .1 * (mott - .5) + .35 * spark, 0, 1))
    # petal: bright core, soft lighter edge, a darker vein line along the middle and a round spot near the tip
    w, h = 128, 256
    yy, xx = (np.mgrid[0:h, 0:w] + .5)
    u, v = xx / w - .5, yy / h
    core = np.exp(-((u / .32) ** 2)) * (.6 + .4 * v)
    spot = np.exp(-((u / .16) ** 2 + ((v - .78) / .09) ** 2))
    vein = np.exp(-((u / .015) ** 2)) * (v < .7)
    edge = np.clip((np.abs(u) - .38) / .12, 0, 1)
    sparks = np.zeros_like(u)
    for _ in range(60):
        cx, cy = rng.uniform(-.4, .4), rng.uniform(0, 1)
        sparks = np.maximum(sparks, np.exp(-(((u - cx) * 90) ** 2 + ((v - cy) * 180) ** 2)))
    petal = np.clip(.55 + .3 * core + .35 * spot - .2 * vein + .25 * edge + .5 * sparks, 0, 1)
    K.save_png('fairy_petal.png', petal)
    K.save_png('fairy_petal_glow.png', np.clip(.15 + .45 * core * v + .5 * spot + .8 * sparks, 0, 1))


def make_mats():
    textures()
    m = K.material
    return {
        'pink': m('Fairy pink', PINK, .55, image='fairy_sparkle.png', tint=True),
        'stripe': m('Fairy stripe', GREEN, .7, tint=True),
        'mint': m('Fairy light stripe', MINT, .7),
        'boot': m('Fairy boot', BOOT, .4),
        'wing': m('Fairy wing', WING, .3, emit=1.2, image='fairy_petal.png', emit_image='fairy_petal_glow.png', tint=True, tint_emission=True),
        'frame': m('Fairy wing frame', FRAME, .35, emit=.5),
        'flower': m('Fairy flower', (1., .97, 1.), .4),
        'heart': m('Fairy flower heart', (1., .78, .3), .35, metal=.4),
    }


# ---------------------------------------------------------------- top
PROFILE = [(.298, -.02), (.306, .02), (.31, .2), (.312, .28), (.305, .34), (.29, .368), (.266, .384)]


def body_r(h):
    for (r0, h0), (r1, h1) in zip(PROFILE, PROFILE[1:]):
        if h0 <= h <= h1: return r0 + (r1 - r0) * (h - h0) / (h1 - h0)
    return PROFILE[-1][0] if h > PROFILE[-1][1] else PROFILE[0][0]


def on_body(h, a, lift=0.):
    r = body_r(h) + lift
    return Vector((r * math.sin(a), h, r * math.cos(a)))


def neckline(a):
    """Top edge of the crop top: a sweetheart dip at the front, high at the back."""
    a = (a + math.pi) % TAU - math.pi
    f = ((1 + math.cos(a)) / 2) ** 3
    dip = .035 * math.exp(-(a / .12) ** 2)
    hearts = .018 * (math.exp(-((abs(a) - .35) / .2) ** 2))
    return .35 - .05 * f - dip + hearts


def flower(M, centre, normal, size, name='Flower'):
    """Five rounded petals and a gold heart, facing 'normal'."""
    n = Vector(normal).normalized()
    a = n.cross(Vector((0, 1, 0)) if abs(n.y) < .9 else Vector((1, 0, 0))).normalized(); b = n.cross(a)
    obs = []
    for k in range(5):
        ang = TAU * k / 5 + .3
        d = a * math.cos(ang) + b * math.sin(ang)
        c = Vector(centre) + d * size * .62 + n * .002
        side = d.cross(n)
        def pt(t, s, c=c, d=d, side=side):
            ang2 = TAU * s; rr = math.sin(math.pi * t)
            return c + d * (size * .55 * (t * 2 - 1)) + side * (size * .32 * rr * math.cos(ang2)) + n * (size * .12 * rr * math.sin(ang2))
        obs.append(surface(name, 6, 10, pt, M['flower'], wrap=True, inside=K.B(*c)))
    obs.append(ellipsoid('Flower heart', tuple(Vector(centre) + n * .006), (size * .26, size * .26, size * .26), M['heart'], (10, 6)))
    return obs


def build_top(M, wings=False):
    parts = []
    inside = K.B(0, .25, 0)
    def top(t, s, lift=.012):
        a = TAU * s; h = lerp(.19, neckline(a), t)
        return on_body(h, a, lift + .004 * math.sin(math.pi * t))
    parts.append(surface('Crop top', 10, 96, top, M['pink'], wrap=True, uv=(4, 1), inside=inside))
    parts.append(tube('Top edge', [top(1, k / 96, .014) for k in range(96)], .006, M['pink'], closed=True, segs=6))
    # striped under-band showing below the top
    for i, (h0, h1) in enumerate(((.15, .17), (.17, .19))):
        parts.append(surface('Under band', 1, 72, lambda t, s, h0=h0, h1=h1: on_body(lerp(h0, h1, t), TAU * s, .009), M['stripe' if i == 0 else 'mint'], wrap=True, inside=inside))
    # choker with a flower charm, and a flower at the neckline
    def choker(t, s):
        a = TAU * s; r = .27 + .003 * math.sin(math.pi * t)
        return Vector((r * math.sin(a), lerp(.372, .398, t), r * math.cos(a)))
    parts.append(surface('Choker', 2, 56, choker, M['pink'], wrap=True, inside=K.B(0, .38, 0)))
    parts += flower(M, (0, .384, .282), (0, 0, 1), .022, 'Choker flower')
    parts += flower(M, on_body(neckline(0) - .012, 0, .022), (0, .15, 1), .034)
    pivots = {}
    if wings:
        w, pv = build_wings(M)
        parts += w; pivots.update(pv)
    return parts, pivots, None


# ---------------------------------------------------------------- wings
ROOT = Vector((.06, .28, -.318))


def petal(M, root, direction, length, width, side, group, curl=.04):
    """A pointed oval petal from root along direction (Unity), lying roughly in the back plane, curled back slightly."""
    d = Vector(direction).normalized()
    back = Vector((0, 0, -1))
    across = d.cross(back).normalized()
    def pt(t, s, off=0.):
        half = width * math.sin(math.pi * t ** .8) ** .9 * (1 - .15 * t)
        p = root + d * (length * t) + across * (half * (2 * s - 1))
        p += back * (curl * math.sin(math.pi * t) + .015 * (2 * s - 1) ** 2 + off)
        return p
    front = surface('Petal', 10, 6, pt, M['wing'], uv=(1, 1), group=group)
    rear = surface('Petal back', 10, 6, lambda t, s: pt(t, s, -.003), M['wing'], uv=(1, 1), group=group, flip=True)
    # orient the two sheets: front faces the viewer behind the player (-z), back faces the body
    for ob, want in ((front, -1), (rear, 1)):
        me = ob.data
        avg = sum((p.normal for p in me.polygons), Vector()) / max(len(me.polygons), 1)
        if (avg.y > 0) != (want > 0):   # Blender y = Unity z
            for p in me.polygons: p.flip()
    rim = [pt(t, 0, -.0015) for t in np.linspace(0, 1, 12)] + [pt(t, 1, -.0015) for t in np.linspace(1, 0, 12)[1:]]
    return [front, rear, tube("Petal rim", rim, .0024, M['frame'], group, segs=5)]


def build_wings(M):
    parts = []
    pivots = {}
    for sd, group in ((1, 'wing_r'), (-1, 'wing_l')):
        root = Vector((sd * ROOT.x, ROOT.y, ROOT.z))
        pivots[group] = K.B(*root)
        for ang, length, width in ((26, .5, .11), (52, .6, .13), (78, .5, .11)):       # upper wing: three petals fanned up and out
            a = math.radians(ang)
            parts += petal(M, root, (sd * math.sin(a), math.cos(a) * .8 + .05, -.22), length, width, sd, group, curl=.06)
        for ang, length, width in ((112, .36, .09), (138, .3, .08)):                    # lower wing: two smaller petals
            a = math.radians(ang)
            parts += petal(M, root + Vector((0, -.03, 0)), (sd * math.sin(a), math.cos(a), -.15), length, width, sd, group, curl=.025)
        parts.append(ellipsoid('Wing root', tuple(root + Vector((0, 0, -.004))), (.02, .02, .014), M['heart'], (10, 6), group=group))
    return parts, pivots


# ---------------------------------------------------------------- skirt
def build_skirt(M):
    parts = []
    inside = K.B(0, .1, 0)
    # striped shorts: alternating bands round the body bottom
    bands = 7
    for i in range(bands):
        h0, h1 = lerp(-.03, .28, i / bands), lerp(-.03, .28, (i + 1) / bands)
        def band(t, s, h0=h0, h1=h1):
            a = TAU * s; h = lerp(h0, h1, t); r = .309 - .012 * max(0, (.02 - h)) / .05
            return Vector((r * math.sin(a), h, r * math.cos(a)))
        parts.append(surface('Shorts', 1, 64, band, M['stripe' if i % 2 == 0 else 'mint'], wrap=True, inside=inside))
    # wrap skirt: long on the left, short on the right, open at the front right
    a0, a1 = math.radians(48), math.radians(48 + 322)
    def hem(a):
        d = abs(((a - (-math.pi / 2)) + math.pi) % TAU - math.pi) / math.pi        # 0 at the left side, 1 at the right
        return -.27 + .25 * d ** 1.2
    def wrap(t, s, off=0.):
        a = lerp(a0, a1, s)
        y = lerp(.272, hem(a), t)
        r = .322 + .14 * t ** 1.3 + .022 * t ** 1.5 * math.sin(9 * a + .5) + off
        return Vector((r * math.sin(a), y, r * math.cos(a)))
    parts.append(surface('Wrap skirt', 12, 64, wrap, M['pink'], uv=(5, 1.5), inside=inside))
    parts.append(surface('Wrap lining', 12, 64, lambda t, s: wrap(t, s, -.006), M['pink'], uv=(5, 1.5), inside=inside, flip=True))
    parts.append(tube('Hem', [wrap(1, s) for s in np.linspace(0, 1, 64)], .005, M['pink'], segs=6))
    def waist(t, s):
        a = TAU * s; return Vector((.33 * math.sin(a), lerp(.25, .285, t), .33 * math.cos(a)))
    parts.append(surface('Waistband', 2, 64, waist, M['pink'], wrap=True, inside=K.B(0, .27, 0)))
    parts += flower(M, (.33 * math.sin(a0), .268, .33 * math.cos(a0) + .006), (math.sin(a0), 0, math.cos(a0)), .03, 'Hip flower')
    return parts, {}, None


# ---------------------------------------------------------------- sleeves
def build_sleeve(M, sx):
    parts = []
    def sleeve(t, s, off=0.):
        x = lerp(.05, .36, t); a = TAU * s
        r = .082 + .03 * max(0, t - .7) / .3 + off
        return Vector((sx * x, r * math.sin(a), r * math.cos(a)))
    parts.append(surface('Sleeve', 10, 28, sleeve, M['pink'], wrap=True, uv=(2, 2), inside=K.B(sx * .2, 0, 0)))
    parts.append(surface('Sleeve lining', 10, 28, lambda t, s: sleeve(t, s, -.006), M['pink'], wrap=True, uv=(2, 2), inside=K.B(sx * .2, 0, 0), flip=True))
    for x, r in ((.05, .084), (.36, .114)):
        parts.append(tube('Sleeve edge', [Vector((sx * x, r * math.sin(TAU * k / 32), r * math.cos(TAU * k / 32))) for k in range(32)], .005, M['pink'], closed=True, segs=6))
    return parts, {}, None


# ---------------------------------------------------------------- stocking and boot
def leg_r(y):
    return .106 if y > 0 else .106 * max(0., (y + .549) / .549)


def build_leg(M):
    parts = []
    inside = K.B(0, -.2, 0)
    stripes = 10
    for i in range(stripes):
        y0, y1 = lerp(.02, -.36, i / stripes), lerp(.02, -.36, (i + 1) / stripes)
        def band(t, s, y0=y0, y1=y1):
            y = lerp(y0, y1, t); a = TAU * s; r = leg_r(y) + .009
            return Vector((r * math.sin(a), y, r * math.cos(a)))
        parts.append(surface('Stocking', 1, 32, band, M['stripe' if i % 2 == 0 else 'mint'], wrap=True, inside=inside))
    # slouch boot: from mid shin to a rounded pointed toe, with soft rings of slouch
    top, tip = -.29, -.57
    def boot(t, s):
        y = lerp(top, tip, t); a = TAU * s
        slouch = .009 * math.sin(t * math.pi * 4) * (1 - t)
        r = (leg_r(y) + .03 + slouch) * (1 - t ** 6) + .004
        return Vector((r * math.sin(a), y, r * math.cos(a) + .03 * t ** 3))
    parts.append(surface('Boot', 16, 32, boot, M['boot'], wrap=True, inside=K.B(0, -.42, 0)))
    def cuff(t, s):
        a = TAU * s; r = leg_r(top) + .036 + .008 * math.sin(math.pi * t)
        return Vector((r * math.sin(a), lerp(top - .015, top + .025, t) + .012 * math.sin(2 * a), r * math.cos(a)))
    parts.append(surface('Boot cuff', 3, 32, cuff, M['boot'], wrap=True, inside=K.B(0, top, 0)))
    return parts, {}, None


def build_hair(M):
    from cosmetic_hair import hair
    mat=K.material('Fairy hair',(.55,.27,.10),.85,clean=True,tint=True)
    return hair(mat,.28),{},K.B(0,.44,.254)

STYLES = {
    'fairy_hair': ('Hat', build_hair),
    'fairy_top': ('Body Top', lambda M: build_top(M)),
    'fairy_top_wings': ('Body Top', lambda M: build_top(M, wings=True)),
    'fairy_skirt': ('Body Bottom', build_skirt),
    'fairy_sleeve_r': ('Arm Right', lambda M: build_sleeve(M, 1)),
    'fairy_sleeve_l': ('Arm Left', lambda M: build_sleeve(M, -1)),
    'fairy_leg_r': ('Leg Right', build_leg),
    'fairy_leg_l': ('Leg Left', build_leg),
}
VIEWS = {'fairy_top': (.6, 1., .3), 'fairy_top_wings': (-.5, -1., .3), 'fairy_skirt': (.4, 1., .25),
         'fairy_sleeve_r': (.3, 1., .5), 'fairy_sleeve_l': (-.3, 1., .5), 'fairy_leg_r': (.6, 1., .1), 'fairy_leg_l': (-.6, 1., .1)}

built = K.run(STYLES, VIEWS, ARGS.root / 'art' / 'cosmetics' / 'fairy', 'fairy', False, setup=make_mats)
if ARGS.preview:
    import bpy
    for ob in built['fairy_top'][1]: ob.hide_render = True
    for key, (slot, parts) in built.items():
        if key == 'fairy_top': continue
        for ob in parts: ob.hide_render = False; ob.matrix_world = K.frame_matrix(K.FRAMES[slot])
    K.stand_in_body()
    scene = bpy.context.scene; cam = scene.camera
    scene.render.film_transparent = False; scene.render.resolution_x = 760; scene.render.resolution_y = 900
    art = ARGS.root / 'art' / 'cosmetics' / 'fairy'; art.mkdir(parents=True, exist_ok=True)
    target = Vector((0, -.05, .2))
    for suffix, eye in (('front', (.9, 2.7, .6)), ('back', (-1.3, -2.6, .8)), ('side', (2.8, -.3, .45))):
        K.look(cam, target + Vector(eye), target); K.render(art / ('fairy_' + suffix + '.png'))
print('FAIRY_COMPLETE', flush=True)
