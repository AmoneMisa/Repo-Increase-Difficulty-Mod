"""Challenge features (high and tight places with hard-to-reach loot) and fairy-tale clutter.

Challenge features are never connected to the level-point graph: monsters cannot walk there and
players need extra jumps, a boost or a crawl. Their loot ids say why they are hard: loot-high-*, loot-tight-*.

The clutter draws on the public-domain tales behind the Land of a Thousand Fables: magic beans,
a pumpkin, a glass slipper, porridge bowls, three little houses, matches, golden eggs, a spinning
wheel, a red hood, a magic mirror, a long braid. All props are original primitives.
"""
import math

from fable_dressing import _frame


def _ball(radius, squash=1.0, steps=6):
    return [(radius*math.sin(math.pi*i/steps), squash*(radius-radius*math.cos(math.pi*i/steps))) for i in range(steps+1)]


# ---- high and tight places ------------------------------------------------------------------------
def jump_ledges(mat='Oak', trim='OakDark'):
    """Two wall ledges: 1.6 m (needs a second jump) and 3.0 m (a third, or a second from the low ledge)."""
    def build(r, name, x, z, opening, rng):
        p, yaw = _frame(x, z, opening)
        flip = rng.choice((-1, 1))
        for i, (lx, top) in enumerate(((-.95*flip, 1.6), (.95*flip, 3.0))):
            r.box(f'{name}-ledge-{i}', p(lx, top-.1, .2), (1.7, .2, 1.1), mat, rot=yaw)
            for j, s in enumerate((-1, 1)):
                r.tube(f'{name}-bracket-{i}-{j}', [p(lx+s*.7, top-.2, -.3), p(lx+s*.7, top-.75, .72)], .05, trim)
            r.box(f'{name}-lip-{i}', p(lx, top+.04, -.33), (1.7, .08, .06), trim, False, yaw)
        r.loot('Small', *p(-.95*flip, 1.6, .25), hard='high'); r.loot('Tiny', *p(-1.5*flip, 1.6, .3), hard='high')
        r.loot('Medium', *p(.95*flip, 3.0, .2), hard='high')
    return build, 1.0, .75


def perch(body='Stone', cap='Moss', round_cap=False):
    """Free-standing 2.2 m pillar with a small top: one valuable, out of reach of a single jump."""
    def build(r, name, x, z, opening, rng):
        r.box(f'{name}-body', (x, 1.05, z), (.8, 2.1, .8), body, rot=(0, rng.uniform(-12, 12), 0))
        r.box(f'{name}-cap', (x, 2.15, z), (1.15, .1, 1.15), cap)
        if round_cap: r.lathe(f'{name}-rim', (x, 2.0, z), [(.45, 0), (.82, .12), (.8, .2)], cap, 14)
        r.loot('Small', x, 2.2, z, hard='high')
    return build, .85, 1.1


def crawl(mat='Bark', roof='Moss'):
    """Low tunnel, 0.8 m clear: crouch or tumble in. Loot sits at the back."""
    def build(r, name, x, z, opening, rng):
        p, yaw = _frame(x, z, opening)
        for i, s in enumerate((-1, 1)): r.box(f'{name}-side-{i}', p(s*.6, .4, 0), (.15, .8, 1.8), mat, rot=yaw)
        r.box(f'{name}-roof', p(0, .875, 0), (1.35, .15, 1.8), roof, rot=yaw)
        r.box(f'{name}-back', p(0, .475, .975), (1.35, .95, .15), mat, rot=yaw)
        r._add(f'{name}-mouth', 'arch', p(0, 0, -.92), (1.05, .8, .2), roof, yaw, False, radius=.06)
        r.loot('Small', *p(0, 0, .45), hard='tight'); r.loot('Tiny', *p(.2, 0, -.25), hard='tight')
    return build, 1.0, 1.15


