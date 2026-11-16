"""Shared authoring kit for Fable Realm room recipes.

A recipe is plain JSON for the Unity LevelAuthoring compiler. The kit keeps every room on the
vanilla module contract: 15 m grid, exits on edge centres, closable exits for Normal rooms,
level points (nav), valuable volumes (loot), extraction pads, truck bay and spawns.

`Room.finish()` runs a local geometric pre-check that mirrors the Unity checks, so most
mistakes are caught before a Unity batch build.
"""
import math
import random

from library_recipe import v

SIDES = {'north': 0, 'east': 1, 'south': 2, 'west': 3}
OPPOSITE = {'north': 'south', 'south': 'north', 'east': 'west', 'west': 'east'}
ROLE_DOORS = {'Start': ('north',), 'Normal': ('north', 'east', 'south', 'west'), 'Passage': ('north', 'south'),
              'DeadEnd': ('south',), 'Extraction': ('south',)}
LOOT = {'Tiny': (.3, .3, .3), 'Small': (.5, .5, .5), 'Medium': (.9, .9, .9), 'Big': (1.4, 1.4, 1.4),
        'Wide': (2.2, 1, 1), 'Tall': (.9, 1.9, .9), 'VeryTall': (1.2, 2.9, 1.2)}

# id: (colour, grain, smoothness, metallic, emission)
PALETTE = {
    'Oak': ('#896047', .26, .25, 0, 0), 'OakLight': ('#a77d57', .22, .25, 0, 0), 'OakDark': ('#48322c', .3, .25, 0, 0),
    'Bark': ('#625047', .35, .2, 0, 0), 'Stone': ('#343b40', .1, .2, 0, 0), 'Brass': ('#c59b58', .12, .45, .55, 0),
    'BookWine': ('#743f4d', .1, .25, 0, 0), 'BookTeal': ('#366364', .1, .25, 0, 0), 'BookOchre': ('#bd8a49', .1, .25, 0, 0),
    'BookInk': ('#394351', .1, .25, 0, 0), 'BookCream': ('#c5b398', .1, .25, 0, 0),
    'WarmGlass': ('#ffb45e', 0, .3, 0, .95), 'MoonGlass': ('#69afbe', 0, .3, 0, 1.0), 'MossGlow': ('#7db98f', 0, .2, 0, .4),
    'Moss': ('#53634a', .18, .1, 0, 0), 'Leaf': ('#3f6b52', .2, .1, 0, 0), 'Hedge': ('#2f4f3c', .3, .08, 0, 0),
    'Canopy': ('#22332c', .25, .05, 0, 0), 'Earth': ('#4f463d', .3, .05, 0, 0), 'Water': ('#3f9fb0', 0, .8, 0, .5),
    'CapPlum': ('#8a4f86', .12, .3, 0, 0), 'CapRose': ('#c0667f', .12, .3, 0, 0), 'CapGlow': ('#8be5cf', 0, .3, 0, .9),
    'Stem': ('#d9cdb4', .15, .2, 0, 0), 'Firefly': ('#f6f0a0', 0, .2, 0, 2.2),
    'Biscuit': ('#b67343', .3, .1, 0, 0), 'BiscuitDark': ('#8a5230', .32, .1, 0, 0), 'Icing': ('#f7e3dc', .06, .45, 0, 0),
    'IcingPink': ('#eba7b5', .06, .45, 0, 0), 'Candy': ('#c8414f', .05, .6, 0, 0), 'Mint': ('#8fd0b4', .05, .5, 0, 0),
    'Chocolate': ('#4a2c22', .2, .35, 0, 0), 'Marzipan': ('#e9cf8c', .12, .3, 0, 0), 'Copper': ('#b8704a', .1, .5, .6, 0),
    'Roof': ('#944b6b', .2, .2, 0, 0), 'Night': ('#151b2e', .05, .05, 0, 0), 'Star': ('#fff3c4', 0, .2, 0, 2.5),
    'Porcelain': ('#ece6d6', .03, .7, 0, 0), 'PorcelainBlue': ('#4f6fa8', .03, .7, 0, 0), 'Gold': ('#d2a550', .08, .55, .7, 0),
    'RoseRed': ('#b64c75', .1, .3, 0, 0), 'Thorn': ('#3a3b2c', .3, .1, 0, 0), 'Cloth': ('#b5493f', .22, .1, 0, 0),
    'ClothCream': ('#e6dcc4', .22, .1, 0, 0), 'ToyRed': ('#c74a3c', .08, .4, 0, 0), 'ToyBlue': ('#3f6fb5', .08, .4, 0, 0),
    'ToyYellow': ('#e2b53f', .08, .4, 0, 0), 'ToyGreen': ('#4f9a62', .08, .4, 0, 0),
    'Limestone': ('#b9b3a3', .18, .15, 0, 0), 'LimestoneDark': ('#7d796f', .2, .15, 0, 0), 'Marble': ('#d9d6d0', .07, .6, 0, 0),
    'MarbleDark': ('#3a3d47', .07, .6, 0, 0), 'Velvet': ('#5c2238', .2, .1, 0, 0), 'Mirror': ('#b8c6d4', 0, .95, .9, .15),
    'Parquet': ('#9a6a42', .3, .45, 0, 0), 'ParquetDark': ('#6b4429', .3, .45, 0, 0), 'Iron': ('#2c2f36', .1, .4, .6, 0),
    'Candle': ('#ffae4a', 0, .3, 0, 1.0), 'LampGlass': ('#96501c', 0, .5, 0, 1.7), 'Wax': ('#e8dcc0', .05, .25, 0, 0),
    'Pumpkin': ('#d9822b', .2, .25, 0, 0), 'Straw': ('#d8c170', .25, .05, 0, 0), 'Hair': ('#e2b84a', .22, .3, 0, 0),
    'BeanRed': ('#e0453a', 0, .4, 0, .9), 'BeanYellow': ('#f0cf4a', 0, .4, 0, .9), 'BeanBlue': ('#4a8fe0', 0, .4, 0, .9),
    'BeanGreen': ('#4fc46a', 0, .4, 0, .9), 'BeanViolet': ('#a45ae0', 0, .4, 0, .9), 'GiantBean': ('#b9d24a', 0, .5, 0, .55),
    'Pane': ('#4f7f8a', 0, .85, 0, .12),
    'Vista': ('#ffffff', 0, .6, 0, .9),
    'SkyView': ('#ffffff', 0, .6, 0, .9),
}

