"""Shared costume pieces on top of tools/cosmetic_kit.py: the robot's body shapes per slot, robes and skirts with
open fronts and tattered hems, hoods, bell sleeves, chains with drops, stitches and textures. Unity coordinates in
each slot's own frame (see cosmetic_kit). Used by tools/build_seer.py, build_jester.py, build_bride.py, build_angel.py.
"""
import math
import random

import numpy as np
from mathutils import Vector

import cosmetic_kit as K
from cosmetic_kit import TAU, lerp, surface, tube, ellipsoid, gem

# ---------------------------------------------------------------- body shapes
PROFILE = [(.298, -.02), (.306, .02), (.31, .2), (.313, .28), (.306, .34), (.29, .368), (.266, .384)]
HEAD_C = Vector((0, 0, .254)); HEAD_R = Vector((.253, .374, .254))   # head-top ellipsoid (Hat, Ears, Face Top frames)


def body_r(h):
    for (r0, h0), (r1, h1) in zip(PROFILE, PROFILE[1:]):
        if h0 <= h <= h1: return r0 + (r1 - r0) * (h - h0) / (h1 - h0)
    return PROFILE[-1][0] if h > PROFILE[-1][1] else PROFILE[0][0]


def on_body(h, a, lift=0.):
    r = body_r(h) + lift
    return Vector((r * math.sin(a), h, r * math.cos(a)))


def leg_r(y):
    return .106 if y > 0 else .106 * max(0., (y + .549) / .549)


def front(a):
    """1 at the front (a = 0), 0 at the back."""
    return (1 + math.cos(a)) / 2


def wrap_angle(a):
    return (a + math.pi) % TAU - math.pi


def tatter(a, teeth=11, depth=.06, seed=0.):
    """Ragged hem offset (<= 0): soft, uneven tongues of torn cloth (rounded, not a sawtooth), some longer than others."""
    x = a * teeth / TAU + seed
    tongue = (.5 + .5 * math.cos(TAU * x)) ** 1.8                 # 1 at a tongue's tip, rounded between
    length = .55 + .45 * math.sin(x * 2.7 + seed * 5) * math.sin(x * 1.3 + 1)
    return -depth * (1 - tongue * length)


def wrinkle(a, t, seed=0.):
    """Irregular soft cloth wrinkles (three unrelated frequencies), stronger further from the seam."""
    return (math.sin(13 * a + 9 * t + seed) * math.sin(5 * a - 7 * t + 1.3 * seed) * .6
            + math.sin(23 * a - 4 * t + 2 * seed) * .25 + math.sin(3 * a + 17 * t * t + seed) * .3) * min(1., t * 2.5)


# ---------------------------------------------------------------- clothes
def body_shell(name, mat, h0, top, lift, inside_y=.2, cols=96, rows=12, uv=(5, 1.4), colour_of=None, mats=None):
    """Bodice on the body top from h0 up to top(a); colour_of(a) -> key picks a material per column band."""
    obs = []
    if colour_of is None:
        obs.append(surface(name, rows, cols, lambda t, s: on_body(lerp(h0, top(TAU * s), t), TAU * s, lift), mat, wrap=True, uv=uv, inside=K.B(0, inside_y, 0)))
        return obs
    # split into angular bands of one material each
    keys = [colour_of(TAU * (c + .5) / cols) for c in range(cols)]
    start = 0
    while start < cols:
        end = start
        while end + 1 < cols and keys[end + 1] == keys[start]: end += 1
        s0, s1 = start / cols, (end + 1) / cols
        n = end - start + 1
        obs.append(surface(name, rows, n, lambda t, s, s0=s0, s1=s1: on_body(lerp(h0, top(TAU * lerp(s0, s1, s)), t), TAU * lerp(s0, s1, s), lift),
                           mats[keys[start]], uv=(uv[0] * (s1 - s0), uv[1]), inside=K.B(0, inside_y, 0)))
        start = end + 1
    return obs