def squeeze(mat='Stone', cap='Moss'):
    """Two tall blocks 0.76 m apart: a slot to sidestep into, with one more valuable on top."""
    def build(r, name, x, z, opening, rng):
        p, yaw = _frame(x, z, opening)
        for i, s in enumerate((-1, 1)):
            r.box(f'{name}-block-{i}', p(s*.93, 1.15, 0), (1.1, 2.3, 1.7), mat, rot=yaw)
            r.box(f'{name}-cap-{i}', p(s*.93, 2.33, 0), (1.2, .06, 1.8), cap, False, yaw)
        r.loot('Small', *p(0, 0, .45), hard='tight'); r.loot('Tiny', *p(0, 0, -.25), hard='tight')
        r.loot('Small', *p(.93*rng.choice((-1, 1)), 2.3, 0), hard='high')
    return build, 1.35, .95


# ---- compact pieces for crowded rooms -------------------------------------------------------------
def wall_shelf(mat='Oak', trim='OakDark'):
    """Bracket shelf 2.4 m up the wall. It hangs above head height, so it fits over routes and furniture."""
    def build(r, name, x, z, opening, rng):
        p, yaw = _frame(x, z, opening)
        r.box(f'{name}-board', p(0, 2.35, 0), (1.3, .1, .5), mat, rot=yaw)
        r.box(f'{name}-lip', p(0, 2.43, -.23), (1.3, .06, .04), trim, False, yaw)
        for i, s in enumerate((-1, 1)): r.tube(f'{name}-bracket-{i}', [p(s*.5, 2.3, -.2), p(s*.5, 1.9, .24)], .035, trim)
        r.loot('Small', *p(-.28, 2.4, .02), hard='high'); r.loot('Tiny', *p(.36, 2.4, .02), hard='high')
    return build, .7, .3


def nook(mat='Stone', cap='Moss'):
    """Low slab on two blocks, 0.65 m clear: reach or crouch under it for the valuables."""
    def build(r, name, x, z, opening, rng):
        p, yaw = _frame(x, z, opening)
        for i, s in enumerate((-1, 1)): r.box(f'{name}-foot-{i}', p(s*.62, .325, 0), (.16, .65, .8), mat, rot=yaw)
        r.box(f'{name}-slab', p(0, .72, 0), (1.5, .14, .9), mat, rot=yaw)
        r.box(f'{name}-cap', p(0, .8, 0), (1.3, .03, .7), cap, False, yaw)
        r.loot('Small', *p(-.24, 0, .05), hard='tight'); r.loot('Tiny', *p(.3, 0, .1), hard='tight')
    return build, .8, .5


def screen(panel='Cloth', frame='OakDark'):
    """Three-leaf folding screen, 2 m tall, with room to crouch behind it."""
    def build(r, name, x, z, opening, rng):
        p, yaw = _frame(x, z, opening)
        for i, (lx, lz, turn) in enumerate(((-.62, -.3, 32), (0, -.5, 0), (.62, -.3, -32))):
            r.box(f'{name}-leaf-{i}', p(lx, 1.0, lz), (.72, 2.0, .06), panel, rot=(0, yaw[1]+turn, 0))
            r.box(f'{name}-top-{i}', p(lx, 2.03, lz), (.76, .06, .09), frame, False, (0, yaw[1]+turn, 0))
            r.box(f'{name}-foot-{i}', p(lx, .03, lz), (.76, .06, .09), frame, False, (0, yaw[1]+turn, 0))
        r.anchors.append(dict(id=name, kind='hide', tag='cover', position=dict(zip('xyz', p(0, 1, .25)))))
    return build, .8, .85


# biome: (shelf, nook, screen) used only when a room is still short of high places, tight places or hiding spots
COMPACT = {
    'forest': (wall_shelf('Bark', 'OakDark'), nook('Stone', 'Moss'), screen('Hedge', 'Bark')),
    'village': (wall_shelf('OakLight', 'Chocolate'), nook('Biscuit', 'Icing'), screen('IcingPink', 'Chocolate')),
    'giant': (wall_shelf('OakLight', 'Copper'), nook('BookTeal', 'BookCream'), screen('BookWine', 'BookCream')),
    'castle': (wall_shelf('Limestone', 'Iron'), nook('LimestoneDark', 'Marble'), screen('Velvet', 'Gold')),
}


