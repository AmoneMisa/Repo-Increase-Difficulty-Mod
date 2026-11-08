"""Blender 5.2: --background --factory-startup --python tools/build_angel.py -- --root PATH [--base JSON] [--preview]

The Mossy Stone Angel set (after the user's reference: a weathered cemetery-statue angel in cracked grey stone, lace
folds, moss and tall folded feather wings):

  angel_veil         Hat          draped stone veil over the head and shoulders, a crown of moss with hanging strands
  angel_robe         Body Top     draped stone robe top with a ruffled lace collar and moss
  angel_robe_wings   Body Top     the same robe with two tall folded stone wings of overlapping feathers
  angel_skirt        Body Bottom  long draped robe to the floor with a ragged lace hem and trailing moss
  angel_sleeve_r/l   Arm R / L    wide ruffled stone sleeves with ragged ends

Tintable: the stone (pick a marble white, a dark granite...). Fixed: moss, the darker stone lining.
Built on tools/cosmetic_kit.py + costume_parts.py.
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
from cosmetic_kit import TAU, lerp, surface, tube, ellipsoid  # noqa: E402

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, required=True)
parser.add_argument('--base', type=Path)
parser.add_argument('--preview', action='store_true')
ARGS = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
ARGS.root = ARGS.root.resolve()
K.configure(ARGS.root, ARGS.base)

STONE = (.7, .7, .68)
SHADE = (.32, .32, .31)


def make_mats():
    rng = random.Random(13)
    P.stone_texture('angel_stone.png', rng)
    P.moss_texture('angel_moss.png', rng)
    m = K.material
    return {
        'stone': m('Angel stone', STONE, .85, image='angel_stone.png', tint=True),
        'shade': m('Angel stone shade', SHADE, .9, image='angel_stone.png'),
        'moss': m('Angel moss', (1, 1, 1), .95, image='angel_moss.png'),
    }


def moss_clump(M, at, size, rng, strands=0, strand_len=.12):
    at = Vector(at)
    obs = []
    for k in range(3):
        off = Vector((rng.uniform(-1, 1), rng.uniform(-.4, .6), rng.uniform(-1, 1))) * size * .6
        obs.append(ellipsoid('Moss', tuple(at + off), (size * rng.uniform(.7, 1.1), size * .55, size * rng.uniform(.7, 1.1)), M['moss'], (8, 5)))
    for k in range(strands):
        start = at + Vector((rng.uniform(-1, 1) * size, 0, rng.uniform(-1, 1) * size))
        length = strand_len * rng.uniform(.6, 1.3)
        pts = [start + Vector((.01 * math.sin(t * 7 + k), -length * t, .01 * math.cos(t * 5 + k))) for t in np.linspace(0, 1, 6)]
        obs.append(tube('Moss strand', pts, lambda s: .006 * (1 - s) + .0015, M['moss'], segs=4))
    return obs


# ---------------------------------------------------------------- veil (head frame)
HC = Vector((0, 0, .24)); HR = Vector((.3, .42, .3))


def veil_edge(a):
    return -50 + 78 * P.front(a) ** 1.4


def build_veil(M):
    rng = random.Random(1)
    parts, point, pt = P.hood('Veil', M['stone'], M['shade'], HC, HR, veil_edge, peak=.0, brim=.035)
    # soft vertical drapery folds pressed into the cloth: thin raised ridges down the sides and back
    for k in range(10):
        a = math.radians(70 + 22 * k)
        ridge = [point(a, lat, .006) for lat in np.linspace(veil_edge(a) + 4, 40, 8)]
        parts.append(tube('Fold', ridge, .007, M['stone'], segs=5))
    for k in range(9):
        a = math.radians(-70 + 17.5 * k)
        parts += moss_clump(M, point(a, 52 + 6 * math.sin(k), .02), .025, rng, strands=2 if k % 3 == 0 else 0, strand_len=.14)
    return parts, {}, K.B(*(HC + Vector((0, HR.y + .04, 0))))


# ---------------------------------------------------------------- robe top (body top), wings
def build_robe(M, wings=False):
    rng = random.Random(2)
    parts = []
    def robe(t, s, lift=.014):
        a = TAU * s; h = lerp(-.02, .385, t)
        p = P.on_body(h, a, lift + .016 * abs(math.sin(7 * a + 2 * h)) * (1 - t * .6) + .005 * P.wrinkle(a, 1 - t))   # carved drapery folds
        return p
    parts.append(surface('Robe', 14, 96, robe, M['stone'], wrap=True, uv=(4, 1.2), inside=K.B(0, .2, 0)))
    parts += P.ruff('Lace collar', M['stone'], .37, .26, .37, pleats=16, layers=2, rise=-.02, droop=.03)
    for a, h in ((.4, .3), (-.5, .2), (1.6, .12), (-2.4, .3), (2.8, .08)):
        parts += moss_clump(M, P.on_body(h, a, .03), .022, rng, strands=1, strand_len=.1)
    pivots = {}
    if wings:
        parts += build_wings(M, rng)
    return parts, pivots, None


def feather(M, root, down, across, back, length, width, rachis=True, round_tip=False):
    """A carved stone feather: a vane widest past its middle, a rounded (coverts) or pointed (flight feathers) tip,
    cupped across its width, with a raised quill down the centre."""
    def pt(t, s, off=0.):
        shape = math.sin(math.pi * min(1., t ** .8 * (1. if round_tip else 1.04))) ** (.45 if round_tip else .8)
        half = width * shape * (1 - .25 * t)
        cup = .014 * (1 - (2 * s - 1) ** 2)                       # the vane curls back from the quill
        return root + down * (length * t) + across * (half * (2 * s - 1)) + back * (.01 * math.sin(math.pi * t) - cup + off)
    obs = [surface('Feather', 7, 4, pt, M['stone'], uv=(.6, 1.2), inside=K.B(*(root - back * .2))),
           surface('Feather under', 7, 4, lambda t, s: pt(t, s, .006), M['shade'], uv=(.6, 1.2), inside=K.B(*(root - back * .2)), flip=True, hem=0)]
    if rachis:
        obs.append(tube('Quill', [pt(t, .5, -.003) for t in np.linspace(0, .92, 6)], lambda u: .0055 * (1 - u) + .0015, M['stone'], segs=5))
    return obs


def build_wings(M, rng):
    """Two tall folded wings: rows of overlapping feathers hanging from a frame behind the shoulders, rising above the
    head and falling to the knees."""
    obs = []
    for sd in (1, -1):
        # a solid stone backing in the wing's outline, so the feathers read as one wing
        def panel(t, s, sd=sd):
            w = (math.sin(math.pi * min(1., .06 + t * 1.25)) * .9 + .06) * (1 - .75 * t ** 3)
            u = s * w
            return Vector((sd * (.1 + .3 * u), lerp(.9, -.5, t), -.335 - .12 * u - .05 * t))
        obs.append(surface('Wing panel', 12, 6, panel, M['shade'], inside=K.B(0, .2, 0)))
        obs.append(surface('Wing panel back', 12, 6, lambda t, s, panel=panel: panel(t, s) + Vector((0, 0, .004)), M['shade'], inside=K.B(0, .2, -1.), ))
        # Real wing structure, top to bottom: short rounded coverts in rows, then medium secondaries, then the long
        # narrow primaries falling to the knees. Each row overlaps the one below and sits a little further out.
        tiers = ((.88, 3, 4, .12, .042, True), (.8, 4, 4, .13, .044, True), (.72, 5, 5, .15, .046, True),
                 (.6, 5, 5, .3, .05, False), (.42, 6, 6, .36, .05, False),
                 (.22, 6, 7, .52, .046, False), (.02, 5, 6, .6, .044, False))
        for r, (y, cols_lo, cols_hi, length, width, coverts) in enumerate(tiers):
            v = r / (len(tiers) - 1)
            width_frac = math.sin(math.pi * min(1., .12 + v * 1.0)) * .88 + .12
            cols = cols_hi                                      # the same on both wings
            for c in range(cols):
                u = (c + .5) / cols * width_frac
                x = sd * (.1 + .3 * u)
                z = -.34 - .12 * u - .05 * v - .012 * r
                root = Vector((x, y, z))
                out = Vector((sd * .25, 0, -1)).normalized()
                fan = (u - .5) * (.25 if not coverts else .1)              # the flight feathers fan out a little
                down = Vector((sd * (.05 * u + fan), -1, -.08)).normalized()
                across = down.cross(out).normalized() * sd
                obs += feather(M, root, down, across, out, length * rng.uniform(.92, 1.08), width, rachis=not coverts, round_tip=coverts)
        # the wing's bony leading edge, and moss caught on its top
        edge = [Vector((sd * (.1 + .3 * math.sin(math.pi * min(1., .25 + v * 1.1)) * .9), lerp(.9, -.05, v), -.33 - .05 * v)) for v in np.linspace(0, .5, 8)]
        obs.append(tube('Wing bone', edge, lambda s: .03 * (1 - s * .5), M['stone'], segs=8))
        obs += moss_clump(M, edge[1] + Vector((0, .02, -.02)), .03, rng, strands=2, strand_len=.2)
    return obs


# ---------------------------------------------------------------- skirt
def build_skirt(M):
    rng = random.Random(3)
    parts = []
    hem = lambda a: -.6 + .03 * P.front(a) + P.tatter(a, 17, .07, .3)
    radius = lambda t, a: .33 + .27 * t ** 1.2
    o, pt = P.robe('Robe skirt', M['stone'], M['shade'], 0, TAU, .28, .33, hem, radius, folds=(.04, 11, .2), cols=96, uv=(6, 2.5), inside=K.B(0, -.2, 0))
    parts += o
    over_hem = lambda a: -.42 + .1 * P.front(a) + P.tatter(a, 9, .12, 2.)
    o, opt = P.robe('Lace overlay', M['stone'], M['shade'], .4, TAU - .4, .26, .34, over_hem, lambda t, a: .345 + .3 * t ** 1.1,
                    folds=(.05, 7, 1.), cols=64, uv=(5, 2), inside=K.B(0, -.2, 0))
    parts += o
    for k in range(10):
        s = (k + .5) / 10
        parts += moss_clump(M, pt(rng.uniform(.1, .5), s, .02), .025, rng, strands=2, strand_len=.25)
    return parts, {}, None


# ---------------------------------------------------------------- sleeves
def build_sleeve(M, sx):
    parts = []
    radius = lambda t, a: .088 + .1 * t ** 1.5 + .012 * math.sin(9 * a) * t
    o, pt = P.sleeve('Sleeve', M['stone'], M['shade'], sx, .03, .44, radius, uv=(2, 2), end_tatter=lambda a: P.tatter(a, 8, .07))
    parts += o
    for x in (.2, .32):
        parts.append(tube('Ruffle', [Vector((sx * x, (radius(x / .44, a) + .008) * math.sin(a), (radius(x / .44, a) + .008) * math.cos(a))) for a in np.linspace(0, TAU, 33)[:-1]],
                          .007, M['stone'], closed=True, segs=6))
    return parts, {}, None


STYLES = {
    'angel_veil': ('Hat', build_veil),
    'angel_robe': ('Body Top', lambda M: build_robe(M)),
    'angel_robe_wings': ('Body Top', lambda M: build_robe(M, wings=True)),
    'angel_skirt': ('Body Bottom', build_skirt),
    'angel_sleeve_r': ('Arm Right', lambda M: build_sleeve(M, 1)),
    'angel_sleeve_l': ('Arm Left', lambda M: build_sleeve(M, -1)),
}
VIEWS = {'angel_veil': (.6, 1., .3), 'angel_robe': (.6, 1., .3), 'angel_robe_wings': (-.5, -1., .3), 'angel_skirt': (.5, 1., .2),
         'angel_sleeve_r': (.3, 1., .5), 'angel_sleeve_l': (-.3, 1., .5)}

built = K.run(STYLES, VIEWS, ARGS.root / 'art' / 'cosmetics' / 'angel', 'angel', False, setup=make_mats)
if ARGS.preview:
    import bpy
    for ob in built['angel_robe'][1]: ob.hide_render = True
    for key, (slot, parts) in built.items():
        if key == 'angel_robe': continue
        for ob in parts: ob.hide_render = False; ob.matrix_world = K.frame_matrix(K.FRAMES[slot])
    K.stand_in_body()
    scene = bpy.context.scene; cam = scene.camera
    scene.render.film_transparent = False; scene.render.resolution_x = 760; scene.render.resolution_y = 900
    art = ARGS.root / 'art' / 'cosmetics' / 'angel'; art.mkdir(parents=True, exist_ok=True)
    target = Vector((0, -.05, .3))
    for suffix, eye in (('front', (.9, 2.9, .7)), ('back', (-1.3, -2.8, .8)), ('side', (3., -.3, .5))):
        K.look(cam, target + Vector(eye), target); K.render(art / ('angel_' + suffix + '.png'))
print('ANGEL_COMPLETE', flush=True)
