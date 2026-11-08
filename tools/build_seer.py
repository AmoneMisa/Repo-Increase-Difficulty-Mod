"""Blender 5.2: --background --factory-startup --python tools/build_seer.py -- --root PATH [--base JSON] [--preview]

The Horned Seer set (after the user's concept: a blindfolded hooded seer in black and green with crescent horns):

  seer_hood     Hat          black hood with a green lining, two crescent bronze horns with side prongs, chains
                             swagged between horns and hood with green crystal drops, a black blindfold, a brow ornament
  seer_robe     Body Top     black wrap bodice with a deep V, a tattered green shawl, a crystal necklace, a deer-skull
                             pauldron with bronze antlers on the left shoulder
  seer_skirt    Body Bottom  floor-length black robe with a ragged hem and a front slit, a longer tattered green veil
                             behind, a wide sash with a bronze ring and hanging crystal chains
  seer_sleeve_r Arm Right    wide ragged bell sleeve, green inside, bronze cuff
  seer_sleeve_l Arm Left

Tintable: the green fabric. Fixed: black cloth, bronze, crystals, bone. Built on tools/cosmetic_kit.py + costume_parts.py.
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
from cosmetic_kit import TAU, lerp, surface, tube, ellipsoid, gem  # noqa: E402

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, required=True)
parser.add_argument('--base', type=Path)
parser.add_argument('--preview', action='store_true')
ARGS = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
ARGS.root = ARGS.root.resolve()
K.configure(ARGS.root, ARGS.base)

GREEN = (.05, .24, .14)
BLACK = (.035, .035, .04)
BRONZE = (.62, .48, .3)
CRYSTAL = (.25, 1., .55)
BONE = (.86, .82, .72)


def make_mats():
    rng = random.Random(9)
    P.cloth_texture('seer_cloth.png', rng, base=.78, weave=.05, mottle=.18)
    m = K.material
    return {
        'green': m('Seer green veil', GREEN, .55, image='seer_cloth.png', tint=True),
        'black': m('Seer black cloth', BLACK, .75, image='seer_cloth.png'),
        'bronze': m('Seer bronze', BRONZE, .32, metal=.9),
        'crystal': m('Seer crystal', CRYSTAL, .06, emit=1.1),
        'bone': m('Seer bone', BONE, .6),
    }


# ---------------------------------------------------------------- hood
HC = Vector((0, 0, .24)); HR = Vector((.315, .44, .315))


def hood_edge(a):
    return -38 + 82 * P.front(a) ** 1.6


def horn(M, sd):
    """A crescent horn rising from the hood's crown, swept out and up, tapering to a point; a short prong at its base."""
    c = Vector((sd * .17, .55, .2)); R = .27
    base_angle = math.atan2(-.2, -.12)                      # the base sits on the hood near the crown
    pts = []
    for t in np.linspace(0, 1, 22):
        ang = lerp(base_angle, math.radians(96), t) if sd > 0 else lerp(base_angle, math.radians(96), t)
        x = math.cos(ang) * R; y = math.sin(ang) * R
        pts.append(Vector((c.x + sd * x, c.y + y, c.z - .05 * math.sin(math.pi * t))))
    obs = [tube('Horn', pts, lambda s: .042 * (1 - s) ** .75 + .003, M['bronze'], segs=10)]
    root = pts[2]
    obs.append(tube('Prong', [root, root + Vector((sd * .03, .08, .07)), root + Vector((sd * .045, .14, .12))],
                    lambda s: .014 * (1 - s) + .002, M['bronze'], segs=7))
    return obs, pts