# id: (pattern, metres per tile, smoothness). Rough materials stay matte; only glazed, polished or metal ones shine.
SURFACE = {
    'Oak': ('planks', 2.4, .16), 'OakLight': ('planks', 2.4, .14), 'OakDark': ('planks', 2.4, .18), 'Parquet': ('planks', 1.6, .42),
    'ParquetDark': ('planks', 1.6, .42), 'Bark': ('bark', 2.6, .04), 'Thorn': ('bark', 1.2, .05), 'Chocolate': ('blocks', 1.2, .3),
    'Stone': ('blocks', 2.0, .06), 'Limestone': ('blocks', 2.4, .08), 'LimestoneDark': ('blocks', 2.4, .06), 'Biscuit': ('pores', 1.6, .03),
    'BiscuitDark': ('pores', 1.6, .03), 'Marzipan': ('pores', 1.2, .12), 'Earth': ('mottle', 2.5, .02), 'Moss': ('mottle', 3.0, .03),
    'Leaf': ('leaves', 1.6, .1), 'Hedge': ('leaves', 1.4, .05), 'Canopy': ('leaves', 3.0, .03), 'Night': ('plain', 6.0, .02),
    'Icing': ('cracks', 1.8, .4), 'IcingPink': ('cracks', 1.8, .4), 'Porcelain': ('cracks', 2.6, .72), 'PorcelainBlue': ('cracks', 2.6, .72),
    'Marble': ('cracks', 3.0, .55), 'MarbleDark': ('cracks', 3.0, .55), 'Candy': ('plain', 1.5, .6), 'Mint': ('plain', 1.5, .5),
    'Cloth': ('weave', 1.2, .03), 'ClothCream': ('weave', 1.2, .03), 'Velvet': ('weave', 1.0, .05), 'Roof': ('blocks', 1.0, .12),
    'Stem': ('bark', 1.4, .1), 'CapPlum': ('mottle', 1.4, .28), 'CapRose': ('mottle', 1.4, .28), 'RoseRed': ('mottle', .8, .22),
    'BookWine': ('weave', .8, .12), 'BookTeal': ('weave', .8, .12), 'BookOchre': ('weave', .8, .12), 'BookInk': ('weave', .8, .12),
    'BookCream': ('plain', .8, .1), 'Brass': ('plain', 1.0, .5), 'Copper': ('plain', 1.0, .5), 'Gold': ('plain', 1.0, .6),
    'Iron': ('plain', 1.0, .35), 'Mirror': ('plain', 2.0, .95), 'ToyRed': ('plain', 1.5, .45), 'ToyBlue': ('plain', 1.5, .45),
    'ToyYellow': ('plain', 1.5, .45), 'ToyGreen': ('plain', 1.5, .45), 'Pumpkin': ('mottle', 1.0, .25), 'Straw': ('weave', .6, .04),
    'Hair': ('weave', .5, .3), 'Wax': ('plain', 1.0, .25),
}
# Minimum tile contrast per pattern; flat-coloured surfaces are what reads as plastic.
DETAIL = {'planks': .26, 'blocks': .26, 'bark': .32, 'pores': .28, 'mottle': .26, 'leaves': .3, 'cracks': .16, 'weave': .22, 'plain': .1}


def surface(name, colour, grain, smoothness, metallic, emission):
    """Material entry for a recipe: shared pattern, texel scale and roughness per material id."""
    if emission > 0:
        # 'Vista' is the painted view behind a window; other glowing materials stay flat.
        return dict(id=name, color=colour, grain=0, smoothness=smoothness, metallic=metallic, emission=emission,
                    pattern={'Vista': 'vista', 'SkyView': 'sky'}.get(name, 'plain'), scale=2)
    pattern, scale, rough = SURFACE.get(name, ('plain', 2.0, smoothness))
    return dict(id=name, color=colour, grain=max(grain, DETAIL[pattern]), smoothness=rough, metallic=metallic, emission=0, pattern=pattern, scale=scale)


def _mat(rx, ry, rz):
    """Unity Quaternion.Euler: R = Ry * Rx * Rz, degrees."""
    a, b, c = (math.radians(t) for t in (rx, ry, rz))
    sx, cx, sy, cy, sz, cz = math.sin(a), math.cos(a), math.sin(b), math.cos(b), math.sin(c), math.cos(c)
    return ((cy*cz+sy*sx*sz, -cy*sz+sy*sx*cz, sy*cx), (cx*sz, cx*cz, -sx), (-sy*cz+cy*sx*sz, sy*sz+cy*sx*cz, cy*cx))


def _mul(m, p): return tuple(m[i][0]*p[0]+m[i][1]*p[1]+m[i][2]*p[2] for i in range(3))
def _tmul(m, p): return tuple(m[0][i]*p[0]+m[1][i]*p[1]+m[2][i]*p[2] for i in range(3))
def _mm(a, b): return tuple(tuple(sum(a[i][k]*b[k][j] for k in range(3)) for j in range(3)) for i in range(3))
def _add(a, b): return (a[0]+b[0], a[1]+b[1], a[2]+b[2])
def _sub(a, b): return (a[0]-b[0], a[1]-b[1], a[2]-b[2])

# Doorways that get a gate. Dead ends and extraction rooms are entered through one, like vanilla side rooms.
GATES = {'Clockmaker': ('south',), 'MarzipanVault': ('south',), 'ForgottenToy': ('south',), 'GardenStore': ('south',),
         'StoryArchive': ('south',), 'RoyalTreasury': ('south',), 'SugarGate': ('north',), 'GingerbreadSquare': ('east',),
         'RoseMaze': ('west',), 'MirrorCourt': ('north',), 'LostPicnic': ('east',), 'SleepingObservatory': ('west',),
         'BeanstalkGrove': ('south',), 'FairyRing': ('south',), 'TeaTray': ('south',), 'CrownChapel': ('south',)}


class _Obb:
    def __init__(self, name, centre, half, rot):
        self.name, self.c, self.h, self.r = name, centre, half, rot
        self.reach = math.sqrt(sum(x*x for x in half))

    def distance(self, p):
        d = _sub(p, self.c)
        if abs(d[0]) > self.reach+1 or abs(d[2]) > self.reach+1: return 9
        q = _tmul(self.r, d)
        return math.sqrt(sum(max(0, abs(q[i])-self.h[i])**2 for i in range(3)))

    def ray(self, origin, direction, limit):
        o = _tmul(self.r, _sub(origin, self.c)); d = _tmul(self.r, direction)
        t0, t1 = 0.0, limit
        for i in range(3):
            if abs(d[i]) < 1e-9:
                if abs(o[i]) > self.h[i]: return None
                continue
            a, b = (-self.h[i]-o[i])/d[i], (self.h[i]-o[i])/d[i]
            if a > b: a, b = b, a
            t0, t1 = max(t0, a), min(t1, b)
            if t0 > t1: return None
        return t0


class RecipeError(Exception):
    pass


