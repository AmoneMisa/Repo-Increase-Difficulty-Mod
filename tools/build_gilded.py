"""Blender 5.2: --background --factory-startup --python tools/build_gilded.py -- --root PATH [--base JSON] [--preview]

The Gilded Shadow set: a hooded sorceress outfit after the user's brown-and-gold concept sheet (original design):
sheer brown fabric with a lattice sheen, gold trim and filigree, amber gems, dangling pendants and crescent moons.

  gilded_hood        Hat          deep hood with a pointed peak, gold-trimmed edge, a jewel and a forehead pendant
  gilded_top         Body Top     corset with gold boning and a star clasp, open cropped coat with a high collar and
                                  lapels, spiked shoulder pieces, choker with a drop necklace
  gilded_skirt       Body Bottom  black shorts, chained belt with a star buckle, long open-front sheer overskirt in two
                                  layers with gold hems, pendants and crescent moons
  gilded_sleeve_r/l  Arm R / L    sheer puff sleeve, gathered cuff with a gold band, a cream frill and a pendant
  gilded_leg_r       Leg Right    dark brown stocking with a gold filigree top and garter, black gold-tipped boot
  gilded_leg_l       Leg Left     cream stocking, same boot (the two legs differ, like the sheet)

Tintable: the brown fabric (hood, top, skirt, sleeves) and each stocking. Fixed: gold, amber, black leather, lining.
Built with tools/cosmetic_kit.py.
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
from cosmetic_kit import TAU, lerp, surface, tube, ellipsoid, gem, ring, ribbon  # noqa: E402

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, required=True)
parser.add_argument('--base', type=Path)
parser.add_argument('--preview', action='store_true')
ARGS = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
ARGS.root = ARGS.root.resolve()
K.configure(ARGS.root, ARGS.base)

BROWN = (.42, .2, .1)
STOCK_R = (.24, .12, .07)
STOCK_L = (.93, .88, .8)
GOLD = (1., .74, .32)
AMBER = (1., .5, .12)
BLACK = (.03, .025, .025)
LINING = (.07, .035, .025)


def textures():
    rng = random.Random(3)
    n = 256
    y, x = (np.mgrid[0:n, 0:n] + .5) / n
    def line(d, w): return np.exp(-(d / w) ** 2)
    # tone-on-tone damask: soft scrolling leaves woven into a fine satin, no hard grid
    mott = K.periodic_noise(n, 4, rng)
    u, v = (x * 3) % 1 - .5, (y * 3) % 1 - .5
    r = np.hypot(u, v); a = np.arctan2(v, u)
    scroll = line(r - (.22 + .07 * np.cos(4 * a)), .02) + .7 * line(np.hypot(np.abs(u) - .5, np.abs(v) - .5) - .12, .02)
    leaf = line(np.abs(r - .32) * (1.5 + np.cos(6 * a)), .02)
    satin = .5 + .5 * np.sin(TAU * (x * 64 + y * 16))
    K.save_png('gilded_lattice.png', np.clip(.8 + .07 * satin * .5 + .1 * (mott - .5) + .1 * np.clip(scroll + .6 * leaf, 0, 1), 0, 1))
    # gold filigree border band (untinted): scrolls and beading between two gold lines on dark brown
    w, h = 512, 64
    yy, xx = (np.mgrid[0:h, 0:w] + .5)
    bu, bv = xx / w, yy / h
    band = np.zeros((h, w))
    band = np.maximum(band, line(bv - .1, .03)); band = np.maximum(band, line(bv - .9, .03))
    for k in range(8):
        cx = (k + .5) / 8; side = 1 if k % 2 else -1
        dx = (bu - cx) * 8; dy = (bv - .5) * 2
        rr = np.hypot(dx, dy); aa = np.arctan2(dy, dx) * side
        spiral = line(rr - (.15 + .5 * ((aa + np.pi) / TAU)), .05) * (rr < .7)
        band = np.maximum(band, spiral)
        band = np.maximum(band, line(np.hypot(dx - side * .62, dy), .1))
    band = np.maximum(band, line(np.abs(((bu * 32) % 1) - .5) * 2 + np.abs(bv - .2) * 9 - .25, .2) * (np.abs(bv - .2) < .05))
    gold = np.array(GOLD); dark = np.array((.12, .055, .03))
    img = dark * (1 - band[..., None]) + gold * band[..., None] * (.8 + .2 * np.sin(TAU * bu * 64)[..., None])
    K.save_png('gilded_filigree.png', img, worn=False)
    # the dark inner skirt: black with gold flecks (untinted, colour)
    img = np.zeros((n, n, 3)); img[...] = np.array(BLACK) + .03 * (mott - .5)[..., None]
    for _ in range(300):
        cx, cy = rng.randrange(n), rng.randrange(n)
        img[cy, cx] = np.array(GOLD) * rng.uniform(.6, 1)
    K.save_png('gilded_dark.png', np.clip(img, 0, 1))


def make_mats():
    textures()
    m = K.material
    return {
        'brown': m('Gilded fabric', BROWN, .5, image='gilded_lattice.png', tint=True),
        'filigree': m('Gilded filigree', (1, 1, 1), .4, metal=.4, image='gilded_filigree.png'),
        'stock_r': m('Gilded stocking dark', STOCK_R, .35, tint=True),
        'stock_l': m('Gilded stocking light', STOCK_L, .4, tint=True),
        'gold': m('Gilded gold', GOLD, .28, metal=.92),
        'amber': m('Gilded amber', AMBER, .08, emit=.7),
        'black': m('Gilded black leather', BLACK, .3),
        'lining': m('Gilded lining', LINING, .8),
        'dark': m('Gilded dark veil', (1, 1, 1), .6, image='gilded_dark.png'),
        'cream': m('Gilded frill', (.96, .93, .86), .7),
    }


def pendant(M, top, length, group='static', moon=False, size=1.):
    """A gold chain from 'top' hanging straight down, ending in an amber drop (or a gold crescent moon)."""
    top = Vector(top)
    bottom = top - Vector((0, length, 0))
    obs = [tube('Chain', [top.lerp(bottom, t) + Vector((.003 * math.sin(t * 20), 0, 0)) for t in np.linspace(0, 1, 6)], .0022 * size, M['gold'], group, segs=5)]
    if moon:
        c = bottom - Vector((0, .035 * size, 0))
        arc = [c + Vector((math.cos(a) * .032 * size, math.sin(a) * .032 * size, 0)) for a in np.linspace(math.radians(110), math.radians(430), 18)]
        obs.append(tube('Moon', arc, .006 * size, M['gold'], group, segs=6))
        obs.append(gem('Drop', tuple(c - Vector((0, .05 * size, 0))), (.008 * size, .02 * size, .008 * size), M['amber']))
    else:
        obs.append(gem('Drop', tuple(bottom - Vector((0, .018 * size, 0))), (.01 * size, .024 * size, .01 * size), M['amber']))
    return obs


def star(M, centre, normal, size, mat):
    """A flat four-point star (two crossed diamonds) facing 'normal'."""
    c = Vector(centre); n = Vector(normal).normalized()
    side = n.cross(Vector((0, 1, 0)) if abs(n.y) < .9 else Vector((0, 0, 1))).normalized(); up = side.cross(n).normalized()
    obs = []
    for ax, ay in ((up, side), (side, up)):
        pts = [c + ax * size, c + ay * size * .28, c - ax * size, c - ay * size * .28]
        verts = [K.B(*p) for p in pts] + [K.B(*(c + n * size * .18)), K.B(*(c - n * size * .05))]
        faces = [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4), (1, 0, 5), (2, 1, 5), (3, 2, 5), (0, 3, 5)]
        obs.append(K.mesh_object('Star', verts, faces, mat, smooth=False, inside=K.B(*(c - n * .02))))
    return obs


# ---------------------------------------------------------------- hood (head-top frame)
HC = Vector((0, 0, .24)); HR = Vector((.31, .43, .31))


def hood_edge(a):
    f = (1 + math.cos(a)) / 2
    return -32 + 77 * f ** 1.5


def hood_point(a, lat, off=0.):
    la = math.radians(lat)
    d = Vector((math.cos(la) * math.sin(a), math.sin(la), math.cos(la) * math.cos(a)))
    p = HC + Vector((HR.x * d.x, HR.y * d.y, HR.z * d.z))
    f = (1 + math.cos(a)) / 2
    # (a plain rounded crown; the old pointed peak read as an onion dome)
    p += Vector((0, 0, .05)) * f ** 3 * math.exp(-((lat - hood_edge(a) - 6) / 16) ** 2)  # the brim juts over the face
    return p + d * off


def build_hood(M):
    parts = []
    def pt(t, s, off=0.):
        a = TAU * s - math.pi; e = hood_edge(a)
        return hood_point(a, lerp(e, 89.5, t ** .9), off)
    inside = K.B(*HC)
    parts.append(surface('Hood', 16, 64, pt, M['brown'], wrap=True, uv=(4, 2), inside=inside))
    parts.append(surface('Hood lining', 16, 64, lambda t, s: pt(t, s, -.014), M['lining'], wrap=True, inside=inside, flip=True))
    edge = [pt(0, k / 96, -.006) for k in range(96)]
    parts.append(tube('Hood trim', edge, .0085, M['gold'], closed=True, segs=7))
    parts += ribbon('Hood filigree band', [pt(.04, k / 96, .004) for k in range(96)], lambda p, i: (pt(.14, i / 96, .004) - pt(.04, i / 96, .004)).normalized(), .035,
                    M['filigree'], closed=True, uv_per_m=5, inside=K.B(*HC), both=False)
    # filigree: a second, thinner gold line a little inside the edge, and a jewel with a pendant at the brow
    inner = [hood_point(TAU * k / 96 - math.pi, hood_edge(TAU * k / 96 - math.pi) + 7, .003) for k in range(96)]
    parts.append(tube('Hood filigree', inner, .004, M['gold'], closed=True, segs=5))
    brow = hood_point(0, hood_edge(0), .004)
    parts += star(M, brow + Vector((0, .02, .012)), (0, .25, 1), .045, M['gold'])
    parts.append(gem('Brow jewel', tuple(brow + Vector((0, .02, .022))), (.013, .018, .008), M['amber']))
    parts += pendant(M, brow - Vector((0, .005, -.01)), .05, size=.9)
    crown = K.B(*(HC + Vector((0, HR.y + .08, .02))))
    return parts, {}, crown


# ---------------------------------------------------------------- top (body-top frame)
PROFILE = [(.298, -.02), (.306, .02), (.31, .2), (.313, .28), (.306, .34), (.29, .368), (.266, .384)]


def body_r(h):
    for (r0, h0), (r1, h1) in zip(PROFILE, PROFILE[1:]):
        if h0 <= h <= h1: return r0 + (r1 - r0) * (h - h0) / (h1 - h0)
    return PROFILE[-1][0] if h > PROFILE[-1][1] else PROFILE[0][0]


def on_body(h, a, lift=0.):
    r = body_r(h) + lift
    return Vector((r * math.sin(a), h, r * math.cos(a)))


def neckline(a):
    a = (a + math.pi) % TAU - math.pi
    f = ((1 + math.cos(a)) / 2) ** 3
    return .352 - .06 * f - .03 * math.exp(-(a / .12) ** 2) + .02 * math.exp(-((abs(a) - .36) / .2) ** 2)


def build_top(M):
    parts = []
    inside = K.B(0, .2, 0)
    # corset
    corset = lambda t, s: on_body(lerp(.05, neckline(TAU * s), t), TAU * s, .011)
    parts.append(surface('Corset', 12, 96, corset, M['brown'], wrap=True, uv=(5, 1.4), inside=inside))
    parts.append(tube('Corset top', [corset(1, k / 96) + Vector((0, .002, 0)) for k in range(96)], .006, M['gold'], closed=True, segs=6))
    parts.append(tube('Corset bottom', [corset(0, k / 96) for k in range(96)], .006, M['gold'], closed=True, segs=6))
    for a in (0, .55, -.55, 1.25, -1.25, 2.2, -2.2, math.pi):
        bone = [on_body(h, a, .015) for h in np.linspace(.06, neckline(a) - .01, 8)]
        parts.append(tube('Boning', bone, .0042, M['gold'], segs=5))
    parts += star(M, on_body(.19, 0, .028), (0, 0, 1), .05, M['gold'])
    parts.append(gem('Clasp gem', tuple(on_body(.19, 0, .036)), (.012, .016, .008), M['amber']))
    # open cropped coat with a high collar and lapels
    a0, a1 = math.radians(34), math.radians(326)
    def coat(t, s, off=0.):
        a = lerp(a0, a1, s); h = lerp(.0, .37, t)
        return on_body(h, a, .034 + off + .008 * math.sin(math.pi * t))
    parts.append(surface('Coat', 10, 60, coat, M['brown'], uv=(4, 1.2), inside=inside))
    parts.append(surface('Coat lining', 10, 60, lambda t, s: coat(t, s, -.008), M['lining'], inside=inside, flip=True))
    def collar(t, s, off=0.):
        a = lerp(math.radians(52), math.radians(308), s)
        back = (1 - math.cos(a)) / 2
        h = lerp(.37, .37 + .1 * back + .03, t)
        r = .3 + .05 * t + off
        return Vector((r * math.sin(a), h, r * math.cos(a)))
    parts.append(surface('Collar', 4, 40, collar, M['brown'], uv=(3, .5), inside=K.B(0, .3, 0)))
    parts.append(surface('Collar lining', 4, 40, lambda t, s: collar(t, s, -.007), M['lining'], inside=K.B(0, .3, 0), flip=True))
    parts.append(tube('Collar trim', [collar(1, s) for s in np.linspace(0, 1, 40)], .006, M['gold'], segs=6))
    for sd in (1, -1):
        # lapel: a wide strip down the coat's front edge, turned outward
        a = sd * a0
        def lapel(t, s, a=a, sd=sd):
            h = lerp(.06, .375, t); w = lerp(.02, .12, t ** 1.4) * s
            base = on_body(h, a, .036)
            out = Vector((math.sin(a), 0, math.cos(a)))
            side = Vector((math.cos(a), 0, -math.sin(a))) * sd
            return base + side * w + out * (.02 * s + .03 * s * t)
        parts.append(surface('Lapel', 8, 3, lapel, M['brown'], uv=(.5, 1), inside=inside))
        parts.append(surface('Lapel back', 8, 3, lapel, M['lining'], inside=inside, flip=True))
        parts.append(tube('Lapel trim', [lapel(t, 1) for t in np.linspace(0, 1, 10)] + [lapel(1, s) for s in np.linspace(1, 0, 4)[1:]], .005, M['gold'], segs=6))
        parts.append(tube('Coat edge', [coat(t, 0 if sd > 0 else 1) for t in np.linspace(0, 1, 8)], .005, M['gold'], segs=6))
        # spiked shoulder piece: three overlapping scales with gold rims and spikes
        a2 = sd * (math.pi / 2 - .12)
        c = on_body(.355, a2, .035)
        parts.append(ellipsoid('Pauldron', tuple(c), (.1, .045, .11), M['brown'], (16, 8)))
        parts.append(tube('Pauldron rim', [c + Vector((.1 * math.cos(t), -.004, .11 * math.sin(t))) for t in np.linspace(0, TAU, 33)[:-1]], .005, M['gold'], closed=True, segs=6))
        for k, tilt in enumerate((-.5, 0., .5)):
            out = Vector((math.sin(a2 + tilt * .6) * .8, .6, math.cos(a2 + tilt * .6) * .8)).normalized()
            parts.append(gem('Spike', tuple(c + out * .06), (.009, .036 - .008 * abs(tilt), .009), M['gold'], facing=(math.sin(a2), 0, math.cos(a2))))
        parts += pendant(M, on_body(.27, sd * 1.45, .07), .07, size=.9)
    # choker and necklace
    def choker(t, s):
        a = TAU * s; r = .272
        return Vector((r * math.sin(a), lerp(.37, .395, t), r * math.cos(a)))
    parts.append(surface('Choker', 2, 56, choker, M['brown'], wrap=True, inside=K.B(0, .38, 0)))
    neck = [on_body(.36 - .07 * math.cos(a / 1.05 * math.pi / 2), a, .03) for a in np.linspace(-1.0, 1.0, 20)]
    parts.append(tube('Necklace', neck, .003, M['gold'], segs=5))
    parts.append(gem('Necklace drop', tuple(on_body(.272, 0, .034)), (.012, .026, .01), M['amber']))
    return parts, {}, None


# ---------------------------------------------------------------- skirt (body-bottom frame)
OPEN = math.radians(30)


def veil_point(t, a, layer, off=0.):
    f = ((1 + math.cos(a)) / 2)
    hem = (-.6 + .42 * f ** 1.4) if layer == 0 else (-.52 + .36 * f ** 1.4)
    r0 = .33 + .008 * layer
    R = .55 + .05 * layer - .08 * f
    y = lerp(.27, hem + .03 * math.sin(6 * a), t)
    r = r0 + (R - r0) * t ** 1.2 + .035 * t ** 1.5 * math.sin((7 + layer * 2) * a + layer) + off
    return Vector((r * math.sin(a), y, r * math.cos(a)))


def build_skirt(M):
    parts = []
    inside = K.B(0, -.1, 0)
    def shorts(t, s):
        a = TAU * s; h = lerp(-.04, .27, t); r = .308 + .006 * math.sin(math.pi * t)
        return Vector((r * math.sin(a), h, r * math.cos(a)))
    parts.append(surface('Shorts', 6, 64, shorts, M['black'], wrap=True, inside=K.B(0, .1, 0)))
    parts.append(tube('Shorts hem', [shorts(0, k / 64) for k in range(64)], .005, M['gold'], closed=True, segs=6))
    # two veils: the inner long one dark with gold flecks, the outer sheer brown, both open at the front
    for layer, mat in ((0, M['dark']), (1, M['brown'])):
        pt = lambda t, s, off=0., layer=layer: veil_point(t, lerp(OPEN + layer * .05, TAU - OPEN - layer * .05, s), layer, off)
        parts.append(surface('Veil', 14, 64, pt, mat, uv=(5, 2), inside=inside))
        parts.append(surface('Veil lining', 14, 64, lambda t, s, pt=pt: pt(t, s, -.006), M['lining'], inside=inside, flip=True))
        hem = [pt(1, s) for s in np.linspace(0, 1, 72)]
        parts.append(tube('Veil hem', hem, .0055, M['gold'], segs=6))
        # a gold filigree border above the hem, lying on the veil
        ss = np.linspace(0, 1, 72)
        border = [pt(.9, s_, .004) for s_ in ss]
        parts += ribbon('Veil filigree', border, lambda p, i, pt=pt, ss=ss: (pt(.99, ss[i], .004) - pt(.9, ss[i], .004)).normalized(), .055,
                        M['filigree'], uv_per_m=6, inside=inside, both=False)
        for s in (0., 1.):
            parts.append(tube('Veil edge', [pt(t, s) for t in np.linspace(0, 1, 14)], .0045, M['gold'], segs=5))
        if layer == 1:
            for k in range(9):
                s = (k + .5) / 9
                parts += pendant(M, pt(1, s), .05 + .03 * (k % 2), moon=(k in (0, 8)))
    # belt: brown band, gold studs, a star buckle off-centre and two hanging chains
    def belt(t, s):
        a = TAU * s; r = .342
        return Vector((r * math.sin(a), lerp(.23, .285, t), r * math.cos(a)))
    parts.append(surface('Belt', 2, 64, belt, M['brown'], wrap=True, inside=K.B(0, .25, 0)))
    for h in (.232, .283):
        parts.append(tube('Belt edge', [belt(0, k / 64) * 1.0 + Vector((0, h - .23, 0)) for k in range(64)], .0035, M['gold'], closed=True, segs=5))
    ab = -.3
    centre = Vector((.35 * math.sin(ab), .257, .35 * math.cos(ab)))
    parts += star(M, centre, (math.sin(ab), 0, math.cos(ab)), .06, M['gold'])
    parts.append(gem('Buckle gem', tuple(centre + Vector((math.sin(ab), 0, math.cos(ab))) * .012), (.012, .016, .008), M['amber'], facing=(math.sin(ab), 0, math.cos(ab))))
    for da, length in ((-.12, .2), (.1, .26)):
        a = ab + da
        parts += pendant(M, Vector((.352 * math.sin(a), .232, .352 * math.cos(a))), length)
    return parts, {}, None


# ---------------------------------------------------------------- sleeves (arm frames)
def build_sleeve(M, sx):
    parts = []
    def sleeve(t, s, off=0.):
        x = lerp(.03, .37, t); a = TAU * s
        r = .085 + .055 * math.sin(math.pi * min(1, t * 1.05)) ** .8 + off
        r += .008 * math.sin(9 * a) * math.sin(math.pi * t)
        return Vector((sx * x, r * math.sin(a), r * math.cos(a)))
    parts.append(surface('Puff sleeve', 12, 36, sleeve, M['brown'], wrap=True, uv=(2, 2), inside=K.B(sx * .2, 0, 0)))
    parts.append(surface('Sleeve lining', 12, 36, lambda t, s: sleeve(t, s, -.007), M['lining'], wrap=True, inside=K.B(sx * .2, 0, 0), flip=True))
    # gathered cuff: brown band, gold ring, cream frill, and a pendant
    def cuff(t, s, rr=.092):
        a = TAU * s; r = rr + .006 * math.sin(math.pi * t)
        return Vector((sx * lerp(.36, .41, t), r * math.sin(a), r * math.cos(a)))
    parts.append(surface('Cuff', 3, 32, cuff, M['brown'], wrap=True, inside=K.B(sx * .38, 0, 0)))
    parts.append(tube('Cuff gold', [cuff(.5, k / 32, .1) for k in range(32)], .007, M['gold'], closed=True, segs=6))
    frill = [Vector((sx * .41, .094 * math.sin(TAU * k / 40), .094 * math.cos(TAU * k / 40))) for k in range(40)]
    parts += ribbon('Frill', frill, lambda p, i: (Vector((sx, 0, 0)) * .7 + Vector((0, p.y, p.z)).normalized() * .7).normalized(), .05,
                    M['cream'], closed=True, inside=K.B(sx * .41, 0, 0))
    parts += star(M, Vector((sx * .385, .104, 0)), (0, 1, 0), .03, M['gold'])
    parts += pendant(M, Vector((sx * .4, -.1, 0)), .05, size=.9)
    return parts, {}, None


# ---------------------------------------------------------------- legs (leg frames)
def leg_r(y):
    return .106 if y > 0 else .106 * max(0., (y + .549) / .549)


def build_leg(M, light):
    parts = []
    stock = M['stock_l' if light else 'stock_r']
    top, knee = .02, -.33
    def stocking(t, s):
        y = lerp(top, knee, t); a = TAU * s; r = leg_r(y) + .009
        return Vector((r * math.sin(a), y, r * math.cos(a)))
    parts.append(surface('Stocking', 8, 32, stocking, stock, wrap=True, inside=K.B(0, -.15, 0)))
    # filigree top: gold band with downward points
    band = [stocking(.04, k / 48) * 1.0 + Vector((0, 0, 0)) for k in range(48)]
    parts.append(tube('Stocking band', [p * 1.03 for p in band], .006, M['gold'], closed=True, segs=6))
    for k in range(8):
        a = TAU * k / 8
        p = stocking(.1, k / 8)
        out = Vector((math.sin(a), 0, math.cos(a)))
        parts.append(gem('Filigree', tuple(p + out * .012), (.012, .03, .004), M['gold'], facing=tuple(out)))
    parts.append(gem('Garter gem', tuple(stocking(.02, 0) + Vector((0, .01, .018))), (.013, .02, .008), M['amber']))
    parts.append(tube('Garter', [stocking(.02, 0) + Vector((0, .01 + .1 * t, .014)) for t in np.linspace(0, 1, 5)], .005, M['gold'], segs=5))
    # boot
    btop, tip = -.28, -.57
    def boot(t, s):
        y = lerp(btop, tip, t); a = TAU * s
        r = (leg_r(y) + .022 + .012 * (1 - t)) * (1 - t ** 6) + .003
        return Vector((r * math.sin(a), y, r * math.cos(a) + .025 * t ** 3))
    parts.append(surface('Boot', 14, 32, boot, M['black'], wrap=True, inside=K.B(0, -.42, 0)))
    parts.append(tube('Boot cuff', [boot(0, k / 40) * 1.02 for k in range(40)], .007, M['gold'], closed=True, segs=6))
    def toe(t, s):
        p = boot(lerp(.78, 1, t), s); return p * 1.0 + Vector((0, 0, 0)) + (p - Vector((0, p.y, 0))).normalized() * .002
    parts.append(surface('Toe cap', 4, 32, toe, M['gold'], wrap=True, inside=K.B(0, -.53, 0)))
    front = boot(.45, 0)
    parts += star(M, front + Vector((0, 0, .008)), (0, 0, 1), .045, M['gold'])
    parts.append(gem('Boot gem', tuple(front + Vector((0, 0, .02))), (.01, .014, .007), M['amber']))
    return parts, {}, None


STYLES = {
    'gilded_hood': ('Hat', build_hood),
    'gilded_top': ('Body Top', build_top),
    'gilded_skirt': ('Body Bottom', build_skirt),
    'gilded_sleeve_r': ('Arm Right', lambda M: build_sleeve(M, 1)),
    'gilded_sleeve_l': ('Arm Left', lambda M: build_sleeve(M, -1)),
    'gilded_leg_r': ('Leg Right', lambda M: build_leg(M, False)),
    'gilded_leg_l': ('Leg Left', lambda M: build_leg(M, True)),
}
VIEWS = {'gilded_hood': (.6, 1., .35), 'gilded_top': (.6, 1., .3), 'gilded_skirt': (.6, 1., .25),
         'gilded_sleeve_r': (.3, 1., .5), 'gilded_sleeve_l': (-.3, 1., .5), 'gilded_leg_r': (.6, 1., .1), 'gilded_leg_l': (-.6, 1., .1)}

K.run(STYLES, VIEWS, ARGS.root / 'art' / 'cosmetics' / 'gilded', 'gilded', ARGS.preview, setup=make_mats,
      preview_views=(('front', (.9, 2.7, .6)), ('back', (-1.3, -2.6, .8)), ('side', (2.8, -.3, .45))))
print('GILDED_COMPLETE', flush=True)