def robe(name, mat, lining, a0, a1, waist_y, waist_r, hem, radius, folds=(.03, 8, 0.), rows=14, cols=64, uv=(5, 2), inside=None, lining_gap=.007):
    """A skirt or robe hanging from the waist round angles a0..a1 (a1 - a0 = TAU closes it): hem(a) is the hem height,
    radius(t, a) the distance from the axis at depth t (0 waist, 1 hem). Outside 'mat', inside 'lining'."""
    closed = abs((a1 - a0) - TAU) < 1e-4
    amp, k, ph = folds
    def pt(t, s, off=0.):
        a = lerp(a0, a1, s)
        r = radius(t, a) + amp * t ** 1.5 * math.sin(k * a + ph) + .009 * wrinkle(a, t, ph) + off
        return Vector((r * math.sin(a), lerp(waist_y, hem(a), t), r * math.cos(a)))
    inside = inside or K.B(0, waist_y - .3, 0)
    outer = surface(name, rows + 4, cols + 8, pt, mat, wrap=closed, uv=uv, inside=inside)
    obs = [outer]
    if lining is not None:
        obs.append(surface(name + ' lining', rows + 4, cols + 8, lambda t, s: pt(t, s, -lining_gap), lining, wrap=closed, uv=uv, inside=inside, flip=True))
    return obs, pt


def hood(name, mat, lining, centre, radii, edge, peak=.03, brim=.05, cols=64, rows=16, uv=(6, 3)):
    """Hood over the head-top frame: covers latitudes from edge(a) (degrees) to the crown; a = 0 is the face."""
    def point(a, lat, off=0.):
        la = math.radians(lat)
        d = Vector((math.cos(la) * math.sin(a), math.sin(la), math.cos(la) * math.cos(a)))
        p = centre + Vector((radii.x * d.x, radii.y * d.y, radii.z * d.z))
        p += Vector((0, peak, peak * .4)) * math.exp(-((lat - 87) / 10) ** 2)
        p += Vector((0, 0, brim)) * front(a) ** 3 * math.exp(-((lat - edge(a) - 6) / 16) ** 2)
        return p + d * off
    def pt(t, s, off=0.):
        a = TAU * s - math.pi
        e = edge(a)
        return point(a, lerp(e, 89.5, t ** .9), off)
    inside = K.B(*centre)
    outer = surface(name, rows, cols, pt, mat, wrap=True, uv=uv, inside=inside)
    obs = [outer, surface(name + ' lining', rows, cols, lambda t, s: pt(t, s, -.014), lining, wrap=True, uv=uv, inside=inside, flip=True, hem=0)]
    obs += K.hem_roll(outer, .009)
    return obs, point, pt


def sleeve(name, mat, lining, sx, x0, x1, radius, uv=(2, 2), rows=12, cols=32, end_tatter=None):
    """A sleeve along the arm (local +x, or -x for the left arm): radius(t, a) at depth t; end_tatter(a) shortens the
    sleeve end per angle (ragged cuffs)."""
    def pt(t, s, off=0.):
        a = TAU * s
        x_end = x1 + (end_tatter(a) if end_tatter else 0.)
        x = lerp(x0, x_end, t)
        r = radius(t, a) + .004 * wrinkle(a, t, x0 * 10) + off
        return Vector((sx * x, r * math.sin(a), r * math.cos(a)))
    inside = K.B(sx * (x0 + x1) / 2, 0, 0)
    outer = surface(name, rows, cols, pt, mat, wrap=True, uv=uv, inside=inside)
    obs = [outer]
    if lining is not None:
        obs.append(surface(name + ' lining', rows, cols, lambda t, s: pt(t, s, -.007), lining, wrap=True, uv=uv, inside=inside, flip=True))
    return obs, pt


def chain(name, a, b, sag, mat, links=10, radius=.0022):
    """A chain from a to b drooping by 'sag' in the middle."""
    a, b = Vector(a), Vector(b)
    pts = [a.lerp(b, t) - Vector((0, sag * 4 * t * (1 - t), 0)) for t in np.linspace(0, 1, links)]
    return tube(name, pts, radius, mat, segs=5)


def drop(name, top, length, chain_mat, gem_mat, size=1., moon=False):
    top = Vector(top); bottom = top - Vector((0, length, 0))
    obs = [tube(name + ' chain', [top.lerp(bottom, t) for t in np.linspace(0, 1, 5)], .0022 * size, chain_mat, segs=5)]
    obs.append(gem(name, tuple(bottom - Vector((0, .02 * size, 0))), (.01 * size, .026 * size, .01 * size), gem_mat))
    return obs


