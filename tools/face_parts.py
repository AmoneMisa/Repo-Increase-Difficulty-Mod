"""Faces for character costumes, fitted to the native head (measured from the game's rig, nothing exported from it).

The game parents Face Top, Face Bottom, Eyewear, Ears and Hat to the head-top frame (the half that lifts when the
robot talks) and Head Bottom to the jaw frame. The native eyes are spheres of radius .106 at (+-.121, .119, .42) in
the head-top frame, so a skin shell needs two round openings there.

  face_shell   skin over the front of the head top, with eye openings and a rim hiding the cut       (Face Top)
  brow, lashes, lid, nose, upper_lip                                                               (Face Top/Bottom)
  jaw_shell, lower_lip                                                                             (Head Bottom)
"""
import math

import numpy as np
from mathutils import Vector

import cosmetic_kit as K
import costume_parts as P
from cosmetic_kit import TAU, tube

EYE_R = .106
EYE_X, EYE_Y, EYE_Z = .121, .119, .42
JAW_R = .261; JAW_TOP = .107          # head-bottom cylinder in its own frame


def eye_centre(side):
    return Vector((side * EYE_X, EYE_Y, EYE_Z))


HEAD_RAD = .258; DOME_Y = .12         # the head top: a cylinder up to DOME_Y, then a dome (slightly squarish, max .26)
AXIS_Z = .254


def head_pt(a, h, lift=0.):
    """Point on the head top: a = 0 at the face, h = height on the cylinder, continuing as arc length over the dome."""
    r = HEAD_RAD + lift
    if h <= DOME_Y: return Vector((r * math.sin(a), h, AXIS_Z + r * math.cos(a)))
    phi = min((h - DOME_Y) / HEAD_RAD, math.pi / 2)
    return Vector((r * math.cos(phi) * math.sin(a), DOME_Y + r * math.sin(phi), AXIS_Z + r * math.cos(phi) * math.cos(a)))


def project(p, lift=0.):
    """Nearest point of the (lifted) head top to p."""
    p = Vector(p); r = HEAD_RAD + lift
    if p.y <= DOME_Y:
        d = Vector((p.x, 0, p.z - AXIS_Z)).normalized()
        return Vector((d.x * r, p.y, AXIS_Z + d.z * r))
    return Vector((0, DOME_Y, AXIS_Z)) + (p - Vector((0, DOME_Y, AXIS_Z))).normalized() * r


def eye_axis(side):
    """The direction an eye's visible disc faces: straight out of the head."""
    return Vector((side * EYE_X, 0, EYE_Z - AXIS_Z)).normalized()


def eye_ring(side, clearance, lift, phi):
    """Point on the lifted head surface round an eye at angle phi (0 = outer corner, 90 deg = top), 'clearance' away
    from the eyeball's surface."""
    c = eye_centre(side)
    n = eye_axis(side); out = Vector((0, 1, 0)).cross(n).normalized() * side
    d = out * math.cos(phi) + Vector((0, 1, 0)) * math.sin(phi)
    lo, hi = .02, .30
    for _ in range(28):
        mid = (lo + hi) / 2
        if (project(c + d * mid, lift) - c).length < EYE_R + clearance: lo = mid
        else: hi = mid
    return project(c + d * hi, lift)