# ---- fairy-tale floor props -----------------------------------------------------------------------
def _pumpkin(r, name, x, z, rng):
    s = rng.uniform(.4, .65)
    r.lathe(f'{name}-gourd', (x, 0, z), [(0, 0), (s*.7, .05), (s, s*.6), (s*.9, s*1.1), (s*.3, s*1.32), (0, s*1.28)], 'Pumpkin', 12)
    r.tube(f'{name}-stem', [(x, s*1.28, z), (x+.05, s*1.5, z), (x+.14, s*1.58, z)], .04, 'Hedge')
    r.blocker(f'{name}-block', (x, s*.65, z), (s*1.5, s*1.3, s*1.5))


def _beans(r, name, x, z, rng):
    # Three glowing beans on a plinth.
    r.box(f'{name}-plinth', (x, .4, z), (.42, .8, .42), 'MarbleDark')
    for i, mat in enumerate(('BeanRed', 'BeanYellow', 'BeanBlue')):
        r.lathe(f'{name}-bean-{i}', (x-.12+i*.12, .8, z+.06*(i % 2)), _ball(.055, 1.5), mat, 8)


def _porridge(r, name, x, z, rng):
    yaw = rng.uniform(0, 180)
    r.box(f'{name}-bench', (x, .2, z), (1.3, .4, .5), 'OakLight', rot=(0, yaw, 0))
    a = math.radians(yaw)
    for i, s in enumerate((.2, .15, .1)):
        lx = -.4+i*.4
        r.lathe(f'{name}-bowl-{i}', (x+lx*math.cos(a), .4, z-lx*math.sin(a)), [(s*.4, 0), (s, s*.7), (s*.95, s*.75), (s*.3, .03)], 'Porcelain', 10)


def _three_houses(r, name, x, z, rng):
    for i, (wall, roof) in enumerate((('Straw', 'Marzipan'), ('OakLight', 'OakDark'), ('Roof', 'Chocolate'))):
        hx = x-.5+i*.5
        r.box(f'{name}-hut-{i}', (hx, .2, z), (.38, .4, .38), wall, False)
        r.lathe(f'{name}-roof-{i}', (hx, .4, z), [(.34, 0), (0, .3)], roof, 4, (0, 45, 0))
    r.blocker(f'{name}-block', (x, .3, z), (1.5, .6, .5))


def _matches(r, name, x, z, rng):
    for i in range(6):
        a = rng.uniform(0, math.tau); length = rng.uniform(.7, 1.0)
        bx, bz = x+rng.uniform(-.25, .25), z+rng.uniform(-.25, .25)
        ex, ez = bx+length*math.cos(a), bz+length*math.sin(a)
        r.tube(f'{name}-stick-{i}', [(bx, .03, bz), (ex, .03, ez)], .025, 'OakLight')
        r.lathe(f'{name}-head-{i}', (ex, .0, ez), _ball(.045), 'Candy', 6)


def _golden_eggs(r, name, x, z, rng):
    r.lathe(f'{name}-nest', (x, 0, z), [(.2, 0), (.42, .08), (.45, .22), (.3, .2), (.12, .08)], 'Straw', 12)
    for i in range(3):
        a = i*math.tau/3
        r.lathe(f'{name}-egg-{i}', (x+.12*math.cos(a), .1, z+.12*math.sin(a)), _ball(.08, 1.3), 'Gold', 8)
    r.blocker(f'{name}-block', (x, .12, z), (.8, .24, .8))


