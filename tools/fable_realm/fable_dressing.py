"""Biome dressing: hiding spots, floor props, wall pieces and ceiling pieces.

`Room._dress()` places these around a finished room, clear of routes, loot, pads and doorways.
Signatures:  hide(r, name, x, z, opening, rng)   floor(r, name, x, z, rng)
             wall(r, name, p, k, t, y, rng)      ceiling(r, name, x, z, rng)
`p(lx, ly, lz)` maps wall-local coordinates (lx along the wall, lz = distance from the room centre) to room space.
"""
import math

from library_recipe import v
from room_kit import SIDES, OPPOSITE
from fable_props import mushroom, rose, crate, candle


def _frame(x, z, opening):
    """Local frame of a prop whose open/front side looks toward `opening`; +lz points to its back."""
    k = SIDES[OPPOSITE[opening]]; a = k*math.pi/2
    return (lambda lx, y, lz: (x+lx*math.cos(a)+lz*math.sin(a), y, z-lx*math.sin(a)+lz*math.cos(a))), (0, k*90, 0)


def _cover(r, name, pos):
    # Visual cover without walls: a place to crouch out of sight, not an enclosed alcove.
    r.anchors.append(dict(id=name, kind='hide', tag='cover', position=v(pos[0], 1, pos[2])))


# ---- hiding spots ---------------------------------------------------------------------------------
# Freestanding furniture with a gap behind it (between its back and the wall): crouch there out of a monster's sight.
# The cover spot sits 1.0 m behind the piece's front line; `reach` 1.6 leaves the wall 0.6 m further back.
def tall_bookcase(mat='OakDark'):
    def build(r, name, x, z, opening, rng):
        p, yaw = _frame(x, z, opening)
        r.shelf(name, *p(0, 0, 0), OPPOSITE[opening], width=1.4, height=2.6, mat=mat, rows=4)
        r.box(f'{name}-back', p(0, 1.3, .38), (1.4, 2.6, .06), mat, rot=yaw)
        _cover(r, name, p(0, 0, 1.0))
    return build, .8, 1.6


def dresser(body='OakDark', knob='Brass'):
    def build(r, name, x, z, opening, rng):
        p, yaw = _frame(x, z, opening)
        r.box(f'{name}-body', p(0, .95, 0), (1.2, 1.9, .7), body, rot=yaw)
        for i, y in enumerate((.45, .95, 1.45)):
            r.box(f'{name}-drawer-{i}', p(0, y, -.36), (1.05, .4, .03), body, False, yaw)
            for s in (-1, 1): r.box(f'{name}-knob-{i}-{s}', p(s*.3, y, -.39), (.08, .08, .05), knob, False, yaw)
        r.box(f'{name}-top', p(0, 1.93, 0), (1.3, .06, .76), body, False, yaw)
        _cover(r, name, p(0, 0, 1.0))
    return build, .75, 1.6


def table_and_chairs(board='Oak', leg='OakDark', seat='OakLight'):
    def build(r, name, x, z, opening, rng):
        p, yaw = _frame(x, z, opening)
        r.box(f'{name}-top', p(0, .88, 0), (1.4, .1, .9), board, rot=yaw)
        for i, (lx, lz) in enumerate(((-.6, -.35), (.6, -.35), (-.6, .35), (.6, .35))):
            r.box(f'{name}-leg-{i}', p(lx, .42, lz), (.1, .84, .1), leg, rot=yaw)
        # Two high-backed chairs on the room side; the gap behind the table is the hiding place.
        for i, lx in enumerate((-.35, .35)):
            r.box(f'{name}-seat-{i}', p(lx, .47, -.8), (.48, .08, .46), seat, rot=yaw)
            r.box(f'{name}-chair-back-{i}', p(lx, .95, -1.01), (.48, 1.0, .06), seat, rot=yaw)
            for j, (sx, sz) in enumerate(((-.2, -.6), (.2, -.6), (-.2, -1.0), (.2, -1.0))):
                r.box(f'{name}-chair-leg-{i}-{j}', p(lx+sx, .22, sz), (.05, .44, .05), leg, False, yaw)
        _cover(r, name, p(0, 0, 1.0))
    return build, .95, 1.6