class Room:
    def __init__(self, key, title, role, seed, ground, wall, trim, height=8.0, ceiling='Night'):
        self.key, self.title, self.role, self.seed = key, title, role, seed
        self.ground, self.wall, self.trim, self.height, self.ceiling = ground, wall, trim, height, ceiling
        self.doors = ROLE_DOORS[role]
        self.elements, self.anchors, self.views = [], [], []
        self.nodes, self.edges, self.ids = {}, [], set()
        self.loot_count = 0
        self.pits = []
        self.dressing = ''
        self.gate = ('OakDark', 'Iron')
        self.ambient, self.fog, self.fog_density = '', '', 0.0

    # ---- primitives -------------------------------------------------------------------------
    def _add(self, name, kind, pos=(0, 0, 0), size=(1, 1, 1), mat='Stone', rot=(0, 0, 0), collider=True, nav=None, variant='', **kw):
        if name in self.ids: raise RecipeError(f'{self.key}: duplicate element {name}')
        if mat not in PALETTE: raise RecipeError(f'{self.key}: unknown material {mat}')
        self.ids.add(name)
        e = dict(id=name, kind=kind, position=v(*pos), size=v(*size), material=mat, rotation=v(*rot), collider=collider,
                 navigation=nav or ('Obstacle' if collider else 'Ignore'), **kw)
        if variant: e['variant'] = variant
        self.elements.append(e)

    def box(self, name, pos, size, mat, collider=True, rot=(0, 0, 0), walk=False, variant=''):
        self._add(name, 'box', pos, size, mat, rot, collider, 'Walkable' if walk else None, variant)

    def blocker(self, name, pos, size, rot=(0, 0, 0)):
        self._add(name, 'blocker', pos, size, self.wall, rot, True)

    def tube(self, name, points, radius, mat):
        self._add(name, 'tube', mat=mat, collider=False, points=[v(*p) for p in points], radius=radius)

    def ring(self, name, centre, radius, thickness, mat, squash=1.0, segments=32):
        x, y, z = centre
        self.tube(name, [(x+radius*math.cos(i*math.tau/segments), y, z+radius*squash*math.sin(i*math.tau/segments))
                         for i in range(segments+1)], thickness, mat)

    def lathe(self, name, pos, profile, mat, segments=20, rot=(0, 0, 0)):
        self._add(name, 'lathe', pos, mat=mat, rot=rot, collider=False, points=[v(r, y, 0) for r, y in profile], count=segments)

    def disc(self, name, pos, width, depth, mat, rot=(90, 0, 0)):
        self._add(name, 'disc', pos, (width, depth, .05), mat, rot, False)

    def light(self, name, pos, mat, intensity=2.0, reach=8.0, shadows=False):
        self._add(name, 'light', pos, mat=mat, collider=False, intensity=intensity, range=reach, shadows=shadows)

    def lamp(self, name, x, y, z, glass='WarmGlass', metal='Brass', intensity=1.8, reach=7.0):
        """Caged lantern: amber panes behind a metal frame, wide pointed roof and a carrying ring.
        The panes use a dark glass with its own glow, so the lantern keeps its colour next to its light."""
        self.lathe(name+'-base', (x, y-.27, z), [(0, 0), (.13, 0), (.14, .04), (.1, .07)], metal, 4, (0, 45, 0))
        self.lathe(name+'-glass', (x, y-.2, z), [(.085, 0), (.125, .38)], 'LampGlass', 4, (0, 45, 0))
        for i in range(4):
            a = math.pi/4+i*math.pi/2
            self.tube(f'{name}-bar-{i}', [(x+.095*math.cos(a), y-.21, z+.095*math.sin(a)), (x+.135*math.cos(a), y+.19, z+.135*math.sin(a))], .02, metal)
        self.lathe(name+'-band', (x, y-.03, z), [(.112, 0), (.118, .03)], metal, 4, (0, 45, 0))
        self.lathe(name+'-cap', (x, y+.18, z), [(.24, 0), (.2, .04), (.07, .17), (.035, .24), (0, .27)], metal, 4, (0, 45, 0))
        self.ring(name+'-ring', (x, y+.49, z), .045, .014, metal, segments=8)
        self.light(name+'-light', (x, y, z), glass, intensity, reach)

    def shelf(self, name, x, y, z, side, width=2.2, height=2.65, mat='OakDark',
              palette=('Brass', 'BookWine', 'BookTeal', 'BookOchre', 'BookInk', 'BookCream'), rows=4):
        """Bookcase whose open front faces away from `side` (the wall it stands against)."""
        yaw = {'north': 0, 'east': 90, 'south': 180, 'west': 270}[side]
        self._add(name, 'shelf', (x, y, z), (width, height, .72), mat, (0, yaw, 0), count=rows, palette=list(palette))

    def stairs(self, name, x, z, toward, width, rise, run, mat, y=0.0):
        """Stair flight centred on (x, z), bottom at height y, climbing toward `toward`."""
        self._add(name, 'stairs', (x, y, z), (width, rise, run), mat, (0, SIDES[toward]*90, 0), nav='Walkable',
                  count=max(6, round(rise/.17)))

    def rail(self, name, x0, z0, x1, z1, y=0.0, mat='OakDark', height=.9):
        if abs(x0-x1) > 1e-6 and abs(z0-z1) > 1e-6: raise RecipeError(f'{self.key}: rail {name} must be axis aligned')
        length = abs(x1-x0)+abs(z1-z0)
        self._add(name, 'rail', ((x0+x1)/2, y, (z0+z1)/2), (length, height, .15), mat, (0, 0 if abs(z0-z1) < 1e-6 else 90, 0),
                  count=max(2, int(length/.45)))

    def deck(self, name, x0, x1, z0, z1, y, mat, posts=(), post_mat=None):
        self.box(name, ((x0+x1)/2, y-.12, (z0+z1)/2), (x1-x0, .24, z1-z0), mat, walk=True)
        for i, (px, pz) in enumerate(posts):
            self.box(f'{name}-post-{i}', (px, (y-.24)/2, pz), (.3, y-.24, .3), post_mat or mat)

    # ---- structure --------------------------------------------------------------------------
    def floor(self, pits=(), depth=3.2, pit_wall='Stone', pit_floor='Earth', probes=None, mat=None):
        """Ground slab with real openings. pits: (x0, x1, z0, z1). Pit walls end below the slab base."""
        mat = mat or self.ground
        self.pits = list(pits)
        xs = sorted({-7.5, 7.5} | {p[i] for p in pits for i in (0, 1)})
        zs = sorted({-7.5, 7.5} | {p[i] for p in pits for i in (2, 3)})
        def inside(x, z): return any(p[0] < x < p[1] and p[2] < z < p[3] for p in pits)
        n = 0
        for i in range(len(xs)-1):
            start = None
            for j in range(len(zs)):
                solid = j < len(zs)-1 and not inside((xs[i]+xs[i+1])/2, (zs[j]+zs[j+1])/2)
                if solid and start is None: start = zs[j]
                if not solid and start is not None:
                    self.box(f'floor-{n}', ((xs[i]+xs[i+1])/2, -.2, (start+zs[j])/2), (xs[i+1]-xs[i], .4, zs[j]-start), mat, walk=True)
                    n += 1; start = None
        for k, (x0, x1, z0, z1) in enumerate(pits):
            cx, cz, w, d, h = (x0+x1)/2, (z0+z1)/2, x1-x0, z1-z0, depth-.45
            self.box(f'pit-{k}-bottom', (cx, -depth-.2, cz), (w+.4, .4, d+.4), pit_floor)
            for s, z in enumerate((z0-.1, z1+.1)): self.box(f'pit-{k}-wall-z{s}', (cx, -.45-h/2, z), (w+.4, h, .2), pit_wall)
            for s, x in enumerate((x0-.1, x1+.1)): self.box(f'pit-{k}-wall-x{s}', (x, -.45-h/2, cz), (.2, h, d), pit_wall)
            px, pz = probes[k] if probes else (x0+.4, z0+.4)
            self.anchors.append(dict(id=f'pit-{k}', kind='pit', position=v(px, 0, pz), size=v(w, depth, d)))

    def shell(self, plug=None, ribs=True, ceiling=True):
        """Walls with real door openings, closable plugs for Normal rooms, exits and connect points."""
        h, wall, trim = self.height, self.wall, self.trim
        for side, k in SIDES.items():
            a = k*math.pi/2
            def p(x, y, z): return (x*math.cos(a)+z*math.sin(a), y, -x*math.sin(a)+z*math.cos(a))
            yaw = (0, k*90, 0)
            if side in self.doors:
                for half in (-1, 1): self.box(f'wall-{side}-{half}', p(half*4.8, h/2, 7.35), (5.1, h, .3), wall, rot=yaw)
                self.box(f'lintel-{side}', p(0, (3.8+h)/2, 7.35), (4.5, h-3.8, .3), wall, rot=yaw)
                self._add(f'door-arch-{side}', 'arch', p(0, 0, 7.1), (4.2, 3.95, .4), trim, yaw, False, nav='Obstacle', radius=.2, palette=[wall])
                if side in GATES.get(self.key, ()): self._gate(side, k, p)
                self.anchors.append(dict(id=f'door-{side}', kind='portal', position=v(*p(0, 0, 7.5)), rotation=v(*yaw), size=v(4.2, 3.8, .4)))
                self.nav(side, *p(0, 0, 6.5), tag='connect', yaw=k*90)
                if self.role == 'Normal':
                    tag = f'closed-door-{side}'
                    self.box(f'plug-{side}', p(0, 1.9, 7.3), (4.5, 3.8, .3), plug or wall, rot=yaw, variant=tag)
                    for i, (x, tilt) in enumerate(((-.9, 28), (.9, -28))):
                        self.box(f'plug-{side}-brace-{i}', p(x, 1.9, 7.1), (.28, 4.3, .12), trim, False, (0, k*90, tilt), variant=tag)
            else:
                self.box(f'wall-{side}', p(0, h/2, 7.35), (15, h, .3), wall, rot=yaw)
            if ribs:
                rng = random.Random(self.seed*7+k)
                for i, x in enumerate((-6.6, -4.6, -2.7, 2.7, 4.6, 6.6)):
                    # Ribs lean a little and vary in width: hand-built, not extruded.
                    self.box(f'rib-{side}-{i}', p(x+rng.uniform(-.06, .06), h/2, 7.16), (rng.uniform(.18, .28), h, .1), trim, False,
                             (0, k*90, rng.uniform(-1.2, 1.2)))
                self.box(f'cornice-{side}', p(0, h-.25, 7.14), (14.4, .3, .16), trim, False, yaw)
                bays = [(-5.6, 1.5), (-3.65, 1.4), (3.65, 1.4), (5.6, 1.5)] + ([] if side in self.doors else [(-1.35, 2.2), (1.35, 2.2)])
                for i, (x, width) in enumerate(bays):
                    self.box(f'plinth-{side}-{i}', p(x, .2, 7.12), (width+.3, .4, .18), trim, False, yaw)
                    # Uneven wall panels break the flat plane and catch the light differently.
                    top = rng.uniform(2.4, h-1.6); depth = rng.uniform(.04, .14)
                    self.box(f'panel-{side}-{i}', p(x, .4+top/2, 7.2-depth/2), (width, top, depth), wall, False, (0, k*90, rng.uniform(-.8, .8)))
                    if rng.random() < .6:
                        self.box(f'patch-{side}-{i}', p(x+rng.uniform(-.3, .3), top+1.0+rng.uniform(0, 1.2), 7.17),
                                 (rng.uniform(.5, 1.1), rng.uniform(.4, .9), .06), wall, False, (0, k*90, rng.uniform(-8, 8)))
        if ceiling: self.box('ceiling', (0, h+.15, 0), (15, .3, 15), self.ceiling)
        self.anchors.append(dict(id='room', kind='volume', position=v(0, h/2, 0), size=v(14.8, h, 14.8)))

    def _gate(self, side, k, p):
        """Double gate standing ajar. Visual only; each leaf carries a `door` anchor (hinge, closed direction,
        leaf size) so the game adapter can replace it with a real hinged door."""
        rng = random.Random(self.seed*3+k); width, height = 2.0, 2.4
        wood, band = self.gate
        for i, s in enumerate((-1, 1)):
            angle = math.radians(rng.uniform(58, 82)); hinge = (s*2.05, 7.12)
            dx, dz = -s*math.cos(angle), -math.sin(angle)
            yaw = k*90 + (180-math.degrees(angle) if s > 0 else math.degrees(angle))
            def leaf(along, y, out=0.0): return p(hinge[0]+dx*along-dz*out, y, hinge[1]+dz*along+dx*out)
            self.box(f'gate-{side}-{i}', leaf(width/2, height/2), (width, height, .1), wood, False, (0, yaw, 0))
            for j, y in enumerate((.45, 1.25, 2.05)): self.box(f'gate-{side}-{i}-band-{j}', leaf(width/2, y), (width*.98, .12, .15), band, False, (0, yaw, 0))
            self.box(f'gate-{side}-{i}-stile', leaf(width-.06, height/2+.12), (.14, height+.24, .16), band, False, (0, yaw, 0))
            self.tube(f'gate-{side}-{i}-post', [p(hinge[0], 0, hinge[1]), p(hinge[0], height+.3, hinge[1])], .07, band)
            self.ring(f'gate-{side}-{i}-pull', leaf(width-.35, 1.2, .1), .1, .02, 'Brass', segments=10)
            self.anchors.append(dict(id=f'gate-{side}-{i}', kind='door', position=v(*p(hinge[0], 0, hinge[1])),
                                     rotation=v(0, k*90+(180 if s > 0 else 0), 0), size=v(width, height, .1)))

    def _dress(self):
        """Biome dressing placed around the finished room. Every solid piece is placed transactionally:
        it stays only if the whole room still passes the pre-check, so dressing never changes gameplay.
        Wall and ceiling pieces have no collision."""
        if not self.dressing: return
        import fable_dressing
        challenges, hides, floor, wall, ceiling = fable_dressing.DRESSING[self.dressing]
        rng = random.Random(self.seed*17+3)
        def shell(b): return b.name.startswith(('wall-', 'lintel-', 'floor-')) or b.name == 'ceiling'
        def solids(): return [b for b in colliders(dict(id=self.key, elements=self.elements))[0] if not shell(b) and b.c[1]+b.reach > .12]
        base = solids()
        low = [n['pos'] for n in self.nodes.values() if n['pos'][1] < 1.0]
        edges = [(self.nodes[a]['pos'], self.nodes[b]['pos']) for a, b in self.edges if min(self.nodes[a]['pos'][1], self.nodes[b]['pos'][1]) < 1.0]
        keep = []  # (x, z, radius) circles that must stay free
        for a in self.anchors:
            x, y, z = (a['position'][c] for c in 'xyz')
            if a['kind'] == 'loot' and y < .6: keep.append((x, z, max(a['size']['x'], a['size']['z'])*.55+.1))
            if a['kind'] == 'hide': keep.append((x, z, 1.7))
            if a['kind'] == 'surface': keep.append((x, z, 1.0))  # puzzle sockets and landing spots stay bare
            if a['kind'] == 'extraction': keep.append((x, z, 2.5))
            if a['kind'] == 'truck': keep.append((x, z, 3.8))
        def segment(px, pz, a, b):
            ax, az, bx, bz = a[0], a[2], b[0], b[2]; ux, uz = bx-ax, bz-az; length = ux*ux+uz*uz
            t = 0 if length < 1e-9 else max(0, min(1, ((px-ax)*ux+(pz-az)*uz)/length))
            return math.hypot(px-ax-ux*t, pz-az-uz*t)
        def free(x, z, radius, route):
            if any(p[0]-radius-.3 < x < p[1]+radius+.3 and p[2]-radius-.3 < z < p[3]+radius+.3 for p in self.pits): return False
            for side in self.doors:
                a = SIDES[side]*math.pi/2; lx, lz = x*math.cos(a)-z*math.sin(a), x*math.sin(a)+z*math.cos(a)
                if abs(lx) < 2.4+radius*.7 and lz > 5.0-radius: return False
            if any(math.hypot(x-n[0], z-n[2]) < radius+route for n in low): return False
            if any(segment(x, z, a, b) < radius+route for a, b in edges): return False
            if any(math.hypot(x-kx, z-kz) < radius+kr for kx, kz, kr in keep): return False
            return not any(b.distance((x, y, z)) < radius+.15 for b in base for y in (.3, .9, 1.5))
        shelves = []
        def clear_wall(x, z, t, side):
            """A spot for a piece hung 2.4 m up: only the wall there must be bare; the floor below may be in use."""
            if side in self.doors and abs(t) < 3.3: return False
            if any(b.distance((x, y, z)) < .95 for b in base for y in (1.9, 2.4, 3.0)): return False
            if any(math.dist((x, 2.4, z), tuple(e['position'][c] for c in 'xyz')) < max(e['size']['x'], e['size']['y'])/2+1.0
                   for e in self.elements if e['kind'] == 'disc'): return False
            if any(math.hypot(x-sx, z-sz) < 2.2 for sx, sz in shelves): return False
            return True
        known = set(precheck(self._recipe(), self.role))  # empty for kit rooms; adapted older rooms carry lint notes
        def attempt(build):
            """Build a piece; undo it if the room no longer validates."""
            mark = (len(self.elements), len(self.anchors), set(self.ids))
            build()
            if set(precheck(self._recipe(), self.role)) <= known: return True
            del self.elements[mark[0]:]; del self.anchors[mark[1]:]; self.ids = mark[2]
            return False

        # Two hiding spots first, then the high and tight places, then a third hiding spot if a wall is still free.
        # Everything stands against a wall where possible; pieces that carry their own back may stand a little way out.
        import fable_tales
        shelf, nook, screen = fable_tales.COMPACT[self.dressing]
        def count(kind, prefix): return sum(1 for a in self.anchors if a['kind'] == kind and a['id'].startswith(prefix))
        climbs = [('climb', n, c, n != 2) for n, c in enumerate(challenges)]  # index 2: wall ledges need the wall
        plan = [('cover', n, h, True) for n, h in enumerate(hides[:2])] + climbs[:2]
        # Crowded rooms get compact pieces, so every room has two hiding spots and both kinds of hard loot.
        # The nook goes in before the high places take the last free floor; shelves hang above everything else.
        plan += [('nook', 0, nook, True, lambda: count('loot', 'loot-tight') < 2)] + climbs[2:]
        plan += [('cover', n+2, h, True) for n, h in enumerate(hides[2:])]
        plan += [('screen', 0, screen, True, lambda: count('hide', '') < 2),
                 ('shelf', 0, shelf, False, lambda: count('loot', 'loot-high') < 2),
                 ('shelf', 1, shelf, False, lambda: count('loot', 'loot-high') < 2)]
        # Every spot is tried, not a random sample: crowded rooms have only a few free places and must not miss them.
        span = [round(-5.8+i*.2, 1) for i in range(59)]
        for label, n, (prop, radius, reach), may_stand_out, *wanted in plan:
            if wanted and not wanted[0](): continue
            overhead = label == 'shelf'
            spots = [(side, t, 7.2-reach) for side in SIDES for t in span]
            rng.shuffle(spots)
            if may_stand_out:
                inner = [(side, t, depth) for side in SIDES for t in span[::2] for depth in (6.0-radius*.5, 5.2, 4.4, 3.6, 2.8)
                         if abs(t) <= depth and not (side in self.doors and abs(t) < 3.4 and depth > 4.0)]
                rng.shuffle(inner); inner.sort(key=lambda s: -s[2]); spots += inner  # nearest the wall first
            failed = 0
            for side, t, depth in spots:
                a = SIDES[side]*math.pi/2
                x, z = t*math.cos(a)+depth*math.sin(a), -t*math.sin(a)+depth*math.cos(a)
                if not (clear_wall(x, z, t, side) if overhead else free(x, z, radius, .1)): continue
                if attempt(lambda: prop(self, f'{label}-{n}', x, z, OPPOSITE[side], rng)):
                    if overhead: shelves.append((x, z))
                    else: keep.append((x, z, radius+.3))
                    base = solids(); break
                failed += 1
                if failed >= 40: break  # each failed attempt costs a full room pre-check
        placed = 0
        for tries in range(700):
            if placed >= 18: break
            prop, radius = rng.choice(floor)
            if rng.random() < .65:
                edge, along = rng.uniform(5.6, 6.85-radius), rng.uniform(-6.6, 6.6)
                x, z = rng.choice(((edge, along), (-edge, along), (along, edge), (along, -edge)))
            else: x, z = rng.uniform(-6.2, 6.2), rng.uniform(-6.2, 6.2)
            if abs(x) > 6.9-radius or abs(z) > 6.9-radius or not free(x, z, radius, .75): continue
            if attempt(lambda: prop(self, f'dress-{placed}', x, z, rng)): keep.append((x, z, radius+.25)); placed += 1
        # Rooms full of routes get a second pass of small props closer to the paths, each one pre-checked.
        small = [(prop, radius) for prop, radius in floor if radius <= .5]
        for tries in range(500):
            if placed >= 10: break
            prop, radius = rng.choice(small); x, z = rng.uniform(-6.4, 6.4), rng.uniform(-6.4, 6.4)
            if not free(x, z, radius, .5): continue
            if attempt(lambda: prop(self, f'dress-{placed}', x, z, rng)): keep.append((x, z, radius+.25)); placed += 1
        hung, used = 0, []
        for tries in range(300):
            if hung >= 12: break
            k = rng.randrange(4); side = [name for name, i in SIDES.items() if i == k][0]
            t, y = rng.uniform(-6.2, 6.2), rng.uniform(2.6, self.height-2.2)
            if side in self.doors and abs(t) < 3.3: continue
            a = k*math.pi/2
            def p(lx, ly, lz, a=a): return (lx*math.cos(a)+lz*math.sin(a), ly, -lx*math.sin(a)+lz*math.cos(a))
            if any(b.distance(p(t, y, 6.9)) < 1.1 for b in base): continue
            # Keep wall pieces off windows and other wall-mounted discs.
            spot = p(t, y, 7.1)
            if any(math.dist(spot, tuple(e['position'][c] for c in 'xyz')) < max(e['size']['x'], e['size']['y'])/2+.9
                   for e in self.elements if e['kind'] == 'disc'): continue
            if any(abs(t-other) < 1.7 for wall_index, other in used if wall_index == k): continue
            rng.choice(wall)(self, f'hang-{hung}', p, k, t, y, rng); used.append((k, t)); hung += 1
        for i in range(8):
            x, z = rng.uniform(-5.5, 5.5), rng.uniform(-5.5, 5.5)
            if any(b.distance((x, self.height-1.4, z)) < 1.2 for b in base): continue
            rng.choice(ceiling)(self, f'drop-{i}', x, z, rng)

    def lighting(self, ambient, fog, density):
        """Room ambience and fog: review lighting, and the intended values for the game's room volume."""
        self.ambient, self.fog, self.fog_density = ambient, fog, density

    def clutter(self, mats, count=34, low=.05, high=.28):
        """Small loose debris on the ground. Visual only: no collision, no navigation."""
        rng = random.Random(self.seed*13+5)
        for i in range(count):
            # Debris gathers along the walls and thins out toward the middle.
            x, z = rng.uniform(-6.9, 6.9), rng.uniform(-6.9, 6.9)
            if rng.random() < .55:
                if rng.random() < .5: x = rng.choice((-1, 1))*rng.uniform(5.4, 6.95)
                else: z = rng.choice((-1, 1))*rng.uniform(5.4, 6.95)
            if any(p[0]-.3 < x < p[1]+.3 and p[2]-.3 < z < p[3]+.3 for p in self.pits): continue
            s = rng.uniform(low, high); flat = rng.uniform(.015, .05)
            self.box(f'clutter-{i}', (x, flat/2+.002, z), (s, flat, s*rng.uniform(.5, 1.4)), rng.choice(mats), False, (0, rng.uniform(0, 180), 0))

    def stars(self, count=26, mat='Star'):
        for i in range(count):
            x, z = math.sin(i*12.9898+self.seed)*6.6, math.cos(i*78.233+self.seed*.37)*6.6
            s = .04+.03*((i*7) % 3)
            self.box(f'star-{i}', (x, self.height-.03, z), (s, .02, s), mat, collider=False)

    def hide(self, name, x, z, opening, mat, roof=None, arch=None, width=2.8):
        """Alcove with opaque back, sides and roof; open toward `opening`. Returns the outside nav node."""
        k = SIDES[OPPOSITE[opening]]; a = k*math.pi/2
        def p(lx, y, lz): return (x+lx*math.cos(a)+lz*math.sin(a), y, z-lx*math.sin(a)+lz*math.cos(a))
        yaw = (0, k*90, 0)
        self.box(f'{name}-back', p(0, 1.3, 1.0), (width, 2.6, .2), mat, rot=yaw)
        for s in (-1, 1): self.box(f'{name}-side-{s}', p(s*(width/2-.1), 1.3, 0), (.2, 2.6, 2.0), mat, rot=yaw)
        self.box(f'{name}-roof', p(0, 2.7, 0), (width, .2, 2.2), roof or mat, rot=yaw)
        self._add(f'{name}-arch', 'arch', p(0, 0, -1.02), (width-.5, 2.5, .2), arch or self.trim, yaw, False, radius=.1)
        self.anchors.append(dict(id=name, kind='hide', position=v(*p(0, 1, 0)), rotation=v(*yaw)))
        self.nav(f'{name}-in', *p(0, 0, -.1)); self.nav(f'{name}-out', *p(0, 0, -1.9))
        # The alcove roof is a 2.8 m shelf: a valuable up there takes a third jump or a boost.
        self.loot('Small', *p(0, 2.8, 0), hard='high')
        self.link(f'{name}-in', f'{name}-out')
        return f'{name}-out'

    def table(self, name, x, z, w, d, top, mat, leg=None, legs=True, thick=.2):
        self.box(name, (x, top-thick/2, z), (w, thick, d), mat)
        if legs:
            for i, (sx, sz) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
                self.box(f'{name}-leg-{i}', (x+sx*(w/2-.2), (top-thick)/2, z+sz*(d/2-.2)), (.22, top-thick, .22), leg or mat)

    def cloth_hide(self, name, x, z, opening, top=1.75, cloth='Cloth', board='Oak', width=2.6, depth=2.4, nav=True):
        """Under-table refuge: a draped table, open on one side. Low roof, so no level point inside."""
        k = SIDES[OPPOSITE[opening]]; a = k*math.pi/2
        def p(lx, y, lz): return (x+lx*math.cos(a)+lz*math.sin(a), y, z-lx*math.sin(a)+lz*math.cos(a))
        yaw = (0, k*90, 0)
        self.box(f'{name}-top', p(0, top-.1, 0), (width+.3, .2, depth+.3), board, rot=yaw)
        self.box(f'{name}-drape-back', p(0, (top-.2)/2, depth/2-.05), (width, top-.2, .1), cloth, rot=yaw)
        for s in (-1, 1): self.box(f'{name}-drape-{s}', p(s*(width/2-.05), (top-.2)/2, 0), (.1, top-.2, depth), cloth, rot=yaw)
        self.box(f'{name}-hem', p(0, top-.45, -depth/2), (width, .3, .06), cloth, False, yaw)
        self.anchors.append(dict(id=name, kind='hide', position=v(*p(0, 1, depth/2-1.0)), rotation=v(*yaw)))
        if not nav: return None
        self.nav(f'{name}-out', *p(0, 0, -depth/2-.9))
        return f'{name}-out'

    # ---- gameplay anchors -------------------------------------------------------------------
    def nav(self, name, x, y, z, tag='', yaw=None):
        if name in self.nodes: raise RecipeError(f'{self.key}: duplicate nav {name}')
        self.nodes[name] = dict(pos=(x, y, z), tag=tag, yaw=yaw)

    def link(self, *names):
        for a, b in zip(names, names[1:]):
            if a not in self.nodes or b not in self.nodes: raise RecipeError(f'{self.key}: unknown nav in edge {a}-{b}')
            if (a, b) not in self.edges and (b, a) not in self.edges: self.edges.append((a, b))

    def loot(self, kind, x, y, z, yaw=0, hard=''):
        """Valuable volume. hard='high' or 'tight' marks a spot off the level-point graph (extra jumps or a crawl)."""
        self.loot_count += 1
        self.anchors.append(dict(id=f'loot-{hard+"-" if hard else ""}{self.loot_count:02d}', kind='loot', tag=kind, position=v(x, y, z), rotation=v(0, yaw, 0), size=v(*LOOT[kind])))

    def extraction(self, x, z, facing, pad='Brass'):
        """Extraction pad: flat 3.2 m square kept empty, with its own level point and room volume."""
        self.anchors.append(dict(id='extraction', kind='extraction', position=v(x, 0, z), rotation=v(0, SIDES[facing]*90, 0)))
        self.anchors.append(dict(id='extraction-zone', kind='volume', tag='extraction', position=v(x, 1.5, z), size=v(3.6, 3, 3.6)))
        for i, (dx, dz, w, d) in enumerate(((0, -1.6, 3.3, .1), (0, 1.6, 3.3, .1), (-1.6, 0, .1, 3.3), (1.6, 0, .1, 3.3))):
            self.box(f'extraction-edge-{i}', (x+dx, .012, z+dz), (w, .024, d), pad, collider=False)
        self.nav('extract', x, 0, z)

    def truck(self, x, z, facing, width=4.4, length=5.6, pad='Brass'):
        """Truck bay for the Start room: empty flat bay, truck level point, spawns and truck volume."""
        k = SIDES[facing]; a = k*math.pi/2
        def p(lx, y, lz): return (x+lx*math.cos(a)+lz*math.sin(a), y, z-lx*math.sin(a)+lz*math.cos(a))
        self.anchors.append(dict(id='truck', kind='truck', position=v(x, 0, z), rotation=v(0, k*90, 0), size=v(width, 3.2, length)))
        self.anchors.append(dict(id='truck-zone', kind='volume', tag='truck', position=v(x, 1.6, z), rotation=v(0, k*90, 0), size=v(width, 3.2, length)))
        self.nav('truck-point', *p(0, 0, length/2-.6), tag='truck', yaw=k*90)
        n = 0
        for lz in (-.4, -1.8):
            for lx in (-1.35, -.45, .45, 1.35):
                self.anchors.append(dict(id=f'spawn-{n}', kind='spawn', position=v(*p(lx, 0, lz)), rotation=v(0, k*90, 0))); n += 1
        for i, s in enumerate((-1, 1)):
            self.box(f'truck-line-{i}', p(s*width/2, .012, 0), (.1, .024, length), pad, False, (0, k*90, 0))

    def view(self, name, pos, target, fov=72):
        self.views.append(dict(id=name, position=v(*pos), target=v(*target), fieldOfView=fov))

    # ---- output and pre-check ---------------------------------------------------------------
    def finish(self):
        if not self.views: raise RecipeError(f'{self.key}: no views')
        problems = precheck(self._recipe(), self.role)
        if problems: raise RecipeError(f'{self.key} (before dressing):\n  ' + '\n  '.join(problems))
        self._dress()
        recipe = self._recipe()
        problems = precheck(recipe, self.role)
        if problems: raise RecipeError(f'{self.key}:\n  ' + '\n  '.join(problems))
        return recipe

    def _recipe(self):
        anchors = []
        for name, n in self.nodes.items():
            a = dict(id=name, kind='nav', position=v(*n['pos']),
                     connectsTo=[b if x == name else x for x, b in self.edges if name in (x, b)])
            if n['tag']: a['tag'] = n['tag']
            if n['yaw'] is not None: a['rotation'] = v(0, n['yaw'], 0)
            anchors.append(a)
        anchors += self.anchors
        used = {e['material'] for e in self.elements} | {m for e in self.elements for m in e.get('palette', [])}
        materials = [surface(k, *PALETTE[k]) for k in PALETTE if k in used]
        return dict(schemaVersion=1, id=self.key, title=self.title, seed=self.seed, bounds=v(15, self.height+4, 15),
                    materials=materials, elements=self.elements, anchors=anchors, views=self.views,
                    ambient=self.ambient, fog=self.fog, fogDensity=self.fog_density,
                    navigation=dict(radius=.35, height=1.8, climb=.25, slope=35, voxel=.08, sampleDistance=.25, maxVerticalSnap=.15))


