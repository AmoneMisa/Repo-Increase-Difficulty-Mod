"""Decorative props for Fable Realm rooms. Visual meshes never collide; simple blockers do."""
import math


def mushroom(r, name, x, z, h, cap_r, cap='CapPlum', y=0.0, solid=True, light=False):
    r.lathe(f'{name}-stem', (x, y, z), [(cap_r*.24, 0), (cap_r*.16, h*.5), (cap_r*.13, h*.9)], 'Stem', 12)
    r.lathe(f'{name}-cap', (x, y, z), [(cap_r*.14, h*.86), (cap_r*.85, h-.12*cap_r), (cap_r, h), (cap_r*.9, h+.2*cap_r),
                                       (cap_r*.55, h+.42*cap_r), (0, h+.5*cap_r)], cap, 18)
    r.lathe(f'{name}-gills', (x, y, z), [(cap_r*.18, h-.1*cap_r), (cap_r*.82, h-.13*cap_r)], 'CapGlow', 18)
    if solid: r.blocker(f'{name}-block', (x, y+h/2, z), (cap_r*.48+.1, h, cap_r*.48+.1))
    if light: r.light(f'{name}-light', (x, y+h-.35, z), 'CapGlow', 1.6, 6)


def tree(r, name, x, z, h=7.6, radius=.45, lean=(.3, .2), mat='Bark'):
    lx, lz = lean
    r.tube(f'{name}-trunk', [(x, 0, z), (x+lx*.3, h*.25, z+lz*.4), (x-lx*.2, h*.5, z+lz), (x+lx, h*.75, z+lz*.5), (x+lx*1.6, h, z)], radius, mat)
    for i in range(4):
        a = i*math.tau/4+.6
        r.tube(f'{name}-root-{i}', [(x, .9, z), (x+math.cos(a)*radius*1.6, .3, z+math.sin(a)*radius*1.6),
                                    (x+math.cos(a)*radius*3, .05, z+math.sin(a)*radius*3)], radius*.4, mat)
    r.blocker(f'{name}-block', (x, 2, z), (radius*2, 4, radius*2))


def lantern_post(r, name, x, z, h=3.0, glass='WarmGlass', metal='Brass', intensity=1.8, reach=7.0):
    """Street lantern: flared foot, slim post with a collar, a short arm and a caged lantern hanging from it."""
    r.lathe(f'{name}-post', (x, 0, z), [(.2, 0), (.2, .06), (.1, .16), (.07, .5), (.05, h*.55), (.07, h*.57), (.045, h*.6), (.04, h+.25)], metal, 8)
    r.tube(f'{name}-arm', [(x, h+.2, z), (x+.2, h+.34, z), (x+.38, h+.3, z)], .025, metal)
    r.tube(f'{name}-hook', [(x+.38, h+.3, z), (x+.38, h+.02, z)], .012, metal)
    r.lamp(name, x+.38, h-.45, z, glass, metal, intensity, reach)
    r.blocker(f'{name}-block', (x, h/2, z), (.24, h, .24))


def candle(r, name, x, y, z, height=.22):
    """Wax candle with a small flame; (x, y, z) is the foot of the candle."""
    r.lathe(f'{name}-wax', (x, y, z), [(.035, 0), (.03, height), (0, height)], 'Wax', 6)
    r.lathe(f'{name}-flame', (x, y+height+.01, z), [(0, 0), (.022, .03), (.012, .07), (0, .1)], 'Candle', 6)


def fireflies(r, name, points, light_every=5):
    # A small round glow: a cube read as a white square hanging in the air.
    ball = [(.03*math.sin(math.pi*k/4), .03-.03*math.cos(math.pi*k/4)) for k in range(5)]
    for i, (x, y, z) in enumerate(points):
        r.lathe(f'{name}-{i}', (x, y-.03, z), ball, 'Firefly', 6)
        if i % light_every == 0: r.light(f'{name}-glow-{i}', (x, y, z), 'Firefly', .9, 4)


def cup(r, name, x, z, s=1.0, y=0.0, mat='Porcelain', trim='Gold', handle=0.0, solid=True):
    outer = [(0, .1), (1, .1), (1.2, .4), (1.6, 2), (1.55, 2.1), (1.42, 2.05), (1.05, .45), (0, .35)]
    r.lathe(f'{name}-body', (x, y, z), [(a*s, b*s) for a, b in outer], mat, 28)
    r.lathe(f'{name}-saucer', (x, y, z), [(0, .02), (1.75*s, .02), (1.95*s, .14*s), (1.75*s, .2*s), (0, .2*s)], mat, 28)
    r.ring(f'{name}-band', (x, y+1.8*s, z), 1.56*s, .035*s+.01, trim)
    c, sn = math.cos(math.radians(handle)), math.sin(math.radians(handle))
    # The arc alone stopped short of the cup wall where the cup narrows; both ends now run into the wall.
    arc = [(1.45+.95*math.sin(i*math.pi/12), 1.15+.75*math.cos(i*math.pi/12)) for i in range(13)]
    arc = [(1.3, 1.9)] + arc + [(1.0, .4)]
    r.tube(f'{name}-handle', [(x+c*a*s, y+b*s, z+sn*a*s) for a, b in arc], .13*s, trim)
    if solid: r.blocker(f'{name}-block', (x, y+1.05*s, z), (2.8*s, 2.1*s, 2.8*s))