def draped_table(cloth='Cloth', board='Oak'):
    def build(r, name, x, z, opening, rng):
        r.cloth_hide(name, x, z, opening, cloth=cloth, board=board, width=2.2, depth=2.0, nav=False)
        p, yaw = _frame(x, z, opening)
        r.lathe(f'{name}-vase', p(.5, 1.75, .2), [(.1, 0), (.2, .25), (.08, .5), (.12, .56)], 'Porcelain', 10)
        r.box(f'{name}-runner', p(0, 1.756, 0), (.6, .012, 2.3), 'ClothCream', False, yaw)
    return build, 1.3, 1.35


def armchair(fabric='Velvet', frame='OakDark'):
    def build(r, name, x, z, opening, rng):
        p, yaw = _frame(x, z, opening)
        r.box(f'{name}-seat', p(0, .3, 0), (1.3, .6, 1.1), fabric, rot=yaw)
        r.box(f'{name}-back', p(0, 1.1, .5), (1.3, 2.2, .25), fabric, rot=yaw)
        for i, s in enumerate((-1, 1)): r.box(f'{name}-arm-{i}', p(s*.72, .55, -.05), (.2, 1.1, 1.1), frame, rot=yaw)
        r.box(f'{name}-crest', p(0, 2.26, .5), (1.4, .12, .3), frame, False, yaw)
        _cover(r, name, p(0, 0, 1.15))
    return build, 1.0, 2.0


def tall_grass(blade='Leaf', tip='Hedge'):
    def build(r, name, x, z, opening, rng):
        for i in range(22):
            a, d = rng.uniform(0, math.tau), rng.uniform(.25, 1.0)
            bx, bz, h = x+d*math.cos(a), z+d*math.sin(a), rng.uniform(1.3, 1.9)
            r.tube(f'{name}-blade-{i}', [(bx, 0, bz), (bx+rng.uniform(-.1, .1), h*.6, bz+rng.uniform(-.1, .1)),
                                         (bx+rng.uniform(-.3, .3), h, bz+rng.uniform(-.3, .3))], .035, blade if i % 3 else tip)
        _cover(r, name, (x, 0, z))
    return build, .9, 1.3


def curtain(fabric='Velvet', rod='Gold'):
    def build(r, name, x, z, opening, rng):
        p, yaw = _frame(x, z, opening)
        r.tube(f'{name}-rod', [p(-1.1, 2.9, -.25), p(1.1, 2.9, -.25)], .04, rod)
        for i in range(6):
            r.box(f'{name}-fold-{i}', p(-.9+i*.36, 1.45, -.25+.05*(i % 2)), (.34, 2.9, .08), fabric, False, (0, yaw[1]+(12 if i % 2 else -12), 0))
        _cover(r, name, p(0, 0, .2))
    return build, .9, .85


def crate_stack(mat='Marzipan', trim='Chocolate'):
    def build(r, name, x, z, opening, rng):
        p, yaw = _frame(x, z, opening)
        for i, (lx, lz, s, y) in enumerate(((-.55, -.5, 1.0, 0), (.5, -.55, .9, 0), (-.5, -.5, .8, 1.0), (.95, .35, .8, 0))):
            cx, _, cz = p(lx, 0, lz); crate(r, f'{name}-crate-{i}', cx, cz, s, mat, trim, y, yaw[1]+rng.uniform(-8, 8))
        _cover(r, name, p(-.1, 0, .6))
    return build, 1.2, 1.9