def colliders(recipe):
    """Collision boxes exactly as the Unity compiler creates them. Returns (base, {variant: boxes})."""
    base, variants = [], {}
    for e in recipe['elements']:
        if not e.get('collider', True): continue
        pos = tuple(e['position'][k] for k in 'xyz'); size = tuple(e['size'][k] for k in 'xyz')
        rot = _mat(*(e['rotation'][k] for k in 'xyz')); out = []
        def add(local, half, r=rot): out.append(_Obb(e['id'], _add(pos, _mul(rot, local)), half, r))
        kind = e['kind']
        if kind in ('box', 'blocker'): add((0, 0, 0), tuple(s/2 for s in size))
        elif kind == 'rail':
            add((0, size[1]/2, 0), (size[0]/2, size[1]/2, .08)); add((0, size[1], 0), (size[0]/2, .06, .09))
        elif kind == 'stairs':
            w, rise, run = size; angle = math.atan2(rise, run)
            add((0, rise/2-.1/math.cos(angle), 0), (w/2, .1, math.hypot(rise, run)/2), _mm(rot, _mat(-math.degrees(angle), 0, 0)))
        elif kind == 'shelf': add((0, size[1]/2, 0), (size[0]/2, size[1]/2, size[2]/2))
        elif kind == 'light': continue
        else: raise RecipeError(f"{recipe['id']}: {kind} '{e['id']}' must not carry a collider; add a blocker instead")
        (variants.setdefault(e['variant'], []) if e.get('variant') else base).extend(out)
    return base, variants