def _storybook(r, name, x, z, rng):
    yaw = rng.uniform(0, 360)
    r.box(f'{name}-post', (x, .5, z), (.16, 1.0, .16), 'OakDark')
    r.box(f'{name}-rest', (x, 1.05, z), (.8, .06, .55), 'OakDark', False, (20, yaw, 0))
    for i, s in enumerate((-1, 1)):
        a = math.radians(yaw)
        r.box(f'{name}-page-{i}', (x+s*.19*math.cos(a), 1.12, z-s*.19*math.sin(a)), (.36, .04, .5), 'BookCream', False, (20, yaw, s*6))
    r.box(f'{name}-ribbon', (x, 1.15, z), (.03, .02, .5), 'Candy', False, (20, yaw, 0))


def _spinning_wheel(r, name, x, z, rng):
    yaw = rng.uniform(0, 180); a = math.radians(yaw); c, s = math.cos(a), math.sin(a)
    r.box(f'{name}-bench', (x, .35, z), (1.1, .1, .35), 'OakDark', False, (0, yaw, 8))
    for i, lx in enumerate((-.45, .45)): r.box(f'{name}-leg-{i}', (x+lx*c, .17, z-lx*s), (.08, .34, .08), 'OakDark', False)
    wx, wz = x+.3*c, z-.3*s
    r.tube(f'{name}-wheel', [(wx+.42*math.cos(t)*c, .85+.42*math.sin(t), wz-.42*math.cos(t)*s) for t in (i*math.tau/20 for i in range(21))], .03, 'Oak')
    for i in range(4):
        t = i*math.pi/4
        r.tube(f'{name}-spoke-{i}', [(wx-.42*math.cos(t)*c, .85-.42*math.sin(t), wz+.42*math.cos(t)*s), (wx+.42*math.cos(t)*c, .85+.42*math.sin(t), wz-.42*math.cos(t)*s)], .015, 'OakLight')
    r.tube(f'{name}-spindle', [(x-.45*c, .4, z+.45*s), (x-.5*c, .75, z+.5*s)], .015, 'Brass')
    r.blocker(f'{name}-block', (x, .65, z), (1.1, 1.3, .4), (0, yaw, 0))


def _apples(r, name, x, z, rng):
    r.lathe(f'{name}-basket', (x, 0, z), [(.22, 0), (.36, .3), (.38, .34), (.3, .3), (.2, .04)], 'OakLight', 12)
    for i in range(5):
        a = i*math.tau/5
        r.lathe(f'{name}-apple-{i}', (x+.14*math.cos(a), .26+.03*(i % 2), z+.14*math.sin(a)), _ball(.08), 'Candy' if i else 'Mint', 8)
    r.blocker(f'{name}-block', (x, .18, z), (.72, .36, .72))


def _slipper(r, name, x, z, rng):
    yaw = rng.uniform(0, 360)
    r.box(f'{name}-stool', (x, .25, z), (.5, .5, .5), 'OakDark')
    r.box(f'{name}-cushion', (x, .56, z), (.56, .12, .56), 'Velvet', False, (0, yaw, 0))
    r.box(f'{name}-sole', (x, .65, z), (.12, .03, .34), 'Pane', False, (-8, yaw, 0))
    r.box(f'{name}-heel', (x, .7, z), (.1, .12, .08), 'Pane', False, (0, yaw, 0))


# ---- fairy-tale wall and ceiling pieces -----------------------------------------------------------
def _red_hood(r, name, p, k, t, y, rng):
    y = min(y, 2.9)
    r.box(f'{name}-peg', p(t, y, 7.1), (.06, .06, .2), 'OakDark', False, (0, k*90, 0))
    r.box(f'{name}-cloak', p(t, y-.75, 7.06), (.7, 1.5, .1), 'Cloth', False, (0, k*90, 3))
    r.lathe(f'{name}-hood', p(t, y-.12, 7.0), [(.26, 0), (.3, .18), (.18, .36), (0, .4)], 'Cloth', 8)