def rose(r, name, x, z, h=1.6, s=1.0, y=0.0, mat='RoseRed'):
    r.tube(f'{name}-stem', [(x, y, z), (x+.12*s, y+h*.5, z), (x, y+h, z)], .05*s+.01, 'Thorn')
    r.lathe(f'{name}-outer', (x, y+h-.05*s, z), [(0, 0), (.42*s, .08*s), (.5*s, .3*s), (.3*s, .52*s)], mat, 10)
    r.lathe(f'{name}-inner', (x, y+h, z), [(0, 0), (.24*s, .1*s), (.2*s, .5*s), (0, .56*s)], mat, 9)
    r.box(f'{name}-leaf', (x+.2*s, y+h*.45, z), (.4*s, .03, .2*s), 'Leaf', collider=False, rot=(0, 30, 20))


def column(r, name, x, z, h, radius=.35, mat='Marble', cap=None, y=0.0):
    r.lathe(f'{name}-shaft', (x, y, z), [(radius*1.35, 0), (radius*1.35, .25), (radius, .4), (radius*.9, h-.45),
                                         (radius*1.3, h-.3), (radius*1.3, h)], mat, 14)
    if cap: r.ring(f'{name}-ring', (x, y+h-.38, z), radius*1.05, .05, cap, segments=16)
    r.blocker(f'{name}-block', (x, y+h/2, z), (radius*2.3, h, radius*2.3))


def candy_cane(r, name, x, z, h=3.2, hook=1):
    r.tube(f'{name}-cane', [(x, 0, z), (x, h-.5, z), (x+hook*.18, h-.12, z), (x+hook*.5, h, z), (x+hook*.8, h-.18, z)], .11, 'Icing')
    for i in range(5): r.ring(f'{name}-stripe-{i}', (x, .35+i*(h-1)/5, z), .115, .03, 'Candy', segments=10)
    r.blocker(f'{name}-block', (x, h/2, z), (.28, h, .28))


def cottage(r, name, x, z, facing, w=3.0, d=3.0, h=2.9, wall='Biscuit', roof='Roof', trim='Icing', light=True, y=0.0):
    """Gingerbread house block; `facing` is the side its door and windows look toward."""
    from room_kit import SIDES
    k = SIDES[facing]; a = k*math.pi/2; yaw = k*90
    def p(lx, ly, lz): return (x+lx*math.cos(a)+lz*math.sin(a), y+ly, z-lx*math.sin(a)+lz*math.cos(a))
    r.box(f'{name}-body', p(0, h/2, 0), (w, h, d), wall, rot=(0, yaw, 0))
    slope = 38; run = w/2+.25; rise = run*math.tan(math.radians(slope))
    for s in (-1, 1):
        r.box(f'{name}-roof-{s}', p(s*run/2, h+rise/2, 0), (run/math.cos(math.radians(slope))+.1, .16, d+.5), roof, False, (0, yaw, -s*slope))
    r.box(f'{name}-gable', p(0, h+rise*.3, 0), (w*.55, rise*.6, d-.05), wall, False, (0, yaw, 0))
    r.tube(f'{name}-ridge', [p(0, h+rise+.05, -d/2-.25), p(0, h+rise+.05, d/2+.25)], .09, trim)
    r.box(f'{name}-door', p(0, 1.05, d/2+.03), (.9, 2.1, .06), 'Chocolate', False, (0, yaw, 0))
    for i, s in enumerate((-1, 1)):
        r.box(f'{name}-window-{i}', p(s*w*.32, 1.65, d/2+.03), (.55, .7, .06), 'WarmGlass', False, (0, yaw, 0))
        r.box(f'{name}-sill-{i}', p(s*w*.32, 1.25, d/2+.07), (.75, .08, .14), trim, False, (0, yaw, 0))
    for i in range(int(w/.42)):
        lx = -w/2+.2+i*.42
        r.tube(f'{name}-drip-{i}', [p(lx, h+.02, d/2+.06), p(lx, h-.22-.1*math.sin(i*1.7), d/2+.06)], .07, trim)
    if light: r.light(f'{name}-light', p(0, 1.8, d/2+.7), 'WarmGlass', 1.5, 5)


