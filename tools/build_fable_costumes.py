"""Blender 5.2: --background --factory-startup --python tools/build_fable_costumes.py -- --root PATH --set NAME [--base JSON] [--preview]

Five costume sets after the user's concept sheets, seven parts each (Hat, Body Top, Body Bottom, both arms, both legs):

  mermaid    shell crown on long red hair, shell top with pearls, scale skirt with a fin veil, fin cuffs, shell boots
  druid      antlered mossy hood with amber drops, robe under a mantle of leaves with an owl, leaf skirt with vines and a tabard,
             bark bracers, rooted boots
  vampire    swept pale hair, coat with a high collar, bat-wing cape, jabot and ruby, striped breeches with a coffin
             charm, ruffled cuffs, buckled boots
  diver      brass helmet with lit portholes, canvas suit with an air tank, breeches with a lantern and an anchor
             tabard, sealed gloves, weighted boots
  clockwork  inventor's top hat with goggles and gears, automaton's uniform with epaulettes and a wind-up key,
             striped breeches under coat tails, banded sleeves, piston boots

Each set's main cloth is tintable (the game's slot colour); a second and an accent fabric follow in the mod's extra
channels (tools/cosmetic_channels.py). Glowing parts keep their colour. Built on cosmetic_kit.py + costume_parts.py.
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
from cosmetic_kit import TAU, lerp, surface, tube, ellipsoid, bez, ribbon, gem  # noqa: E402

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, required=True)
parser.add_argument('--base', type=Path)
parser.add_argument('--set', required=True)
parser.add_argument('--preview', action='store_true')
ARGS = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
ARGS.root = ARGS.root.resolve()
K.configure(ARGS.root, ARGS.base)

HC, HR = P.HEAD_C, P.HEAD_R
UP = Vector((0, 1, 0))
m = K.material


# ---------------------------------------------------------------- shared shapes
def dirv(a):
    return Vector((math.sin(a), 0, math.cos(a)))


def head_pt(a, lat, off=0., grow=(0, 0, 0), centre=None):
    la = math.radians(lat)
    d = Vector((math.cos(la) * math.sin(a), math.sin(la), math.cos(la) * math.cos(a)))
    return (centre or HC) + Vector(((HR.x + grow[0]) * d.x, (HR.y + grow[1]) * d.y, (HR.z + grow[2]) * d.z)) + d * off


def head_cap(name, mat, edge, off=.03, rows=10, cols=40):
    return [surface(name, rows, cols, lambda t, s: head_pt(TAU * s, lerp(edge(TAU * s), 89.5, t ** .9), off), mat, wrap=True, uv=(3, 1.5), inside=K.B(*HC))]


def disc(name, c, n, r, h, mat, segs=20):
    c = Vector(c); n = Vector(n).normalized()
    return tube(name, [c - n * h, c + n * h], r, mat, segs=segs)


def plane_axes(n):
    n = Vector(n).normalized()
    a = n.cross(UP if abs(n.y) < .9 else Vector((1, 0, 0))).normalized()
    return n, a, n.cross(a)


def cog(name, c, n, r, mat, teeth=10, hub=None):
    c = Vector(c); n, a, b = plane_axes(n)
    obs = [K.ring(name, c, n, r * .72, r * .16, mat, segs=20), disc(name + ' hub', c, n, r * .26, r * .14, hub or mat, 10)]
    for k in range(teeth):
        d = a * math.cos(TAU * k / teeth) + b * math.sin(TAU * k / teeth)
        obs.append(tube(name + ' tooth', [c + d * r * .8, c + d * r * 1.1], r * .13, mat, segs=4))
    for k in range(4):
        d = a * math.cos(TAU * k / 4 + .4) + b * math.sin(TAU * k / 4 + .4)
        obs.append(tube(name + ' spoke', [c + d * r * .2, c + d * r * .66], r * .08, mat, segs=4))
    return obs


def dial(name, c, n, r, face, rim, hand):
    """A clock or gauge face: pale disc, metal rim, two hands."""
    c = Vector(c); n, a, b = plane_axes(n)
    obs = [disc(name, c, n, r, r * .12, face), K.ring(name + ' rim', c + n * r * .1, n, r, r * .14, rim, segs=20)]
    for ang, length in ((.6, .7), (2.4, .5)):
        d = a * math.cos(ang) + b * math.sin(ang)
        obs.append(tube(name + ' hand', [c + n * r * .16, c + n * r * .16 + d * r * length], r * .06, hand, segs=4))
    return obs


def fan(name, base, up, facing, size, mat, ribs=7, spread=1.15):
    """A scallop shell: ribs fanning out from a hinge."""
    base = Vector(base); up = Vector(up).normalized(); f = Vector(facing).normalized()
    side = up.cross(f).normalized(); up = f.cross(side).normalized()
    obs = []
    for th in np.linspace(-spread, spread, ribs):
        d = up * math.cos(th) + side * math.sin(th)
        tip = base + d * size * (1 - .14 * abs(th) / spread)
        mid = base + d * size * .55 + f * size * .2
        path = [bez([base, mid, tip], t) for t in np.linspace(0, 1, 7)]
        obs.append(tube(name, path, lambda s: size * (.07 + .14 * math.sin(math.pi * min(1, s * .85 + .1))), mat, segs=8, flat=.45, up=tuple(f)))
    obs.append(ellipsoid(name + ' hinge', tuple(base), (size * .15,) * 3, mat, (8, 5)))
    return obs


def star(name, c, n, r, mat, arms=5):
    c = Vector(c); n, a, b = plane_axes(n)
    obs = [ellipsoid(name, tuple(c), (r * .3,) * 3, mat, (8, 5))]
    for k in range(arms):
        d = a * math.cos(TAU * k / arms + math.pi / 2) + b * math.sin(TAU * k / arms + math.pi / 2)
        obs.append(tube(name + ' arm', [c, c + d * r], lambda s: r * (.28 * (1 - s) + .07), mat, segs=6))
    return obs


def lock(name, start, drop, sway, r, mat, out, n=22, phase=0.):
    """A wavy lock of hair hanging from 'start'."""
    start = Vector(start); out = Vector(out)
    path = [start + Vector((0, -drop * t, 0)) + out * (sway * math.sin(t * 7 + phase) * t + .05 * t) + out.cross(UP) * (sway * .8 * math.cos(t * 6 + phase) * t) for t in np.linspace(0, 1, n)]
    return tube(name, path, lambda s: r * (1 - .5 * s ** 2) * (1 + .12 * math.sin(s * 11 + phase)) + .004, mat, segs=8)


def pearls(name, pts, r, mat):
    return [ellipsoid(name, tuple(p), (r, r, r), mat, (8, 5)) for p in pts]


def body_ring(y, r, n=48, wave=0., k=6):
    return [Vector(((r + wave * math.sin(k * TAU * i / n)) * math.sin(TAU * i / n), y + wave * math.cos(k * TAU * i / n), (r + wave * math.sin(k * TAU * i / n)) * math.cos(TAU * i / n))) for i in range(n)]


def belt(name, y, r, thick, mat, flat=1.):
    return tube(name, body_ring(y, r), thick, mat, closed=True, segs=6, flat=flat)


def buckle(name, c, n, w, h, mat, thick=.008):
    c = Vector(c); n, a, b = plane_axes(n)
    pts = [c + a * w + b * h, c - a * w + b * h, c - a * w - b * h, c + a * w - b * h]
    return [tube(name, pts, thick, mat, closed=True, segs=5), tube(name + ' pin', [c - a * w, c + a * w * .4], thick * .7, mat, segs=4)]


def boot(name, sx, shaft, sole, top=-.28, cuff=None, toe=None):
    r0 = max(P.leg_r(top) + .024, .084)
    def sh(t, s):
        a = TAU * s; r = lerp(r0, .074, t)
        return Vector((r * math.sin(a), lerp(top, -.5, t), r * math.cos(a) * 1.05))
    obs = [surface(name + ' shaft', 6, 24, sh, shaft, wrap=True, uv=(2, 1), inside=K.B(0, (top - .5) / 2, 0)),
           tube(name + ' cuff', [sh(0, k / 24) for k in range(24)], .012, cuff or sole, closed=True, segs=6),
           ellipsoid(name + ' foot', (sx * .008, -.512, .065), (.086, .056, .17), shaft, (16, 10)),
           ellipsoid(name + ' sole', (sx * .008, -.553, .06), (.092, .02, .178), sole, (16, 6)),
           ellipsoid(name + ' heel', (sx * .008, -.535, -.055), (.05, .04, .05), sole, (10, 6))]
    if toe: obs.append(ellipsoid(name + ' toe cap', (sx * .008, -.51, .165), (.072, .052, .072), toe, (12, 7)))
    return obs, sh


def arm_ring(name, sx, x, r, thick, mat):
    return tube(name, [Vector((sx * x, r * math.sin(TAU * k / 28), r * math.cos(TAU * k / 28))) for k in range(28)], thick, mat, closed=True, segs=6)


def mitt(name, sx, mat, x0=.36, r=.086):
    o, _ = P.sleeve(name, mat, None, sx, x0, .5, lambda t, a: r)
    return o + [ellipsoid(name + ' hand', (sx * .52, 0, 0), (.074, .082, .086), mat, (14, 8))]


def cuff_ruffle(name, sx, x, r, height, mat):
    curve = [Vector((sx * x, r * math.sin(TAU * k / 40), r * math.cos(TAU * k / 40))) for k in range(40)]
    return ribbon(name, curve, lambda p, i: (Vector((sx, 0, 0)) * .6 + Vector((0, p.y, p.z)).normalized() * (.7 + .2 * math.sin(i * 1.57))).normalized(),
                  height, mat, closed=True, inside=K.B(sx * x, 0, 0))


def mantle(name, mat, y_top, y_bot, r0, r1, teeth, depth, seed, a0=0., a1=TAU, under=None):
    """A short cape round the shoulders with a tongued (leaf, bat-wing) hem."""
    closed = abs(a1 - a0 - TAU) < 1e-4
    def pt(t, s, off=0.):
        a = lerp(a0, a1, s)
        r = lerp(r0, r1, t ** .8) + .012 * math.sin(teeth * a + seed) * t + off
        return Vector((r * math.sin(a), lerp(y_top, y_bot + P.tatter(a, teeth, depth, seed), t) + .02 * math.sin(math.pi * t), r * math.cos(a)))
    inside = K.B(0, (y_top + y_bot) / 2, 0)
    return [surface(name, 8, 80, pt, mat, wrap=closed, uv=(5, 1), inside=inside),
            surface(name + ' under', 8, 80, lambda t, s: pt(t, s, -.008), under or mat, wrap=closed, uv=(5, 1), inside=inside, flip=True)], pt


def panel(name, mat, a0, a1, y0, y1, r, teeth=5, depth=.05):
    """A hanging cloth panel (tabard) in the body-bottom frame."""
    def pt(t, s, off=0.):
        a = lerp(a0, a1, s)
        return Vector(((r + off) * math.sin(a), lerp(y0, y1 + P.tatter(a * 6, teeth, depth), t), (r + off) * math.cos(a) + .03 * t))
    inside = K.B(0, (y0 + y1) / 2, 0)
    return [surface(name, 8, 14, pt, mat, uv=(1, 2), inside=inside), surface(name + ' back', 8, 14, lambda t, s: pt(t, s, -.006), mat, inside=inside, flip=True)], pt


def leaf_blade(name, mat, p0, tip, normal, width, back=None):
    """One leaf: a cupped pointed blade from p0 to tip facing 'normal', with a midrib."""
    p0 = Vector(p0); ax = Vector(tip) - p0
    side = ax.cross(Vector(normal)).normalized(); n = side.cross(ax).normalized()
    def pt(t, s_, off=0.):
        w = width * math.sin(math.pi * min(1, t * .8 + .1)) ** .8 * (1 - t ** 4)
        u = 2 * s_ - 1
        return p0 + ax * t + side * (u * w) + n * (.05 * math.sin(math.pi * t) * ax.length - .25 * w * u * u + off)
    inside = K.B(*(p0 + ax * .5 - n * .5))
    return [surface(name, 5, 4, pt, mat, uv=(.5, .5), inside=inside),
            surface(name + ' back', 5, 4, lambda t, s_: pt(t, s_, -.006), back or mat, inside=inside, flip=True, hem=0),
            tube(name + ' rib', [pt(t, .5, .003) for t in np.linspace(.05, .95, 5)], .004, back or mat, segs=4)]


def leaf_row(M, keys, y, r, count, length, width, droop, seed, back):
    """A ring of separate leaves hanging outwards from radius r at height y; droop is the angle below horizontal."""
    rng = random.Random(seed); obs = []
    for k in range(count):
        a = TAU * (k + rng.uniform(-.25, .25)) / count; d = droop + rng.uniform(-.12, .12); ln = length * rng.uniform(.82, 1.15)
        p0 = dirv(a) * r + Vector((0, y, 0))
        tip = p0 + dirv(a + rng.uniform(-.2, .2)) * ln * math.cos(d) - UP * ln * math.sin(d)
        obs += leaf_blade('Leaf', M[rng.choice(keys)], p0, tip, dirv(a) * math.sin(d) + UP * math.cos(d), width * rng.uniform(.85, 1.15), back)
    return obs


def rays_texture(name, rng):
    n = 128
    y, x = np.mgrid[0:n, 0:n] / n
    K.save_png(name, np.clip(.8 + .16 * np.sin(TAU * x * 9) + .08 * (K.periodic_noise(n, 3, rng) - .5) + .08 * y, 0, 1))


def scales_texture(name):
    n = 128
    y, x = np.mgrid[0:n, 0:n] / n
    u, v = x * 4, y * 4
    row = np.floor(v); uu = (u + .5 * (row % 2)) % 1 - .5; vv = v % 1
    d = np.hypot(uu, vv - 1.)
    K.save_png(name, np.clip(.6 + .4 * np.cos(np.clip(d * 2.1, 0, 1.57)) - .22 * (np.abs(d - .56) < .05), 0, 1))


def stripes_texture(name, lo=.62, hi=1.):
    n = 64
    y, x = np.mgrid[0:n, 0:n] / n
    K.save_png(name, np.where(np.sin(TAU * x * 4) > 0, hi, lo) * np.ones((n, n)))


# ================================================================ mermaid
def mermaid_mats():
    rng = random.Random(11)
    scales_texture('mermaid_scales.png'); rays_texture('mermaid_fin.png', rng)
    return {'hair': m('Mermaid hair', (.62, .07, .1), .45, tint=True),
            'shell': m('Mermaid shell accent', (.8, .66, .96), .25, metal=.2, tint=True),
            'scales': m('Mermaid scales', (.25, .78, .72), .3, metal=.25, image='mermaid_scales.png', tint=True),
            'fin': m('Mermaid fin trim', (.74, .62, .98), .35, image='mermaid_fin.png', tint=True),
            'cloth': m('Mermaid cloth', (.62, .5, .82), .6, tint=True),
            'pearl': m('Mermaid pearl', (.97, .93, .88), .15, metal=.3), 'gold': m('Mermaid gold', (.95, .68, .2), .3, metal=.85),
            'coral': m('Mermaid coral', (.95, .36, .3), .5), 'glow': m('Mermaid glow drop', (.35, .95, .85), .2, emit=1.6)}


def mermaid_crown(M):
    parts = []
    edge = lambda a: 34 - 62 * (1 - P.front(a)) ** 1.2   # above the eyes in front, down to the nape behind
    parts += head_cap('Hair', M['hair'], edge, .028)
    for k in range(13):                                    # long waves down the back and sides
        a = lerp(.95, TAU - .95, k / 12)
        parts.append(lock('Lock', head_pt(a, edge(a) + 14, .03), .62 + .12 * math.sin(k * 1.7), .1, .085, M['hair'], dirv(a), phase=k * 1.3))
    for sd in (1, -1):
        parts.append(lock('Face lock', head_pt(sd * .85, 30, .03), .5, .035, .06, M['hair'], dirv(sd * 1.15), phase=sd))
    parts += fan('Crown shell', head_pt(0, 54, .035), (0, 1, .25), (0, .3, 1), .2, M['shell'])
    parts += pearls('Pearl', [head_pt(k * .2, edge(k * .2) + 3, .04) for k in range(-5, 6)], .02, M['pearl'])
    for sd in (1, -1):
        root = head_pt(sd * .55, 50, .03)
        for dx, dy in ((.1, .12), (.16, .05), (.04, .17)):
            tip = root + Vector((sd * dx, dy, .02))
            parts.append(tube('Coral crown', [root, root.lerp(tip, .5) + Vector((0, .02, 0)), tip], lambda s: .016 * (1 - s) + .006, M['gold'], segs=6))
        d = head_pt(sd * 1.0, edge(sd * 1.0) + 2, .04)
        parts += [tube('Drop chain', [d, d - Vector((0, .1, 0))], .003, M['gold'], segs=4),
                  ellipsoid('Glow drop', tuple(d - Vector((0, .125, 0))), (.028, .03, .028), M['glow'], (10, 6))]
    return parts, {}, K.B(*(HC + Vector((0, HR.y + .05, 0))))


def mermaid_top(M):
    parts = []
    for sd in (1, -1):
        parts += fan('Shell cup', P.on_body(.16, sd * .42, .02), (sd * .25, 1, 0), dirv(sd * .42), .16, M['shell'], ribs=6, spread=1.2)
    parts.append(ellipsoid('Centre pearl', tuple(P.on_body(.19, 0, .035)), (.03, .03, .03), M['pearl'], (10, 6)))
    parts.append(tube('Band', [P.on_body(.18, TAU * k / 48, .008) for k in range(48)], .012, M['gold'], closed=True, segs=6))
    for h0, sag in ((.37, .1), (.36, .17)):
        parts += pearls('Pearl', [P.on_body(h0 - sag * math.cos(k * .154) ** 2, k * .14, .022) for k in range(-9, 10)], .016, M['pearl'])
    return parts, {}, None


def mermaid_skirt(M):
    parts = []
    for k, (y0, depth, r0) in enumerate(((.27, .16, .322), (.16, .17, .334), (.05, .18, .346))):   # rows of big scales
        o, _ = P.robe('Scale tier', M['scales'], M['cloth'], 0, TAU, y0, r0, lambda a, y0=y0, depth=depth, k=k: y0 - depth + .045 * abs(math.sin(7 * a + k * 1.57)),
                      lambda t, a, r0=r0: r0 + .03 * t, folds=(.004, 14, k), rows=5, cols=84, uv=(7, 1), inside=K.B(0, y0 - .2, 0))
        parts += o
    count = 7                                             # tail fins round the sides and back, each with its own rays
    for i in range(count):
        ac = lerp(.8, TAU - .8, i / (count - 1)); hw = .4; lift = .014 * (i % 2)
        o, _ = P.robe('Tail fin', M['fin'], M['fin'], ac - hw, ac + hw, .2, .35 + lift,
                      lambda a, ac=ac, hw=hw, i=i: -.5 + .05 * (i % 2) + .3 * ((a - ac) / hw) ** 2,
                      lambda t, a, lift=lift, ac=ac: .35 + lift + .2 * t ** 1.3, folds=(.012, 16, i), rows=10, cols=10, uv=(1, 1), inside=K.B(0, -.1, 0))
        parts += o
    parts += pearls('Belt pearl', body_ring(.27, .338, 36), .017, M['pearl'])
    parts += pearls('Swag pearl', [dirv(a) * .37 + Vector((0, .2 - .09 * math.sin((a + .9) / 1.8 * math.pi), 0)) for a in np.linspace(-.9, .9, 14)], .014, M['pearl'])
    a = .95; parts += star('Starfish', dirv(a) * .352 + Vector((0, .2, 0)), dirv(a), .06, M['gold'])
    a = -.75; root = dirv(a) * .345 + Vector((0, .18, 0))
    for dx, dy in ((-.04, .1), (.03, .12), (-.09, .04), (.07, .05)):
        tip = root + dirv(a + 1.57) * dx + Vector((0, dy, 0)) + dirv(a) * .02
        parts.append(tube('Coral', [root, root.lerp(tip, .5) + dirv(a) * .015, tip], lambda s: .015 * (1 - s) + .006, M['coral'], segs=6))
    d = dirv(1.5) * .35 + Vector((0, .22, 0))
    parts += [tube('Drop chain', [d, d - Vector((0, .14, 0))], .003, M['gold'], segs=4), ellipsoid('Glow drop', tuple(d - Vector((0, .17, 0))), (.032, .034, .032), M['glow'], (10, 6))]
    return parts, {}, None


def mermaid_sleeve(M, sx):
    parts = cuff_ruffle('Fin frill', sx, .2, .09, .13, M['fin'])
    parts += pearls('Pearl', [Vector((sx * .2, .097 * math.sin(TAU * k / 14), .097 * math.cos(TAU * k / 14))) for k in range(14)], .014, M['pearl'])
    parts += mitt('Glove', sx, M['cloth'], .36)
    return parts, {}, None


def mermaid_boot(M, sx):
    parts, _ = boot('Boot', sx, M['cloth'], M['shell'])
    parts += fan('Boot shell', (sx * .008, -.5, .2), (0, 1, .5), (0, .5, 1), .085, M['shell'], ribs=5)
    parts.append(ellipsoid('Boot pearl', (sx * .008, -.5, .228), (.02, .02, .02), M['pearl'], (8, 5)))
    path = [Vector((sx * lerp(.07, .2, t), lerp(-.44, -.36, t), lerp(-.02, -.1, t))) for t in np.linspace(0, 1, 6)]
    parts.append(tube('Ankle fin', path, lambda s: .012 + .06 * math.sin(math.pi * min(1, s * .9 + .1)), M['fin'], segs=8, flat=.18))
    return parts, {}, None


# ================================================================ druid
def druid_mats():
    rng = random.Random(21)
    P.cloth_texture('druid_cloth.png', rng, base=.8, weave=.06, mottle=.2); P.moss_texture('druid_moss.png', rng)
    return {'robe': m('Druid robe', (.26, .13, .2), .7, image='druid_cloth.png', tint=True),
            'leaf': m('Druid leaf accent', (.84, .42, .1), .6, image='druid_cloth.png', tint=True),
            'green': m('Druid green trim', (.45, .56, .16), .6, image='druid_cloth.png', tint=True),
            'cloak': m('Druid cloak', (.42, .23, .13), .7, image='druid_cloth.png'),
            'wood': m('Druid wood', (.4, .25, .14), .7), 'moss': m('Druid moss', (1, 1, 1), .9, image='druid_moss.png'),
            'cream': m('Druid linen', (.86, .72, .5), .7), 'stone': m('Druid rune stone', (.52, .48, .56), .6),
            'amber': m('Druid amber glow', (1., .72, .15), .2, emit=1.8), 'flask': m('Druid flask', (.5, .24, .72), .15),
            'rope': m('Druid rope', (.62, .46, .25), .8)}


def rune_stone(M, c, n, size):
    c = Vector(c); n = Vector(n).normalized()
    return [ellipsoid('Rune stone', tuple(c), (size, size * .9, size * .55) if abs(n.z) > .5 else (size * .55, size * .9, size), M['stone'], (8, 5)),
            K.ring('Rune', c + n * size * .5, n, size * .42, size * .09, M['amber'], segs=6), ellipsoid('Rune dot', tuple(c + n * size * .52), (size * .12,) * 3, M['amber'], (6, 4))]


def amber_drop(M, top, length, size=1.):
    top = Vector(top); bottom = top - Vector((0, length, 0))
    return [tube('Drop cord', [top, bottom], .003, M['rope'], segs=4), ellipsoid('Bead', tuple(top.lerp(bottom, .5)), (.009 * size,) * 3, M['wood'], (6, 4)),
            gem('Amber drop', tuple(bottom - Vector((0, .026 * size, 0))), (.018 * size, .034 * size, .018 * size), M['amber'])]


def druid_hood(M):
    edge = lambda a: 42 - 78 * (1 - P.front(a)) ** .8
    R = HR + Vector((.04, .035, .04))
    parts, point, pt = P.hood('Hood', M['robe'], M['cloak'], HC, R, edge, peak=.04, brim=.05)
    parts.append(tube('Moss band', [pt(0, k / 60, .008) for k in range(60)], .022, M['moss'], closed=True, segs=7))
    for sd in (1, -1):
        b = point(sd * .95, 60)
        ctrl = [b, b + Vector((sd * .16, .14, 0)), b + Vector((sd * .32, .28, -.03)), b + Vector((sd * .27, .47, -.02))]
        beam = [bez(ctrl, t) for t in np.linspace(0, 1, 16)]
        parts.append(tube('Antler', beam, lambda s: .032 * (1 - s) + .008, M['wood'], segs=8))
        for at, (dx, dy, dz) in ((5, (.12, .12, .04)), (9, (-.06, .16, .05)), (12, (.11, .09, -.03))):
            tip = beam[at] + Vector((sd * dx, dy, dz))
            parts.append(tube('Tine', [beam[at], beam[at].lerp(tip, .5) + Vector((0, .02, 0)), tip], lambda s: .016 * (1 - s) + .005, M['wood'], segs=6))
            parts += amber_drop(M, tip, .1 + .03 * at % .07, .9)
            parts.append(gem('Leaf', tuple(beam[at] + Vector((sd * .02, .035, .02))), (.022, .04, .006), M['green']))
        parts += amber_drop(M, beam[7] - Vector((0, .02, 0)), .2, 1.3)
        parts.append(ellipsoid('Moss clump', tuple(point(sd * .5, 68, .01)), (.07, .03, .06), M['moss'], (10, 6)))
    parts += rune_stone(M, point(0, edge(0) + 15, .02) + Vector((0, 0, .02)), (0, .3, 1), .06)
    return parts, {}, K.B(*(HC + Vector((0, R.y + .04, 0))))


def druid_robe(M):
    parts = P.body_shell('Robe', M['robe'], -.02, lambda a: .384, .012)
    o, _ = mantle('Mantle', M['cloak'], .4, .2, .26, .37, 9, .06, .3)
    parts += o
    for k, (y, r, count, ln, droop) in enumerate(((.3, .345, 16, .21, 1.15), (.35, .31, 14, .2, 1.0), (.4, .26, 12, .2, .75))):
        parts += leaf_row(M, ('leaf', 'leaf', 'green'), y, r, count, ln, .062, droop, 100 + k, M['cloak'])
    parts.append(tube('Scarf', body_ring(.4, .24, 40, .01, 5), .05, M['cloak'], closed=True, segs=8, flat=.7))
    cord = [P.on_body(.36 - .13 * math.cos(a * 1.4) ** 2, a, .1) for a in np.linspace(-1.1, 1.1, 20)]
    parts.append(tube('Amulet cord', cord, .006, M['rope'], segs=5))
    parts += rune_stone(M, P.on_body(.22, 0, .11), (0, 0, 1), .045)
    for sd in (1, -1):
        parts.append(ellipsoid('Moss clump', tuple(P.on_body(.39, sd * 1.3, .09)), (.06, .025, .07), M['moss'], (10, 6)))
        parts.append(gem('Leaf', tuple(P.on_body(.42, sd * .9, .08)), (.03, .012, .05), M['green']))
    c = Vector((.29, .5, -.08))                           # a little owl on the right shoulder
    parts += [ellipsoid('Owl', tuple(c), (.06, .075, .055), M['wood'], (12, 8)), ellipsoid('Owl head', tuple(c + Vector((0, .09, .012))), (.06, .05, .054), M['cloak'], (12, 8)),
              gem('Owl beak', tuple(c + Vector((0, .078, .066))), (.01, .016, .012), M['leaf'])]
    for sd in (1, -1):
        e = c + Vector((sd * .026, .096, .05))
        parts += [disc('Owl eye', e, (0, 0, 1), .022, .006, M['cream'], 12), ellipsoid('Owl pupil', tuple(e + Vector((0, 0, .008))), (.011, .011, .006), M['amber'], (8, 5)),
                  gem('Owl ear', tuple(c + Vector((sd * .04, .14, .0))), (.013, .028, .011), M['wood']),
                  ellipsoid('Owl wing', tuple(c + Vector((sd * .055, -.005, -.005))), (.02, .06, .045), M['cloak'], (8, 6))]
    return parts, {}, None


def druid_skirt(M):
    parts = []
    o, _ = P.robe('Underskirt', M['cloak'], M['robe'], 0, TAU, .28, .325, lambda a: -.3 + P.tatter(a, 9, .1, .4),
                  lambda t, a: .325 + .08 * t, folds=(.02, 9, 0.), rows=8, cols=64, inside=K.B(0, 0, 0))
    parts += o
    for k, (y, r, count, ln, droop) in enumerate(((-.16, .39, 20, .27, 1.3), (-.03, .375, 20, .26, 1.25), (.1, .355, 18, .24, 1.2), (.24, .335, 18, .22, 1.15))):
        parts += leaf_row(M, ('leaf', 'leaf', 'green', 'cloak'), y, r, count, ln, .068, droop, 200 + k, M['cloak'])
    for ph in (0., math.pi):   # two vines twisted into a belt
        ring_pts = [Vector((.355 * math.sin(TAU * i / 60), .25 + .018 * math.sin(9 * TAU * i / 60 + ph), .355 * math.cos(TAU * i / 60))) for i in range(60)]
        parts.append(tube('Vine belt', ring_pts, .016, M['wood'] if ph == 0 else M['green'], closed=True, segs=6))
    o, pt = panel('Tabard', M['cream'], -.2, .2, .22, -.3, .45)
    parts += o
    trunk = [pt(.75, .5, .006), pt(.42, .5, .006)]
    parts.append(tube('Tree trunk', trunk, .008, M['wood'], segs=5))
    for s_ in (.2, .36, .64, .8):
        parts.append(tube('Tree branch', [pt(.45, .5, .006), pt(.3 - .08 * abs(s_ - .5), s_, .006)], .005, M['wood'], segs=4))
    a = .85; c = dirv(a) * .45 + Vector((0, .1, 0))
    parts += [tube('Flask cord', [dirv(a) * .36 + Vector((0, .25, 0)), c + Vector((0, .07, 0))], .004, M['rope'], segs=4),
              ellipsoid('Flask', tuple(c), (.05, .052, .05), M['flask'], (12, 7)), tube('Flask neck', [c + Vector((0, .04, 0)), c + Vector((0, .085, 0))], .016, M['flask'], segs=8),
              ellipsoid('Cork', tuple(c + Vector((0, .09, 0))), (.017, .014, .017), M['wood'], (8, 5))]
    parts += rune_stone(M, dirv(-.6) * .45 + Vector((0, .12, 0)), dirv(-.6), .04)
    parts.append(tube('Rune cord', [dirv(-.6) * .36 + Vector((0, .25, 0)), dirv(-.6) * .45 + Vector((0, .15, 0))], .004, M['rope'], segs=4))
    for a, ln in ((.45, .2), (-1.1, .16), (1.6, .24), (2.6, .18), (-2.3, .22)):
        parts += amber_drop(M, dirv(a) * .43 + Vector((0, .2, 0)), ln, 1.1)
    return parts, {}, None


def druid_glove(M, sx):
    o, _ = P.sleeve('Bark bracer', M['wood'], None, sx, .2, .37, lambda t, a: .09 + .006 * math.sin(9 * a) + .004 * math.sin(20 * t))
    parts = o + cuff_ruffle('Leaf cuff', sx, .21, .092, .1, M['leaf'])
    parts.append(arm_ring('Moss band', sx, .37, .09, .018, M['moss']))
    parts.append(tube('Vine', [Vector((sx * lerp(.21, .36, t), .1 * math.sin(t * 9), .1 * math.cos(t * 9))) for t in np.linspace(0, 1, 20)], .008, M['green'], segs=5))
    return parts, {}, None


def druid_boot(M, sx):
    parts, sh = boot('Boot', sx, M['robe'], M['wood'], top=-.26)
    parts.append(ellipsoid('Moss toe', (sx * .008, -.475, .14), (.07, .03, .08), M['moss'], (10, 6)))
    parts.append(tube('Root wrap', [sh(t, t * 2.3 + .1) * 1.0 + Vector((0, 0, 0)) for t in np.linspace(0, 1, 26)], .012, M['wood'], segs=5))
    parts.append(tube('Root over foot', [Vector((sx * .008 + .09 * math.sin(u), -.5 + .05 * math.cos(u), .1)) for u in np.linspace(-1.5, 1.5, 10)], .011, M['wood'], segs=5))
    parts += rune_stone(M, Vector((sx * .09, -.33, .02)), (sx, 0, 0), .03)
    return parts, {}, None


# ================================================================ vampire
def vampire_mats():
    rng = random.Random(31)
    P.cloth_texture('vampire_cloth.png', rng, base=.82, weave=.04, mottle=.14); stripes_texture('vampire_stripes.png', .55)
    return {'coat': m('Vampire coat', (.17, .07, .15), .6, image='vampire_cloth.png', tint=True),
            'wine': m('Vampire wine accent', (.5, .05, .13), .5, image='vampire_cloth.png', tint=True),
            'red': m('Vampire red trim', (.78, .06, .1), .45, image='vampire_cloth.png', tint=True),
            'stripe': m('Vampire breeches', (.3, .15, .26), .6, image='vampire_stripes.png'),
            'white': m('Vampire linen', (.94, .89, .86), .6), 'gold': m('Vampire gold', (.92, .66, .22), .3, metal=.85),
            'hair': m('Vampire hair', (.86, .8, .84), .45, tint=True), 'ruby': m('Vampire ruby gem', (.95, .06, .16), .1, emit=1.2),
            'leather': m('Vampire leather', (.2, .09, .09), .5)}


def bat(M, c, n, size):
    """A small gold bat: body and two swept wings."""
    c = Vector(c); n, a, b = plane_axes(n)
    obs = [ellipsoid('Bat', tuple(c), (size * .25,) * 3, M['gold'], (6, 4))]
    for sd in (1, -1):
        obs.append(tube('Bat wing', [c, c + a * sd * size * .6 + b * size * .3, c + a * sd * size + b * size * .05], lambda s: size * (.2 * (1 - s) + .05), M['gold'], segs=5, flat=.4))
    return obs


def vampire_hair(M):
    parts = []
    edge = lambda a: 36 - 46 * (1 - P.front(a))
    parts += head_cap('Hair', M['hair'], edge, .04)
    rng = random.Random(5)
    for k in range(22):                                  # combed-back waves lying on the head, flicking out and up at the ends
        a = lerp(.5, TAU - .5, k / 21) + rng.uniform(-.05, .05); o = dirv(a)
        lats = np.linspace(80, edge(a) + 2, 9)
        path = [head_pt(a + .1 * math.sin(i * .7 + k), lat, .04 + .05 * math.sin(math.pi * i / 8)) for i, lat in enumerate(lats)]
        end = path[-1]; flick = rng.uniform(.8, 1.25)
        path += [end + o * .05 * flick - UP * .02, end + o * .11 * flick + UP * .02, end + o * .14 * flick + UP * .075 * flick]
        parts.append(tube('Wave', path, lambda s_: .062 * math.sin(math.pi * min(1, s_ * .8 + .15)) ** .6 * (1 - s_ ** 3) + .012, M['hair'], segs=8, flat=.5, up=tuple(o)))
    st = head_pt(.2, 74, .04)                            # the forelock swept across the brow, a shorter wave to the other side
    for ctrl, r in (([st, st + Vector((-.1, .08, .16)), st + Vector((-.26, .0, .14)), st + Vector((-.33, -.1, .05)), st + Vector((-.37, -.04, .02))], .075),
                    ([st, st + Vector((.12, .08, .12)), st + Vector((.27, .0, .08)), st + Vector((.32, -.1, .0)), st + Vector((.36, -.05, -.03))], .065)):
        parts.append(tube('Forelock', [bez(ctrl, t) for t in np.linspace(0, 1, 14)], lambda s_, r=r: r * math.sin(math.pi * min(1, s_ * .8 + .18)) ** .6 * (1 - s_ ** 3) + .012, M['hair'], segs=8, flat=.55))
    return parts, {}, K.B(*(HC + Vector((0, HR.y + .06, 0))))


def vampire_coat(M):
    parts = P.body_shell('Coat', None, -.02, lambda a: .384, .012, colour_of=lambda a: 'wine' if abs(P.wrap_angle(a)) < .3 else 'coat', mats=M)
    for a in (-.3, .3):
        parts.append(tube('Lapel edge', [P.on_body(h, a, .014) for h in np.linspace(-.01, .38, 12)], .005, M['gold'], segs=5))
    parts += pearls('Button', [P.on_body(h, 0, .018) for h in (.02, .08, .14)], .014, M['gold'])
    for h, w in ((.33, .3), (.27, .25), (.21, .19)):   # the jabot: three falls of ruffled linen
        curve = [P.on_body(h, a, .03 + .012 * math.sin(a * 40)) for a in np.linspace(-w, w, 15)]
        parts += ribbon('Jabot', curve, lambda p, i: Vector((0, -1, .3 + .25 * math.sin(i * 1.9))).normalized(), .085, M['white'], inside=K.B(0, .2, 0))
    c = P.on_body(.35, 0, .055)
    parts += [gem('Ruby', tuple(c), (.03, .036, .016), M['ruby']), K.ring('Brooch', c - Vector((0, 0, .004)), (0, 0, 1), .036, .008, M['gold'], segs=16)]
    parts.append(P.chain('Chain', c, P.on_body(.26, .95, .03), .07, M['gold'], links=12, radius=.004))
    parts.append(P.chain('Chain', c, P.on_body(.26, -.95, .03), .07, M['gold'], links=12, radius=.004))
    # high collar open at the front, flaring out behind the head
    def collar(t, s, off=0.):
        a = lerp(.6, TAU - .6, s); f = P.front(a)
        r = lerp(.3, .45 + .05 * f, t ** .8) + off
        return Vector((r * math.sin(a), lerp(.36, .6 + .12 * f ** 2, t), r * math.cos(a)))
    parts.append(surface('Collar', 8, 48, collar, M['coat'], uv=(3, 1), inside=K.B(0, .45, 0)))
    parts.append(surface('Collar lining', 8, 48, lambda t, s: collar(t, s, -.01), M['red'], uv=(3, 1), inside=K.B(0, .45, 0), flip=True, hem=0))
    parts.append(tube('Collar edge', [collar(1, s, -.004) for s in np.linspace(0, 1, 48)], .008, M['gold'], segs=5))
    o, _ = mantle('Cape', M['coat'], .37, .0, .33, .5, 6, .12, .5, a0=.55, a1=TAU - .55, under=M['red'])   # bat-wing hem
    parts += o
    parts += bat(M, Vector((0, .2, -.49)), (0, 0, -1), .09)
    return parts, {}, None


def vampire_breeches(M):
    parts = []
    o, _ = P.robe('Breeches', M['stripe'], M['leather'], 0, TAU, .28, .318, lambda a: -.07 + .012 * math.sin(6 * a),
                  lambda t, a: .318 + .028 * math.sin(math.pi * t), folds=(.008, 12, .5), rows=6, cols=64, uv=(6, 1), inside=K.B(0, .1, 0))
    parts += o
    parts.append(belt('Belt', .25, .336, .026, M['leather'], .45))
    parts += buckle('Buckle', Vector((0, .25, .345)), (0, 0, 1), .04, .034, M['gold'])
    a = .8; top = dirv(a) * .345 + Vector((0, .24, 0)); c = dirv(a) * .37 + Vector((0, .07, 0))
    parts += [P.chain('Charm chain', top, c + Vector((0, .07, 0)), .0, M['gold'], links=6, radius=.004),
              gem('Coffin', tuple(c), (.045, .075, .02), M['coat'], facing=tuple(dirv(a))), K.ring('Coffin rim', c + Vector((0, .008, 0)), dirv(a), .04, .006, M['gold'], segs=6)]
    parts += bat(M, c + dirv(a) * .022, dirv(a), .035)
    f = c + Vector((0, -.12, 0)) - dirv(a + 1.57) * .05
    parts += [tube('Flask cord', [c - dirv(a + 1.57) * .03 + Vector((0, -.05, 0)), f + Vector((0, .05, 0))], .003, M['gold'], segs=4),
              ellipsoid('Blood flask', tuple(f), (.034, .038, .034), M['ruby'], (10, 6)), tube('Flask neck', [f + Vector((0, .03, 0)), f + Vector((0, .06, 0))], .011, M['gold'], segs=6)]
    return parts, {}, None


def vampire_sleeve(M, sx):
    o, pt = P.sleeve('Sleeve', M['coat'], M['red'], sx, .03, .4, lambda t, a: .09 + .012 * math.sin(math.pi * t))
    parts = o + cuff_ruffle('Cuff ruffle', sx, .39, .088, .07, M['white']) + cuff_ruffle('Cuff ruffle', sx, .36, .09, .05, M['white'])
    parts.append(arm_ring('Cuff band', sx, .35, .094, .008, M['gold']))
    parts.append(ellipsoid('Cuff button', tuple(pt(.8, .25, .006)), (.014, .014, .014), M['gold'], (8, 5)))
    return parts, {}, None


def vampire_boot(M, sx):
    parts, sh = boot('Boot', sx, M['leather'], M['coat'], top=-.24, cuff=M['gold'], toe=None)
    parts.append(tube('Strap', [sh(.45, k / 24) * 1.0 + Vector((0, 0, 0)) for k in range(24)], .014, M['coat'], closed=True, segs=5, flat=.5))
    parts += buckle('Boot buckle', sh(.45, 0) + Vector((0, 0, .012)), (0, 0, 1), .026, .022, M['gold'], .006)
    parts += bat(M, Vector((sx * .008, -.485, .2)), (0, .6, 1), .06)
    return parts, {}, None


# ================================================================ diver
def diver_mats():
    rng = random.Random(41)
    P.cloth_texture('diver_canvas.png', rng, base=.8, weave=.1, mottle=.22); P.stone_texture('diver_metal.png', rng, base=.8)
    return {'brass': m('Diver brass', (.74, .5, .17), .42, metal=.8, image='diver_metal.png', tint=True),
            'canvas': m('Diver canvas', (.74, .62, .43), .8, image='diver_canvas.png', tint=True),
            'patina': m('Diver patina accent', (.3, .62, .54), .5, metal=.5, image='diver_metal.png', tint=True),
            'leather': m('Diver leather', (.32, .17, .12), .6), 'rubber': m('Diver rubber', (.13, .1, .14), .5),
            'lamp': m('Diver lamp glow', (1., .76, .22), .2, emit=1.7), 'barnacle': m('Diver barnacle', (.86, .8, .7), .8),
            'coral': m('Diver coral', (.93, .4, .25), .5), 'weed': m('Diver weed', (.46, .52, .14), .6), 'rope': m('Diver rope', (.72, .58, .34), .8)}


def barnacles(M, c, n, size, count=4, seed=0):
    rng = random.Random(seed); c = Vector(c); n, a, b = plane_axes(n); obs = []
    for _ in range(count):
        p = c + a * rng.uniform(-size, size) + b * rng.uniform(-size, size); r = rng.uniform(.5, .9) * size * .6
        obs += [ellipsoid('Barnacle', tuple(p), (r, r, r), M['barnacle'], (8, 5)), ellipsoid('Barnacle mouth', tuple(p + n * r * .75), (r * .4,) * 3, M['rubber'], (6, 4))]
    return obs


def diver_helmet(M):
    parts = []
    G = (.09, .03, .09); HCC = HC + Vector((0, .02, 0))
    hp = lambda a, lat, off=0.: head_pt(a, lat, off, G, HCC)
    W, LO, HI = .95, -16, 44                              # the face window: |a| < W, latitude LO..HI
    pieces = (('Dome', lambda t, s: (TAU * s, lerp(HI, 89.5, t)), True, 40), ('Helmet back', lambda t, s: (lerp(W, TAU - W, s), lerp(-38, HI, t)), False, 40),
              ('Chin', lambda t, s: (lerp(-W, W, s), lerp(-38, LO, t)), False, 16))
    for name, f, wrap, cols in pieces:
        parts.append(surface(name, 8, cols, lambda t, s, f=f: hp(*f(t, s)), M['brass'], wrap=wrap, uv=(3, 1), inside=K.B(*HCC)))
        parts.append(surface(name + ' inside', 8, cols, lambda t, s, f=f: hp(*f(t, s), -.012), M['rubber'], wrap=wrap, inside=K.B(*HCC), flip=True, hem=0))
    rim = [hp(a, LO, .012) for a in np.linspace(-W, W, 12)] + [hp(W, lat, .012) for lat in np.linspace(LO, HI, 8)[1:]] + \
          [hp(a, HI, .012) for a in np.linspace(W, -W, 12)[1:]] + [hp(-W, lat, .012) for lat in np.linspace(HI, LO, 8)[1:-1]]
    parts.append(tube('Window frame', rim, .03, M['brass'], closed=True, segs=8))
    for a in (-.32, .32):
        parts.append(tube('Grille', [hp(a, lat, .016) for lat in np.linspace(LO, HI, 8)], .012, M['brass'], segs=6))
    parts.append(tube('Grille', [hp(a, 14, .016) for a in np.linspace(-W, W, 12)], .012, M['brass'], segs=6))
    for sd in (1, -1):
        a = sd * 1.6
        parts += [K.ring('Porthole', hp(a, 12, .03), dirv(a), .1, .024, M['brass'], segs=20), disc('Porthole glass', hp(a, 12, .012), dirv(a), .092, .014, M['lamp']),
                  tube('Porthole bar', [hp(a, 12, .034) - UP * .09, hp(a, 12, .034) + UP * .09], .008, M['brass'], segs=5),
                  tube('Porthole bar', [hp(a, 12, .034) - dirv(a + 1.57) * .09, hp(a, 12, .034) + dirv(a + 1.57) * .09], .008, M['brass'], segs=5)]
    top = hp(0, 90, .0)
    parts += [K.ring('Top porthole', top + UP * .012, UP, .085, .022, M['brass'], segs=20), disc('Top glass', top, UP, .08, .012, M['lamp'])]
    parts.append(tube('Collar', [hp(TAU * k / 48, -38, .02) for k in range(48)], .042, M['brass'], closed=True, segs=8))
    parts += pearls('Rivet', [hp(TAU * (k + .5) / 18, -27, .006) for k in range(18) if abs(P.wrap_angle(TAU * (k + .5) / 18)) > W + .1], .016, M['brass'])
    parts += pearls('Rivet', [hp(TAU * (k + .5) / 14, 50, .006) for k in range(14)], .014, M['brass'])
    parts += barnacles(M, hp(.7, 68, .0), hp(.7, 68, 1.) - hp(.7, 68, 0.), .06, 5, 1) + barnacles(M, hp(-2.2, 55, .0), hp(-2.2, 55, 1.) - hp(-2.2, 55, 0.), .05, 4, 2)
    st = hp(2.5, -5, .02)
    ctrl = [st, st + Vector((.16, .05, -.18)), st + Vector((.2, -.3, -.2)), st + Vector((.08, -.52, -.12))]
    parts.append(tube('Air hose', [bez(ctrl, t) for t in np.linspace(0, 1, 18)], .036, M['rubber'], segs=8))
    parts.append(K.ring('Hose fitting', st, dirv(2.5), .04, .014, M['brass'], segs=12))
    return parts, {}, K.B(*(HCC + Vector((0, HR.y + G[1] + .05, 0))))


def diver_suit(M):
    parts = P.body_shell('Suit', M['canvas'], -.02, lambda a: .384, .02)
    def plate(t, s, off=0.):
        a = TAU * s; r = lerp(.25, .37, t ** .8) + off
        return Vector((r * math.sin(a), lerp(.415, .3 - .03 * P.front(a), t) + .02 * math.sin(math.pi * t), r * math.cos(a)))
    parts.append(surface('Corselet', 6, 64, plate, M['brass'], wrap=True, uv=(4, 1), inside=K.B(0, .3, 0)))
    parts.append(surface('Corselet under', 6, 64, lambda t, s: plate(t, s, -.01), M['brass'], wrap=True, inside=K.B(0, .3, 0), flip=True))
    parts += pearls('Rivet', [plate(.85, k / 16, .008) for k in range(16)], .015, M['brass'])
    for sd in (1, -1):   # crossed leather straps
        parts.append(tube('Strap', [P.on_body(lerp(.3, .02, (a + 1.3) / 2.6), sd * a, .034) for a in np.linspace(-1.3, 1.3, 16)], .024, M['leather'], segs=6, flat=.35))
    parts += buckle('Buckle', P.on_body(.16, 0, .05), (0, 0, 1), .034, .03, M['brass'])
    for k in range(2):   # a coil of rope over one shoulder
        parts.append(tube('Rope', [P.on_body(.3 - (.15 + .03 * k) * math.cos(a * .7) ** 2, a + .3, .05 + .012 * k) for a in np.linspace(-1.9, 1.5, 26)], .017, M['rope'], segs=6))
    parts += star('Starfish', P.on_body(.27, .75, .06), dirv(.75), .065, M['coral'])
    parts += barnacles(M, P.on_body(.2, -.8, .03), dirv(-.8), .045, 4, 3)
    for a0, ln in ((1.9, .45), (-2.0, .38)):
        st = P.on_body(.33, a0, .05)
        parts.append(tube('Seaweed', [st + Vector((0, -ln * t, 0)) + dirv(a0 + 1.57) * .03 * math.sin(t * 9) + dirv(a0) * .02 * t for t in np.linspace(0, 1, 14)],
                          lambda s: .008 + .03 * math.sin(math.pi * min(1, s * .9 + .1)), M['weed'], segs=6, flat=.18))
    # air tank on the back
    z = -.44
    parts += [tube('Tank', [Vector((0, .0, z)), Vector((0, .34, z))], .115, M['patina'], segs=18), ellipsoid('Tank top', (0, .34, z), (.115, .07, .115), M['patina'], (16, 8)),
              ellipsoid('Tank bottom', (0, .0, z), (.115, .06, .115), M['patina'], (16, 8)), tube('Valve', [Vector((0, .4, z)), Vector((0, .46, z))], .03, M['brass'], segs=8),
              tube('Valve wheel', [Vector((-.05, .47, z)), Vector((.05, .47, z))], .014, M['brass'], segs=6)]
    for y in (.08, .26):
        parts.append(K.ring('Tank band', Vector((0, y, z)), UP, .118, .014, M['brass'], segs=20))
    for sd in (1, -1):
        parts.append(tube('Tank strap', [Vector((sd * .1, .3, z + .06)), Vector((sd * .2, .33, -.24)), Vector((sd * .26, .36, -.05))], .016, M['leather'], segs=5, flat=.4))
    return parts, {}, None


def diver_breeches(M):
    parts = []
    o, _ = P.robe('Breeches', M['canvas'], M['leather'], 0, TAU, .28, .318, lambda a: -.1 + .014 * math.sin(5 * a),
                  lambda t, a: .318 + .055 * math.sin(math.pi * t ** .8), folds=(.012, 9, .5), rows=8, cols=64, inside=K.B(0, .1, 0))
    parts += o
    parts.append(belt('Belt', .25, .345, .03, M['leather'], .4))
    parts += buckle('Buckle', Vector((0, .25, .356)), (0, 0, 1), .045, .036, M['brass'], .009)
    parts += pearls('Belt rivet', body_ring(.25, .353, 12)[1:], .012, M['brass'])
    o, pt = panel('Tabard', M['canvas'], .12, .5, .22, -.28, .395)
    parts += o
    cx = .5; ink = M['rubber']
    parts += [tube('Anchor shank', [pt(.3, cx, .006), pt(.75, cx, .006)], .008, ink, segs=5), tube('Anchor stock', [pt(.38, .25, .006), pt(.38, .75, .006)], .007, ink, segs=5),
              tube('Anchor arms', [pt(.6 + .16 * (1 - (2 * s - 1) ** 2), s, .006) for s in np.linspace(.15, .85, 9)], .008, ink, segs=5),
              K.ring('Anchor ring', pt(.26, cx, .006), dirv(.31), .018, .005, ink, segs=10)]
    a = -.7; top = dirv(a) * .35 + Vector((0, .24, 0)); c = dirv(a) * .4 + Vector((0, .02, 0))
    parts += [P.chain('Lantern chain', top, c + Vector((0, .1, 0)), .0, M['brass'], links=6, radius=.005),
              ellipsoid('Lantern light', tuple(c), (.046, .06, .046), M['lamp'], (12, 7)), disc('Lantern cap', c + Vector((0, .065, 0)), UP, .05, .012, M['brass'], 12),
              disc('Lantern base', c - Vector((0, .065, 0)), UP, .05, .012, M['brass'], 12), K.ring('Lantern loop', c + Vector((0, .095, 0)), dirv(a), .02, .006, M['brass'], segs=10)]
    for k in range(5):
        d = dirv(TAU * k / 5) * .05
        parts.append(tube('Lantern bar', [c + d - Vector((0, .06, 0)), c + d * 1.1, c + d + Vector((0, .06, 0))], .006, M['brass'], segs=4))
    parts.append(tube('Rope loop', [dirv(a_) * .35 + Vector((0, .24 - .12 * math.sin((a_ - .7) / 1.2 * math.pi), 0)) + dirv(a_) * .03 for a_ in np.linspace(.7, 1.9, 12)], .015, M['rope'], segs=6))
    return parts, {}, None


def diver_glove(M, sx):
    o, _ = P.sleeve('Sleeve', M['canvas'], None, sx, .03, .34, lambda t, a: .092 + .016 * math.sin(math.pi * t) + .004 * math.sin(7 * a))
    parts = o
    o, _ = P.sleeve('Cuff', M['leather'], None, sx, .3, .38, lambda t, a: .098)
    parts += o + [arm_ring('Cuff ring', sx, .3, .1, .012, M['brass']), arm_ring('Cuff ring', sx, .38, .1, .012, M['brass'])]
    parts += pearls('Rivet', [Vector((sx * .34, .104 * math.sin(TAU * k / 8), .104 * math.cos(TAU * k / 8))) for k in range(8)], .01, M['brass'])
    parts += mitt('Glove', sx, M['rubber'], .37)
    return parts, {}, None


def diver_boot(M, sx):
    parts, sh = boot('Boot', sx, M['leather'], M['brass'], top=-.22, cuff=M['brass'], toe=M['brass'])
    for t in (.3, .65):
        parts.append(tube('Strap', [sh(t, k / 24) for k in range(24)], .016, M['brass'], closed=True, segs=5, flat=.45))
    parts += pearls('Rivet', [Vector((sx * .008 + .095 * math.sin(u), -.545, .06 + .18 * math.cos(u))) for u in np.linspace(-1.2, 1.2, 7)], .012, M['brass'])
    parts += barnacles(M, Vector((sx * .07, -.47, .1)), (sx, .5, .3), .03, 3, 7)
    return parts, {}, None


# ================================================================ clockwork toy
def clockwork_mats():
    rng = random.Random(51)
    P.cloth_texture('clockwork_cloth.png', rng, base=.82, weave=.06, mottle=.14); stripes_texture('clockwork_stripes.png', .6)
    return {'cloth': m('Clockwork cloth', (.5, .1, .2), .6, image='clockwork_cloth.png', tint=True),
            'stripe': m('Clockwork stripe', (.64, .34, .42), .6, image='clockwork_stripes.png', tint=True),
            'brass': m('Clockwork brass accent', (.86, .62, .22), .35, metal=.85, tint=True),
            'leather': m('Clockwork leather', (.3, .15, .1), .55), 'cream': m('Clockwork dial', (.93, .87, .72), .5),
            'steel': m('Clockwork steel', (.22, .22, .27), .35, metal=.7), 'lamp': m('Clockwork lamp glow', (1., .6, .12), .2, emit=1.7),
            'lens': m('Clockwork lens', (.56, .3, .76), .1, metal=.3)}


def hat_pt(r, y, a):
    return Vector((r * math.sin(a), y, HC.z + r * math.cos(a)))


def clockwork_hat(M):
    parts = []
    y0, y1 = .3, .64
    crown = lambda t, s, off=0.: hat_pt(lerp(.2, .235, t) + off, lerp(y0, y1, t), TAU * s)
    parts.append(surface('Crown', 6, 40, crown, M['cloth'], wrap=True, uv=(4, 1.5), inside=K.B(0, .45, HC.z)))
    parts.append(disc('Hat top', hat_pt(0, y1, 0), UP, .235, .012, M['cloth'], 28))
    brim = lambda t, s, off=0.: hat_pt(lerp(.19, .36, t), y0 + off + .035 * t * t * math.cos(2 * TAU * s + math.pi), TAU * s)
    parts.append(surface('Brim', 4, 48, brim, M['cloth'], wrap=True, uv=(4, 1), inside=K.B(0, y0 - .2, HC.z)))
    parts.append(surface('Brim under', 4, 48, lambda t, s: brim(t, s, -.012), M['leather'], wrap=True, inside=K.B(0, y0 - .2, HC.z), flip=True))
    parts.append(tube('Brim edge', [brim(1, k / 48, -.006) for k in range(48)], .01, M['brass'], closed=True, segs=6))
    parts.append(tube('Hat band', [crown(.1, k / 40, .006) for k in range(40)], .03, M['leather'], closed=True, segs=6, flat=.35))
    for sd in (1, -1):   # goggles pushed up on the crown
        c = crown(.42, sd * .06 + (0 if sd > 0 else 1), .03)
        n = (c - hat_pt(0, c.y, 0)).normalized()
        parts += [K.ring('Goggle', c, n, .068, .018, M['brass'], segs=20), disc('Goggle lens', c - n * .004, n, .062, .012, M['lens'])]
    parts.append(tube('Goggle strap', [crown(.42, k / 40, .008) for k in range(40)], .016, M['leather'], closed=True, segs=5, flat=.4))
    parts += cog('Hat gear', crown(.55, .25, .012), dirv(TAU * .25), .075, M['brass'], 10, M['steel'])
    parts += cog('Small gear', crown(.25, .32, .012), dirv(TAU * .32), .042, M['brass'], 8)
    parts += dial('Gauge', crown(.5, .72, .02), dirv(TAU * .72), .065, M['cream'], M['brass'], M['steel'])
    st = hat_pt(.14, y1, 2.7)   # a cream plume
    ctrl = [st, st + Vector((.05, .22, -.04)), st + Vector((-.1, .34, -.14)), st + Vector((-.28, .3, -.2))]
    parts.append(tube('Plume', [bez(ctrl, t) for t in np.linspace(0, 1, 14)], lambda s: .012 + .075 * math.sin(math.pi * min(1, s * .85 + .08)), M['cream'], segs=8, flat=.22))
    pipe = [hat_pt(.24, y0 + .06, 3.9), hat_pt(.3, y0 + .1, 3.9), hat_pt(.31, y1 + .02, 3.9), hat_pt(.31, y1 + .12, 3.9)]
    parts += [tube('Steam pipe', pipe, .022, M['brass'], segs=8), K.ring('Pipe lip', pipe[-1], UP, .026, .009, M['brass'], segs=12)]
    return parts, {}, K.B(*hat_pt(0, y1 + .05, 0))


def clockwork_coat(M):
    parts = P.body_shell('Coat', None, -.02, lambda a: .384, .012, colour_of=lambda a: 'stripe' if abs(P.wrap_angle(a)) < .28 else 'cloth', mats=M)
    for a in (-.28, .28):
        parts.append(tube('Front trim', [P.on_body(h, a, .014) for h in np.linspace(-.01, .38, 12)], .007, M['brass'], segs=5))
    parts += pearls('Button', [P.on_body(h, sd * .2, .018) for h in (.04, .12, .2) for sd in (1, -1)], .015, M['brass'])
    c = P.on_body(.36, 0, .05)   # bow tie
    for sd in (1, -1):
        parts.append(ellipsoid('Bow', tuple(c + Vector((sd * .06, 0, 0))), (.06, .04, .022), M['cloth'], (10, 6)))
    parts.append(ellipsoid('Bow knot', tuple(c + Vector((0, 0, .008))), (.022, .026, .024), M['cloth'], (8, 5)))
    for sd in (1, -1):   # epaulettes with a fringe
        e = P.on_body(.385, sd * 1.57, .01) + Vector((0, .02, 0))
        parts.append(ellipsoid('Epaulette', tuple(e), (.115, .026, .095), M['brass'], (16, 6)))
        edge = [e + Vector((.112 * math.cos(u) * 1.0, -.005, .092 * math.sin(u))) for u in np.linspace(0, TAU, 28, endpoint=False) if sd * math.cos(u) > -.35]
        parts += ribbon('Fringe', edge, lambda p, i: Vector((0, -1, 0)), .075, M['brass'], inside=K.B(*e))
        parts += pearls('Fringe bead', [p - Vector((0, .08, 0)) for p in edge[::2]], .011, M['brass'])
    g = P.on_body(.2, .55, .03)
    parts += cog('Chest gear', g, dirv(.55), .07, M['brass'], 10, M['steel']) + [disc('Gear light', g + dirv(.55) * .004, dirv(.55), .03, .012, M['lamp'], 10)]
    parts += cog('Chest gear small', P.on_body(.1, .82, .03), dirv(.82), .042, M['brass'], 8)
    parts += dial('Chest gauge', P.on_body(.16, -.6, .035), dirv(-.6), .058, M['cream'], M['brass'], M['steel'])
    parts.append(tube('Strap', [P.on_body(lerp(.32, .04, (a + 1.3) / 2.6), a, .03) for a in np.linspace(-1.3, 1.3, 16)], .022, M['leather'], segs=6, flat=.35))
    # the wind-up key in the back
    parts += [tube('Key shaft', [Vector((0, .2, -.3)), Vector((0, .2, -.5))], .028, M['brass'], segs=10), K.ring('Key collar', Vector((0, .2, -.33)), (0, 0, 1), .045, .014, M['brass'], segs=14)]
    for sd in (1, -1):
        parts.append(K.ring('Key wing', Vector((0, .2 + sd * .09, -.57)), (1, 0, 0), .075, .024, M['brass'], segs=20))
    return parts, {}, None


def clockwork_breeches(M):
    parts = []
    o, _ = P.robe('Breeches', M['stripe'], M['leather'], 0, TAU, .28, .318, lambda a: -.07 + .012 * math.sin(6 * a),
                  lambda t, a: .318 + .028 * math.sin(math.pi * t), folds=(.008, 12, .5), rows=6, cols=64, uv=(6, 1), inside=K.B(0, .1, 0))
    parts += o
    o, pt = P.robe('Coat tails', M['cloth'], M['stripe'], .5, TAU - .5, .28, .33, lambda a: -.3 + P.tatter(a, 4, .14, .5),
                   lambda t, a: .33 + .1 * t, folds=(.015, 6, 0.), rows=10, cols=56)
    parts += o
    parts.append(tube('Tail trim', [pt(1, s, .004) for s in np.linspace(0, 1, 60)], .008, M['brass'], segs=5))
    parts.append(belt('Belt', .25, .345, .028, M['leather'], .4))
    parts += buckle('Buckle', Vector((0, .25, .356)), (0, 0, 1), .042, .034, M['brass'], .009)
    a = -.35; c = dirv(a) * .375 + Vector((0, .06, 0))
    parts += dial('Pocket watch', c, dirv(a), .075, M['cream'], M['brass'], M['steel'])
    parts.append(P.chain('Watch chain', dirv(a) * .35 + Vector((0, .24, 0)), c + Vector((0, .075, 0)), .0, M['brass'], links=6, radius=.005))
    parts += cog('Hip gear', dirv(.6) * .352 + Vector((0, .12, 0)), dirv(.6), .05, M['brass'], 8, M['steel'])
    return parts, {}, None


def clockwork_sleeve(M, sx):
    o, pt = P.sleeve('Sleeve', M['cloth'], M['stripe'], sx, .03, .39, lambda t, a: .09 + .01 * math.sin(math.pi * t))
    parts = o + [arm_ring('Cuff band', sx, x, .096, .013, M['brass']) for x in (.29, .385)]
    parts += pearls('Rivet', [Vector((sx * .34, .098 * math.sin(TAU * k / 8), .098 * math.cos(TAU * k / 8))) for k in range(8)], .01, M['brass'])
    parts += cog('Sleeve gear', Vector((sx * .17, 0, .1)), (0, 0, 1), .045, M['brass'], 8, M['steel'])
    return parts, {}, None


def clockwork_boot(M, sx):
    parts, sh = boot('Boot', sx, M['leather'], M['brass'], top=-.2, cuff=M['brass'], toe=M['brass'])
    parts.append(ellipsoid('Knee cap', (0, -.2, .075), (.06, .06, .035), M['brass'], (12, 7)))
    parts.append(tube('Strap', [sh(.5, k / 24) for k in range(24)], .016, M['brass'], closed=True, segs=5, flat=.45))
    for k in (-1, 0, 1):   # glowing vents down the front
        p = sh(.74, k * .045 + (0 if k >= 0 else 1)) + Vector((0, 0, .006))
        parts.append(tube('Vent', [p + Vector((0, .035, 0)), p - Vector((0, .035, 0))], .011, M['lamp'], segs=5))
    parts += pearls('Rivet', [sh(.28, k / 6) + Vector((0, 0, 0)) for k in range(6)], .012, M['brass'])
    return parts, {}, None


def sided(fn):
    return {'r': lambda M: fn(M, 1), 'l': lambda M: fn(M, -1)}


SETS = {
    'mermaid': (mermaid_mats, {'mermaid_crown': ('Hat', mermaid_crown), 'mermaid_top': ('Body Top', mermaid_top), 'mermaid_skirt': ('Body Bottom', mermaid_skirt),
                               'mermaid_sleeve': ('Arm', mermaid_sleeve), 'mermaid_boot': ('Leg', mermaid_boot)}),
    'druid': (druid_mats, {'druid_hood': ('Hat', druid_hood), 'druid_robe': ('Body Top', druid_robe), 'druid_skirt': ('Body Bottom', druid_skirt),
                           'druid_glove': ('Arm', druid_glove), 'druid_boot': ('Leg', druid_boot)}),
    'vampire': (vampire_mats, {'vampire_hair': ('Hat', vampire_hair), 'vampire_coat': ('Body Top', vampire_coat), 'vampire_breeches': ('Body Bottom', vampire_breeches),
                               'vampire_sleeve': ('Arm', vampire_sleeve), 'vampire_boot': ('Leg', vampire_boot)}),
    'diver': (diver_mats, {'diver_helmet': ('Hat', diver_helmet), 'diver_suit': ('Body Top', diver_suit), 'diver_breeches': ('Body Bottom', diver_breeches),
                           'diver_glove': ('Arm', diver_glove), 'diver_boot': ('Leg', diver_boot)}),
    'clockwork': (clockwork_mats, {'clockwork_hat': ('Hat', clockwork_hat), 'clockwork_coat': ('Body Top', clockwork_coat), 'clockwork_breeches': ('Body Bottom', clockwork_breeches),
                                   'clockwork_sleeve': ('Arm', clockwork_sleeve), 'clockwork_boot': ('Leg', clockwork_boot)}),
}
if ARGS.set not in SETS: raise SystemExit(f'Unknown set {ARGS.set}; known: {list(SETS)}')
setup, pieces = SETS[ARGS.set]
STYLES = {}; VIEWS = {}
for key, (slot, fn) in pieces.items():
    if slot in ('Arm', 'Leg'):
        for side, sx in (('r', 1), ('l', -1)):
            STYLES[f'{key}_{side}'] = (f'{slot} {"Right" if sx > 0 else "Left"}', lambda M, fn=fn, sx=sx: fn(M, sx))
            VIEWS[f'{key}_{side}'] = ((.3 if slot == 'Arm' else .6) * sx, 1., .5 if slot == 'Arm' else .2)
    else:
        STYLES[key] = (slot, fn); VIEWS[key] = (.5, 1., .3)
K.run(STYLES, VIEWS, ARGS.root / 'art' / 'cosmetics' / ARGS.set, ARGS.set, ARGS.preview, setup=setup,
      preview_views=(('front', (1.2, 3.6, 1.0)), ('back', (-1.7, -3.5, 1.1)), ('side', (3.7, -.4, .8))))
print('FABLE_COSTUME_COMPLETE', ARGS.set, flush=True)