def build_hood(M):
    parts, point, pt = P.hood('Hood', M['black'], M['green'], HC, HR, hood_edge, peak=.02, brim=.06)
    parts.append(tube('Hood hem', [pt(0, k / 80, -.005) for k in range(80)], .006, M['green'], closed=True, segs=6))
    horn_pts = {}
    for sd in (1, -1):
        h, pts = horn(M, sd)
        parts += h; horn_pts[sd] = pts
        # chains swagged from the horn to the hood's side, a crystal at the bottom of each swag and one at the horn tip
        for a_t, side_lat, sag in ((.35, 30, .07), (.6, 8, .12)):
            a = horn_pts[sd][int(a_t * 21)]
            b = point(sd * math.radians(85), side_lat, .01)
            parts.append(P.chain('Chain', a, b, sag, M['bronze']))
            low = a.lerp(b, .5) - Vector((0, sag, 0))
            parts += P.drop('Crystal', low, .04, M['bronze'], M['crystal'], size=.9)
        parts += P.drop('Tip crystal', horn_pts[sd][-4] + Vector((sd * .01, 0, 0)), .16, M['bronze'], M['crystal'], size=1.1)
    # blindfold round the eyes, under the hood
    def band(t, s):
        a = lerp(-1.45, 1.45, s); y = lerp(.11, .2, t)
        la = math.asin(max(-1, min(1, y / P.HEAD_R.y)))
        r = math.cos(la)
        return P.HEAD_C + Vector((P.HEAD_R.x * r * math.sin(a), y, P.HEAD_R.z * r * math.cos(a))) * 1.0 + Vector((math.sin(a), 0, math.cos(a))) * .012
    parts.append(surface('Blindfold', 3, 40, band, M['black'], inside=K.B(*P.HEAD_C)))
    brow = point(0, hood_edge(0), .004)
    parts.append(gem('Brow ornament', tuple(brow + Vector((0, .015, .015))), (.018, .03, .008), M['bronze']))
    parts.append(gem('Brow crystal', tuple(brow + Vector((0, .012, .024))), (.008, .014, .005), M['crystal']))
    return parts, {}, K.B(*(HC + Vector((0, HR.y + .04, 0))))


# ---------------------------------------------------------------- robe top
def neck_v(a):
    a = P.wrap_angle(a)
    return .385 - .2 * math.exp(-(a / .32) ** 2)        # a deep V at the front


def build_robe(M):
    parts = P.body_shell('Robe', M['black'], -.02, neck_v, .012)
    edge = [P.on_body(neck_v(a), a, .014) for a in np.linspace(-1.1, 1.1, 30)]
    parts.append(tube('V edge', edge, .006, M['bronze'], segs=6))
    # tattered green shawl over the shoulders
    def shawl(t, s, off=0.):
        a = TAU * s; f = P.front(a)
        r = lerp(.29, .36, t) + .015 * math.sin(7 * a) * t + off
        y = lerp(.4, .2 - .1 * f + P.tatter(a, 13, .08), t) + .03 * math.sin(math.pi * t)
        return Vector((r * math.sin(a), y, r * math.cos(a)))
    parts.append(surface('Shawl', 8, 80, shawl, M['green'], wrap=True, uv=(5, 1), inside=K.B(0, .3, 0)))
    parts.append(surface('Shawl under', 8, 80, lambda t, s: shawl(t, s, -.008), M['green'], wrap=True, uv=(5, 1), inside=K.B(0, .3, 0), flip=True))
    # necklace and crystal
    neck = [P.on_body(.37 - .1 * math.cos(a * 1.4) ** 2, a, .03) for a in np.linspace(-1.1, 1.1, 22)]
    parts.append(tube('Necklace', neck, .0025, M['bronze'], segs=5))
    parts += P.drop('Necklace crystal', P.on_body(.27, 0, .03), .03, M['bronze'], M['crystal'], size=1.2)
    # deer-skull pauldron on the left shoulder
    a = -math.pi / 2 + .1
    c = P.on_body(.36, a, .06) + Vector((0, .02, 0))
    out = Vector((math.sin(a), 0, math.cos(a)))
    parts.append(ellipsoid('Skull', tuple(c), (.04, .035, .05), M['bone'], (12, 8)))
    parts.append(ellipsoid('Snout', tuple(c + out * .03 + Vector((0, -.012, .0))), (.025, .022, .03), M['bone'], (10, 6)))
    for side in (1, -1):
        base = c + Vector((0, .03, side * .03))
        beam = [base, base + Vector((0, .06, side * .04)), base + Vector((0, .12, side * .05)) - out * .01, base + Vector((0, .17, side * .02))]
        parts.append(tube('Antler', beam, lambda s: .008 * (1 - s) + .002, M['bronze'], segs=6))
        for k, at in enumerate((1, 2)):
            tine = beam[at]
            parts.append(tube('Tine', [tine, tine + Vector((0, .04, side * .03)) + out * .01], lambda s: .005 * (1 - s) + .0015, M['bronze'], segs=5))
        parts += P.drop('Skull crystal', c + Vector((0, -.03, side * .03)), .07, M['bronze'], M['crystal'], size=.8)
    return parts, {}, None


