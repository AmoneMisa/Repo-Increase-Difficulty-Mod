"""Chel (technical key 'sunpriestess'): hair, face, bandeau, wrap skirt, bare arms and legs on the native robot rig.

Ten parts: the seven standard ones plus a face (Face Top), lips (Face Bottom) and a jaw with the lower lip
(Head Bottom). Skin, hair, lips, the pink band and the turquoise are fixed colours so the character stays
recognisable; only the white cloth takes the slot colour.
"""
import math

import numpy as np
from mathutils import Vector

import cosmetic_kit as K
import costume_parts as P
import face_parts as F

TAU = math.tau
SKIN = (.40, .185, .078)


def materials(M):
    m = dict(M)
    m['skin'] = K.material('sunpriestess warm brown skin', SKIN, .78, clean=True)
    m['hair'] = K.material('sunpriestess black hair', (.010, .009, .018), .55, clean=True)
    m['brow'] = K.material('sunpriestess brow and lash', (.008, .006, .008), .7, clean=True)
    m['lip'] = K.material('sunpriestess red lips', (.50, .035, .045), .42, clean=True)
    m['pink'] = K.material('sunpriestess pink band', (.80, .10, .42), .8, image='adventure_linen.png')
    m['stripe'] = K.material('sunpriestess red stripe', (.55, .03, .06), .8, clean=True)
    m['jewel'] = K.material('sunpriestess turquoise', (.05, .55, .47), .45, clean=True)
    return m


# ---------------------------------------------------------------- hair (Hat)
def head(M):
    hair = M['hair']
    parts, point, _ = P.hood('Hair cap', hair, hair, P.HEAD_C, Vector((.289, .416, .289)), lambda a: -8 + 72 * P.front(a) ** 1.3, peak=0, brim=.004)

    # One smooth, heavy sheet of straight hair down the back to the hips, swept behind the shoulders.
    def sheet(t, s, off=0.):
        ease = min(1., t / .32)
        half = K.lerp(2.02, 1.28, ease * ease * (3 - 2 * ease))
        a = math.pi + (2 * s - 1) * half
        top = point(math.pi + (2 * s - 1) * 2.02, 30, .012)
        r0 = math.hypot(top.x, top.z - P.HEAD_C.z)
        r = r0 + (.352 - r0) * ease ** .8 - .012 * t ** 4 + .0035 * math.sin(38 * a) * (.3 + .7 * t) + off
        y = K.lerp(top.y, -.70 + .018 * math.sin(11 * a) - .02 * abs(2 * s - 1) ** 3, t)
        return Vector((r * math.sin(a), y, P.HEAD_C.z + r * math.cos(a)))
    inside = K.B(0, -.3, P.HEAD_C.z)
    parts.append(K.surface('Long straight hair', 30, 72, sheet, hair, uv=(6, 4), inside=inside))
    parts.append(K.surface('Long straight hair under', 30, 72, lambda t, s: sheet(t, s, -.009), hair, uv=(6, 4), inside=inside, flip=True))

    # Blunt fringe straight across the forehead, ending above the eyes.
    def fringe(t, s, off=0.):
        a = -.86 + 1.72 * s
        lat = math.radians(78 - 36 * t)
        r = Vector((.303 + off, .421 + off, .303 + off))
        return Vector((r.x * math.cos(lat) * math.sin(a), r.y * math.sin(lat) - .004 * math.sin(s * 60) * t, P.HEAD_C.z + r.z * math.cos(lat) * math.cos(a)))
    parts.append(K.surface('Blunt fringe', 10, 48, fringe, hair, inside=K.B(*P.HEAD_C)))
    parts.append(K.surface('Blunt fringe under', 10, 48, lambda t, s: fringe(t, s, -.012), hair, inside=K.B(*P.HEAD_C), flip=True))

    pivots = {}
    for side, suffix in ((-1, 'l'), (1, 'r')):
        # A flat panel of hair in front of each ear, framing the face down to the jaw.
        def panel(t, s, off=0., side=side):
            a = side * K.lerp(K.lerp(1.0, 1.1, t * t), 1.48, s)
            r = K.lerp(.243, .296, min(1., t / .3) ** .6) + .003 * math.sin(s * 14) + off
            y = K.lerp(.25, -.07 - .06 * s, t)
            return Vector((r * math.sin(a), y, P.HEAD_C.z + r * math.cos(a)))
        parts.append(K.surface('Face framing hair', 12, 8, panel, hair, inside=K.B(*P.HEAD_C)))
        parts.append(K.surface('Face framing hair under', 12, 8, lambda t, s, panel=panel: panel(t, s, -.009), hair, inside=K.B(*P.HEAD_C), flip=True))
        # Large turquoise hoops.
        group = 'earring_' + suffix
        centre = Vector((side * .300, -.07, P.HEAD_C.z - .035))
        pivots[group] = K.B(side * .29, .0, P.HEAD_C.z - .035)
        def hoop(t, s, centre=centre):
            u, w = TAU * t, TAU * s
            r = .056 + .014 * math.cos(w)
            return centre + Vector((.011 * math.sin(w), r * math.cos(u), r * math.sin(u)))
        parts.append(K.surface('Turquoise hoop', 32, 10, hoop, M['jewel'], group=group, inside=K.B(*(centre + Vector((0, .056, 0))))))
        parts.append(K.tube('Hoop hook', [(side * .287, .02, P.HEAD_C.z - .035), (side * .298, -.012, P.HEAD_C.z - .035)], .006, M['jewel'], group, segs=6))
    return parts, pivots, K.B(0, .44, .254)