def checker(r, name, x0, x1, z0, z1, n, a, b, y=.006):
    sx, sz = (x1-x0)/n, (z1-z0)/n
    for i in range(n):
        for j in range(n):
            if (i+j) % 2: continue
            r.box(f'{name}-{i}-{j}', (x0+(i+.5)*sx, y, z0+(j+.5)*sz), (sx, .012, sz), a, collider=False)
    r.box(f'{name}-base', ((x0+x1)/2, y-.004, (z0+z1)/2), (x1-x0, .006, z1-z0), b, collider=False)


def bunting(r, name, a, b, colours, count=9, sag=.5):
    pts = []
    for i in range(count+1):
        t = i/count
        pts.append((a[0]+(b[0]-a[0])*t, a[1]+(b[1]-a[1])*t-sag*math.sin(math.pi*t), a[2]+(b[2]-a[2])*t))
    r.tube(f'{name}-cord', pts, .02, 'Icing')
    yaw = math.degrees(math.atan2(b[0]-a[0], b[2]-a[2]))+90
    for i, p in enumerate(pts[1:-1]):
        r.box(f'{name}-flag-{i}', (p[0], p[1]-.17, p[2]), (.26, .3, .02), colours[i % len(colours)], False, (0, yaw, 0))


def chandelier(r, name, x, y, z, radius=1.4, arms=6, top=None, glass='Candle', metal='Brass', intensity=2.4, reach=11):
    r.tube(f'{name}-chain', [(x, top if top else y+2.5, z), (x, y, z)], .03, metal)
    r.ring(f'{name}-ring', (x, y, z), radius, .06, metal)
    for i in range(arms):
        a = i*math.tau/arms
        r.lathe(f'{name}-cup-{i}', (x+radius*math.cos(a), y+.02, z+radius*math.sin(a)), [(.03, 0), (.07, .05)], metal, 6)
        candle(r, f'{name}-candle-{i}', x+radius*math.cos(a), y+.06, z+radius*math.sin(a), .26)
    r.light(f'{name}-light', (x, y-.2, z), glass, intensity, reach, True)


def crate(r, name, x, z, s=.9, mat='OakLight', trim='OakDark', y=0.0, yaw=0):
    r.box(name, (x, y+s/2, z), (s, s, s), mat, rot=(0, yaw, 0))
    r.box(f'{name}-band', (x, y+s/2, z), (s+.03, .08, s+.03), trim, False, (0, yaw, 0))


def wall_stair(r, prefix, sx, sz, z_end, mat, rail_mat='OakDark', y=2.7, rail_gaps=()):
    """Stair flight along the x = sx wall, starting near the z = sz corner, with a gallery deck
    continuing to z_end. Returns node names (approach, foot, mid, top)."""
    toward = 'north' if sz < 0 else 'south'
    r.stairs(f'{prefix}-stair', sx*6.3, sz*4.2, toward, 1.8, y, 4.4, mat)
    lo, hi = sorted((sz*2.0, z_end))
    r.deck(f'{prefix}-deck', min(sx*5.4, sx*7.2), max(sx*5.4, sx*7.2), lo, hi, y, mat)
    cuts = sorted(rail_gaps)
    edges = [lo] + [c for gap in cuts for c in gap] + [hi]
    for i in range(0, len(edges), 2):
        if edges[i+1]-edges[i] > .3: r.rail(f'{prefix}-rail-{i//2}', sx*5.4, edges[i], sx*5.4, edges[i+1], y, rail_mat)
    r.tube(f'{prefix}-handrail', [(sx*5.45, 1.0, sz*6.4), (sx*5.45, y+1.0, sz*2.0)], .05, 'Brass')
    for i in range(3):
        z = sz*(5.6-i*1.5); h = y*(6.4-abs(z))/4.4
        r.tube(f'{prefix}-baluster-{i}', [(sx*5.45, h, z), (sx*5.45, h+1.0, z)], .04, rail_mat)
    # The mid point sits 5 cm above the slope so a standing capsule clears the incline.
    r.nav(f'{prefix}-approach', sx*4.6, 0, sz*6.6); r.nav(f'{prefix}-foot', sx*6.3, 0, sz*6.6)
    r.nav(f'{prefix}-mid', sx*6.3, y/2+.05, sz*4.2); r.nav(f'{prefix}-top', sx*6.3, y, sz*1.8)
    r.link(f'{prefix}-approach', f'{prefix}-foot', f'{prefix}-mid', f'{prefix}-top')
    return f'{prefix}-approach', f'{prefix}-top'