def fallen_log(mat='Bark'):
    def build(r, name, x, z, opening, rng):
        p, yaw = _frame(x, z, opening)
        r.box(f'{name}-log', p(0, .5, -.3), (2.6, 1.0, .9), mat, rot=yaw)
        r.lathe(f'{name}-end', p(1.31, .5, -.3), [(0, 0), (.42, 0), (.45, .02)], 'OakLight', 10, (0, yaw[1], 90))
        for i in range(3): mushroom(r, f'{name}-shroom-{i}', *p(-.9+i*.8, 1.0, -.3)[::2], .35+.1*i, .22, 'CapRose', 1.0, False)
        _cover(r, name, p(0, 0, .65))
    return build, 1.35, 1.7


# ---- floor props ----------------------------------------------------------------------------------
def _mushrooms(r, name, x, z, rng):
    for i in range(3):
        mushroom(r, f'{name}-{i}', x+rng.uniform(-.35, .35), z+rng.uniform(-.35, .35), rng.uniform(.4, 1.0), rng.uniform(.22, .4),
                 rng.choice(('CapPlum', 'CapRose', 'CapGlow')), solid=False)
    r.blocker(f'{name}-block', (x, .35, z), (.5, .7, .5))


def _rock(r, name, x, z, rng):
    s = rng.uniform(.5, .95)
    r.box(name, (x, s*.3, z), (s, s*.6, s*.8), 'Stone', rot=(0, rng.uniform(0, 90), 0))
    r.box(f'{name}-moss', (x, s*.6+.02, z), (s*.7, .05, s*.55), 'Moss', False, (0, rng.uniform(0, 90), 0))


def _fern(r, name, x, z, rng):
    for i in range(7):
        a = i*math.tau/7+rng.uniform(-.2, .2); reach = rng.uniform(.45, .8)
        r.tube(f'{name}-frond-{i}', [(x, 0, z), (x+reach*.5*math.cos(a), .55, z+reach*.5*math.sin(a)), (x+reach*math.cos(a), .35, z+reach*math.sin(a))], .03, 'Leaf')


def _stump(r, name, x, z, rng):
    h = rng.uniform(.45, .8)
    r.lathe(f'{name}-wood', (x, 0, z), [(.5, 0), (.4, .15), (.36, h), (0, h)], 'Bark', 10); r.blocker(f'{name}-block', (x, h/2, z), (.72, h, .72))
    r.lathe(f'{name}-rings', (x, h+.004, z), [(0, 0), (.33, 0)], 'OakLight', 10)


def _barrel(r, name, x, z, rng):
    r.lathe(f'{name}-wood', (x, 0, z), [(0, 0), (.3, 0), (.38, .45), (.3, .9), (0, .9)], 'OakLight', 12); r.blocker(f'{name}-block', (x, .45, z), (.7, .9, .7))
    for i, y in enumerate((.22, .68)): r.ring(f'{name}-hoop-{i}', (x, y, z), .36, .025, 'Copper', segments=12)


def _sacks(r, name, x, z, rng):
    crate(r, f'{name}-a', x, z, rng.uniform(.6, .85), rng.choice(('Marzipan', 'ClothCream', 'IcingPink')), 'Chocolate', 0, rng.uniform(0, 60))
    r.box(f'{name}-b', (x+.1, .95, z-.05), (.5, .3, .45), 'ClothCream', False, (0, rng.uniform(0, 60), 8))


def _lollipop(r, name, x, z, rng):
    h = rng.uniform(1.5, 2.3); colour = rng.choice(('Candy', 'Mint', 'IcingPink', 'Marzipan'))
    r.tube(f'{name}-stick', [(x, 0, z), (x, h, z)], .04, 'Icing')
    r.lathe(f'{name}-pop', (x, h+.35, z), [(0, -.06), (.35, -.06), (.35, .06), (0, .06)], colour, 14, (90, rng.uniform(0, 90), 0))
    r.blocker(f'{name}-block', (x, h/2, z), (.2, h, .2))


