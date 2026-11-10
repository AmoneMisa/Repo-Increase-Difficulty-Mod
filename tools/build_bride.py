"""Blender 5.2: --background --factory-startup --python tools/build_bride.py -- --root PATH [--base JSON] [--preview]

The Ghost Bride set: an original haunted-bride outfit in pale ivory lace with a faint blue chill.

  bride_veil     Hat          crown of blue roses and small white blossoms, a light gathered tulle veil to mid-back
  bride_corset   Body Top     strapless floral-lace bodice with a pointed front, boning, blue roses at the neckline,
                              and a satin sash tied in a bow at the back
  bride_skirt    Body Bottom  ball-gown skirt in three ruffled lace tiers, a bustle behind and a satin underskirt
  bride_glove_r  Arm Right    slim blue arm and hand under a torn lace cap sleeve
  bride_glove_l  Arm Left     the skeleton arm: humerus, radius and ulna, a bony wrist
  bride_leg_r    Leg Right    the skeleton leg in a white ankle boot
  bride_leg_l    Leg Left     blue leg in a white ankle boot
  bride_face     Face Top     blue skin round the native eyes, thin sad brows, long lashes, a small nose
  bride_lips     Face Bottom  small rose upper lip
  bride_neck     Head Bottom  blue jaw and neck with the lower lip

Tintable: the lace, tulle and satin. Fixed: skin, bone, hair, roses, leaves, the face. Built on tools/cosmetic_kit.py + costume_parts.py.
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
import face_parts as F  # noqa: E402
from cosmetic_hair import hair
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
        'hair': m('Bride midnight blue hair', (.008,.018,.13), .72, clean=True),
        'satin': m('Bride satin', SATIN, .65, clean=True, tint=True),
        'skin': m('Bride blue skin', (.17,.44,.62), .8, clean=True),
        'brow': m('Bride navy brow and lash', (.012,.02,.09), .7, clean=True),
        'lip': m('Bride rose lips', (.52,.13,.26), .5, clean=True),
        'bone': m('Bride ivory bone', (.78,.72,.55), .85, clean=True),
        'shadow': m('Bride rib opening', (.009,.025,.04), .92, clean=True),
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
    parts = hair(M['hair'], .58, wave=.036)
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
    parts.append(surface('Bodice', 16, 96, bodice, M['satin'], wrap=True, uv=(9, 2.4), inside=K.B(0, .2, 0)))
    # bare blue shoulders and collar above the strapless neckline
    parts += P.body_shell('Blue shoulders', M['skin'], .2, lambda a: .384, .007)
    parts += F.shoulder_cap('Blue collar', M['skin'], lift=.007)
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
    # The torn left side of the corset exposes curved ivory ribs.
    parts.append(surface('Torn corset opening',8,32,lambda t,s:P.on_body(.14+.09*t*math.sin(TAU*s),-.55+.18*t*math.cos(TAU*s),.031),M['shadow'],wrap=True))
    for j in range(4):
        path=[P.on_body(.20-j*.035-.012*math.sin(t*math.pi),-.73+.34*t,.041) for t in np.linspace(0,1,15)]
        parts.append(tube('Exposed ivory rib',path,.009,M['bone'],segs=6))
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
    # One flowing split gown, a fitted hip and a longer train behind; no stacked tiers.
    a0,a1=.56,TAU+.02          # open over the right leg, where the bones show
    def radius(t,a):return .33+.24*t**1.6+.07*(1-P.front(a))*t*t
    hem=lambda a:-.57-.015*(1-P.front(a))+.009*math.sin(7*a)
    o,pt=P.robe('Split bridal gown',M['satin'],M['lace'],a0,a1,.28,.33,hem,radius,
        folds=(.018,9,.3),rows=20,cols=96,uv=(5,3),inside=inside)
    parts+=o
    # A satin slip over the hips, so the open front shows legs and not the robot's body.
    def slip(t,s):
        a=TAU*s;r=.303*min(1.,(1.6-t)/.6) if t>1 else .303
        return Vector((r*math.sin(a),lerp(.286,-.03,min(t,1.)),r*math.cos(a)))
    parts.append(surface('Satin slip',8,64,lambda t,s:slip(t*1.6,s),M['satin'],wrap=True,uv=(4,1),inside=K.B(0,.1,0)))
    # Lace confined to the trailing hem, rather than covering the whole dress in a repeated motif.
    parts.append(surface('Lace train border',5,96,lambda t,s:pt(.82+.18*t,s,.004),M['lace'],uv=(10,1),inside=inside))
    return parts, {}, None


# ---------------------------------------------------------------- arms
def lace_cap(M, sx):
    """What is left of a lace sleeve: a short torn cap over the shoulder."""
    o, _ = P.sleeve('Torn lace cap sleeve', M['lace'], M['lace'], sx, -.02, .13, lambda t, a: .093 + .012 * t, uv=(3, 1), end_tatter=lambda a: P.tatter(a, 7, .06, .3))
    return o


def build_glove(M, sx):
    parts = lace_cap(M, sx)
    if sx > 0:
        o, _ = P.sleeve('Blue arm', M['skin'], None, sx, .0, .50, lambda t, a: .0765)
        parts += o
        parts.append(ellipsoid('Blue shoulder', (0, 0, 0), (.08, .08, .08), M['skin'], (14, 8)))
        parts += F.hand('Blue hand', M['skin'], sx)
        return parts, {}, None
    # The skeleton arm. The native arm stays inside: a dark sleeve reads as the gap between the bones.
    o, _ = P.sleeve('Hollow of the arm', M['shadow'], None, sx, .0, .52, lambda t, a: .0735)
    parts += o
    parts.append(ellipsoid('Shoulder joint', (0, 0, 0), (.088, .088, .088), M['bone'], (14, 8)))
    # humerus: one stout bone with flared ends
    o, _ = P.sleeve('Humerus', M['bone'], None, sx, .03, .235, lambda t, a: .0775 + .013 * (abs(2 * t - 1) ** 2.5), uv=(2, 1))
    parts += o
    for z in (-.035, .035):
        parts.append(ellipsoid('Elbow condyle', (sx * .245, 0, z), (.04, .062, .05), M['bone'], (12, 7)))
        # radius and ulna side by side, the dark hollow showing between them
        path = [Vector((sx * x, 0, z * (1.1 + .12 * math.sin(math.pi * (x - .26) / .22)))) for x in np.linspace(.26, .48, 12)]
        parts.append(tube('Forearm bone', path, lambda t: .054 + .008 * abs(2 * t - 1) ** 2, M['bone'], segs=12, up=(0, 1, 0)))
    parts.append(ellipsoid('Bony wrist', (sx * .515, 0, 0), (.068, .078, .084), M['bone'], (14, 8)))   # no finger bones: robots have none
    return parts, {}, None


# ---------------------------------------------------------------- legs
def boot(M, sx):
    """A white ankle boot with a small heel at the foot of the leg."""
    def shaft(t, s):
        a = TAU * s; r = lerp(.074, .066, t)
        return Vector((r * math.sin(a), lerp(-.37, -.5, t), r * math.cos(a) * 1.05))
    parts = [surface('Boot shaft', 6, 24, shaft, M['satin'], wrap=True, inside=K.B(0, -.43, 0)),
             tube('Boot cuff', [shaft(0, k / 24) for k in range(24)], .009, M['satin'], closed=True, segs=6),
             ellipsoid('Boot foot', (sx * .008, -.515, .065), (.078, .05, .165), M['satin'], (16, 10)),
             ellipsoid('Boot heel', (sx * .008, -.53, -.05), (.045, .04, .045), M['satin'], (10, 6))]
    return parts


def build_leg(M, sx):
    parts = boot(M, sx)
    if sx < 0:
        parts.append(ellipsoid('Blue leg', (0, -.16, 0), (.113, .30, .112), M['skin'], (20, 12)))
        return parts, {}, None
    # The skeleton leg: thigh bone, knee, shin and calf bones down into the boot; the native leg hides in the dark.
    def hollow(t, s):
        a = TAU * s; y = lerp(.11, -.40, t); r = P.leg_r(y) + .003
        return Vector((r * math.sin(a), y, r * math.cos(a)))
    parts.append(surface('Hollow of the leg', 10, 24, hollow, M['shadow'], wrap=True, inside=K.B(0, -.1, 0)))
    def femur(t, s):
        a = TAU * s; y = lerp(.11, -.19, t); r = P.leg_r(y) + .008 + .014 * abs(2 * t - 1) ** 2.5
        return Vector((r * math.sin(a), y, r * math.cos(a)))
    parts.append(surface('Thigh bone', 12, 28, femur, M['bone'], wrap=True, uv=(2, 1), inside=K.B(0, -.05, 0)))
    for x in (-.04, .04):
        parts.append(ellipsoid('Knee condyle', (x, -.215, .0), (.05, .045, .075), M['bone'], (12, 7)))
        path = [Vector((x * lerp(.95, .55, u), lerp(-.24, -.40, u), .01 * math.sin(math.pi * u))) for u in np.linspace(0, 1, 8)]
        parts.append(tube('Shin bone', path, lambda t: .036 - .012 * math.sin(math.pi * t), M['bone'], segs=10))
    parts.append(ellipsoid('Kneecap', (0, -.215, .078), (.036, .04, .022), M['bone'], (10, 6)))
    return parts, {}, None


# ---------------------------------------------------------------- face (Face Top), lips (Face Bottom), jaw (Head Bottom)
def build_face(M):
    parts = F.face_shell('Blue face', M['skin'])
    parts += F.nose('Small nose', M['skin'], y=.045, size=(.016, .024, .018))
    for side in (-1, 1):
        parts += F.liner('Lash line', M['brow'], side, phi0=-20, phi1=200, radius=.006)
        parts += F.lashes('Long lash', M['brow'], side, angles=(26, 42, 58), length=.04)
        # thin brows, raised at the inner ends: her worried, hopeful look
        parts += F.brow('Thin sad brow', M['brow'], side, inner=(.06, .30), outer=(.24, .268), arch=.016, radius=.0075)
    return parts, {}, None


def build_lips(M):
    return F.upper_lip('Small upper lip', M['lip'], half=.062, radius=.0105, bow=.005), {}, None


def build_neck(M):
    parts = F.jaw_shell('Blue jaw and neck', M['skin'])
    parts += F.lower_lip('Small lower lip', M['lip'], half=.05, radius=.0125)
    return parts, {}, None


STYLES = {
    'bride_veil': ('Hat', build_veil),
    'bride_corset': ('Body Top', build_corset),
    'bride_skirt': ('Body Bottom', build_skirt),
    'bride_glove_r': ('Arm Right', lambda M: build_glove(M, 1)),
    'bride_glove_l': ('Arm Left', lambda M: build_glove(M, -1)),
    'bride_leg_r': ('Leg Right', lambda M: build_leg(M, 1)),
    'bride_leg_l': ('Leg Left', lambda M: build_leg(M, -1)),
    'bride_face': ('Face Top', build_face),
    'bride_lips': ('Face Bottom', build_lips),
    'bride_neck': ('Head Bottom', build_neck),
}
VIEWS = {'bride_veil': (-.6, -1., .35), 'bride_corset': (.5, 1., .3), 'bride_skirt': (.5, 1., .2), 'bride_glove_r': (.3, 1., .5), 'bride_glove_l': (-.3, 1., .5),
         'bride_leg_r': (.5, 1., .2), 'bride_leg_l': (-.5, 1., .2), 'bride_face': (.2, .3, 1.), 'bride_lips': (0, .2, 1.), 'bride_neck': (.2, .5, 1.)}
_body = K.stand_in_body
K.stand_in_body = lambda: (_body(), F.preview_eyes())     # review renders show the native eyes too
K.run(STYLES, VIEWS, ARGS.root / 'art' / 'cosmetics' / 'bride', 'bride', ARGS.preview, setup=make_mats,
      preview_views=(('front', (.9, 2.7, .7)), ('back', (-1.3, -2.6, .8)), ('side', (2.8, -.3, .5)), ('left', (-2.6, 1.2, .5))))
print('BRIDE_COMPLETE', flush=True)