# ---------------------------------------------------------------- face (Face Top), lips (Face Bottom), jaw (Head Bottom)
def face(M):
    parts = F.face_shell('Face skin', M['skin'])
    parts += F.nose('Nose', M['skin'], y=.04, size=(.022, .03, .024))
    for side in (-1, 1):
        # Chel's half-lidded look: heavy upper lids, a dark lash line with a flick and high arched brows.
        parts += F.lid('Heavy eyelid', M['skin'], M['brow'], side, cover=.38, tilt=.12)
        parts += F.lashes('Lash flick', M['brow'], side, angles=(12, 26), length=.034)
        parts += F.brow('Arched brow', M['brow'], side, inner=(.05, .268), outer=(.255, .246), arch=.03, radius=.0105)
    return parts, {}, None


def lips(M):
    return F.upper_lip('Upper lip', M['lip'], half=.11, radius=.014), {}, None


def neck(M):
    parts = F.jaw_shell('Jaw and neck skin', M['skin'])
    parts += F.lower_lip('Lower lip', M['lip'], half=.09, radius=.018)
    return parts, {}, None


# ---------------------------------------------------------------- bandeau (Body Top)
def body(M):
    parts = P.body_shell('Shoulders and chest skin', M['skin'], -.03, lambda a: .384, .010)
    parts += F.shoulder_cap('Collar skin', M['skin'], lift=.010)
    parts += P.body_shell('White bandeau', M['white'], .045, lambda a: .165, .020)
    parts += P.body_shell('Pink bandeau band', M['pink'], .158, lambda a: .228 - .008 * P.front(a) ** 4, .024)
    for h, mat, r in ((.158, M['stripe'], .0045), (.045, M['white'], .006)):
        parts.append(K.tube('Bandeau edge', [P.on_body(h, TAU * k / 72, .026 if mat is M['stripe'] else .022) for k in range(72)], r, mat, closed=True, segs=6))
    parts.append(K.tube('Bandeau top edge', [P.on_body(.228 - .008 * P.front(TAU * k / 72) ** 4, TAU * k / 72, .025) for k in range(72)], .0055, M['pink'], closed=True, segs=6))
    return parts, {}, None


# ---------------------------------------------------------------- wrap skirt (Body Bottom)
HIP_R = .294
SLIT = -.62          # her left thigh


def waist(M):
    parts = []
    def midriff(t, s):
        a = TAU * s; r = HIP_R + .013
        return Vector((r * math.sin(a), K.lerp(.09, .288, t), r * math.cos(a)))
    parts.append(K.surface('Bare midriff', 5, 72, midriff, M['skin'], wrap=True, uv=(4, 1), inside=K.B(0, .2, 0)))
    near = lambda a: math.exp(-(P.wrap_angle(a - SLIT) / .42) ** 2)
    hem = lambda a: -.215 + .12 * near(a) - .02 * (1 - P.front(a))
    o, pt = P.robe('Wrap skirt', M['white'], M['white'], 0, TAU, .135, HIP_R + .024, hem, lambda t, a: HIP_R + .024 + .05 * t,
                   folds=(.006, 7, .4), rows=12, cols=72, uv=(5, 1.5), inside=K.B(0, -.05, 0))
    parts += o
    # The over-wrapped front panel with its diagonal edge, tied at the left hip.
    a0, a1 = SLIT - .08, .55
    o, over = P.robe('Wrap over panel', M['white'], M['white'], a0, a1, .14, HIP_R + .036,
                     lambda a: -.20 - .09 * (a - a0) / (a1 - a0), lambda t, a: HIP_R + .036 + .055 * t,
                     folds=(.005, 5, 1.), rows=12, cols=28, uv=(2.5, 1.5), inside=K.B(0, -.05, 0))
    parts += o
    band = [Vector(((HIP_R + .034) * math.sin(TAU * k / 72), .137 + .006 * math.sin(TAU * k / 72 * 3), (HIP_R + .034) * math.cos(TAU * k / 72))) for k in range(72)]
    parts.append(K.tube('Rolled waistband', band, .015, M['white'], closed=True, segs=8))
    knot = Vector(((HIP_R + .05) * math.sin(a0), .135, (HIP_R + .05) * math.cos(a0)))
    out = Vector((math.sin(a0), 0, math.cos(a0)))
    parts.append(K.ellipsoid('Hip knot', tuple(knot), (.03, .026, .03), M['white'], (12, 8)))
    for k, (dx, length) in enumerate(((-.03, .15), (.02, .11))):
        side = Vector((math.cos(a0), 0, -math.sin(a0)))
        tail = [knot + side * (dx * t) + out * (.012 * math.sin(math.pi * t)) + Vector((0, -length * t, 0)) for t in np.linspace(0, 1, 10)]
        parts += K.ribbon('Knot tail', tail, lambda p, i, side=side: side, .034 - .008 * k, M['white'], uv_per_m=4)
    return parts, {}, None


# ---------------------------------------------------------------- bare arms and legs
def gloves(M, side):
    parts, _ = P.sleeve('Bare arm', M['skin'], None, side, .0, .50, lambda t, a: .077)
    parts += F.hand('Hand', M['skin'], side)
    parts.append(K.ellipsoid('Shoulder', (0, 0, 0), (.082, .082, .082), M['skin'], (14, 8)))
    parts.append(K.ring('Turquoise bracelet', (side * .40, 0, 0), (1, 0, 0), .086, .017, M['jewel']))
    parts.append(K.ring('Turquoise bracelet rim', (side * .425, 0, 0), (1, 0, 0), .083, .008, M['jewel']))
    return parts, {}, None


def boots(M, side, E):
    parts = [E('Bare leg', (0, -.22, 0), (.112, .33, .11), M['skin']), E('Bare foot', (side * .01, -.51, .075), (.115, .065, .19), M['skin'])]
    return parts, {}, None