# ---------------------------------------------------------------- skirt
SLIT = math.radians(10)


def build_skirt(M):
    parts = []
    inside = K.B(0, -.15, 0)
    robe_hem = lambda a: -.57 + .04 * P.front(a) + P.tatter(a, 15, .05)
    robe_r = lambda t, a: .33 + .2 * t ** 1.2
    o, pt = P.robe('Robe', M['black'], M['black'], SLIT, TAU - SLIT, .28, .33, robe_hem, robe_r, folds=(.028, 9, .3), cols=72, inside=inside)
    parts += o
    veil_hem = lambda a: -.6 + .32 * P.front(a) ** 1.3 + P.tatter(a, 11, .12, 1.7)
    veil_r = lambda t, a: .345 + .26 * t ** 1.2
    o, vpt = P.robe('Veil', M['green'], M['green'], math.radians(40), TAU - math.radians(40), .27, .345, veil_hem, veil_r,
                    folds=(.04, 7, 1.1), cols=64, inside=inside)
    parts += o
    # wide sash with a bronze ring and hanging crystal chains
    def sash(t, s):
        a = TAU * s; r = .35
        return Vector((r * math.sin(a), lerp(.17, .29, t), r * math.cos(a)))
    parts.append(surface('Sash', 3, 64, sash, M['black'], wrap=True, inside=K.B(0, .23, 0)))
    for h in (.172, .288):
        parts.append(tube('Sash edge', [Vector((.353 * math.sin(TAU * k / 64), h, .353 * math.cos(TAU * k / 64))) for k in range(64)], .003, M['bronze'], closed=True, segs=5))
    ring_c = Vector((0, .23, .362))
    parts.append(K.ring('Sash ring', ring_c, (0, 0, 1), .035, .007, M['bronze']))
    for dx, length in ((-.04, .3), (0, .42), (.04, .24)):
        parts += P.drop('Sash chain', ring_c + Vector((dx, -.03, .004)), length, M['bronze'], M['crystal'], size=1.1)
    return parts, {}, None


# ---------------------------------------------------------------- sleeves
def build_sleeve(M, sx):
    parts = []
    radius = lambda t, a: .086 + .13 * t ** 1.8
    o, pt = P.sleeve('Bell sleeve', M['black'], M['green'], sx, .03, .48, radius, end_tatter=lambda a: P.tatter(a, 7, .09) - .03 * (1 + math.cos(a)) / 2)
    parts += o
    cuff = [pt(.1, k / 32) * 1.0 for k in range(32)]
    parts.append(tube('Bronze cuff', [Vector((p.x, p.y * 1.08, p.z * 1.08)) for p in cuff], .006, M['bronze'], closed=True, segs=6))
    return parts, {}, None


STYLES = {
    'seer_hood': ('Hat', build_hood),
    'seer_robe': ('Body Top', build_robe),
    'seer_skirt': ('Body Bottom', build_skirt),
    'seer_sleeve_r': ('Arm Right', lambda M: build_sleeve(M, 1)),
    'seer_sleeve_l': ('Arm Left', lambda M: build_sleeve(M, -1)),
}
VIEWS = {'seer_hood': (.6, 1., .3), 'seer_robe': (-.6, 1., .3), 'seer_skirt': (.6, 1., .25), 'seer_sleeve_r': (.3, 1., .5), 'seer_sleeve_l': (-.3, 1., .5)}
K.run(STYLES, VIEWS, ARGS.root / 'art' / 'cosmetics' / 'seer', 'seer', ARGS.preview, setup=make_mats,
      preview_views=(('front', (.9, 2.7, .7)), ('back', (-1.3, -2.6, .8)), ('side', (2.8, -.3, .5))))
print('SEER_COMPLETE', flush=True)