def precheck(recipe, role):
    """Mirror of RoomChecks.ValidateGeometry / ValidateRole plus NavMesh-margin lint. Returns problem strings."""
    base, variants = colliders(recipe)
    walk = {e['id'] for e in recipe['elements'] if e['navigation'] == 'Walkable'}
    problems = []
    def ray(o, d, limit, boxes=base):
        best = None
        for b in boxes:
            t = b.ray(o, d, limit)
            if t is not None and (best is None or t < best[0]): best = (t, b)
        return best
    def near(p, radius):
        for b in base:
            if b.distance(p) < radius: return b.name
        return None
    def ground(p, tol):
        hit = ray((p[0], p[1]+.25, p[2]), (0, -1, 0), .55)
        if hit is None or abs(p[1]+.25-hit[0]-p[1]) > tol: return None
        return hit[1].name
    def standing(name, p):
        g = ground(p, .12)
        if g is None: problems.append(f'missing floor at {name}'); return
        y = .35
        while y <= 1.451:
            hit = near((p[0], p[1]+y, p[2]), .3)
            if hit: problems.append(f'blocked clearance at {name} by {hit}'); return
            y += .1
    def vec(a, key='position'):
        d = a.get(key) or {}
        return (d.get('x', 0), d.get('y', 0), d.get('z', 0))
    def filled(centre, half, rot, step=.2):
        counts = [max(1, math.ceil(2*h/step)) for h in half]
        for i in range(counts[0]+1):
            for j in range(counts[1]+1):
                for k in range(counts[2]+1):
                    local = (-half[0]+2*half[0]*i/counts[0], -half[1]+2*half[1]*j/counts[1], -half[2]+2*half[2]*k/counts[2])
                    hit = near(_add(centre, _mul(rot, local)), .005)
                    if hit: return hit
        return None

    anchors = {a['id']: a for a in recipe['anchors']}
    if len(anchors) != len(recipe['anchors']): problems.append('duplicate anchor id')
    for a in recipe['anchors']:
        p, kind, name = vec(a), a['kind'], a['id']
        rot = _mat(*vec(a, 'rotation')); forward = _mul(rot, (0, 0, 1))
        if kind == 'portal': standing(name, _sub(p, tuple(.7*c for c in forward)))
        if kind in ('nav', 'surface', 'spawn'): standing(name, p)
        if kind == 'nav':
            g = ground(p, .12)
            if g is not None and g not in walk: problems.append(f'level point {name} stands on non-walkable {g}')
            y = .55
            while y <= 1.9:
                hit = near((p[0], p[1]+y, p[2]), .44)
                if hit: problems.append(f'level point {name} too close to {hit} for the NavMesh agent'); break
                y += .15
        if kind == 'pit':
            hit = ray((p[0], p[1]+.1, p[2]), (0, -1, 0), 20)
            if hit is None or p[1]+.1-hit[0] > p[1]-vec(a, 'size')[1]+.1: problems.append(f'pit is covered at {name}')
        if kind == 'hide' and a.get('tag') == 'cover':
            if ray(p, (0, -1, 0), 1.3) is None or near(p, .3): problems.append(f'cover spot {name} has no floor or is blocked')
        elif kind == 'hide':
            back = tuple(-c for c in forward)
            if ray(p, forward, 1.3) is None or ray(p, back, 1.3) is not None or near(p, .3):
                problems.append(f'invalid hide entrance/back at {name}')
        if kind == 'loot':
            size = LOOT[a.get('tag') or 'Small']
            if ray((p[0], p[1]+.1, p[2]), (0, -1, 0), .3) is None: problems.append(f'unsupported loot volume {name}')
            hit = filled((p[0], p[1]+size[1]/2+.06, p[2]), tuple(s/2 for s in size), rot)
            if hit: problems.append(f'loot volume {name} obstructed by {hit}')
        if kind in ('extraction', 'truck'):
            pad = vec(a, 'size') if kind == 'truck' else (3.2, 2.2, 3.2)
            hit = filled((p[0], p[1]+pad[1]/2+.08, p[2]), tuple(s/2 for s in pad), rot, .3)
            if hit: problems.append(f'pad {name} obstructed by {hit}')
            for sx in (-1, 0, 1):
                for sz in (-1, 0, 1):
                    corner = _add(p, _mul(rot, (sx*(pad[0]/2-.15), 0, sz*(pad[2]/2-.15))))
                    if ground(corner, .05) is None: problems.append(f'pad {name} is not flat'); break
    for a in recipe['anchors']:
        if a['kind'] != 'nav': continue
        p = vec(a)
        for target in a.get('connectsTo', []):
            if a['id'] >= target: continue
            q = vec(anchors[target]); delta = _sub(q, p); length = math.sqrt(sum(c*c for c in delta))
            steps = max(2, math.ceil(length/.08)); bad = None
            for i in range(steps+1):
                foot = _add(p, tuple(c*i/steps for c in delta))
                bad = near((foot[0], foot[1]+.9, foot[2]), .44) or near((foot[0], foot[1]+1.6, foot[2]), .3)
                if bad: problems.append(f"edge {a['id']} -> {target} too tight at {bad}"); break
                if i % 3 == 0 and ground(foot, .15) is None:
                    problems.append(f"unsupported edge {a['id']} -> {target} near ({foot[0]:.1f}, {foot[2]:.1f})"); break
    for tag, boxes in variants.items():
        portal = anchors.get(tag[7:])
        if portal is None: problems.append(f'variant {tag} has no portal'); continue
        p = vec(portal); out = _mul(_mat(*vec(portal, 'rotation')), (0, 0, 1)); side = (out[2], 0, -out[0])
        half = max(.6, vec(portal, 'size')[0]/2-.45)
        for offset in (-half, 0, half):
            for height in (.3, 1, 1.7):
                origin = (p[0]-1.4*out[0]+side[0]*offset, p[1]+height, p[2]-1.4*out[2]+side[2]*offset)
                if ray(origin, out, 1.8, base+boxes) is None: problems.append(f'closed variant leaves a gap at {tag}')
    # Role contract.
    count = lambda kind, tag=None: sum(1 for a in recipe['anchors'] if a['kind'] == kind and (tag is None or a.get('tag') == tag))
    need_loot = {'DeadEnd': 10, 'Start': 0, 'Extraction': 5}.get(role, 8)
    if count('loot') < need_loot: problems.append(f'{role} needs {need_loot} loot volumes, has {count("loot")}')
    if count('extraction') != (1 if role in ('Extraction', 'Start') else 0): problems.append('wrong extraction count for role')
    if role == 'Start' and (count('truck') != 1 or count('spawn') < 6 or count('nav', 'truck') != 1): problems.append('Start needs truck, truck point, 6 spawns')
    return problems
