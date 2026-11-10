"""Faces for the mascot costumes: fur or skin round the native eyes with the character's muzzle, beak, teeth or nose
below them (Face Top), and a matching jaw (Head Bottom). Before these, the hoods carried the nose and beak on the
forehead, above the robot's eyes, and the face itself stayed bare.

Head-top frame: eyes at (+-.121, .119, .42), radius .106; the front of the head is z = .512; its lower edge y = -.019.
Jaw frame: the visible band is y = .025 .. .107 at radius .261.
"""
import math

import numpy as np
from mathutils import Vector

import cosmetic_kit as K
import face_parts as F

TAU = math.tau
KINDS = ('rabbid', 'musketeer', 'bombbird', 'talkingdog', 'ogre')
FRONT = F.AXIS_Z + F.HEAD_RAD


def E(name, pos, size, mat, segs=(16, 10)):
    return K.ellipsoid(name, pos, size, mat, segs)


def cone(name, a, b, r0, mat, flat=1., segs=12):
    a, b = Vector(a), Vector(b)
    return K.tube(name, [a.lerp(b, t) for t in np.linspace(0, 1, 7)], lambda t: r0 * (1 - t) ** .8 + .004, mat, segs=segs, flat=flat, up=(1, 0, 0))


def face(M, kind):
    parts = F.face_shell('Face', M['main'])
    if kind == 'rabbid':
        # Two big buck teeth hang from the upper lip over the mouth and lift with the head when it screams.
        for s in (-1, 1):
            verts = [K.B(s * x, y, z) for x, y, z in ((.006, .012, FRONT - .012), (.062, .012, FRONT - .012), (.062, -.072, FRONT - .004), (.006, -.072, FRONT - .004),
                                                    (.006, .012, FRONT + .02), (.062, .012, FRONT + .02), (.058, -.078, FRONT + .024), (.010, -.078, FRONT + .024))]
            parts.append(K.mesh_object('Buck tooth', verts, [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (3, 7, 6, 2), (0, 4, 7, 3), (1, 2, 6, 5)], M['white']))
        parts.append(K.tube('Upper gum', [F.head_pt(a, .008, .008) for a in np.linspace(-.42, .42, 17)], lambda t: .011 * math.sin(math.pi * t) + .003, M['pink'], segs=6))
    elif kind == 'musketeer':
        for s in (-1, 1):
            parts.append(E('Muzzle puff', (s * .046, .008, FRONT - .012), (.062, .04, .04), M['light']))
            for k in range(3):
                y = .0 + k * .022
                parts.append(K.tube('Whisker', [(s * .095, y, FRONT + .01), (s * .19, y + .012 * (k - 1), FRONT - .005), (s * .285, y + .03 * (k - 1), FRONT - .06)], .003, M['light'], segs=5))
        parts.append(E('Pink nose', (0, .04, FRONT + .016), (.024, .017, .016), M['pink']))
    elif kind == 'bombbird':
        parts.append(cone('Upper beak', (0, .028, FRONT - .03), (0, -.004, FRONT + .13), .05, M['gold'], flat=1.7))
        for s in (-1, 1):
            # heavy brows slanting down to the beak: the angry look
            pts = [F.project(Vector((s * K.lerp(.03, .27, t), K.lerp(.232, .345, t), .5)), .016) for t in np.linspace(0, 1, 9)]
            parts.append(K.tube('Angry brow', pts, lambda t: .026 * (1 - .45 * t), M['accent'], segs=8, flat=.55))
    elif kind == 'talkingdog':
        parts.append(E('Muzzle', (0, .008, FRONT - .005), (.098, .042, .062), M['light']))
        parts.append(E('Big dog nose', (0, .034, FRONT + .052), (.046, .032, .028), M['dark']))
    elif kind == 'ogre':
        parts.append(E('Broad nose', (0, .034, FRONT + .008), (.05, .038, .04), M['main']))
        for s in (-1, 1):
            parts.append(E('Nostril', (s * .022, .018, FRONT + .034), (.012, .009, .008), M['dark']))
            parts += F.brow('Heavy brow', M['leather'], s, inner=(.045, .272), outer=(.245, .258), arch=.022, radius=.014)
    return parts, {}, None


def neck(M, kind):
    parts = F.jaw_shell('Jaw', M['main'])      # the main fur or skin, so the jaw takes the slot colour with the face
    r = F.JAW_R + .006
    if kind == 'rabbid':
        parts.append(E('Open mouth', (0, .062, r - .004), (.105, .04, .012), M['dark']))
    elif kind == 'musketeer':
        parts.append(E('Light chin', (0, .07, r - .008), (.085, .034, .018), M['light']))
    elif kind == 'bombbird':
        parts.append(cone('Lower beak', (0, .088, r - .03), (0, .098, r + .085), .03, M['gold'], flat=2.2))
    elif kind == 'talkingdog':
        parts.append(E('Light chin', (0, .07, r - .01), (.10, .036, .02), M['light']))
        parts.append(E('Tongue', (.012, .056, r + .008), (.036, .05, .011), M['pink']))
    elif kind == 'ogre':
        parts.append(K.tube('Wide mouth', [Vector((r * math.sin(a), .088 - .012 * math.cos(a * 3.2), r * math.cos(a))) for a in np.linspace(-.5, .5, 21)],
                            lambda t: .007 * math.sin(math.pi * t) + .002, M['dark'], segs=6))
    return parts, {}, None


def styles(kind):
    return {kind + '_face': ('Face Top', lambda m, k=kind: face(m, k)), kind + '_neck': ('Head Bottom', lambda m, k=kind: neck(m, k))}