def _magic_mirror(r, name, p, k, t, y, rng):
    rot = (0, k*90, 0)
    r.disc(f'{name}-glass', p(t, y, 7.14), .9, 1.3, 'Mirror', rot)
    r.tube(f'{name}-frame', [p(t+.47*math.cos(i*math.tau/20), y+.67*math.sin(i*math.tau/20), 7.12) for i in range(21)], .05, 'Gold')
    r.box(f'{name}-crown', p(t, y+.78, 7.1), (.3, .2, .05), 'Gold', False, (0, k*90, 45))


def _rainbow(r, name, p, k, t, y, rng):
    for i, mat in enumerate(('Candy', 'Marzipan', 'Mint', 'PorcelainBlue')):
        rad = 1.1-i*.13
        r.tube(f'{name}-band-{i}', [p(t+rad*math.cos(math.pi*j/10), y-.4+rad*math.sin(math.pi*j/10), 7.13) for j in range(11)], .05, mat)


def _story_page(r, name, p, k, t, y, rng):
    tilt = rng.uniform(-8, 8)
    r.box(f'{name}-page', p(t, y, 7.15), (.8, 1.1, .03), 'BookCream', False, (0, k*90, tilt))
    for i in range(5): r.box(f'{name}-line-{i}', p(t, y+.35-i*.16, 7.12), (.55-.1*(i % 2), .03, .02), 'BookInk', False, (0, k*90, tilt))
    r.box(f'{name}-seal', p(t+.25, y-.42, 7.11), (.12, .12, .02), 'Candy', False, (0, k*90, 45))


def _braid(r, name, x, z, rng):
    top = r.height; length = top-rng.uniform(1.2, 2.2)
    points = [(x+.09*math.sin(i*1.9), top-i*length/14, z+.09*math.cos(i*1.9)) for i in range(15)]
    r.tube(f'{name}-braid', points, .09, 'Hair'); r.tube(f'{name}-strand', [(px+.05, py, pz-.05) for px, py, pz in points], .06, 'Straw')
    r.box(f'{name}-bow', (x, top-length+.05, z), (.34, .12, .1), 'Candy', False, (0, rng.uniform(0, 90), 0))


def _mobile(r, name, x, z, rng):
    top = r.height
    r.tube(f'{name}-bar', [(x-.6, top-.8, z), (x+.6, top-.8, z)], .02, 'Brass'); r.tube(f'{name}-cord', [(x, top, z), (x, top-.8, z)], .012, 'Brass')
    for i, lx in enumerate((-.55, 0, .55)):
        drop = rng.uniform(.4, .9)
        r.tube(f'{name}-string-{i}', [(x+lx, top-.8, z), (x+lx, top-.8-drop, z)], .01, 'Brass')
        r.box(f'{name}-star-{i}', (x+lx, top-.9-drop, z), (.16, .16, .04), 'Star', False, (0, 0, 45))


# ---- landmarks: the same places, told as a tale ----------------------------------------------------
def either(tale, plain, share=.65):
    """A tale landmark or its plain twin on the same footprint, so rooms differ while placement stays the same."""
    assert tale[1:] == plain[1:], 'landmark and plain piece must share a footprint'
    def build(r, name, x, z, opening, rng): (tale if rng.random() < share else plain)[0](r, name, x, z, opening, rng)
    return (build,) + tale[1:]