def face_shell(name, mat, lift=.006, a_max=2.0, h0=-.017, h1=.47, rows=56, cols=120, clearance=.004):
    """Skin over the front and sides of the head top (the hair covers the rest), open round both eyes."""
    verts = []; keep = []
    for r in range(rows + 1):
        for c in range(cols + 1):
            p = head_pt(K.lerp(-a_max, a_max, c / cols), K.lerp(h0, h1, r / rows), lift)
            verts.append(K.B(*p))
            keep.append(min((p - eye_centre(s)).length for s in (-1, 1)) > EYE_R + clearance)
    faces = []; uvs = []
    w = cols + 1
    for r in range(rows):
        for c in range(cols):
            quad = (r * w + c, r * w + c + 1, (r + 1) * w + c + 1, (r + 1) * w + c)
            if not all(keep[i] for i in quad): continue
            faces.append(quad)
            uvs.append(tuple((i % w / cols * 3, i // w / rows * 2) for i in quad))
    obs = [K.mesh_object(name, verts, faces, mat, loop_uvs=uvs, inside=K.B(0, .1, AXIS_Z))]
    for side in (-1, 1):
        obs.append(tube(name + ' eye rim', [eye_ring(side, clearance + .004, lift, TAU * k / 48) for k in range(48)], .0075, mat, closed=True, segs=6))
    return obs


def liner(name, mat, side, phi0=-25., phi1=205., radius=.0075, lift=.012, wing=.0):
    """Dark lash line round the upper part of an eye; 'wing' extends a flick past the outer corner."""
    pts = [eye_ring(side, .012, lift, math.radians(K.lerp(phi0, phi1, k / 40))) for k in range(41)]
    obs = [tube(name, pts, lambda t: radius * (.45 + .55 * math.sin(math.pi * min(1., t * 1.15 + .12))), mat, segs=6)]
    if wing:
        a = eye_ring(side, .012, lift, math.radians(8)); b = eye_ring(side, .012 + wing, lift, math.radians(24))
        obs.append(tube(name + ' wing', [a, project((a + b) / 2 + Vector((0, -.004, 0)), lift), b], lambda t: radius * (1 - .8 * t), mat, segs=6))
    return obs


def lashes(name, mat, side, angles=(28, 44, 60), length=.03, lift=.012):
    obs = []
    for deg in angles:
        phi = math.radians(deg)
        a = eye_ring(side, .012, lift, phi); b = eye_ring(side, .012 + length, lift + .01, phi - .12)
        obs.append(tube(name, [a, project((a + b) / 2, lift + .008), b], lambda t: .0045 * (1 - .75 * t), mat, segs=5))
    return obs


def brow(name, mat, side, inner=(.045, .262), outer=(.215, .245), arch=.03, radius=.0085, lift=.012):
    """An eyebrow over one eye from 'inner' to 'outer' (x, y in the head frame, x for the right side), bowed up by
    'arch' in the middle, thickest near the nose."""
    pts = []
    for k in range(17):
        t = k / 16
        x = K.lerp(inner[0], outer[0], t); y = K.lerp(inner[1], outer[1], t) + arch * math.sin(math.pi * t ** .8)
        pts.append(project(Vector((side * x, y, .5)), lift))
    return [tube(name, pts, lambda t: radius * (1 - .7 * t ** 1.5) * (.6 + .4 * min(1., t * 6)), mat, segs=6, flat=.6)]


def lid(name, mat, line_mat, side, cover=.34, tilt=.0, radius=EYE_R + .007):
    """A heavy upper eyelid: a cap over the top 'cover' of the eye's visible disc (0..1 of its height), lower at the
    outer corner by 'tilt' radians, with a lash line along its lower edge."""
    c = eye_centre(side)
    fwd = eye_axis(side)
    across = Vector((0, 1, 0)).cross(fwd).normalized() * side      # towards the outer corner
    up = Vector((0, 1, 0))
    visible = math.asin(.094 / EYE_R)                              # the disc's half-angle
    def pt(t, s):
        az = math.radians(K.lerp(-80, 80, s))                      # 0 inner corner .. 1 outer corner
        low = visible - 2 * visible * cover - tilt * (2 * s - 1)
        el = K.lerp(math.radians(80), low, t)                      # elevation above the eye's axis
        d = (fwd * math.cos(az) + across * math.sin(az)) * math.cos(el) + up * math.sin(el)
        return c + d * radius
    obs = [K.surface(name, 8, 20, pt, mat, inside=K.B(*c))]
    obs.append(tube(name + ' lash line', [pt(1, s) for s in np.linspace(0, 1, 21)], lambda t: .0055 * math.sin(math.pi * t) + .0025, line_mat, segs=6))
    return obs


def nose(name, mat, y=.045, size=(.024, .034, .026)):
    p = project(Vector((0, y, .5)), .002)
    return [K.ellipsoid(name, tuple(p), size, mat, (12, 8)),
            K.ellipsoid(name + ' tip', tuple(p + Vector((0, -size[1] * .45, size[2] * .35))), (size[0] * 1.15, size[1] * .45, size[2] * .8), mat, (12, 6))]


def upper_lip(name, mat, half=.105, y=-.004, radius=.013, bow=.007, lift=.004):
    """Upper lip on the lower edge of the head top, with a cupid's bow."""
    pts = []
    for k in range(25):
        u = 2 * k / 24 - 1
        a = u * half / (HEAD_RAD + lift)
        dip = bow * (math.exp(-(u / .16) ** 2) - .9 * math.exp(-((abs(u) - .3) / .2) ** 2))
        r = HEAD_RAD + lift
        pts.append(Vector((r * math.sin(a), y - dip - .006 * u * u, AXIS_Z + r * math.cos(a))))
    return [tube(name, pts, lambda t: radius * (math.sin(math.pi * t) ** .55) + .001, mat, segs=8, flat=1.25, up=(0, 1, 0))]


def jaw_shell(name, mat, lift=.006, y0=-.03, y1=JAW_TOP + .003):
    """Skin round the visible band of the head bottom (the jaw and neck), in the Head Bottom frame."""
    def pt(t, s):
        a = TAU * s; r = JAW_R + lift
        return Vector((r * math.sin(a), K.lerp(y0, y1, t), r * math.cos(a)))
    return [K.surface(name, 4, 72, pt, mat, wrap=True, uv=(4, .5), inside=K.B(0, .04, 0))]


def lower_lip(name, mat, half=.085, y=.084, radius=.017, lift=.004):
    pts = []
    for k in range(21):
        u = 2 * k / 20 - 1
        r = JAW_R + lift; a = u * half / r
        pts.append(Vector((r * math.sin(a), y + .008 * u * u, r * math.cos(a))))
    return [tube(name, pts, lambda t: radius * (math.sin(math.pi * t) ** .6) + .001, mat, segs=8, flat=1.2, up=(0, 1, 0))]


def shoulder_cap(name, mat, lift=.019, neck=.245):
    """Skin over the flat top of the body between the shoulders' edge and the neck (Body Top frame)."""
    top = P.PROFILE[-1]
    def pt(t, s):
        a = TAU * s; r = K.lerp(top[0] + lift, neck, t)
        return Vector((r * math.sin(a), top[1] + .004 + .012 * math.sin(math.pi * t / 2), r * math.cos(a)))
    return [K.surface(name, 3, 64, pt, mat, wrap=True, uv=(4, .3), inside=K.B(0, .2, 0))]


def hand(name, mat, sx, x=.47):
    """The rounded end of the arm (local +x, or -x on the left), covering the stub. No fingers: these are robots."""
    return [K.ellipsoid(name, (sx * (x + .035), 0, 0), (.09, .081, .081), mat, (16, 10))]


def preview_eyes():
    """Stand-ins for the native eyes at their measured place, for review renders only (never exported)."""
    white = K.material('Preview sclera', (.92, .91, .87), .35, clean=True); black = K.material('Preview pupil', (.012, .012, .014), .3, clean=True)
    m = K.frame_matrix(K.FRAMES['Hat'])
    for side in (-1, 1):
        c = eye_centre(side)
        for ob in (K.ellipsoid('Preview eye', tuple(c), (EYE_R,) * 3, white, (24, 14)),
                   K.ellipsoid('Preview pupil', (c.x, c.y, c.z + EYE_R - .012), (.036, .036, .016), black, (16, 8))):
            ob.matrix_world = m