def _gumdrop(r, name, x, z, rng):
    s = rng.uniform(.3, .5)
    r.lathe(f'{name}-drop', (x, 0, z), [(s, 0), (s*.95, s*.6), (s*.6, s*1.1), (0, s*1.25)], rng.choice(('Candy', 'Mint', 'IcingPink')), 10)
    r.blocker(f'{name}-block', (x, s*.6, z), (s*1.5, s*1.2, s*1.5))


def _spool(r, name, x, z, rng):
    r.lathe(f'{name}-spool', (x, 0, z), [(.5, 0), (.5, .1), (.28, .14), (.28, .86), (.5, .9), (.5, 1.0), (0, 1.0)], 'OakLight', 14)
    r.lathe(f'{name}-thread', (x, .14, z), [(.38, 0), (.4, .36), (.38, .72)], rng.choice(('RoseRed', 'PorcelainBlue', 'Gold')), 14)
    r.blocker(f'{name}-block', (x, .5, z), (.95, 1.0, .95))


def _thimble(r, name, x, z, rng):
    r.lathe(f'{name}-thimble', (x, 0, z), [(.36, 0), (.38, .06), (.3, .55), (.2, .68), (0, .7)], 'Brass', 12); r.blocker(f'{name}-block', (x, .35, z), (.7, .7, .7))


def _flowerpot(r, name, x, z, rng):
    r.lathe(f'{name}-pot', (x, 0, z), [(.26, 0), (.4, .55), (.44, .6), (.44, .7), (.34, .68)], 'Copper', 12); r.blocker(f'{name}-block', (x, .35, z), (.8, .7, .8))
    rose(r, f'{name}-rose', x, z, rng.uniform(.9, 1.5), rng.uniform(.7, 1.1), .6, rng.choice(('RoseRed', 'IcingPink')))


def _button(r, name, x, z, rng):
    s = rng.uniform(.4, .7)
    r.lathe(f'{name}-button', (x, 0, z), [(0, .02), (s*.7, .02), (s, .08), (s, .12), (0, .1)], rng.choice(('ToyRed', 'ToyBlue', 'Porcelain', 'ToyYellow')), 14)


def _urn(r, name, x, z, rng):
    h = rng.uniform(.9, 1.4)
    r.lathe(f'{name}-urn', (x, 0, z), [(.2, 0), (.24, .06), (.14, .2), (.36, h*.55), (.3, h*.85), (.16, h*.92), (.22, h)], rng.choice(('Marble', 'MarbleDark', 'Gold')), 12)
    r.blocker(f'{name}-block', (x, h/2, z), (.66, h, .66))


def _candelabra(r, name, x, z, rng):
    r.tube(f'{name}-stand', [(x, 0, z), (x, 1.5, z)], .04, 'Iron'); r.lathe(f'{name}-foot', (x, 0, z), [(.26, 0), (.08, .12)], 'Iron', 8)
    for i in range(3):
        a = i*math.tau/3
        r.tube(f'{name}-arm-{i}', [(x, 1.4, z), (x+.25*math.cos(a), 1.5, z+.25*math.sin(a))], .025, 'Iron')
        candle(r, f'{name}-candle-{i}', x+.25*math.cos(a), 1.5, z+.25*math.sin(a))
    r.blocker(f'{name}-block', (x, .8, z), (.3, 1.6, .3))


def _books(r, name, x, z, rng):
    y = 0
    for i in range(rng.randrange(3, 6)):
        h = rng.uniform(.08, .16)
        r.box(f'{name}-{i}', (x+rng.uniform(-.05, .05), y+h/2, z+rng.uniform(-.05, .05)), (.5, h, .36),
              rng.choice(('BookWine', 'BookTeal', 'BookOchre', 'BookInk')), False, (0, rng.uniform(-25, 25), 0)); y += h
    r.blocker(f'{name}-block', (x, y/2, z), (.55, y, .45))