def braid_tower(body='Stone', roof='Roof'):
    """The tower with no door: a braid hangs from the top room. The valuable waits up there, 2.2 m high."""
    def build(r, name, x, z, opening, rng):
        p, yaw = _frame(x, z, opening)
        r.box(f'{name}-body', (x, 1.05, z), (.8, 2.1, .8), body, rot=yaw)
        r.box(f'{name}-cap', (x, 2.15, z), (1.15, .1, 1.15), body, rot=yaw)
        for i in range(4): r.box(f'{name}-course-{i}', (x, .45+i*.5, z), (.84, .05, .84), 'OakDark', False, yaw)
        for i, (sx, sz) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
            r.tube(f'{name}-post-{i}', [p(sx*.5, 2.2, sz*.5), p(sx*.5, 3.5, sz*.5)], .04, 'OakDark')
        r.lathe(f'{name}-roof', (x, 3.5, z), [(.95, 0), (.85, .12), (.3, 1.0), (0, 1.5)], roof, 4, (0, yaw[1]+45, 0))
        r.lathe(f'{name}-finial', (x, 5.0, z), _ball(.07), 'Gold', 6)
        r.disc(f'{name}-window', p(0, 1.45, -.41), .34, .5, 'WarmGlass', yaw)
        r.box(f'{name}-sill', p(0, 1.17, -.44), (.46, .05, .1), 'OakDark', False, yaw)
        side = rng.choice((-1, 1))
        points = [p(side*.3+.05*math.sin(i*1.9), 2.3-i*.14, -.62-.04*math.cos(i*1.9)) for i in range(14)]
        r.tube(f'{name}-braid', points, .07, 'Hair'); r.tube(f'{name}-strand', [(a+.035, b, c-.035) for a, b, c in points], .045, 'Straw')
        r.box(f'{name}-bow', points[-1], (.26, .1, .08), 'Candy', False, yaw)
        r.loot('Small', x, 2.2, z, hard='high')
    return build, .85, 1.1


def pig_houses(brick='BiscuitDark', trim='Icing'):
    """The straw house and the brick house, 0.76 m apart: squeeze between them; one more valuable on the brick roof."""
    def build(r, name, x, z, opening, rng):
        p, yaw = _frame(x, z, opening)
        flip = rng.choice((-1, 1))
        for i, (s, wall) in enumerate(((-flip, 'Straw'), (flip, brick))):
            r.box(f'{name}-block-{i}', p(s*.93, 1.15, 0), (1.1, 2.3, 1.7), wall, rot=yaw)
            r.box(f'{name}-door-{i}', p(s*.93, .7, -.86), (.5, 1.4, .04), 'Chocolate', False, yaw)
            r.disc(f'{name}-window-{i}', p(s*.93, 1.85, -.87), .3, .3, 'WarmGlass', yaw)
        # Thatch: a shaggy pitched roof, no collision. Brick: a flat roof with a chimney, and the valuable.
        r.lathe(f'{name}-thatch', p(-flip*.93, 2.3, 0), [(1.15, -.12), (1.05, 0), (.5, .7), (0, 1.0)], 'Straw', 4, (0, yaw[1]+45, 0))
        for i in range(5): r.tube(f'{name}-straw-{i}', [p(-flip*.93-.5+i*.25, 2.25, -.9), p(-flip*.93-.5+i*.25, 1.95-.08*(i % 2), -.95)], .03, 'Straw')
        r.box(f'{name}-eave', p(flip*.93, 2.33, 0), (1.22, .06, 1.82), trim, False, yaw)
        r.box(f'{name}-chimney', p(flip*1.25, 2.65, .6), (.26, .7, .26), brick, False, yaw)
        r.box(f'{name}-chimney-top', p(flip*1.25, 3.02, .6), (.34, .06, .34), trim, False, yaw)
        r.loot('Small', *p(0, 0, .45), hard='tight'); r.loot('Tiny', *p(0, 0, -.25), hard='tight')
        r.loot('Small', *p(flip*.85, 2.3, -.1), hard='high')
    return build, 1.35, .95


