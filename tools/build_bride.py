"""Blender 5.2: --background --factory-startup --python tools/build_bride.py -- --root PATH [--base JSON] [--preview]

The Ghost Bride set: an original haunted-bride outfit in pale ivory lace with a faint blue chill.

  bride_veil     Hat          crown of blue roses and small white blossoms, a light gathered tulle veil to mid-back
  bride_corset   Body Top     strapless floral-lace bodice with a pointed front, boning, blue roses at the neckline,
                              and a satin sash tied in a bow at the back
  bride_skirt    Body Bottom  ball-gown skirt in three ruffled lace tiers, a bustle behind and a satin underskirt
  bride_glove_r  Arm Right    long lace opera glove, softly torn at the top
  bride_glove_l  Arm Left

Tintable: the lace and tulle. Fixed: roses, leaves, satin. Built on tools/cosmetic_kit.py + costume_parts.py.
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
from cosmetic_kit import TAU, lerp, surface, tube, ellipsoid, gem, ribbon  # noqa: E402

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, required=True)
parser.add_argument('--base', type=Path)
parser.add_argument('--preview', action='store_true')
ARGS = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
ARGS.root = ARGS.root.resolve()
K.configure(ARGS.root, ARGS.base)

LACE = (.93, .93, .97)
TULLE = (.9, .92, .98)
SATIN = (.72, .8, .92)
ROSE = (.3, .44, .9)
LEAF = (.16, .3, .22)


def floral_lace(name, rng):
    """Fine floral lace: a soft hexagonal tulle net with scattered five-petal blossoms and curling vines."""
    n = 256
    y, x = (np.mgrid[0:n, 0:n] + .5) / n
    def line(d, w): return np.exp(-(d / w) ** 2)
    # fine hexagonal net
    gx, gy = x * 18, y * 18 * .866
    net = np.zeros((n, n))
    for ang in (0, TAU / 6, TAU / 3):
        c, s_ = math.cos(ang), math.sin(ang)
        d = (gx * c + gy * s_) % 1 - .5
        net = np.maximum(net, line(d, .07))
    motif = np.zeros((n, n))
    for _ in range(26):
        cx, cy, r = rng.random(), rng.random(), rng.uniform(.03, .055)
        rot = rng.uniform(0, TAU)
        dx = (x - cx + .5) % 1 - .5; dy = (y - cy + .5) % 1 - .5
        rr = np.hypot(dx, dy); aa = np.arctan2(dy, dx) + rot
        petal = r * (.55 + .45 * np.abs(np.cos(2.5 * aa)))
        motif = np.maximum(motif, line(rr - petal, .006) + .8 * line(rr, r * .18))
        # a curling vine from each blossom
        for k in range(18):
            t = k / 17; va = rot + t * 2.4; vr = r * (1.4 + 2.2 * t)
            px, py = cx + math.cos(va) * vr, cy + math.sin(va) * vr
            ddx = (x - px + .5) % 1 - .5; ddy = (y - py + .5) % 1 - .5
            motif = np.maximum(motif, .85 * line(np.hypot(ddx, ddy), .0045))
    lace = np.clip(np.maximum(net * .45, motif), 0, 1)
    K.save_png(name, np.clip(.7 + .3 * lace, 0, 1), worn=False)   # a bride's lace stays clean


def make_mats():
    rng = random.Random(21)
    floral_lace('bride_lace.png', rng)
    K.save_png('bride_tulle.png', np.clip(.9 + .06 * K.periodic_noise(128, 3, rng), 0, 1), worn=False)
    m = K.material
    return {
        'lace': m('Bride lace', LACE, .6, image='bride_lace.png', tint=True),
        'tulle': m('Bride tulle', TULLE, .5, image='bride_tulle.png', tint=True),
        'satin': m('Bride satin', SATIN, .3, clean=True),
        'rose': m('Bride rose', ROSE, .5),
        'rose_dark': m('Bride rose shadow', (.17, .25, .62), .55),
        'leaf': m('Bride leaf', LEAF, .6),
        'blossom': m('Bride blossom', (.96, .95, .92), .5, clean=True),
    }


def rose(M, centre, normal, size):
    """A rose: a dark bud inside two rings of cupped petals, with two leaves."""
    c = Vector(centre); n = Vector(normal).normalized()
    obs = [ellipsoid('Rose bud', tuple(c + n * size * .25), (size * .35, size * .35, size * .35), M['rose_dark'], (10, 6))]
    side = n.cross(Vector((0, 1, 0)) if abs(n.y) < .9 else Vector((1, 0, 0))).normalized(); up = side.cross(n)
    for ring_i, (count, dist, s) in enumerate(((5, .32, .38), (6, .55, .42))):
        for k in range(count):
            ang = TAU * (k + ring_i * .5) / count
            d = side * math.cos(ang) + up * math.sin(ang)
            obs.append(ellipsoid('Petal', tuple(c + d * size * dist + n * size * (.1 - .08 * ring_i)), (size * s, size * s, size * s * .8), M['rose'], (8, 5)))
    for sd in (1, -1):
        obs.append(gem('Leaf', tuple(c + side * sd * size * 1.05 - n * size * .1), (size * .5, size * .22, size * .12), M['leaf'], facing=tuple(n)))
    return obs


def blossom(M, centre, normal, size):
    c = Vector(centre); n = Vector(normal).normalized()
    side = n.cross(Vector((0, 1, 0)) if abs(n.y) < .9 else Vector((1, 0, 0))).normalized(); up = side.cross(n)
    obs = []
    for k in range(5):
        a = TAU * k / 5
        obs.append(ellipsoid('Blossom', tuple(c + (side * math.cos(a) + up * math.sin(a)) * size * .5), (size * .4, size * .4, size * .4), M['blossom'], (6, 4)))
    return obs


# ---------------------------------------------------------------- crown and veil (head frame)
CR = P.HEAD_R + Vector((.03, .03, .03))


def crown_point(a, lat=46., off=0.):
    la = math.radians(lat)
    d = Vector((math.cos(la) * math.sin(a), math.sin(la), math.cos(la) * math.cos(a)))
    return P.HEAD_C + Vector((CR.x * d.x, CR.y * d.y, CR.z * d.z)) + d * off


def build_veil(M):
    parts = []
    for k in range(12):
        a = TAU * k / 12
        p = crown_point(a, 46 + 3 * math.sin(3 * a), .02)
        parts += rose(M, p, (p - P.HEAD_C), .048 if k % 2 else .04)
        q = crown_point(a + TAU / 24, 47, .018)
        parts += blossom(M, q, (q - P.HEAD_C), .02)
    parts.append(tube('Crown vine', [crown_point(TAU * k / 48, 46, .008) for k in range(48)], .007, M['leaf'], closed=True, segs=6))
    # a light veil gathered at the back of the crown, falling to mid-back in soft folds
    a0, a1 = math.radians(118), math.radians(242)          # gathered behind the head, not beside it
    def veil(t, s, off=0.):
        a = lerp(a0, a1, s)
        top = crown_point(a, 46, .01 + off)
        gather = .5 + .5 * math.cos(TAU * s * 7)                       # gathered pleats at the crown
        y = lerp(top.y, -.3 + .03 * math.sin(3 * a) + P.tatter(a, 7, .02, .4), t)
        # falls back and out over the shoulders, clear of the bodice and its bow
        rr = lerp(math.hypot(top.x, top.z - P.HEAD_C.z), .43 + .03 * math.sin(5 * a), t ** .7) + (.012 * gather) * (1 - t) + .02 * t * math.sin(9 * a) + off
        return Vector((rr * math.sin(a), y, P.HEAD_C.z + rr * math.cos(a)))
    parts.append(surface('Veil', 14, 40, veil, M['tulle'], uv=(5, 3), inside=K.B(0, -.1, P.HEAD_C.z)))
    parts.append(surface('Veil back', 14, 40, lambda t, s: veil(t, s, -.006), M['tulle'], uv=(5, 3), inside=K.B(0, -.1, P.HEAD_C.z), flip=True))
    return parts, {}, K.B(*(P.HEAD_C + Vector((0, CR.y + .03, 0))))


# ---------------------------------------------------------------- bodice (body top)
def neckline(a):
    a = P.wrap_angle(a)
    f = P.front(a) ** 3
    return .36 - .07 * f - .03 * math.exp(-(a / .12) ** 2) + .022 * math.exp(-((abs(a) - .36) / .2) ** 2)


def bottom(a):
    return .02 - .1 * math.exp(-(P.wrap_angle(a) / .38) ** 2)       # the pointed basque at the front


def build_corset(M):
    parts = []
    def bodice(t, s, lift=.012):
        a = TAU * s; h = lerp(bottom(a), neckline(a), t)
        p = P.on_body(max(h, -.02), a, lift)
        return p + Vector((0, min(0, h + .02), 0)) + Vector((math.sin(a), 0, math.cos(a))) * (.012 if h < -.02 else 0)
    parts.append(surface('Bodice', 16, 96, bodice, M['lace'], wrap=True, uv=(9, 2.4), inside=K.B(0, .2, 0)))
    for a in (0, .35, -.35, .8, -.8, 1.3, -1.3, 2.0, -2.0, 2.6, -2.6, math.pi):
        bone = [bodice(t, (a % TAU) / TAU, .016) for t in np.linspace(.08, .92, 10)]
        parts.append(tube('Boning', bone, .003, M['satin'], segs=5))
    top = [bodice(1, k / 96, .014) for k in range(96)]
    parts.append(tube('Neckline', top, .006, M['satin'], closed=True, segs=6))
    parts += ribbon('Lace frill', top, lambda p, i: (Vector((p.x, 0, p.z)).normalized() * .5 + Vector((0, 1, 0))).normalized(), .028,
                    M['lace'], closed=True, uv_per_m=10, inside=K.B(0, .2, 0))
    for a, size in ((0, .042), (-.3, .032), (.3, .032)):
        p = bodice(1, (a % TAU) / TAU, .02) + Vector((0, -.012, 0))
        parts += rose(M, p, Vector((math.sin(a), .3, math.cos(a))), size)
    # satin sash round the waist, tied in a bow at the back with two trailing tails
    def sash(t, s):
        a = TAU * s; h = lerp(.005, .055, t)
        return P.on_body(h, a, .02 + .004 * math.sin(math.pi * t))
    parts.append(surface('Sash', 3, 72, sash, M['satin'], wrap=True, inside=K.B(0, .03, 0)))
    knot = P.on_body(.03, math.pi, .03)
    parts.append(ellipsoid('Bow knot', tuple(knot), (.03, .025, .02), M['satin'], (10, 6)))
    for sd in (1, -1):
        loop = [knot + Vector((sd * .09 * math.sin(math.pi * t) + sd * .02, .05 * math.sin(TAU * t), -.025 * math.sin(math.pi * t))) for t in np.linspace(0, 1, 14)]
        parts.append(tube('Bow loop', loop, lambda u: .012 + .01 * math.sin(math.pi * u), M['satin'], segs=8, flat=.4))
        tail = [knot + Vector((sd * (.02 + .05 * t), -.03 - .3 * t, -.02 - .03 * math.sin(math.pi * t))) for t in np.linspace(0, 1, 10)]
        parts += ribbon('Bow tail', tail, lambda p, i: Vector((1, 0, 0)), .035, M['satin'], uv_per_m=3)
    return parts, {}, None


# ---------------------------------------------------------------- ball-gown skirt (body bottom)
def build_skirt(M):
    parts = []
    inside = K.B(0, -.2, 0)
    # satin underskirt to the floor
    under_r = lambda t, a: .32 + (.6 + .08 * (1 - P.front(a)) - .32) * t ** 1.1
    o, _ = P.robe('Underskirt', M['satin'], M['satin'], 0, TAU, .28, .32, lambda a: -.6 + .015 * math.sin(4 * a), under_r,
                  folds=(.02, 10, .3), cols=64, inside=inside)
    parts += o
    # three ruffled lace tiers, each lower and wider, the back fuller (a bustle)
    tiers = ((.29, -.12, .33, .48), (-.06, -.36, .45, .57), (-.3, -.6, .54, .66))
    for k, (y0, y1, r0, r1) in enumerate(tiers):
        def radius(t, a, r0=r0, r1=r1, k=k):
            bustle = (.07 if k < 2 else .04) * (1 - P.front(a)) ** 2
            return r0 + bustle * .6 + (r1 + bustle - r0 - bustle * .6) * t ** 1.3
        hem = lambda a, y1=y1, k=k: y1 + .012 * math.sin((20 + 4 * k) * a + k)          # a fluted, gently waved hem
        o, pt = P.robe('Tier', M['lace'], M['lace'], 0, TAU, y0, r0, hem, radius, folds=(.035, 20 + 4 * k, k), rows=6, cols=96, uv=(14, 2), inside=inside)
        parts += o
    return parts, {}, None


# ---------------------------------------------------------------- gloves
def build_glove(M, sx):
    parts = []
    def glove(t, s, off=0.):
        a = TAU * s
        x0 = .1 + .04 * (P.tatter(a, 5, 1.) + .5)
        x = lerp(x0, .5, t)
        r = (.082 if x < .44 else .082 * (1 - (x - .44) / .1 * .75)) + .003 * P.wrinkle(a, t) + off
        return Vector((sx * x, r * math.sin(a), r * math.cos(a)))
    parts.append(surface('Glove', 14, 28, glove, M['lace'], wrap=True, uv=(5, 7), inside=K.B(sx * .3, 0, 0)))
    parts.append(surface('Glove lining', 14, 28, lambda t, s: glove(t, s, -.006), M['satin'], wrap=True, inside=K.B(sx * .3, 0, 0), flip=True))
    return parts, {}, None


STYLES = {
    'bride_veil': ('Hat', build_veil),
    'bride_corset': ('Body Top', build_corset),
    'bride_skirt': ('Body Bottom', build_skirt),
    'bride_glove_r': ('Arm Right', lambda M: build_glove(M, 1)),
    'bride_glove_l': ('Arm Left', lambda M: build_glove(M, -1)),
}
VIEWS = {'bride_veil': (-.6, -1., .35), 'bride_corset': (.5, 1., .3), 'bride_skirt': (.5, 1., .2), 'bride_glove_r': (.3, 1., .5), 'bride_glove_l': (-.3, 1., .5)}
K.run(STYLES, VIEWS, ARGS.root / 'art' / 'cosmetics' / 'bride', 'bride', ARGS.preview, setup=make_mats,
      preview_views=(('front', (.9, 2.7, .7)), ('back', (-1.3, -2.6, .8)), ('side', (2.8, -.3, .5))))
print('BRIDE_COMPLETE', flush=True)
