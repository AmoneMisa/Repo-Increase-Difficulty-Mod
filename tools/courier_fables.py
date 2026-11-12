"""Four more couriers: mermaid, skeleton cat, stone elemental and phoenix. Called by build_courier.py.

Recolour slots:
  mermaid    main = scales and tail, second = skin and fins, accent = hair and gear
  skeleton   main = bones, second = neckerchief, accent = goggles and gear
  elemental  main = stone, second = energy (eyes, core; glows), accent = runes (glow) and gear
  phoenix    main = feathers, second = belly, accent = crest, flight and tail plumes; the flame glow keeps its colour
Parts tagged with lit() are exported as "glow" surfaces: the game lights them from within.
"""
import math, random

import bpy
from mathutils import Matrix, Vector


def install(G):
    E, orb, loft, tube, cone, box, ring, leaf, tuft = (G[n] for n in ('ellipsoid', 'orb', 'loft', 'tube', 'cone', 'box', 'ring', 'leaf', 'tuft'))
    frame, eyes, smile, gear, arm, foot, bird_wing, bird_foot = (G[n] for n in (
        'frame', 'eyes', 'smile', 'gear', 'arm', 'foot', 'bird_wing', 'bird_foot'))
    front, back, right, body_parts, add, spline = (G[n] for n in ('front', 'back', 'right', 'body_parts', 'add', 'spline'))
    F, PIV, PARTS, BVH = G['F'], G['PIV'], G['PARTS'], G['_BVH']
    PINK, EYE_Z = G['PINK'], G['EYE_Z']
    W = (1.0, 1.0, 1.0)
    SIDES = ((1, 'l'), (-1, 'r'))

    def retag(o, channel, color, pat=None):
        """Moves a part made by a shared helper to another recolour slot (before anything measures against it)."""
        group, _, label = o.name.split('|', 2)
        o.name = f'{group}|{channel}|{label}'; o['ihm_color'] = list(color); o['ihm_pat'] = pat or ''
        return o

    def lit(o):
        o['ihm_glow'] = 1
        return o

    def rock(group, channel, color, label, loc, radii, seed=0, rot=(0, 0, 0), pat=None, detail=1):
        """A faceted chunk of stone."""
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=detail, radius=1, location=(0, 0, 0))
        o = bpy.context.object; rng = random.Random(seed)
        for v in o.data.vertices: v.co *= 1 + rng.uniform(-.17, .1)
        o.scale = radii; o.rotation_euler = rot; o.location = loc
        add(o, group, channel, color, label, pat)
        for poly in o.data.polygons: poly.use_smooth = False
        return o

    # ------------------------------------------------------------------ mermaid

    def mermaid():
        scales, skin, fin, hair, pink = (.1, .62, .66), (1.0, .8, .7), (.78, .5, .95), (.8, .16, .5), (.98, .5, .72)
        pearl, lilac, gold = (.99, .96, .9), (.8, .66, .98), (1.0, .74, .16)
        h, b = frame(skin, head=(.2, .178, .172), body=(.15, .138, .16), cheek=.12, pear=.1, pat='skin')
        retag(h, 'second', skin, 'skin'); retag(b, 'second', skin, 'skin')
        head = F['head']; top = F['top']; hx = F['hx']
        # A fish tail and no legs at all: scaled hips she sits on, the tail sweeping round her left side to the front.
        orb('body', 'main', scales, 'hips', (0, -.005, .121), (.166, .154, .118), 28, 14, pat='mscales')
        for k in range(10):   # a frill of little fins round the waist
            a = math.tau * (k + .5) / 10; sx, cy = math.sin(a), math.cos(a)
            leaf('body', 'second', fin, 'waist fin', (sx * .13, cy * .12, .215), (sx * .2, cy * .185, .15), .045, normal=(sx, cy, .9),
                 camber=.01, thick=.005, rows=5, cols=4, pat='fin')
        by = back(0, .07, [F['body']])
        path = [(.0, by + .07, .08), (.17, by + .03, .066), (.265, -.02, .07), (.27, .1, .08), (.2, .185, .1)]
        tube('tail', 'main', scales, 'tail', path, .1, .03, segs=14, per=5, pat='mscales')
        end = Vector(path[-1])
        for k, (dx, dy, dz) in enumerate(((.075, .09, .1), (-.07, .075, .11))):
            leaf('tail', 'second', fin, f'fluke{k}', end - Vector((0, .012, .012)), end + Vector((dx, dy, dz)), .062, normal=(0, -.75, 1),
                 camber=.012, thick=.006, rows=8, cols=4, prof=lambda t: math.sin(math.pi * min(1, t * .8 + .1)) ** .7 * (1 - t ** 5), pat='fin')
        PIV['tail'] = (.0, by + .07, .08)
        for s, side in SIDES:
            arm(s, skin, channel='second', pat='skin')
            g = 'arm_' + side; sx = right(.02, .235, [F['body']], F['bx'])
            leaf(g, 'second', fin, 'arm fin', (s * (sx + .02), .03, .2), (s * (sx + .1), -.02, .15), .034, normal=(0, 1, .3),
                 camber=.008, thick=.005, rows=5, cols=4, pat='fin')
            g = 'ear_' + side; x = right(-.01, .42, [head])
            leaf(g, 'second', fin, 'ear fin', (s * (x - .03), -.01, .4), (s * (x + .1), -.04, .475), .05, camber=.012, thick=.007, rows=6, cols=4,
                 prof=lambda t: (.55 + .45 * math.sin(math.pi * min(1, t * 1.2))) * (1 - t ** 3), pat='fin')
            PIV[g] = (s * (x - .01), -.01, .41)
        eyes(.074, .425, .058, iris=(.14, .6, .72)); smile(.33, .034)
        # Long wavy hair with a pearl string, a starfish and a scallop crown.
        cap = E('body', 'accent', hair, 'hair cap', (0, -.02, top - .04), (hx * .99, F['hy'] * .97, .094), segs=22, rings=12, pat='hair')
        E('body', 'accent', hair, 'back hair', (0, -.1, .43), (.178, .1, .125), segs=18, rings=10, pat='hair')
        for s, side in SIDES:
            x = right(0, .4, [head])
            tube('body', 'accent', hair, 'side lock', [(s * .16, .02, .52), (s * (x + .03), .0, .4), (s * (x + .0), -.03, .29), (s * (x + .05), -.03, .2)],
                 .052, .018, segs=10, per=4, pat='hair')
            tube('body', 'accent', hair, 'curl', [(s * .13, -.1, .47), (s * .2, -.13, .36), (s * .15, -.15, .28)], .045, .016, segs=10, per=4, pat='hair')
        for k in (-2, -1, 0, 1, 2):
            x = k * .045
            tuft('body', 'accent', hair, 'fringe', (x, front(x, .525, [head]) - .02, .537), (x * 1.15 - .014, front(x, .485, [head]) + .01, .48 + abs(k) * .006), .03, pat='hair')
        for k in range(-3, 4):
            x = k * .042; z = .522 - abs(k) * .006
            E('body', 'fixed', pearl, 'pearl', (x, front(x, z, [cap, head]) + .006, z), (.012, .012, .012), segs=10, rings=6)
        cx, cz = .118, .55
        cy = front(cx, cz, [cap, head], .1) + .01
        for k in range(5):
            a = math.tau * k / 5 + math.pi / 2
            cone('body', 'fixed', gold, f'starfish arm{k}', (cx, cy, cz), (cx + math.cos(a) * .05, cy - .004, cz + math.sin(a) * .05), .017, verts=8)
        E('body', 'fixed', gold, 'starfish', (cx, cy + .004, cz), (.02, .012, .02), segs=10, rings=6)
        for k, a in enumerate((-.95, -.48, 0, .48, .95)):
            leaf('body', 'fixed', lilac, f'shell rib{k}', (0, .01, top - .005), (math.sin(a) * .075, .012, top - .005 + math.cos(a) * .082), .024,
                 normal=(0, 1, 0), camber=.012, thick=.009, rows=6, cols=4, prof=lambda t: (.35 + .65 * math.sin(math.pi * min(1, t * .7 + .2))) * (1 - t ** 6))
        E('body', 'fixed', pearl, 'crown pearl', (0, .03, top + .005), (.018, .018, .018), segs=10, rings=6)
        gear(accent=pink, with_cap=False)

    # ------------------------------------------------------------------ skeleton cat

    def skeleton():
        """A skeleton cat on all fours. Front legs are the arm groups, hind legs the foot groups; the pack rides on its back."""
        bone, cloth, gold, void, leather = (.94, .87, .72), (.46, .2, .64), (.96, .62, .1), (.06, .045, .08), (.36, .2, .1)
        first = len(PARTS)
        h, b = frame(bone, head=(.2, .172, .16), body=(.05, .05, .05), cheek=.15, pear=0, pat='bone')
        PARTS.remove(b); bpy.data.objects.remove(b)
        head = F['head']; top = F['top']
        # Skull (built upright at the usual place, then moved forward over the front legs).
        sockets = []
        for s in (1, -1):
            sf = front(s * .076, .43, [head])
            sockets.append(E('body', 'fixed', void, 'socket', (s * .076, sf - .045, .43), (.077, .05, .082), segs=20, rings=12))
        eyes(.076, .43, .054, iris=(1.0, .56, .04), sclera=(1.0, .86, .2), on=sockets + [head])
        f0 = front(0, .365, [head])
        cone('body', 'fixed', void, 'nose hole', (0, f0 - .004, .378), (0, f0 + .004, .35), .02, verts=8, r2=.004)
        smile(.322, .042, [head])
        my = front(0, .322, [head])
        for k in (-2, -1, 0, 1, 2):
            box('body', 'fixed', (.99, .96, .86), f'tooth{k}', (k * .0155, my + .006 - abs(k) * .003, .341), (.013, .01, .02 if k % 2 == 0 else .016), bevel=.003)
        for s, side in SIDES:
            base = Vector((s * .115, -.005, top - .06)); tip = Vector((s * .172, .0, top + .1))
            loft('body', 'main', bone, 'ear_' + side, [(base, .066, .04), (base.lerp(tip, .45), .046, .026), (base.lerp(tip, .82), .018, .011), (tip, .003, .003)], 12, side=(1, 0, 0), pat='bone')
            leaf('body', 'fixed', void, 'ear_in_' + side, base.lerp(tip, .35) + Vector((0, .028, 0)), tip + Vector((-s * .01, .012, -.024)), .026, normal=(0, 1, 0), camber=.002, thick=.004, rows=6, cols=3)
            ring('body', 'accent', gold, 'goggle rim', (s * .068, .082, top - .012), .046, .011, rot=(math.pi / 2.6, 0, 0), pat='plain')
            E('body', 'fixed', (.5, .3, .82), 'goggle lens', (s * .068, .086, top - .01), (.039, .02, .03))
            E('body', 'fixed', leather, 'ear flap', (s * (right(-.03, .4, [head]) + .002), -.035, .395), (.02, .06, .075), pat='leather')
        E('body', 'fixed', leather, 'aviator cap', (0, -.05, top - .04), (F['hx'] * .93, F['hy'] * .9, .07), segs=22, rings=12, pat='leather')
        tube('body', 'fixed', (.2, .12, .07), 'goggle strap', [(-.04, .112, top - .03), (0, .118, top - .026), (.04, .112, top - .03)], .008, .008, segs=6, per=2, pat='leather')
        shift = Matrix.Translation((0, .105, -.035))
        for o in PARTS[first:]: o.data.transform(shift)
        BVH.clear()
        top -= .035
        # Trunk: a dark hollow inside ribs, shoulder and hip bones, a spine along the top.
        cy, cz, half = -.105, .2, .165
        core = E('body', 'fixed', void, 'hollow', (0, cy, cz), (.082, half, .08), segs=20, rings=12)
        F['body'] = core
        for k, y in enumerate((-.0, -.055, -.11, -.165, -.215)):
            w = math.sqrt(max(.05, 1 - ((y - cy) / half) ** 2))
            ring('body', 'main', bone, f'rib{k}', (0, y, cz), 1.0, .085, scale=(.082 * w + .012, .08 * w + .012, .13), rot=(math.pi / 2, 0, 0), pat='bone')
        for k in range(9):
            y = .02 - k * .036
            E('body', 'main', bone, 'vertebra', (0, y, cz + .088 - .006 * abs(k - 4) * .5), (.022, .017, .02), segs=10, rings=6, pat='bone')
        tube('body', 'main', bone, 'neck', [(0, .0, .28), (0, .045, .31)], .026, .03, segs=10, pat='bone')
        E('body', 'main', bone, 'shoulders', (0, .012, cz + .005), (.098, .05, .075), segs=16, rings=10, pat='bone')
        E('body', 'main', bone, 'pelvis', (0, -.25, cz), (.096, .055, .072), segs=16, rings=10, pat='bone')
        E('body', 'fixed', void, 'pelvis hollow', (0, -.295, cz - .005), (.036, .02, .03), segs=12, rings=8)
        def leg(g, joints, toes_forward=.05):
            pts = [Vector(j) for j in joints]
            for i, (a0, a1) in enumerate(zip(pts, pts[1:])):
                tube(g, 'main', bone, f'bone{i}', [a0, a1], .018 - .002 * i, .016 - .002 * i, segs=8, pat='bone')
                E(g, 'main', bone, f'joint{i}', a0, (.026 - .003 * i,) * 3, segs=10, rings=6, pat='bone')
            P0 = pts[-1]
            E(g, 'main', bone, 'paw', P0 + Vector((0, .018, -.004)), (.04, .052, .024), segs=12, rings=8, pat='bone')
            for k in (-1, 0, 1):
                E(g, 'main', bone, f'toe{k}', P0 + Vector((k * .026, toes_forward + .02 - abs(k) * .008, -.01)), (.014, .022, .014), segs=8, rings=6, pat='bone')
            PIV[g] = tuple(pts[0])
        for s, side in SIDES:
            leg('arm_' + side, [(s * .092, .02, .2), (s * .098, -.005, .115), (s * .095, .03, .026)])
            leg('foot_' + side, [(s * .09, -.25, .2), (s * .1, -.205, .125), (s * .096, -.262, .068), (s * .096, -.235, .026)])
        path = spline([(0, -.3, .225), (0, -.37, .25), (.015, -.425, .33), (.04, -.42, .42), (.075, -.38, .475)], 3)
        n = len(path) - 1
        for i, c in enumerate(path):
            r = .027 - .013 * i / n
            E('tail', 'main', bone, 'tail bone', c, (r, r, r * .8), segs=10, rings=6, pat='bone')
        tube('tail', 'main', bone, 'tail cord', [tuple(c) for c in path], .009, .005, segs=6, per=1, pat='bone')
        PIV['tail'] = (0, -.3, .225)
        # Neckerchief (second slot) with the parcel badge.
        ring('scarf', 'second', cloth, 'neck band', (0, .03, .262), .07, .022, scale=(1.15, 1, 1), rot=(.5, 0, 0), pat='cloth')
        leaf('scarf', 'second', cloth, 'neckerchief', (0, .092, .225), (0, .135, .075), .1, normal=(0, 1, .2), camber=.012, thick=.006, rows=8, cols=6,
             prof=lambda t: (1 - t) ** .8 * min(1, t * 6 + .55), pat='cloth')
        box('scarf', 'fixed', (.99, .9, .62), 'parcel badge', (0, .128, .165), (.034, .012, .034), bevel=.002, rot=(0, math.pi / 4, 0))
        PIV['scarf'] = (0, .09, .23)
        # Delivery pack strapped on the back, bedroll on top, satchel at the side.
        py, pz = -.125, .345
        box('body', 'accent', (.56, .38, .2), 'pack', (0, py, pz), (.19, .2, .12), bevel=.022, pat='canvas')
        box('body', 'accent', (.46, .3, .16), 'pack_lid', (0, py, pz + .062), (.2, .212, .03), bevel=.012, pat='canvas')
        tube('body', 'accent', gold, 'bedroll', [(-.1, py - .03, pz + .1), (.1, py - .03, pz + .1)], .03, .03, segs=12, pat='cloth')
        for sx in (-.06, .06):
            ring('body', 'fixed', (.2, .12, .07), 'bedroll strap', (sx, py - .03, pz + .1), .032, .006, rot=(0, math.pi / 2, 0), pat='leather')
        yb = py - .104
        for k, (dx, dz, sx, sz) in enumerate(((0, .028, .056, .007), (0, -.028, .056, .007), (.025, 0, .007, .056), (-.025, 0, .007, .056), (0, .008, .056, .005))):
            box('body', 'fixed', (.2, .13, .08), f'pack_mark{k}', (dx, yb, pz + .004 + dz), (sx, .006, sz), bevel=0)
        box('body', 'fixed', (1.0, .66, .1), 'pack_strip', (0, yb, pz - .044), (.1, .008, .014), bevel=.003)
        for y in (-.06, -.19):
            w = math.sqrt(max(.05, 1 - ((y - cy) / half) ** 2))
            ring('body', 'fixed', (.24, .2, .2), 'pack strap', (0, y, cz), 1.0, .013, scale=(.082 * w + .024, .08 * w + .024, 1.4), rot=(math.pi / 2, 0, 0), pat='leather')
        box('body', 'fixed', (.16, .14, .13), 'buckle', (.0, -.06, cz - .105), (.05, .03, .014), bevel=.005)
        box('body', 'fixed', (.34, .17, .07), 'satchel', (-.118, -.125, .2), (.05, .1, .09), bevel=.009, pat='leather')
        box('body', 'fixed', (.58, .31, .12), 'satchel flap', (-.146, -.125, .225), (.012, .11, .05), bevel=.006, pat='leather')
        box('body', 'fixed', (.95, .7, .25), 'satchel buckle', (-.154, -.125, .205), (.006, .025, .024), bevel=.002)

    # ------------------------------------------------------------------ stone elemental

    def elemental():
        stone, energy, rune, night = (.52, .46, .52), (.22, .72, 1.0), (1.0, .76, .16), (.05, .07, .14)
        frame(stone, head=(.172, .16, .15), body=(.15, .14, .14), cheek=.0, pear=.0, pat='stone')
        head = F['head']; body = F['body']; top = F['top']
        f = front(0, .395, [head])
        face = E('body', 'fixed', night, 'face', (0, f - .05, .395), (.128, .072, .104), segs=24, rings=14)
        # A glowing face: every expression is drawn in light (second slot).
        r = .044
        for s, side in SIDES:
            ex = s * .054; z = .41
            y = front(ex, z, [face]) - .004
            lit(E('eyes_open', 'second', energy, 'eye_' + side, (ex, y, z), (r * .62, .012, r * .92), segs=16, rings=10))
            lit(E('eyes_wide', 'second', energy, 'eye_' + side, (ex, y, z + .004), (r * .86, .012, r * 1.1), segs=16, rings=10))
            E('eyes_wide', 'fixed', night, 'pupil_' + side, (ex, y + .008, z + .004), (r * .26, .008, r * .32), segs=10, rings=6)
            lit(ring('eyes_happy', 'second', energy, 'smile_' + side, (ex, y + .004, z - .012), r * .6, r * .15, rot=(math.pi / 2, 0, 0), arc=True))
            lit(ring('eyes_sleepy', 'second', energy, 'sleep_' + side, (ex, y + .004, z + .008), r * .55, r * .12, rot=(math.pi / 2, math.pi, 0), arc=True))
            for name, angle in (('brows_sad', .38), ('brows_focus', -.42)):
                lit(box(name, 'second', energy, 'brow_' + side, (ex, y + .004, z + r * 1.3), (r * 1.0, .01, r * .2), bevel=.003, rot=(0, s * angle, 0)))
        lit(ring('body', 'second', energy, 'mouth', (0, front(0, .352, [face]) + .001, .36), .022, .0055, rot=(math.pi / 2, math.pi, 0), arc=True))
        # Shell plates round the head, the rune stone on top.
        for s in (1, -1):
            x = right(0, .41, [head])
            rock('body', 'main', stone, 'cheek plate', (s * (x + .012), -.01, .4), (.05, .1, .09), seed=11 + s, rot=(0, 0, s * .2), pat='stone')
        rock('body', 'main', stone, 'back plate', (0, back(0, .42, [head]) - .01, .43), (.12, .05, .1), seed=5, pat='stone')
        crown = rock('body', 'main', stone, 'rune stone', (0, .01, top + .035), (.098, .088, .056), seed=3, rot=(.12, 0, 0), pat='stone', detail=2)
        ry = front(0, top + .04, [crown]) + .002
        for k, (dx, dz, w, hgt) in enumerate(((0, .022, .05, .008), (0, -.022, .05, .008), (-.021, 0, .008, .05), (.021, -.006, .008, .036), (.004, .0, .022, .008))):
            lit(box('body', 'accent', rune, f'rune{k}', (dx, ry, top + .04 + dz), (w, .008, hgt), bevel=.002, rot=(.12, 0, 0)))
        # Core: a ring of stone round a well of light.
        cy = front(0, .115, [body])
        lit(E('body', 'second', energy, 'core', (0, cy - .014, .115), (.05, .028, .05), segs=18, rings=10))
        ring('body', 'main', stone, 'core rim', (0, cy + .004, .115), .056, .017, rot=(math.pi / 2, 0, 0), pat='stone')
        for k in range(6):
            a = math.tau * k / 6
            rock('body', 'main', stone, 'core stud', (math.cos(a) * .058, cy + .012, .115 + math.sin(a) * .058), (.017, .014, .017), seed=20 + k, pat='stone')
        # No limbs: fists, feet stones and a trail of shards float beside the body.
        for s, side in SIDES:
            g = 'arm_' + side
            rock(g, 'main', stone, 'shoulder', (s * .205, -.005, .25), (.052, .05, .05), seed=31 + s, pat='stone')
            rock(g, 'main', stone, 'fist', (s * .235, .05, .135), (.066, .066, .062), seed=33 + s, pat='stone', detail=2)
            lit(box(g, 'accent', rune, 'fist rune', (s * .235, .05 + .062, .135), (.022, .01, .038), bevel=.003))
            PIV[g] = (s * .17, 0, .255)
            g = 'foot_' + side
            rock(g, 'main', stone, 'foot stone', (s * .085, .025, -.022), (.058, .07, .042), seed=41 + s, pat='stone')
            rock(g, 'main', stone, 'pebble', (s * .16, -.05, .005), (.026, .026, .022), seed=43 + s, pat='stone')
            PIV[g] = (s * .085, .02, .0)
            g = 'ear_' + side
            rock(g, 'main', stone, 'orbit shard', (s * .285, -.03, .5), (.036, .03, .05), seed=51 + s, rot=(.3, s * .4, 0), pat='stone')
            lit(box(g, 'accent', rune, 'shard rune', (s * .285, -.03 + .03, .5), (.012, .008, .03), bevel=.002, rot=(0, s * .3, 0)))
            PIV[g] = (s * .2, -.02, .43)
            g = 'wing_' + side
            rock(g, 'main', stone, 'back shard', (s * .23, -.17, .33), (.04, .034, .058), seed=61 + s, rot=(-.3, s * .5, 0), pat='stone')
            rock(g, 'main', stone, 'back chip', (s * .3, -.12, .2), (.026, .024, .032), seed=63 + s, pat='stone')
            PIV[g] = (s * .12, -.1, .3)
        by = back(0, .08, [body])
        for k, (x, dy, z, size) in enumerate(((0, -.045, .075, .05), (.03, -.125, .05, .036), (-.01, -.185, .035, .024))):
            rock('tail', 'main', stone, f'tail shard{k}', (x, by + dy, z), (size, size * 1.2, size * .9), seed=70 + k, pat='stone')
        PIV['tail'] = (0, by, .08)
        gear(accent=rune, with_cap=False)

    # ------------------------------------------------------------------ phoenix

    def phoenix():
        red, cream, orange, flame, yellow = (.84, .13, .09), (1.0, .86, .6), (1.0, .56, .08), (1.0, .88, .3), (1.0, .72, .12)
        frame(red, head=(.195, .178, .17), body=(.174, .162, .167), cheek=.07, pear=.2)
        head = F['head']; body = F['body']; top = F['top']
        E('body', 'second', cream, 'belly', (0, .045, .15), (.128, .13, .138), segs=24, rings=14)
        fy = front(.07, EYE_Z - .02)
        for s in (1, -1):
            E('body', 'second', cream, 'cheek', (s * .072, fy - .062, EYE_Z - .022), (.082, .07, .088), segs=20, rings=12)
        eyes(.07, EYE_Z, .058, iris=(.95, .42, .04), on=body_parts())
        f0 = front(0, EYE_Z - .06, body_parts())
        loft('body', 'fixed', yellow, 'beak', [((0, f0 - .01, EYE_Z - .05), .036, .025), ((0, f0 + .032, EYE_Z - .06), .023, .017), ((0, f0 + .066, EYE_Z - .076), .004, .004)], 12, pat='beak')
        loft('body', 'fixed', (.95, .5, .08), 'beak lower', [((0, f0 - .01, EYE_Z - .071), .027, .013), ((0, f0 + .022, EYE_Z - .075), .017, .009), ((0, f0 + .044, EYE_Z - .077), .004, .003)], 10, pat='beak')
        # A crest of flame: plumes with a bright heart.
        for k, a in enumerate((-1.0, -.5, 0, .5, 1.0)):
            root = (a * .04, -.01, top - .03); tip = (a * .11, -.07 - abs(a) * .01, top + .15 - abs(a) * .045)
            leaf('body', 'accent', orange, f'crest{k}', root, tip, .038, normal=(0, 1, .4), camber=.012, thick=.008, curl=-.2, rows=8, cols=4,
                 prof=lambda t: math.sin(math.pi * min(1, t * .75 + .15)) ** .7 * (1 - t ** 4), pat=f'flame:z:{top - .02:.3f}:{top + .12:.3f}')
        for k, a in enumerate((-.5, 0, .5)):
            lit(leaf('body', 'fixed', flame, f'crest heart{k}', (a * .035, .004, top - .02), (a * .085, -.04, top + .09 - abs(a) * .03), .017, normal=(0, 1, .4),
                     camber=.008, thick=.006, curl=-.2, rows=6, cols=3))
        for k in range(7):   # a ruff of small flames under the chin
            a = (k - 3) * .26; x = math.sin(a) * .135
            tuft('body', 'accent', orange, 'ruff', (x, front(x, .262, [body]) - .02, .268), (x * 1.18, front(x, .222, [body]) + .014, .216), .026, pat='flame:z:.27:.215')
        for s, side in SIDES:
            bird_wing(s, red, feathers=0, length=.2)
            g = 'arm_' + side; L = .21
            S = Vector(PIV[g])
            for k in range(6):
                f = k / 5
                root = S + Vector((s * .036, .02 - .05 * f, -L * (.36 + .16 * f)))
                tip = S + Vector((s * (.05 + .035 * f), -.02 - .1 * f, -L * (1.0 + .2 * f)))
                leaf(g, 'accent', orange, f'flame feather{k}', root, tip, .042, normal=(s, 0, 0), camber=.006, thick=.006,
                     prof=lambda t: (.5 + .5 * math.sin(math.pi * min(1, t * 1.15))) * (1 - t ** 4), rows=6, cols=4, pat='flame:z:.19:.03')
            bird_foot(s, yellow, .064, claw=(.32, .16, .1))
        # Two fans of tail plumes, either side of the backpack, each with a bright heart.
        by = back(0, .1, [body])
        for k, a in enumerate((-1.5, -1.12, -.75, .75, 1.12, 1.5)):
            root = Vector((a * .085, by + .03, .12)); tip = Vector((a * .235, by - .1, .38 - (abs(a) - .75) * .17))
            leaf('tail', 'accent', orange, f'tail plume{k}', root, tip, .052, normal=(0, -1, .3), camber=.012, thick=.007, curl=.14, rows=10, cols=4,
                 prof=lambda t: math.sin(math.pi * min(1, t * .8 + .12)) ** .6 * (1 - t ** 6), pat='flame:z:.13:.33')
            lit(leaf('tail', 'fixed', flame, f'plume heart{k}', root.lerp(tip, .45) + Vector((0, -.012, 0)), tip + Vector((0, -.022, .0)), .02, normal=(0, -1, .3),
                     camber=.008, thick=.006, curl=.14, rows=6, cols=3))
        PIV['tail'] = (0, by + .03, .1)
        gear(accent=(.98, .64, .1), with_cap=False)

    # ------------------------------------------------------------------ surfaces

    def mscales(p, pat):   # mermaid scales: rounded rows with a pearly shimmer, lighter towards the tail tip
        d = p.vor(38.0, (1, 1, 1.35), 'DISTANCE_TO_EDGE'); soft = p.vor(38.0, (1, 1, 1.35))
        return p.mul(p.mix(p.rng(d, .0, .12), (.46, .52, .78), W), p.mix(p.m('MULTIPLY', p.rng(soft, .25, .75), .4), W, (.6, .7, .9)),
                     p.mix(p.rng(p.noise(7.0, detail=1.0), .4, .65), (1.0, .8, 1.0), (.82, 1.0, .92)))

    def fin(p, pat):       # translucent-looking fin: fine rays and a pale edge
        rays = p.rng(p.noise(95.0, (3, 3, .3), 2.0), .3, .7)
        return p.mul(p.mix(rays, (.72, .7, .9), W), p.mix(p.rng(p.noise(9.0), .35, .7), (.84, .92, 1.0), W))

    def bone(p, pat):      # old bone: stained, pitted, hairline cracks
        cracks = p.rng(p.vor(17.0, feature='DISTANCE_TO_EDGE'), .0, .014)
        return p.mul(p.mix(p.rng(p.noise(20.0, detail=3.0), .35, .7), (.84, .77, .68), W), p.mix(cracks, (.62, .52, .46), W),
                     p.mix(p.rng(p.noise(120.0), .55, .75), W, (.84, .8, .76)))

    def stone(p, pat):     # weathered rock: mottling, grain, cracks
        cracks = p.rng(p.vor(12.0, feature='DISTANCE_TO_EDGE'), .0, .016)
        return p.mul(p.mix(p.rng(p.noise(13.0, detail=4.0), .3, .7), (.72, .7, .78), W), p.mix(cracks, (.42, .4, .5), W),
                     p.mix(p.rng(p.noise(75.0), .4, .7), (.86, .86, .9), W))

    def pfire(p, pat):     # phoenix feathers: scallops shading to ember-dark edges, warmer low down
        d = p.vor(44.0, (1, 1, 1.5), 'DISTANCE_TO_EDGE')
        return p.mul(p.mix(p.rng(d, .0, .13), (.6, .36, .44), W), p.mix(p.rng(p.noise(150.0, (2, 2, .4), 2.0), .3, .7), (.84, .8, .82), W),
                     p.mix(p.rng(p.z, .44, .58), W, (1.0, .86, .74)))

    def down(p, pat):
        return p.mix(p.rng(p.noise(150.0, (2, 2, .4), 2.0), .3, .7), (.88, .84, .8), W)

    def flame(p, pat):     # 'flame:<axis>:<root>:<tip>': deep red at the root, the part's own colour at the tip
        _, axis, a, b = pat.split(':')
        v = p.z if axis == 'z' else p.y
        a, b = float(a), float(b)
        ragged = p.m('ADD', v, p.m('MULTIPLY', p.m('SUBTRACT', p.noise(26.0), .5), .05))
        t = p.rng(ragged, a, b) if a < b else p.rng(ragged, a, b, 0.0, 1.0)
        return p.mul(p.mix(t, (.86, .26, .16), W), p.mix(p.rng(p.noise(150.0, (2, 2, .4), 2.0), .3, .7), (.86, .82, .8), W))

    G['EXTRA_PAT'].update({'mscales': mscales, 'fin': fin, 'bone': bone, 'stone': stone, 'pfire': pfire, 'down': down, 'flame': flame})
    G['ANIMALS'].update({'mermaid': mermaid, 'skeleton': skeleton, 'elemental': elemental, 'phoenix': phoenix})
    G['DEFAULT_PAT'].update({'mermaid': ('mscales', 'skin'), 'skeleton': ('bone', 'cloth'), 'elemental': ('stone', 'plain'),
                             'phoenix': ('pfire', 'down')})
