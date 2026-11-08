"""Blender 5.2: --background --factory-startup --python tools/build_jester.py -- --root PATH [--base JSON] [--preview]

The Card Jester set (after the user's concept sheet: a red-and-black harlequin with a ruff, bells and playing cards):

  jester_hat       Hat         cap split red / black with three floppy points, brass bells and playing-card charms
  jester_mask      Face Top    white porcelain mask with black almond eyes and a thin smile
  jester_doublet   Body Top    doublet split red / black with a white front panel, pompom buttons, white cross
                               stitches, a two-layer pleated ruff and a white ruffled peplum
  jester_trousers  Body Bottom wide floor-length trousers, one leg red and one black, cross stitches, a ribbon with a bell
  jester_sleeve_r  Arm Right   red puffed sleeve with a white ruffled cuff and a bell
  jester_sleeve_l  Arm Left    black puffed sleeve, same cuff

Tintable: the red cloth. Fixed: black, white, brass, cards, mask. Built on tools/cosmetic_kit.py + costume_parts.py.
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
import costume_parts as P  # noqa: E402
from cosmetic_kit import TAU, lerp, surface, tube, ellipsoid, bez, ribbon  # noqa: E402

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, required=True)
parser.add_argument('--base', type=Path)
parser.add_argument('--preview', action='store_true')
ARGS = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
ARGS.root = ARGS.root.resolve()
K.configure(ARGS.root, ARGS.base)

RED = (.62, .05, .07)
BLACK = (.05, .04, .05)
WHITE = (.93, .9, .86)
BRASS = (.85, .62, .3)


def card_texture():
    w, h = 64, 96
    yy, xx = (np.mgrid[0:h, 0:w] + .5)
    u, v = xx / w, yy / h
    img = np.ones((h, w, 3)) * np.array(WHITE)
    border = (u < .06) | (u > .94) | (v < .04) | (v > .96)
    img[border] = (.75, .72, .68)
    def heart(cx, cy, s):
        x = (u - cx) / s; y = (v - cy) / s
        return (x * x + (y - np.sqrt(np.abs(x)) * .8) ** 2) < .35
    big = heart(.5, .5, .3); img[big] = (.75, .05, .07)
    for cx, cy in ((.2, .15), (.8, .85)):
        img[heart(cx, cy, .09)] = (.75, .05, .07)
    K.save_png('jester_card.png', img)


def make_mats():
    rng = random.Random(4)
    P.cloth_texture('jester_cloth.png', rng, base=.8, weave=.05, mottle=.12)
    card_texture()
    m = K.material
    return {
        'red': m('Jester red', RED, .6, image='jester_cloth.png', tint=True),
        'black': m('Jester black', BLACK, .6, image='jester_cloth.png'),
        'white': m('Jester white', WHITE, .65),
        'brass': m('Jester brass', BRASS, .3, metal=.85),
        'heart': m('Jester heart', (.75, .05, .07), .4),
        'mask': m('Jester mask', (.96, .95, .93), .25),
        'ink': m('Jester ink', (.01, .01, .012), .3),
    }


def bell(M, at, size=1.):
    at = Vector(at)
    return [ellipsoid('Bell', tuple(at), (.024 * size, .022 * size, .024 * size), M['brass'], (10, 6)),
            tube('Bell slot', [at + Vector((-.014, -.012, .02)) * size, at + Vector((.014, -.012, .02)) * size], .003 * size, M['ink'], segs=4)]


def card(M, top, size=1.):
    """A playing card hanging from 'top' on a short ring."""
    top = Vector(top)
    w, h = .045 * size, .065 * size
    def pt(t, s):
        return top + Vector((w * (s - .5), -.012 - h * (1 - t), 0))
    mid = top + Vector((0, -.012 - h * .5, 0))
    obs = [surface('Card', 1, 1, pt, M['white'], inside=K.B(*(top + Vector((0, -.04, -.05))))),
           surface('Card back', 1, 1, lambda t, s: pt(t, s) - Vector((0, 0, .0025)), M['white'], inside=K.B(*(top + Vector((0, -.04, -.05)))), flip=True),
           tube('Card ring', [top, top - Vector((0, .012, 0))], .002, M['brass'], segs=4)]
    for z in (.003, -.0055):  # a raised red heart on both faces
        for dx in (-.007, .007):
            obs.append(ellipsoid('Heart', tuple(mid + Vector((dx * size, .006 * size, z))), (.009 * size, .009 * size, .002), M['heart'], (8, 5)))
        obs.append(K.gem('Heart point', tuple(mid + Vector((0, -.006 * size, z))), (.013 * size, .012 * size, .002), M['heart']))
    return obs


# ---------------------------------------------------------------- hat (head frame)
CAP_C = P.HEAD_C; CAP_R = P.HEAD_R + Vector((.03, .03, .03))


def cap_point(a, lat, off=0.):
    la = math.radians(lat)
    d = Vector((math.cos(la) * math.sin(a), math.sin(la), math.cos(la) * math.cos(a)))
    return CAP_C + Vector((CAP_R.x * d.x, CAP_R.y * d.y, CAP_R.z * d.z)) + d * off


def build_hat(M):
    parts = []
    edge = lambda a: 30 - 16 * (1 - P.front(a))       # sits above the eyes at the front, lower at the back
    for mat, a0, a1 in ((M['black'], 0., math.pi), (M['red'], math.pi, TAU)):
        parts.append(surface('Cap', 10, 32, lambda t, s, a0=a0, a1=a1: cap_point(lerp(a0, a1, s), lerp(edge(lerp(a0, a1, s)), 89.5, t ** .9)),
                             mat, uv=(2, 1), inside=K.B(*CAP_C)))
    parts.append(tube('Band', [cap_point(TAU * k / 64, edge(TAU * k / 64), .006) for k in range(64)], .013, M['black'], closed=True, segs=7))
    # three floppy points: left red, right black, back red; bells and cards at the tips
    for sd, mat, back in ((-1, M['red'], 0), (1, M['black'], 0), (0, M['red'], 1)):
        base = cap_point(sd * 1.2 if not back else math.pi, 62)
        if back:
            ctrl = [base, base + Vector((0, .25, -.18)), base + Vector((0, .05, -.42))]
        else:
            ctrl = [base, base + Vector((sd * .3, .3, -.02)), base + Vector((sd * .42, -.12, -.06))]
        path = [bez(ctrl, t) for t in np.linspace(0, 1, 18)]
        parts.append(tube('Point', path, lambda s: .085 * (1 - s) ** .9 + .012, mat, segs=12))
        tip = path[-1]
        parts += bell(M, tip - Vector((0, .03, 0)))
        parts += card(M, tip - Vector((0, .055, 0)))
        for t in (.35, .65):
            p = bez(ctrl, t)
            out = (p - CAP_C).normalized()
            parts += P.cross_stitch('Stitch', p + out * (.085 * (1 - t) + .014), out, .012, M['white'])
    for a, lat in ((.6, 50), (-.7, 45), (2.4, 40), (-2.3, 55)):
        p = cap_point(a, lat, .004); parts += P.cross_stitch('Stitch', p, (p - CAP_C), .014, M['white'])
    return parts, {}, K.B(*(CAP_C + Vector((0, CAP_R.y + .05, 0))))


# ---------------------------------------------------------------- mask (Face Top: the head-top frame)
def build_mask(M):
    parts = []
    def mask(t, s, off=0.):
        a = lerp(-1.05, 1.05, s); lat = lerp(-10, 54, t)
        la = math.radians(lat)
        d = Vector((math.cos(la) * math.sin(a), math.sin(la), math.cos(la) * math.cos(a)))
        bulge = .02 * math.cos(a * 1.4) * math.sin(math.radians(lerp(-10, 54, t) + 30))
        return P.HEAD_C + Vector((P.HEAD_R.x * d.x, P.HEAD_R.y * d.y, P.HEAD_R.z * d.z)) + d * (.022 + bulge + off)
    parts.append(surface('Mask', 12, 28, mask, M['mask'], inside=K.B(*P.HEAD_C)))
    parts.append(surface('Mask back', 12, 28, lambda t, s: mask(t, s, -.006), M['ink'], inside=K.B(*P.HEAD_C), flip=True))
    for sd in (1, -1):
        # a flat almond-shaped eye hole lying on the mask: pointed at both ends, the outer corner raised (a sly slant)
        s0, t0 = .5 + sd * .19, .36
        def eye(v, u, s0=s0, t0=t0, sd=sd):
            x = u * 2 - 1                                  # -1 inner corner .. 1 outer corner (mirrored per side)
            half = .055 * (1 - x * x) ** .75
            return mask(t0 + .05 * x + half * (2 * v - 1), s0 + sd * .095 * x, .0015)
        parts.append(surface('Eye', 6, 16, eye, M['ink'], inside=K.B(*P.HEAD_C)))
    smile = [mask(.13 + .08 * (2 * s - 1) ** 2, s, .003) for s in np.linspace(.22, .78, 18)]
    parts.append(tube('Smile', smile, .0035, M['ink'], segs=5))
    return parts, {}, None


# ---------------------------------------------------------------- doublet (body top)
def build_doublet(M):
    parts = []
    colour = lambda a: 'white' if abs(P.wrap_angle(a)) < .36 else ('red' if P.wrap_angle(a) < 0 else 'black')
    parts += P.body_shell('Doublet', None, -.02, lambda a: .384, .012, colour_of=colour, mats=M)
    for h, mat in ((.31, 'black'), (.21, 'black'), (.11, 'red')):
        parts.append(ellipsoid('Pompom', tuple(P.on_body(h, 0, .03)), (.032, .032, .025), M[mat], (12, 8)))
    for a, h in ((-.9, .28), (-.8, .12), (-1.5, .2), (.95, .25), (1.6, .1), (2.6, .2), (-2.5, .3)):
        p = P.on_body(h, a, .015); parts += P.cross_stitch('Stitch', p, Vector((math.sin(a), 0, math.cos(a))), .016, M['white'])
    for a in (-.36, .36):
        parts.append(tube('Seam', [P.on_body(h, a, .014) for h in np.linspace(-.01, .38, 12)], .0035, M['white'], segs=5))
    parts += P.ruff('Ruff', M['white'], .385, .25, .45, pleats=20, layers=2, rise=.05)
    # ruffled white peplum in two tiers below the doublet
    for k, (y0, depth, r0) in enumerate(((.02, .12, .318), (-.01, .17, .324))):
        o, _ = P.robe('Peplum', M['white'], M['white'], 0, TAU, y0, r0, lambda a, y0=y0, depth=depth: y0 - depth + P.tatter(a, 12, .04, k * .5),
                      lambda t, a, r0=r0: r0 + .08 * t, folds=(.02, 14, k), rows=4, cols=72, inside=K.B(0, y0 - .3, 0))
        parts += o
    return parts, {}, None


# ---------------------------------------------------------------- trousers (body bottom)
def build_trousers(M):
    """Breeches over the hips (red right half, black left); the trouser legs are their own Leg Right / Leg Left parts."""
    parts = []
    hem = lambda a: -.06 + .012 * math.sin(6 * a)
    radius = lambda t, a: .318 + .02 * math.sin(math.pi * t)
    for mat, a0, a1 in ((M['red'], 0., math.pi), (M['black'], math.pi, TAU)):
        o, pt = P.robe('Breeches', mat, M['black'], a0, a1, .28, .318, hem, radius, folds=(.01, 12, .5), rows=6, cols=36, inside=K.B(0, .1, 0))
        parts += o
        p = pt(.5, .5, .004); a = lerp(a0, a1, .5)
        parts += P.cross_stitch('Stitch', p, Vector((math.sin(a), 0, math.cos(a))), .02, M['white'])
    for a in (0., math.pi):
        parts.append(tube('Side seam', [Vector(((.318 + .02 * math.sin(math.pi * t) + .003) * math.sin(a), lerp(.28, -.06, t), (.318 + .02 * math.sin(math.pi * t) + .003) * math.cos(a))) for t in np.linspace(0, 1, 8)], .004, M['white'], segs=5))
    def band(t, s):
        a = TAU * s; return Vector((.33 * math.sin(a), lerp(.24, .29, t), .33 * math.cos(a)))
    parts.append(surface('Waistband', 2, 64, band, M['black'], wrap=True, inside=K.B(0, .26, 0)))
    # a long red ribbon from the left hip ending in a bell, and bells on the hem at the front
    a = -1.25
    top = Vector((.335 * math.sin(a), .25, .335 * math.cos(a)))
    path = [top + Vector((-.02 * t, -.55 * t, .03 * math.sin(t * 6))) for t in np.linspace(0, 1, 14)]
    side = Vector((math.cos(a), 0, -math.sin(a)))
    parts += ribbon('Ribbon', path, lambda p, i: side, .03, M['red'], uv_per_m=2)
    parts += bell(M, path[-1] - Vector((0, .03, 0)))
    return parts, {}, None


def build_leg(M, sd):
    """A wide trouser leg falling to the floor from the hip, flaring out (sd 1 right leg, red; -1 left leg, black)."""
    parts = []
    mat = M['red'] if sd > 0 else M['black']
    def leg(t, s, off=0.):
        a = TAU * s; y = lerp(.08, -.57, t)
        r = lerp(.125, .158, t ** 1.2) + .012 * t * math.sin(7 * a + sd) + off
        cx = sd * .028 * t                                   # the flare leans outward so the legs don't cross
        return Vector((cx + r * math.sin(a), y + .01 * math.sin(4 * a) * t, r * math.cos(a)))
    parts.append(surface('Trouser leg', 14, 40, leg, mat, wrap=True, uv=(2.5, 3), inside=K.B(0, -.25, 0)))
    parts.append(surface('Trouser lining', 14, 40, lambda t, s: leg(t, s, -.007), M['black'], wrap=True, inside=K.B(0, -.25, 0), flip=True))
    for t, s in ((.3, .1), (.55, .85), (.8, .3)):
        p = leg(t, s, .003); a = TAU * s
        parts += P.cross_stitch('Stitch', p, Vector((math.sin(a), 0, math.cos(a))), .018, M['white'])
    parts.append(tube('Hem', [leg(1, k / 40, .002) for k in range(40)], .005, M['black'] if sd > 0 else M['red'], closed=True, segs=5))
    parts += bell(M, leg(1, .25 if sd > 0 else .75) + Vector((0, -.02, 0)), .9)
    return parts, {}, None


# ---------------------------------------------------------------- sleeves
def build_sleeve(M, sx):
    parts = []
    mat = M['red'] if sx > 0 else M['black']
    radius = lambda t, a: .084 + .06 * max(0., 1 - t / .32) ** .7 * math.sin(math.pi * min(1, t / .32 + .2)) + .004 * math.sin(10 * a) * (t < .32)
    o, pt = P.sleeve('Sleeve', mat, M['black'], sx, .03, .42, radius)
    parts += o
    cuff = [Vector((sx * .42, .088 * math.sin(TAU * k / 40), .088 * math.cos(TAU * k / 40))) for k in range(40)]
    parts += ribbon('Cuff ruffle', cuff, lambda p, i: (Vector((sx, 0, 0)) * .6 + Vector((0, p.y, p.z)).normalized() * .8 + Vector((0, 0, 0))).normalized(),
                    .055, M['white'], closed=True, inside=K.B(sx * .42, 0, 0))
    parts += bell(M, Vector((sx * .44, -.1, .02)), 1.)
    parts += P.cross_stitch('Stitch', pt(.6, .25, .003), Vector((0, 1, 0)), .014, M['white'])
    return parts, {}, None


STYLES = {
    'jester_hat': ('Hat', build_hat),
    'jester_mask': ('Hat', build_mask),           # previewed on the head frame; registered in the Face Top slot
    'jester_doublet': ('Body Top', build_doublet),
    'jester_trousers': ('Body Bottom', build_trousers),
    'jester_leg_r': ('Leg Right', lambda M: build_leg(M, 1)),
    'jester_leg_l': ('Leg Left', lambda M: build_leg(M, -1)),
    'jester_sleeve_r': ('Arm Right', lambda M: build_sleeve(M, 1)),
    'jester_sleeve_l': ('Arm Left', lambda M: build_sleeve(M, -1)),
}
VIEWS = {'jester_leg_r': (.6, 1., .15), 'jester_leg_l': (-.6, 1., .15), 'jester_hat': (.5, 1., .3), 'jester_mask': (.35, 1., .1), 'jester_doublet': (.5, 1., .35), 'jester_trousers': (.5, 1., .2),
         'jester_sleeve_r': (.3, 1., .5), 'jester_sleeve_l': (-.3, 1., .5)}
K.run(STYLES, VIEWS, ARGS.root / 'art' / 'cosmetics' / 'jester', 'jester', ARGS.preview, setup=make_mats,
      preview_views=(('front', (.9, 2.7, .7)), ('back', (-1.3, -2.6, .8)), ('side', (2.8, -.3, .5))))
print('JESTER_COMPLETE', flush=True)
