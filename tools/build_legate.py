"""Blender 5.2: --background --factory-startup --python tools/build_legate.py -- --root PATH [--base JSON] [--preview]

The Legate set: a Roman officer's kit after the user's reference, fitted to the R.E.P.O. robot.

  legate_cuirass      Body Top     polished silver muscle cuirass, red baldric with gold studs, red belt with gold plates
  legate_cuirass_cape Body Top     the same cuirass under a draped red mantle with a gold laurel border, shoulder fringe
                                   and a long cape behind (the robot has no separate cape slot, so this is a second variant)
  legate_skirt        Body Bottom  two rows of tooled leather strips over a red tunic, red waistband with gold rivets
  legate_bracer_r     Arm Right    red leather bracer with cream trim and a gold emblem
  legate_bracer_l     Arm Left

Red cloth and leather are tintable (the colour picked for the slot); silver, gold, the laurel embroidery and the
leather strips keep their colours. Built with tools/cosmetic_kit.py (shared helpers; slot frames in Unity coordinates).
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
from cosmetic_kit import TAU, lerp, surface, tube, ellipsoid, ribbon  # noqa: E402

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, required=True)
parser.add_argument('--base', type=Path)
parser.add_argument('--preview', action='store_true')
ARGS = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
ARGS.root = ARGS.root.resolve()
K.configure(ARGS.root, ARGS.base)

RED = (.62, .045, .05)
TUNIC = (.42, .04, .06)
SILVER = (.84, .85, .88)
GOLD = (1., .7, .24)
CREAM = (.9, .86, .74)


# ---------------------------------------------------------------- textures
def textures():
    rng = random.Random(11)
    # woven cloth: grey with a fine twill and soft mottling (tinted red in game)
    n = 256
    y, x = np.mgrid[0:n, 0:n] / n
    twill = .5 + .5 * np.sin(TAU * (x * 48 + y * 48))
    mott = K.periodic_noise(n, 4, rng)
    K.save_png('legate_cloth.png', .78 + .1 * twill * .5 + .14 * (mott - .5))
    # laurel embroidery band (untinted): gold laurel vine between two gold lines on deep red
    w, h = 512, 64
    yy, xx = (np.mgrid[0:h, 0:w] + .5)
    u, v = xx / w, yy / h
    bg = np.zeros((h, w, 3)); bg[...] = (.36, .03, .035)
    gold = np.array([1., .76, .3])
    def line(d, wd): return np.exp(-(d / wd) ** 2)
    mask = np.maximum(line(v - .1, .025), line(v - .9, .025))
    stem_y = .5 + .06 * np.sin(TAU * u * 5)
    mask = np.maximum(mask, line(v - stem_y, .02))
    for k in range(10):
        cx = (k + .5) / 10
        up = 1 if k % 2 == 0 else -1
        ly = .5 + .06 * math.sin(TAU * cx * 5) + up * .17
        ang = .6 * up
        du = (u - cx) * 10 * 1.5; dv = (v - ly) * 2.6
        ru = du * math.cos(ang) + dv * math.sin(ang); rv = -du * math.sin(ang) + dv * math.cos(ang)
        leaf = np.clip(1 - (ru / .9) ** 2 - (rv / .32) ** 2, 0, 1) ** .4
        mask = np.maximum(mask, leaf * .95)
    berries = np.zeros_like(u)
    for k in range(5):
        cx = (k + .25) / 5
        berries = np.maximum(berries, line(np.hypot((u - cx) * 512 / 64, v - .5), .06))
    mask = np.maximum(mask, berries)
    shade = .75 + .25 * np.sin(TAU * u * 64) ** 2
    img = bg * (1 - mask[..., None]) + gold * (shade * mask)[..., None]
    K.save_png('legate_laurel.png', img)
    # leather strip (untinted): charcoal leather, stitched border, tooled red flame and diamond (v 1 = top)
    w, h = 64, 256
    yy, xx = (np.mgrid[0:h, 0:w] + .5)
    u, v = xx / w - .5, yy / h
    leather = .1 + .03 * K.periodic_noise(64, 3, rng)[np.arange(h)[:, None] % 64, np.arange(w)[None, :] % 64]
    img = np.zeros((h, w, 3)); img[...] = leather[..., None] * np.array([1., .95, .95])
    edge = np.clip((np.abs(u) - .4) / .1, 0, 1)
    img *= (1 - .5 * edge)[..., None]
    stitch = ((np.abs(np.abs(u) - .36) < .025) & ((yy.astype(int) // 6) % 2 == 0)).astype(float)
    red = np.array([.55, .1, .08])
    # flame: teardrop pointing up around v .35, diamond around v .72
    fy = (v - .33) / .14
    flame = (np.abs(u) < .2 * np.clip(1 - fy, 0, 1) ** .7 * np.clip(fy + 1, 0, 1) ** .5) & (fy > -1) & (fy < 1)
    inner = (np.abs(u) < .1 * np.clip(1 - fy, 0, 1) ** .7 * np.clip(fy + .6, 0, 1) ** .5) & (fy > -.6) & (fy < 1)
    diamond = (np.abs(u) / .17 + np.abs(v - .72) / .09) < 1
    dia_in = (np.abs(u) / .08 + np.abs(v - .72) / .045) < 1
    pattern = (flame & ~inner) | (diamond & ~dia_in)
    img = np.where(pattern[..., None], red, img)
    img = np.where(stitch[..., None] > 0, np.array([.45, .4, .36]), img)
    K.save_png('legate_strip.png', img)


def make_mats():
    textures()
    m = K.material
    return {
        'cloth': m('Legate cloth', RED, .85, image='legate_cloth.png', tint=True),
        'leather': m('Legate red leather', RED, .45, tint=True),
        'tunic': m('Legate tunic', TUNIC, .85, image='legate_cloth.png', tint=True),
        'silver': m('Legate silver', SILVER, .2, metal=.92),
        'gold': m('Legate gold', GOLD, .3, metal=.9),
        'laurel': m('Legate laurel', (1, 1, 1), .5, image='legate_laurel.png'),
        'strip': m('Legate strips', (1, 1, 1), .55, image='legate_strip.png'),
        'cream': m('Legate trim', CREAM, .7),
    }


# ---------------------------------------------------------------- cuirass
PROFILE = [(.302, -.02), (.312, .02), (.316, .15), (.322, .28), (.315, .34), (.296, .37), (.266, .386)]


def base_r(h):
    for (r0, h0), (r1, h1) in zip(PROFILE, PROFILE[1:]):
        if h0 <= h <= h1: return r0 + (r1 - r0) * (h - h0) / (h1 - h0)
    return PROFILE[-1][0] if h > PROFILE[-1][1] else PROFILE[0][0]


def g(x, s): return math.exp(-(x / s) ** 2)


def cuirass_r(h, a):
    a = (a + math.pi) % TAU - math.pi
    r = base_r(h)
    if abs(a) < 1.3:
        for sd in (1, -1):
            r += .024 * g(a - sd * .42, .3) * g(h - .265, .055)                     # pectorals
            for hc in (.175, .115, .055):
                r += .0095 * g(a - sd * .15, .085) * g(h - hc, .024)                 # abdominals
        r -= .007 * g(a, .07) * (1 if h > .03 else 0)                                # centre line
        r += .006 * g(h - .335, .03)                                                 # collarbone ridge
    else:
        b = abs(a) - math.pi
        r += .012 * g(abs(a) - 2.6, .3) * g(h - .26, .07)                            # shoulder blades
        r -= .004 * g(b, .06)                                                        # spine
    return r


def on_body(h, a, lift=0.):
    r = cuirass_r(h, a) + lift
    return Vector((r * math.sin(a), h, r * math.cos(a)))


def build_cuirass(M, cape=False):
    parts = []
    parts.append(surface('Cuirass', 34, 96, lambda t, s: on_body(lerp(-.02, .386, t), TAU * s), M['silver'], wrap=True, inside=K.B(0, .18, 0)))
    parts.append(tube('Cuirass rim', [on_body(-.018, TAU * k / 64, .004) for k in range(64)], .009, M['silver'], closed=True, segs=8))
    parts.append(tube('Neck rim', [Vector((.27 * math.sin(TAU * k / 56), .385, .27 * math.cos(TAU * k / 56))) for k in range(56)], .007, M['silver'], closed=True, segs=8))
    # belt with three gold plates
    parts.append(surface('Belt', 3, 80, lambda t, s: on_body(lerp(.022, .074, t), TAU * s, .009), M['leather'], wrap=True, inside=K.B(0, .05, 0)))
    for a in (-.38, 0., .38):
        parts += plate(M, on_body(.048, a, .013), a, .021, .019)
    # baldric from the left shoulder to the right hip, with two gold studs
    def strap(t, s):
        h = lerp(.375, .08, t); a = lerp(-.62, .58, t ** 1.1) + (s - .5) * .16
        return on_body(h, a, .011 + .004 * math.sin(math.pi * s))
    parts.append(surface('Baldric', 20, 3, strap, M['leather'], inside=K.B(0, .2, 0)))
    for t in (.3, .62):
        h = lerp(.375, .08, t); a = lerp(-.62, .58, t ** 1.1)
        parts += plate(M, on_body(h, a, .016), a, .014, .014)
    if cape:
        parts += build_cape(M)
    return parts, {}, None


def plate(M, centre, a, hw, hh):
    """Small square gold plate facing outward at angle a, with a raised round boss."""
    out = Vector((math.sin(a), 0, math.cos(a))); side = Vector((math.cos(a), 0, -math.sin(a))); up = Vector((0, 1, 0))
    def pt(t, s):
        return centre + side * (hw * (2 * s - 1)) + up * (hh * (2 * t - 1))
    obs = [surface('Plate', 1, 1, pt, M['gold'], inside=centre - out * .05)]
    obs.append(K.mesh_object('Plate edge', [K.B(*(centre + side * (hw * x) + up * (hh * y) + out * z)) for z in (0, -.008) for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1))],
                             [(0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)], M['gold'], smooth=False, inside=K.B(*(centre - out * .05))))
    obs.append(ellipsoid('Boss', tuple(centre + out * .002), (hw * .45, hh * .45, hw * .45), M['gold'], (10, 6)))
    return obs


# ---------------------------------------------------------------- mantle and cape
def front(a): return ((1 + math.cos(a)) / 2) ** 2


def mantle_point(t, a, off=0.):
    f = front(a)
    r = lerp(.27, .392 - .03 * f, t) + .01 * t * math.sin(7 * a + .4) + .02 * math.sin(math.pi * t)
    y = lerp(.408, .25 + .095 * f, t) + .035 * math.sin(math.pi * t) + off
    return Vector((r * math.sin(a), y, r * math.cos(a)))


CAPE_TOP = (math.radians(80), math.radians(280))
CAPE_BOT = (math.radians(112), math.radians(248))


def cape_point(t, s, off=0.):
    a = lerp(lerp(CAPE_TOP[0], CAPE_BOT[0], t ** .7), lerp(CAPE_TOP[1], CAPE_BOT[1], t ** .7), s)
    edge = min(s, 1 - s) * 2
    y = lerp(.3, -.6 + .1 * (1 - edge) ** 2, t)
    r = lerp(.352, .45, t ** 1.1) + .032 * t * math.sin(9 * a + .8) + off
    return Vector((r * math.sin(a), y, r * math.cos(a)))


def build_cape(M):
    parts = []
    inside = K.B(0, .3, 0)
    parts.append(surface('Mantle', 8, 72, lambda t, s: mantle_point(t, TAU * s), M['cloth'], wrap=True, uv=(6, 1), inside=inside))
    parts.append(surface('Mantle under', 8, 72, lambda t, s: mantle_point(t, TAU * s, -.012), M['cloth'], wrap=True, uv=(6, 1), inside=inside, flip=True))
    # gold laurel border along the mantle's front edge, and an upright band round the neck
    edge = [mantle_point(1, a, .002) for a in np.linspace(-math.radians(95), math.radians(95), 40)]
    parts += ribbon('Laurel border', edge, lambda p, i: (Vector((p.x, 0, p.z)).normalized() * .35 + Vector((0, -1, 0))).normalized(), .045,
                    M['laurel'], uv_per_m=2.6, inside=inside)
    neck = [Vector((.274 * math.sin(TAU * k / 64), .405, .274 * math.cos(TAU * k / 64))) for k in range(64)]
    parts += ribbon('Laurel collar', neck, lambda p, i: Vector((p.x * .15, 1, p.z * .15)).normalized(), .034, M['laurel'], closed=True, uv_per_m=2.6, inside=inside)
    # cape hanging behind, with laurel bands down both front edges
    parts.append(surface('Cape', 18, 40, cape_point, M['cloth'], uv=(3, 4), inside=K.B(0, -.1, 0)))
    parts.append(surface('Cape lining', 18, 40, lambda t, s: cape_point(t, s, -.01), M['cloth'], uv=(3, 4), inside=K.B(0, -.1, 0), flip=True))
    for s, sd in ((0., 1), (1., -1)):
        line = [cape_point(t, s, .004) for t in np.linspace(.05, 1, 22)]
        def inward(p, i, line=line, sd=sd):
            a = math.atan2(p.x, p.z)
            return Vector((math.cos(a), 0, -math.sin(a))) * sd
        parts += ribbon('Cape border', line, inward, .05, M['laurel'], uv_per_m=2.6, inside=K.B(0, -.1, 0))
    # shoulder fringe where the mantle meets the cape
    for sd in (1, -1):
        for k in range(10):
            a = sd * math.radians(72 + 3 * k)
            top = mantle_point(1, a, .004)
            def strip(t, s, top=top, a=a):
                side = Vector((math.cos(a), 0, -math.sin(a)))
                out = Vector((math.sin(a), 0, math.cos(a)))
                return top + side * (.0055 * (2 * s - 1)) + Vector((0, -.055 * t, 0)) + out * (.012 * t)
            parts.append(surface('Fringe', 2, 1, strip, M['cloth'], inside=K.B(0, .26, 0)))
            parts.append(surface('Fringe back', 2, 1, strip, M['cloth'], inside=K.B(0, .26, 0), flip=True))
    return parts


# ---------------------------------------------------------------- war skirt
def build_skirt(M):
    parts = []
    inside = K.B(0, .05, 0)
    def tunic(t, s, off=0.):
        a = TAU * s; r = lerp(.31, .385, t) + .01 * t * math.sin(12 * a) + off
        return Vector((r * math.sin(a), lerp(.28, -.17, t), r * math.cos(a)))
    parts.append(surface('Tunic', 8, 72, tunic, M['tunic'], wrap=True, uv=(6, 1), inside=inside))
    parts.append(surface('Tunic lining', 8, 72, lambda t, s: tunic(t, s, -.006), M['tunic'], wrap=True, uv=(6, 1), inside=inside, flip=True))
    rows = ((16, 0., .262, -.15, .328, .382, .086), (16, .5, .27, -.035, .343, .385, .09))
    for count, phase, y0, y1, r0, r1, width in rows:
        for k in range(count):
            ac = TAU * (k + phase) / count
            half = width / 2
            def pt(t, s, ac=ac, half=half, off=0.):
                r = lerp(r0, r1, t ** .9) + off
                a = ac + (s - .5) * 2 * half / r
                return Vector((r * math.sin(a), lerp(y0, y1, t), r * math.cos(a)))
            parts.append(surface('Strip', 4, 2, lambda t, s, pt=pt: pt(1 - t, s), M['strip'], uv=(1, 1), inside=inside))
            parts.append(surface('Strip back', 4, 2, lambda t, s, pt=pt: pt(1 - t, s, off=-.008), M['strip'], uv=(1, 1), inside=inside, flip=True))
    # waistband with gold rivets
    def band(t, s):
        a = TAU * s; return Vector((.352 * math.sin(a), lerp(.24, .292, t), .352 * math.cos(a)))
    parts.append(surface('Waistband', 2, 72, band, M['leather'], wrap=True, inside=K.B(0, .26, 0)))
    for k in range(12):
        a = TAU * (k + .5) / 12
        parts.append(ellipsoid('Rivet', (.358 * math.sin(a), .266, .358 * math.cos(a)), (.009, .009, .009), M['gold'], (8, 5)))
    return parts, {}, None


# ---------------------------------------------------------------- bracers
def build_bracer(M, sx):
    parts = []
    def body(t, s):
        x = lerp(.25, .44, t); r = lerp(.083, .089, t) + .002 * math.sin(math.pi * t); a = TAU * s
        return Vector((sx * x, r * math.sin(a), r * math.cos(a)))
    parts.append(surface('Bracer', 6, 32, body, M['leather'], wrap=True, inside=K.B(sx * .34, 0, 0)))
    for x0, x1, rr in ((.24, .262, .09), (.428, .45, .096)):
        def trim(t, s, x0=x0, x1=x1, rr=rr):
            a = TAU * s; r = rr + .004 * math.sin(math.pi * t)
            return Vector((sx * lerp(x0, x1, t), r * math.sin(a), r * math.cos(a)))
        parts.append(surface('Trim', 3, 32, trim, M['cream'], wrap=True, inside=K.B(sx * .34, 0, 0)))
    parts.append(ellipsoid('Emblem', (sx * .345, .088, 0), (.034, .007, .026), M['gold'], (14, 8)))
    parts.append(tube('Emblem rim', [Vector((sx * (.345 + .036 * math.cos(TAU * k / 28)), .09, .028 * math.sin(TAU * k / 28))) for k in range(28)], .0035, M['gold'], closed=True, segs=6))
    return parts, {}, None


STYLES = {
    'legate_cuirass': ('Body Top', lambda M: build_cuirass(M)),
    'legate_cuirass_cape': ('Body Top', lambda M: build_cuirass(M, cape=True)),
    'legate_skirt': ('Body Bottom', build_skirt),
    'legate_bracer_r': ('Arm Right', lambda M: build_bracer(M, 1)),
    'legate_bracer_l': ('Arm Left', lambda M: build_bracer(M, -1)),
}
VIEWS = {'legate_cuirass': (.6, 1., .25), 'legate_cuirass_cape': (.9, .7, .3), 'legate_skirt': (.6, 1., .2),
         'legate_bracer_r': (.3, 1., .5), 'legate_bracer_l': (-.3, 1., .5)}

# The plain cuirass is a Body Top variant of the caped one: preview the caped version only.
preview_styles = dict(STYLES)
built = K.run(STYLES, VIEWS, ARGS.root / 'art' / 'cosmetics' / 'legate', 'legate', False, setup=make_mats)
if ARGS.preview:
    import bpy
    for ob in built['legate_cuirass'][1]: ob.hide_render = True
    for key, (slot, parts) in built.items():
        if key == 'legate_cuirass': continue
        for ob in parts: ob.hide_render = False; ob.matrix_world = K.frame_matrix(K.FRAMES[slot])
    K.stand_in_body()
    scene = bpy.context.scene; cam = scene.camera
    scene.render.film_transparent = False; scene.render.resolution_x = 760; scene.render.resolution_y = 900
    art = ARGS.root / 'art' / 'cosmetics' / 'legate'; art.mkdir(parents=True, exist_ok=True)
    target = Vector((0, -.05, .15))
    for suffix, eye in (('front', (.9, 2.6, .7)), ('back', (-1.2, -2.5, .8)), ('side', (2.8, -.3, .45))):
        K.look(cam, target + Vector(eye), target); K.render(art / ('legate_' + suffix + '.png'))
print('LEGATE_COMPLETE', flush=True)