def cross_stitch(name, centre, normal, size, mat):
    """An X of two short thread tubes on a surface facing 'normal'."""
    c = Vector(centre); n = Vector(normal).normalized()
    side = n.cross(Vector((0, 1, 0)) if abs(n.y) < .9 else Vector((0, 0, 1))).normalized(); up = side.cross(n).normalized()
    return [tube(name, [c + (side * sx + up) * size, c + n * .002, c - (side * sx + up) * size], .0028, mat, segs=4) for sx in (1, -1)]


def ruff(name, mat, centre_y, r_in, r_out, pleats=24, layers=2, rise=.03, droop=.0, inside=None):
    """A pleated ruff collar round the vertical axis: rings from r_in to r_out, folded into pleats."""
    obs = []
    for layer in range(layers):
        y0 = centre_y + layer * .022
        ro = r_out - layer * .03
        def pt(t, s, off=0., y0=y0, ro=ro):
            a = TAU * s
            r = lerp(r_in, ro, t)
            fold = math.sin(pleats * a) * .022 * t
            return Vector((r * math.sin(a), y0 + rise * t - droop * t * t + fold + off, r * math.cos(a)))
        obs.append(surface(name, 3, pleats * 4, pt, mat, wrap=True, inside=inside or K.B(0, centre_y - .2, 0)))
        obs.append(surface(name + ' under', 3, pleats * 4, lambda t, s, pt=pt: pt(t, s, -.004), mat, wrap=True, inside=inside or K.B(0, centre_y - .2, 0), flip=True))
    return obs


# ---------------------------------------------------------------- textures
def lace_texture(name, rng, base=.2, thread=.85):
    m = 256
    y, x = (np.mgrid[0:m, 0:m] + .5) / m
    def line(d, w): return np.exp(-(d / w) ** 2)
    u, v = (x * 4) % 1 - .5, (y * 4) % 1 - .5
    net = line(np.abs(np.abs(u) + np.abs(v) - .5), .05)
    r = np.hypot((x * 2) % 1 - .5, (y * 2) % 1 - .5)
    ang = np.arctan2((y * 2) % 1 - .5, (x * 2) % 1 - .5)
    petal = line(r - (.14 + .06 * np.cos(6 * ang)), .025) + line(r - .04, .025)
    vine = line(np.abs(r - .3) * (1 + .5 * np.cos(3 * ang)), .02)
    lace = np.clip(np.maximum.reduce([net * .7, petal, vine * .7]), 0, 1)
    K.save_png(name, np.clip(base + (thread - base) * lace + .05 * (K.periodic_noise(m, 3, rng) - .5), 0, 1))


def cloth_texture(name, rng, base=.8, weave=.06, mottle=.12):
    n = 256
    y, x = np.mgrid[0:n, 0:n] / n
    twill = .5 + .5 * np.sin(TAU * (x * 48 + y * 48))
    K.save_png(name, np.clip(base + weave * twill + mottle * (K.periodic_noise(n, 4, rng) - .5), 0, 1))


def stone_texture(name, rng, base=.72):
    n = 256
    mott = K.periodic_noise(n, 6, rng)
    fine = K.periodic_noise(n, 7, rng)
    y, x = np.mgrid[0:n, 0:n] / n
    cracks = np.zeros((n, n))
    for _ in range(6):
        # wandering hairline cracks, tileable through wrap-around
        px, py = rng.random(), rng.random(); ang = rng.uniform(0, TAU)
        for step in range(120):
            ang += rng.uniform(-.35, .35)
            px = (px + math.cos(ang) / n * 1.6) % 1; py = (py + math.sin(ang) / n * 1.6) % 1
            cracks[int(py * n) % n, int(px * n) % n] = 1
    from_cracks = np.maximum(cracks, np.roll(cracks, 1, 0) * .5)
    K.save_png(name, np.clip(base + .16 * (mott - .5) + .08 * (fine - .5) - .2 * from_cracks, 0, 1))


def moss_texture(name, rng):
    n = 128
    a = K.periodic_noise(n, 6, rng); b = K.periodic_noise(n, 5, rng)
    img = np.zeros((n, n, 3))
    img[..., 0] = .16 + .1 * a; img[..., 1] = .22 + .12 * a + .06 * b; img[..., 2] = .09 + .05 * b
    K.save_png(name, np.clip(img, 0, 1))
