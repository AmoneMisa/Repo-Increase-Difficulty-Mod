"""The other seven couriers: sheep, cow, dog and the four spirits (fairy, devil, angel, dragon). Called by build_courier.py."""
import math

from mathutils import Vector


def install(G):
    E, orb, loft, tube, cone, box, ring, leaf, tuft = (G[n] for n in ('ellipsoid', 'orb', 'loft', 'tube', 'cone', 'box', 'ring', 'leaf', 'tuft'))
    frame, eyes, smile, gear, arm, foot, hoof_leg, hoof_arm, cheek_fluff = (G[n] for n in (
        'frame', 'eyes', 'smile', 'gear', 'arm', 'foot', 'hoof_leg', 'hoof_arm', 'cheek_fluff'))
    front, back, right, body_parts, mesh_obj, add = (G[n] for n in ('front', 'back', 'right', 'body_parts', 'mesh_obj', 'add'))
    F, PIV = G['F'], G['PIV']
    CREAM, PINK, DARK = G['CREAM'], G['PINK'], G['DARK']
    SIDES = ((1, 'l'), (-1, 'r'))

    def sheep():
        wool, skin, lilac = (1.0, .95, .88), (.46, .3, .38), (.66, .36, .86)
        frame(wool, head=(.19, .172, .17), body=(.172, .162, .168), cheek=.05, pear=.2)
        head = F['head']; top = F['top']
        f = front(0, .385)
        face = E('body', 'second', skin, 'face', (0, f - .052, .385), (.136, .078, .126), segs=24, rings=14)
        eyes(.066, .415, .054, iris=(.55, .3, .8), on=[face, head])
        smile(.335, .03, [face])
        for s in (1, -1): E('body', 'fixed', (.25, .14, .2), 'nostril', (s * .016, front(s * .016, .362, [face]) - .003, .362), (.008, .006, .006), segs=8, rings=6)
        # A woolly rim round the face, a pile on top and two rows round the body.
        for k in range(14):
            a = math.tau * (k + .5) / 14; x = math.cos(a) * .152; z = .392 + math.sin(a) * .138
            E('body', 'main', wool, 'wool lock', (x, front(x * .86, .39 + (z - .39) * .86, [head]) - .012, z), (.042, .036, .042), segs=10, rings=6)
        for k, (x, y, z) in enumerate(((0, .03, .03), (-.06, .0, .015), (.06, .0, .015), (0, -.05, .02), (-.035, -.02, .05), (.035, -.02, .05))):
            E('body', 'main', wool, 'wool top', (x, y, top + z), (.052, .05, .046), segs=10, rings=6)
        for row, z in enumerate((.055, .108)):
            rx = right(0, z, [F['body']]); ry = front(0, z, [F['body']])
            for k in range(14):
                a = math.tau * (k + row * .5) / 14
                E('body', 'main', wool, 'wool lock', (math.sin(a) * rx, math.cos(a) * ry, z), (.04, .036, .038), rot=(0, 0, -a), segs=10, rings=6)
        for s, side in SIDES:
            g = 'ear_' + side; x = right(0, .435, [head])
            E(g, 'second', skin, 'ear', (s * (x + .035), .0, .425), (.068, .028, .04), rot=(0, s * .45, 0))
            E(g, 'fixed', PINK, 'inner ear', (s * (x + .04), .018, .423), (.045, .014, .024), rot=(0, s * .45, 0), segs=12, rings=8)
            PIV[g] = (s * (x - .01), 0, .44)
            hoof_arm(s, skin, 'second'); hoof_leg(s, skin, 'second')
            ring('body', 'fixed', (.92, .62, .14), 'goggle rim', (s * .07, .07, top + .012), .046, .009, rot=(math.pi / 3, 0, 0))
            E('body', 'fixed', (.2, .78, .86), 'goggle lens', (s * .07, .073, top + .014), (.04, .02, .03))
        by = back(0, .08, [F['body']])
        E('tail', 'main', wool, 'tail', (0, by - .02, .085), (.058, .058, .056), segs=12, rings=8)
        PIV['tail'] = (0, by, .08)
        gear(accent=lilac, with_cap=False)

    def cow():
        main, patch, horn = (1.0, .96, .9), (.2, .12, .1), (.98, .86, .56)
        frame(main, head=(.2, .176, .172), cheek=.1, pear=.2)
        head = F['head']; top = F['top']
        fy = front(.085, .44)
        E('body', 'second', patch, 'eye patch', (-.088, fy - .042, .442), (.086, .05, .094), segs=18, rings=10)
        f = front(0, .335)
        muzzle = E('body', 'fixed', PINK, 'muzzle', (0, f - .04, .335), (.118, .078, .07), segs=22, rings=12, pat='skin')
        eyes(.08, .44, .055, iris=(.34, .2, .1), on=body_parts())
        for s in (1, -1): E('body', 'fixed', (.5, .2, .26), 'nostril', (s * .045, front(s * .045, .357, [muzzle]) - .004, .357), (.014, .008, .02), segs=10, rings=6)
        smile(.31, .034, [muzzle])
        for k in (-1, 0, 1):
            x = k * .034
            tuft('body', 'second', patch, 'forelock', (x, .05, top - .012), (x * 1.3, front(x, top - .05, [head]) + .014, top - .056), .028)
        for s, side in SIDES:
            tube('body', 'fixed', horn, 'horn', [(s * .112, -.01, top - .035), (s * .165, -.01, top + .035), (s * .152, .0, top + .095)], .03, .006, segs=10, pat='horn')
            g = 'ear_' + side; x = right(0, .45, [head])
            E(g, 'main', main, 'ear', (s * (x + .04), .0, .448), (.07, .028, .044), rot=(0, s * .3, 0))
            E(g, 'fixed', PINK, 'inner ear', (s * (x + .045), .018, .446), (.046, .014, .026), rot=(0, s * .3, 0), segs=12, rings=8)
            PIV[g] = (s * (x - .01), 0, .455)
            hoof_arm(s, main, 'main'); hoof_leg(s, main, 'main')
        by = back(0, .1, [F['body']])
        tube('tail', 'main', main, 'tail', [(0, by + .02, .11), (.08, by - .06, .085), (.135, by - .06, .16)], .014, .01, segs=8)
        tuft('tail', 'second', patch, 'tail tassel', (.13, by - .06, .145), (.15, by - .055, .235), .03)
        PIV['tail'] = (0, by + .02, .11)
        gear(accent=(.92, .1, .12), with_cap=False)
        y = front(0, .2, body_parts())
        E('body', 'fixed', (1.0, .72, .1), 'bell', (0, y + .016, .198), (.028, .026, .032))
        E('body', 'fixed', (.4, .25, .05), 'bell slot', (0, y + .04, .186), (.016, .006, .006), segs=8, rings=6)

    def dog():
        main, cream, ear = (.93, .56, .18), (1.0, .9, .68), (.52, .25, .1)
        frame(main, head=(.198, .172, .172), cheek=.12, pear=.18)
        head = F['head']; top = F['top']
        E('body', 'second', cream, 'belly', (0, .045, .148), (.118, .112, .13), segs=20, rings=12)
        f = front(0, .337)
        muzzle = E('body', 'second', cream, 'muzzle', (0, f - .034, .337), (.1, .078, .062), segs=20, rings=12)
        eyes(.07, .425, .055, iris=(.5, .27, .08))
        for s in (1, -1):
            E('body', 'second', cream, 'brow dot', (s * .068, front(s * .068, .492, [head]) - .003, .492), (.024, .01, .016), segs=10, rings=6)
        E('body', 'fixed', DARK, 'nose', (0, front(0, .366, [muzzle]) - .004, .368), (.034, .02, .024))
        smile(.312, .036, [muzzle])
        for s, side in SIDES:
            g = 'ear_' + side
            path = G['spline']([(s * .145, -.01, top - .03), (s * .215, -.008, .475), (s * .25, .008, .37), (s * .235, .02, .285)], 3)
            n = len(path) - 1
            loft(g, 'second', ear, 'ear', [(c, .017, .034 + .042 * math.sin(math.pi * min(1, i / n * .9 + .12)) ** .8) for i, c in enumerate(path)], 12, side=(s, 0, 0), pat='fur')
            PIV[g] = (s * .16, -.01, top - .035)
            arm(s, main, tip=cream); foot(s, main, toes=cream)
        cheek_fluff(main, n=2)
        by = back(0, .08, [F['body']])
        tube('tail', 'main', main, 'tail', [(.02, by + .03, .09), (.1, by - .06, .13), (.14, by - .05, .23), (.09, by - .01, .29)], .038, .028, segs=12, per=5)
        E('tail', 'second', cream, 'tail tip', (.075, by + .0, .3), (.036, .036, .038))
        PIV['tail'] = (.02, by + .03, .09)
        gear(accent=(.1, .52, .9))

    # ------------------------------------------------------------------ spirits

    def spirit(main, iris, claws=None, pat=None):
        frame(main, head=(.198, .172, .172), body=(.142, .132, .16), cheek=.1, pear=.16, pat=pat)
        for s, _ in SIDES:
            arm(s, main, claws=claws, pat=pat); foot(s, main, claws=claws, pat=pat)

    def wing_root(s):
        return Vector((s * .108, back(0, .3, [F['body']]) - .025, .3))

    def membrane(s, main, second, size=1.0):
        """A scalloped bat wing: skin stretched between three fingers."""
        g = 'wing_' + ('l' if s > 0 else 'r'); R = wing_root(s)
        tips = [R + Vector((s * .1, -.025, .17)) * size, R + Vector((s * .26, -.04, .11)) * size,
                R + Vector((s * .23, -.04, -.08)) * size, R + Vector((s * .05, -.012, -.12)) * size]
        outline = [R, tips[0]]
        for start, end in zip(tips, tips[1:]):
            for k in range(1, 9):
                t = k / 8; point = start.lerp(end, t)
                outline.append(point + (R - point) * (.24 * math.sin(math.pi * t)))
        n = len(outline); vs = [p + Vector((0, off, 0)) for off in (-.004, .004) for p in outline]
        faces = []
        for k in range(1, n - 1): faces.extend([(0, k + 1, k), (n, n + k, n + k + 1)])
        faces.extend((k, (k + 1) % n, (k + 1) % n + n, k + n) for k in range(n))
        add(mesh_obj('membrane', vs, faces), g, 'second', second, 'scalloped wing', 'membrane')
        for k, tip in enumerate(tips[:3]):
            tube(g, 'main', main, f'wing finger{k}', [R, R.lerp(tip, .5) + Vector((0, 0, .012)), tip], .013, .004, segs=8, per=3)
        PIV[g] = tuple(R)

    def fairy():
        skin, hair, wing, green = (1.0, .77, .65), (.62, .27, .85), (.86, .78, 1.0), (.5, .8, .2)
        spirit(skin, None, pat='skin')
        head = F['head']; top = F['top']; hx = F['hx']
        eyes(.073, .425, .058, iris=(.2, .72, .5)); smile(.33, .032)
        for s, side in SIDES:
            x = right(0, .405, [head])
            cone('body', 'main', skin, 'pointed ear', (s * (x - .02), -.005, .405), (s * (x + .075), -.02, .455), .03, verts=10, pat='skin')
            tube('body', 'second', hair, 'side lock', [(s * .165, .03, .51), (s * (x + .012), .035, .41), (s * (x - .005), .05, .315)], .04, .012, segs=10, per=4, pat='hair')
            tube('body', 'fixed', (.34, .16, .46), 'antenna', [(s * .05, .04, top + .02), (s * .075, .07, top + .085), (s * .115, .085, top + .125)], .006, .004, segs=6, per=3)
            E('body', 'fixed', (1.0, .86, .2), 'antenna tip', (s * .118, .087, top + .13), (.017, .017, .017), segs=10, rings=6)
        E('body', 'second', hair, 'hair cap', (0, -.018, top - .04), (hx * .99, F['hy'] * .97, .092), segs=22, rings=12, pat='hair')
        for k in (-2, -1, 0, 1, 2):
            x = k * .045
            tuft('body', 'second', hair, 'fringe', (x, front(x, .525, [head]) - .02, .535), (x * 1.12 + .012, front(x, .485, [head]) + .01, .478 + abs(k) * .006), .03, pat='hair')
        E('body', 'second', hair, 'hair bun', (.075, -.055, top + .05), (.076, .072, .076), pat='hair')
        ring('body', 'accent', green, 'bun band', (.068, -.05, top + .005), .05, .009, rot=(.2, .35, 0))
        fx, fz = -.125, .525
        fy = front(fx, fz, body_parts()) + .012
        for k in range(5):
            a = math.tau * k / 5
            E('body', 'fixed', (1.0, .95, .8), 'flower petal', (fx + math.cos(a) * .032, fy, fz + math.sin(a) * .032), (.024, .012, .024), segs=10, rings=6)
        E('body', 'fixed', (1.0, .7, .1), 'flower heart', (fx, fy + .008, fz), (.016, .012, .016), segs=10, rings=6)
        for k in range(9):
            a = math.tau * (k + .5) / 9; sx, cy = math.sin(a), math.cos(a)
            leaf('body', 'accent', green, 'leaf skirt', (sx * .095, cy * .09, .14), (sx * .2, cy * .19, .045), .05, normal=(sx, cy, .8),
                 camber=.012, thick=.006, rows=6, cols=4, pat='leafskirt')
        for s, side in SIDES:
            g = 'wing_' + side; R = wing_root(s)
            leaf(g, 'second', wing, 'upper wing', R, R + Vector((s * .31, -.07, .25)), .092, camber=.014, thick=.004, rows=10, cols=6,
                 prof=lambda t: math.sin(math.pi * min(1, t ** .75)) ** .7, pat='fairywing')
            leaf(g, 'second', wing, 'lower wing', R, R + Vector((s * .24, -.06, -.2)), .068, camber=.012, thick=.004, rows=10, cols=6,
                 prof=lambda t: math.sin(math.pi * min(1, t ** .75)) ** .7, pat='fairywing')
            PIV[g] = tuple(R)
        gear(accent=green, with_cap=False)

    def devil():
        main, second, gold = (.94, .15, .1), (.38, .1, .46), (1.0, .7, .1)
        spirit(main, None, claws=CREAM)
        head = F['head']; top = F['top']
        E('body', 'fixed', (1.0, .52, .2), 'belly', (0, .04, .142), (.1, .102, .12), segs=18, rings=10)
        eyes(.073, .425, .058, iris=(1.0, .8, .08)); smile(.33, .04)
        fy = front(.03, .345)
        for s in (1, -1): cone('body', 'fixed', (1.0, .97, .9), 'fang', (s * .026, fy - .004, .35), (s * .026, fy + .004, .322), .009, verts=8)
        for s, side in SIDES:
            tube('body', 'second', second, 'horn', [(s * .112, 0, top - .04), (s * .165, 0, top + .04), (s * .14, .01, top + .105)], .034, .005, segs=10)
            g = 'ear_' + side; x = right(0, .41, [head])
            leaf(g, 'main', main, 'pointed ear', (s * (x - .03), -.005, .4), (s * (x + .12), -.03, .475), .042, camber=.012, thick=.012, rows=6, cols=4,
                 prof=lambda t: (.6 + .4 * math.sin(math.pi * min(1, t * 1.3))) * (1 - t ** 3))
            PIV[g] = (s * (x - .01), 0, .41)
            membrane(s, main, second, 1.1)
        by = back(0, .08, [F['body']])
        tube('tail', 'main', main, 'tail', [(.02, by + .03, .08), (.15, by - .07, .06), (.25, by - .05, .14), (.245, by + .02, .23)], .028, .01, segs=10, per=5)
        leaf('tail', 'second', second, 'tail spade', (.245, by + .02, .22), (.24, by + .04, .32), .045, normal=(0, -1, .2), camber=.004, thick=.01, rows=6, cols=4,
             prof=lambda t: math.sin(math.pi * min(1, t * 1.6 + .12)) * (1 - t ** 2) ** .6 + .05 * (1 - t))
        PIV['tail'] = (.02, by + .03, .08)
        gear(accent=gold, with_cap=False)

    def angel():
        skin, white, gold = (1.0, .8, .66), (1.0, .98, .93), (1.0, .72, .1)
        spirit(skin, None, pat='skin')
        head = F['head']; top = F['top']
        robe = loft('body', 'second', white, 'robe', [((0, 0, .014), .2, .19), ((0, 0, .05), .195, .185), ((0, 0, .12), .172, .16), ((0, 0, .215), .145, .135), ((0, 0, .25), .1, .095)],
                    30, shape=lambda i, a: 1 + (.06 if i == 0 else .035 if i == 1 else 0) * math.cos(10 * a))
        eyes(.073, .425, .058, iris=(.2, .58, 1.0)); smile(.33, .03)
        E('body', 'accent', gold, 'hair cap', (0, -.018, top - .04), (F['hx'] * .98, F['hy'] * .97, .09), segs=22, rings=12, pat='goldhair')
        for k in range(11):
            a = math.tau * k / 11; z = top - .07 - .012 * math.cos(a)
            rr = right(0, z, [head], .15)
            E('body', 'accent', gold, 'curl', (math.sin(a) * rr * .98, math.cos(a) * rr * .86, z), (.05, .046, .042), rot=(0, 0, -a), segs=12, rings=8, pat='goldhair')
        for k, (x, y, z) in enumerate(((0, .05, .0), (-.07, .0, .0), (.07, .0, .0), (0, -.06, .0), (0, 0, .03))):
            E('body', 'accent', gold, 'curl top', (x, y, top + z), (.06, .058, .045), segs=12, rings=8, pat='goldhair')
        ring('body', 'accent', gold, 'halo', (0, -.01, top + .115), .12, .013, rot=(.12, .1, 0), pat='plain')
        for s, side in SIDES:
            g = 'wing_' + side; R = wing_root(s)
            edge = [R, R + Vector((s * .1, -.02, .06)), R + Vector((s * .2, -.03, .075))]
            tube(g, 'second', white, 'wing arm', edge, .022, .012, segs=8, per=3, pat='angelwing')
            for k in range(7):
                f = k / 6
                base = R + Vector((s * (.01 + .19 * f), -.022 - .008 * f, .01 + .068 * f))
                tip = base + Vector((s * (.035 + .1 * f), -.018, -(.175 - .055 * f)))
                leaf(g, 'second', white, f'flight feather{k}', base, tip, .034, camber=.008, thick=.005, rows=6, cols=4,
                     prof=lambda t: (.5 + .5 * math.sin(math.pi * min(1, t * 1.1))) * (1 - t ** 5), pat='angelwing')
            for k in range(5):
                f = k / 4
                base = R + Vector((s * (.01 + .17 * f), -.008, .015 + .062 * f))
                leaf(g, 'second', white, f'covert{k}', base, base + Vector((s * (.03 + .04 * f), .0, -.085)), .032, camber=.01, thick=.006, rows=5, cols=4,
                     prof=lambda t: (.6 + .4 * math.sin(math.pi * min(1, t * 1.1))) * (1 - t ** 5), pat='angelwing')
            PIV[g] = tuple(R)
        gear(accent=gold, with_cap=False)

    def dragon():
        main, second, teal = (.58, .25, .85), (1.0, .66, .18), (.08, .72, .66)
        spirit(main, None, claws=CREAM)
        head = F['head']; top = F['top']
        E('body', 'second', second, 'belly plate', (0, .04, .142), (.105, .105, .124), segs=18, rings=12)
        f = front(0, .34)
        snout = E('body', 'main', main, 'snout', (0, f - .034, .338), (.105, .084, .06), segs=20, rings=12)
        eyes(.074, .43, .058, iris=(.15, .85, .4))
        for s in (1, -1): E('body', 'fixed', (.2, .06, .3), 'nostril', (s * .04, front(s * .04, .362, [snout]) - .004, .364), (.013, .008, .012), segs=8, rings=6)
        smile(.316, .04, [snout])
        fy = front(.035, .33, [snout])
        for s in (1, -1): cone('body', 'fixed', (1.0, .97, .9), 'fang', (s * .034, fy - .006, .334), (s * .034, fy + .002, .306), .009, verts=8)
        for k in range(5):
            th = -.25 + k * .38
            p = Vector((0, -math.sin(th) * F['hy'], G['HEAD_Z'] + math.cos(th) * F['hz'])); d = Vector((0, -math.sin(th), math.cos(th)))
            cone('body', 'second', second, f'crest spine{k}', p - d * .012, p + d * (.06 - abs(k - 1.5) * .006) + Vector((0, -.012, 0)), .024, verts=8, pat='horn')
        for s, side in SIDES:
            tube('body', 'second', second, 'horn', [(s * .115, -.02, top - .04), (s * .17, -.04, top + .03), (s * .16, -.07, top + .1)], .032, .005, segs=10, pat='horn')
            g = 'ear_' + side; x = right(-.01, .42, [head])
            leaf(g, 'second', second, 'ear fin', (s * (x - .03), -.01, .405), (s * (x + .115), -.045, .49), .052, camber=.012, thick=.008, rows=6, cols=4,
                 prof=lambda t: (.55 + .45 * math.sin(math.pi * min(1, t * 1.2))) * (1 - t ** 3), pat='membrane')
            PIV[g] = (s * (x - .01), -.01, .415)
            membrane(s, main, second, 1.0)
        by = back(0, .08, [F['body']])
        pts = [(.0, by + .04, .075), (.12, by - .07, .06), (.225, by - .06, .13), (.235, by + .0, .225)]
        tube('tail', 'main', main, 'tail', pts, .044, .014, segs=12, per=5)
        leaf('tail', 'second', second, 'tail blade', (.235, by + .0, .215), (.225, by + .03, .315), .04, normal=(0, -1, .2), camber=.004, thick=.01, rows=6, cols=4,
             prof=lambda t: math.sin(math.pi * min(1, t * 1.6 + .12)) * (1 - t ** 2) ** .6 + .05 * (1 - t))
        for k, (x, y, z) in enumerate(((.07, by - .03, .1), (.17, by - .075, .125))):
            cone('tail', 'second', second, f'tail spine{k}', (x, y, z), (x + .005, y - .02, z + .055), .018, verts=8, pat='horn')
        PIV['tail'] = (.0, by + .04, .075)
        gear(accent=teal, with_cap=False)

    G['ANIMALS'].update({'sheep': sheep, 'cow': cow, 'dog': dog, 'fairy': fairy, 'devil': devil, 'angel': angel, 'dragon': dragon})
    G['DEFAULT_PAT'].update({'sheep': ('wool', 'fur'), 'cow': ('cow', 'fur'), 'dog': ('dog', 'fur'), 'fairy': ('skin', 'hair'),
                             'devil': ('devilskin', 'horn'), 'angel': ('skin', 'robe'), 'dragon': ('scales', 'plates')})