def _rubble(r, name, x, z, rng):
    r.lathe(f'{name}-drum', (x, 0, z), [(.36, 0), (.36, .5), (.3, .56), (0, .56)], 'Marble', 12); r.blocker(f'{name}-block', (x, .28, z), (.72, .56, .72))
    for i in range(3): r.box(f'{name}-chip-{i}', (x+rng.uniform(-.7, .7), .07, z+rng.uniform(-.7, .7)), (.22, .14, .18), 'LimestoneDark', False, (0, rng.uniform(0, 90), 12))


# ---- wall pieces ----------------------------------------------------------------------------------
def _vine(mat='Leaf'):
    def build(r, name, p, k, t, y, rng):
        top = y+rng.uniform(1.0, 2.0)
        r.tube(f'{name}-vine', [p(t, top, 7.12), p(t+rng.uniform(-.3, .3), top-1.2, 7.05), p(t+rng.uniform(-.4, .4), top-2.4, 7.1),
                                p(t+rng.uniform(-.3, .3), max(.3, top-3.6), 7.02)], .045, mat)
        for i in range(4): r.box(f'{name}-leaf-{i}', p(t+rng.uniform(-.35, .35), top-.5-i*.75, 7.0), (.3, .02, .18), mat, False, (rng.uniform(-30, 30), k*90+rng.uniform(-40, 40), 25))
    return build


def _shelf_fungus(r, name, p, k, t, y, rng):
    for i in range(3):
        r.lathe(f'{name}-{i}', p(t+i*.3-.3, y-i*.35, 7.14), [(0, 0), (.3, -.03), (.34, -.09), (0, -.12)], rng.choice(('CapPlum', 'CapGlow', 'CapRose')), 10)


def _round_window(r, name, p, k, t, y, rng):
    r.disc(f'{name}-glass', p(t, y, 7.16), .9, .9, 'WarmGlass', (0, k*90, 0))
    for i, (dx, dy, w, h) in enumerate(((0, 0, .9, .06), (0, 0, .06, .9))): r.box(f'{name}-bar-{i}', p(t+dx, y+dy, 7.13), (w, h, .04), 'Icing', False, (0, k*90, 0))
    r.box(f'{name}-sill', p(t, y-.52, 7.1), (1.1, .08, .2), 'Icing', False, (0, k*90, 0))


def _icing_drip(r, name, p, k, t, y, rng):
    top = r.height-.45
    for i in range(6):
        lx = t-.75+i*.3
        r.tube(f'{name}-{i}', [p(lx, top, 7.12), p(lx, top-rng.uniform(.3, .9), 7.12)], .07, 'Icing')


def _plate(r, name, p, k, t, y, rng):
    s = rng.uniform(.5, .9)
    r.lathe(f'{name}-plate', p(t, y, 7.16), [(0, 0), (s*.7, 0), (s, .06), (s*1.04, .1)], rng.choice(('Porcelain', 'PorcelainBlue')), 16, (90, k*90, 0) if k % 2 == 0 else (0, 0, 90 if k == 1 else -90))


def _banner(r, name, p, k, t, y, rng):
    top = min(r.height-1.2, y+1.6); colour = rng.choice(('Velvet', 'BookTeal', 'BookInk'))
    r.box(f'{name}-cloth', p(t, top-1.3, 7.12), (1.0, 2.6, .04), colour, False, (0, k*90, 0))
    r.box(f'{name}-rod', p(t, top, 7.1), (1.2, .06, .08), 'Gold', False, (0, k*90, 0))
    r.box(f'{name}-crest', p(t, top-1.0, 7.09), (.4, .5, .03), 'Gold', False, (0, k*90, 45))


def _painting(r, name, p, k, t, y, rng):
    w, h = rng.uniform(.9, 1.5), rng.uniform(.8, 1.3)
    r.box(f'{name}-frame', p(t, y, 7.14), (w+.16, h+.16, .06), 'Gold', False, (0, k*90, rng.uniform(-3, 3)))
    r.box(f'{name}-canvas', p(t, y, 7.1), (w, h, .03), rng.choice(('MarbleDark', 'BookInk', 'Velvet')), False, (0, k*90, 0))