def grandmothers_bed(frame='OakDark', quilt='Cloth'):
    """Grandmother's bed with the wolf under the quilt. Crawl beneath it, 0.8 m clear, for the valuables."""
    def build(r, name, x, z, opening, rng):
        p, yaw = _frame(x, z, opening)
        for i, s in enumerate((-1, 1)): r.box(f'{name}-side-{i}', p(s*.6, .4, 0), (.15, .8, 1.8), frame, rot=yaw)
        r.box(f'{name}-roof', p(0, .875, 0), (1.35, .15, 1.8), frame, rot=yaw)
        r.box(f'{name}-back', p(0, .475, .975), (1.35, .95, .15), frame, rot=yaw)
        r.box(f'{name}-mattress', p(0, 1.05, 0), (1.3, .2, 1.75), 'ClothCream', rot=yaw)
        r.box(f'{name}-headboard', p(0, 1.5, .975), (1.5, 1.1, .12), frame, False, yaw)
        for i, s in enumerate((-1, 1)): r.lathe(f'{name}-knob-{i}', p(s*.7, 2.05, .975), _ball(.08), 'Brass', 6)
        r.box(f'{name}-quilt', p(0, 1.2, -.3), (1.4, .14, 1.2), quilt, False, yaw)
        r.box(f'{name}-fold', p(0, 1.24, .28), (1.4, .1, .22), 'ClothCream', False, yaw)
        r.box(f'{name}-pillow', p(0, 1.22, .68), (.9, .16, .4), 'Icing', False, yaw)
        # The wolf: grey head on the pillow, long snout, two ears and a nightcap.
        r.lathe(f'{name}-wolf-head', p(0, 1.28, .6), _ball(.19, .9), 'Stone', 8)
        r.box(f'{name}-wolf-snout', p(0, 1.42, .38), (.14, .12, .3), 'Stone', False, yaw)
        r.box(f'{name}-wolf-nose', p(0, 1.44, .22), (.08, .07, .05), 'BookInk', False, yaw)
        for i, s in enumerate((-1, 1)): r.lathe(f'{name}-wolf-ear-{i}', p(s*.13, 1.58, .66), [(.06, 0), (0, .16)], 'Stone', 4)
        r.lathe(f'{name}-nightcap', p(0, 1.58, .78), [(.17, 0), (.1, .14), (.03, .3)], 'IcingPink', 8, (35, yaw[1], 0))
        r.loot('Small', *p(0, 0, .45), hard='tight'); r.loot('Tiny', *p(.2, 0, -.25), hard='tight')
    return build, 1.0, 1.15


def bears_table(cloth='ClothCream', board='OakLight'):
    """The three bears' table: a great bowl, a middle bowl and a wee bowl of porridge. Hide under the cloth."""
    def build(r, name, x, z, opening, rng):
        r.cloth_hide(name, x, z, opening, cloth=cloth, board=board, width=2.2, depth=2.0, nav=False)
        p, yaw = _frame(x, z, opening)
        for i, (lx, s) in enumerate(((-.75, .3), (0, .22), (.65, .15))):
            r.lathe(f'{name}-bowl-{i}', p(lx, 1.75, -.1), [(s*.4, 0), (s, s*.7), (s*.95, s*.75), (s*.3, .03)], 'Porcelain', 12)
            r.lathe(f'{name}-porridge-{i}', p(lx, 1.75+s*.55, -.1), [(0, .02), (s*.8, 0)], 'Marzipan', 12)
            r.tube(f'{name}-spoon-{i}', [p(lx+s*.4, 1.75+s*.6, -.1), p(lx+s*1.5, 1.75+s*1.1, -.25)], .02, 'OakDark')
            # Three chair backs in three sizes stand behind the table, on the wall side, clear of the way in.
            r.box(f'{name}-chairback-{i}', p(lx, 1.75+s*2.2, .95), (s*1.8, s*4.4, .06), board, False, yaw)
    return build, 1.3, 1.35