def _sconce(r, name, p, k, t, y, rng):
    r.box(f'{name}-arm', p(t, y, 7.05), (.08, .08, .3), 'Iron', False, (0, k*90, 0))
    candle(r, f'{name}-candle', *p(t, y+.04, 6.92))


# ---- ceiling pieces -------------------------------------------------------------------------------
def _hanging(mat, tip=None, glow=None):
    def build(r, name, x, z, rng):
        length = rng.uniform(1.2, 2.8); top = r.height
        r.tube(f'{name}-strand', [(x, top, z), (x+rng.uniform(-.2, .2), top-length*.5, z+rng.uniform(-.2, .2)), (x+rng.uniform(-.3, .3), top-length, z+rng.uniform(-.3, .3))], .03, mat)
        if tip: r.box(f'{name}-tip', (x, top-length-.12, z), (.2, .26, .2), tip, False, (0, rng.uniform(0, 90), 0))
        if glow: r.box(f'{name}-glow', (x, top-length-.12, z), (.1, .14, .1), glow, False)
    return build


DRESSING = {
    # biome: (hiding spots, floor props with radius, wall pieces, ceiling pieces)
    'forest': ([tall_grass(), fallen_log(), tall_grass('Hedge', 'Leaf')],
               [(_mushrooms, .45), (_rock, .55), (_fern, .6), (_stump, .5), (_fern, .6), (_mushrooms, .45)],
               [_vine('Leaf'), _shelf_fungus, _vine('Hedge')], [_hanging('Leaf'), _hanging('Hedge', glow='Firefly')]),
    'village': ([draped_table('ClothCream', 'OakLight'), crate_stack(), armchair('IcingPink', 'Chocolate')],
                [(_barrel, .5), (_sacks, .6), (_lollipop, .3), (_gumdrop, .45), (_barrel, .5), (_gumdrop, .45)],
                [_round_window, _icing_drip, _icing_drip], [_hanging('Icing', 'Candy'), _hanging('Icing', 'Mint'), _hanging('Copper', glow='Candle')]),
    'giant': ([tall_grass(), armchair('Cloth', 'OakLight'), draped_table('Cloth', 'OakLight')],
              [(_spool, .6), (_thimble, .45), (_flowerpot, .5), (_button, .6), (_fern, .6), (_flowerpot, .5)],
              [_vine('Leaf'), _plate, _vine('Hedge')], [_hanging('Leaf', 'RoseRed'), _hanging('Hedge')]),
    'castle': ([curtain(), armchair(), draped_table('Velvet', 'OakDark')],
               [(_urn, .45), (_candelabra, .3), (_books, .4), (_rubble, .8), (_urn, .45), (_candelabra, .3)],
               [_banner, _painting, _sconce], [_hanging('Iron', glow='Candle'), _hanging('Iron')]),
}


# More places to hide in every biome: bookcases, dressers, tables and chairs (placed after the biome's own pieces).
FURNITURE = {
    'forest': [tall_bookcase('Bark'), table_and_chairs('Oak', 'Bark', 'OakLight'), dresser('Bark', 'Brass')],
    'village': [tall_bookcase('Chocolate'), table_and_chairs('OakLight', 'Chocolate', 'IcingPink'), dresser('Chocolate', 'Icing')],
    'giant': [tall_bookcase('OakLight'), table_and_chairs('OakLight', 'OakDark', 'Oak'), dresser('OakLight', 'Copper')],
    'castle': [tall_bookcase('OakDark'), table_and_chairs('OakDark', 'OakDark', 'Velvet'), dresser('OakDark', 'Gold')],
}


from fable_tales import extend  # noqa: E402  (needs the helpers above)
extend(DRESSING)