def _rainbows_end(r, name, x, z, rng):
    """A rainbow under the ceiling from one cloud to another, with the pot of gold at its end. One per room."""
    if getattr(r, 'has_rainbow', False): return _mobile(r, name, x, z, rng)
    r.has_rainbow = True
    a = rng.uniform(0, math.pi); c, s = math.cos(a), math.sin(a); base = r.height-4.4
    span = min(3.0, 6.2-max(abs(x), abs(z)))
    for i, mat in enumerate(('Candy', 'Marzipan', 'BeanYellow', 'Mint', 'PorcelainBlue', 'CapPlum')):
        rad = span-i*.14
        r.tube(f'{name}-band-{i}', [(x+rad*math.cos(math.pi*j/14)*c, base+rad*math.sin(math.pi*j/14), z+rad*math.cos(math.pi*j/14)*s) for j in range(15)], .075, mat)
    for e, side in enumerate((-1, 1)):
        cx, cz = x+side*(span-.35)*c, z+side*(span-.35)*s
        for i, (dx, dy, size) in enumerate(((-.45, 0, .34), (0, .08, .46), (.45, 0, .36), (.2, -.1, .3))):
            r.lathe(f'{name}-cloud-{e}-{i}', (cx+dx*c, base-.3+dy, cz+dx*s), _ball(size, .8), 'Icing', 8)
    px, pz = x+(span-.35)*c, z+(span-.35)*s
    r.lathe(f'{name}-pot', (px, base+.1, pz), [(.16, 0), (.3, .12), (.32, .3), (.24, .42), (.28, .46)], 'Iron', 10)
    r.lathe(f'{name}-gold', (px, base+.5, pz), [(.24, 0), (.14, .1), (0, .14)], 'Gold', 10)
    r.light(f'{name}-glint', (px, base+.9, pz), 'Gold', 1.2, 4)


def _set(crawl_mats, squeeze_mats, ledge_mats, perch_a, perch_b, bed, brick, tower):
    return [either(grandmothers_bed(*bed), crawl(*crawl_mats)), either(pig_houses(*brick), squeeze(*squeeze_mats)), jump_ledges(*ledge_mats),
            either(braid_tower(*tower), perch(*perch_a), .5), perch(*perch_b)]


CHALLENGES = {
    'forest': _set(('Bark', 'Moss'), ('Stone', 'Moss'), ('Bark', 'OakDark'), ('Bark', 'CapPlum', True), ('Stone', 'Moss'),
                   ('Bark', 'Cloth'), ('Stone', 'Moss'), ('Stone', 'Roof')),
    'village': _set(('Biscuit', 'Icing'), ('BiscuitDark', 'Icing'), ('OakLight', 'Chocolate'), ('Icing', 'Candy', True), ('Chocolate', 'IcingPink'),
                    ('Chocolate', 'Cloth'), ('BiscuitDark', 'Icing'), ('Biscuit', 'Roof')),
    'giant': _set(('BookTeal', 'BookCream'), ('BookWine', 'BookCream'), ('OakLight', 'Copper'), ('OakLight', 'ToyRed', True), ('Porcelain', 'PorcelainBlue', True),
                  ('OakLight', 'Cloth'), ('BookWine', 'BookCream'), ('Porcelain', 'ToyRed')),
    'castle': _set(('LimestoneDark', 'Marble'), ('LimestoneDark', 'Marble'), ('Limestone', 'Iron'), ('Marble', 'Gold'), ('MarbleDark', 'Marble'),
                   ('OakDark', 'Velvet'), ('LimestoneDark', 'Marble'), ('Limestone', 'Velvet')),
}


FABLE_FLOOR = [(_pumpkin, .6), (_beans, .35), (_porridge, .75), (_three_houses, .85), (_matches, .7), (_golden_eggs, .5),
               (_storybook, .5), (_spinning_wheel, .7), (_apples, .45), (_slipper, .4)]
FABLE_WALL = [_red_hood, _magic_mirror, _rainbow, _story_page]
FABLE_CEILING = [_braid, _mobile, _rainbows_end]


def extend(dressing):
    """Add the challenge features and the fairy-tale props to every biome's dressing set."""
    for biome, (hides, floor, wall, ceiling) in list(dressing.items()):
        # The bears' table is the first hiding spot tried, so most rooms that have the wall space get one.
        table = bears_table(*{'forest': ('ClothCream', 'Bark'), 'village': ('ClothCream', 'OakLight'),
                              'giant': ('Cloth', 'OakLight'), 'castle': ('Velvet', 'OakDark')}[biome])
        dressing[biome] = (CHALLENGES[biome], [table]+hides, floor+FABLE_FLOOR, wall+FABLE_WALL, ceiling+FABLE_CEILING)
