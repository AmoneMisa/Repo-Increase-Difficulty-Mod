"""Forest (01, 03-05) and gingerbread village (07-12) rooms."""
import math

from room_kit import Room
from fable_props import (mushroom, tree, lantern_post, fireflies, cup, candy_cane, cottage, checker, bunting, crate, wall_stair, GALLERY_EDGE)


def _views(r, start_north=False):
    z = 6.3 if start_north else -6.3
    r.view('entrance', (0, 1.65, z), (0, 1.8, -z*.4), 74)
    r.view('overview', (3.4, r.height-2.4, -6.6) if start_north else (6.2, r.height-2.2, z), (-.5, .6, 1.5 if start_north else -z*.15), 82)


def _forest(key, title, role, seed, pits=(), depth=3.2, probes=None):
    r = Room(key, title, role, seed, 'Moss', 'Bark', 'OakDark', 8.0, 'Canopy')
    r.floor(pits, depth, 'Bark', 'Earth', probes); r.shell()
    r.dressing = 'forest'; r.gate = ('OakDark', 'Bark')
    r.lighting('#27393b', '#0d1a1a', .03); r.clutter(('Leaf', 'Bark', 'Earth', 'CapRose', 'Hedge'))
    r.light('moonlight', (0, 6.6, 0), 'MoonGlass', 2.3, 15, True)
    for i in range(10):
        a = i*math.tau/10+seed
        r.tube(f'canopy-branch-{i}', [(7*math.cos(a), 7.9, 7*math.sin(a)), (4.2*math.cos(a+.3), 7.3, 4.2*math.sin(a+.3)),
                                      (1.6*math.cos(a+.9), 7.7, 1.6*math.sin(a+.9))], .16, 'Bark')
    return r


def _village(key, title, role, seed, pits=(), depth=3.2, probes=None, height=8.0):
    r = Room(key, title, role, seed, 'Biscuit', 'BiscuitDark', 'Icing', height, 'Night')
    r.floor(pits, depth, 'Chocolate', 'Chocolate', probes); r.shell(); r.stars()
    r.dressing = 'village'; r.gate = ('Chocolate', 'Icing')
    r.lighting('#33282a', '#170f10', .024); r.clutter(('Icing', 'BiscuitDark', 'Marzipan', 'Candy', 'Chocolate'), high=.2)
    r.light('dusk', (0, height-1.4, 0), 'MoonGlass', 1.5, 15, True)
    return r


def book_gate():
    r = _forest('BookGate', 'Врата раскрытой книги', 'Start', 101)
    r.truck(0, -4.2, 'north'); r.extraction(4.9, 2.6, 'west')
    # Giant open book framing the only exit.
    for i, s in enumerate((-1, 1)):
        r.box(f'cover-{i}', (s*3.7, 2.7, 5.9), (.35, 5.4, 2.7), 'BookWine', rot=(0, -s*22, 0))
        for j in range(4):
            r.box(f'page-{i}-{j}', (s*(3.3-j*.22), 2.6, 6.2+j*.12), (.05, 5.0-j*.2, 2.0), 'BookCream', False, (0, -s*(14-j*3), 0))
        r.box(f'cover-clasp-{i}', (s*3.55, 2.7, 4.75), (.12, .5, .12), 'Brass', collider=False)
    r.tube('book-spine', [(-3.2, 5.5, 6.9), (0, 6.3, 7.0), (3.2, 5.5, 6.9)], .22, 'BookWine')
    r.disc('book-emblem', (0, 5.0, 7.12), 1.3, 1.3, 'Brass', (0, 0, 0))
    out = r.hide('burrow', -5.4, 1.6, 'east', 'Bark', 'Moss')
    tree(r, 'tree-ne', 5.9, 5.9, lean=(-.4, -.3)); tree(r, 'tree-sw', -5.9, -5.7); tree(r, 'tree-se', 5.9, -5.6, lean=(-.3, .3))
    mushroom(r, 'shroom-a', -5.9, 5.7, 1.5, .8, light=True); mushroom(r, 'shroom-b', -4.7, 6.4, .9, .5, 'CapRose')
    r.box('stump', (3.9, .25, -2.6), (.8, .5, .8), 'Bark')
    lantern_post(r, 'gate-lamp-w', -2.6, 6.75); lantern_post(r, 'gate-lamp-e', 2.6, 6.75)
    r.light('extraction-glow', (4.9, 2.4, 2.6), 'MossGlow', 1.6, 6)
    fireflies(r, 'firefly', [(-3.2, 1.6, 3.6), (2.1, 2.3, -1.2), (-4.3, 2.8, -3.4), (5.6, 1.9, -.6), (-1.6, 3.2, 1.9), (3.4, 2.6, 5.1)], 3)
    r.nav('mid', 0, 0, 0.8); r.nav('glade', -3.2, 0, 4.4)
    r.link('truck-point', 'mid', 'north'); r.link('mid', 'extract'); r.link('mid', out); r.link('mid', 'glade')
    r.loot('Medium', -6.4, 0, 4.2); r.loot('Tiny', 3.9, .5, -2.6); r.loot('Small', 6.4, 0, -2.6); r.loot('Small', -6.5, 0, -2.6)
    _views(r, True)
    return r.finish()


def mushroom_choir():
    r = _forest('MushroomChoir', 'Грибной хор', 'Normal', 103, [(-2.5, 2.5, -2.5, 2.5)])
    r.box('bridge', (0, -.12, 0), (3.2, .24, 5.2), 'Oak', walk=True)
    for i in range(16): r.box(f'bridge-board-{i}', (0, .008, -2.4+i*.32), (3.2, .016, .28), 'OakLight', collider=False)
    for i, x in enumerate((-1.7, 1.7)): r.rail(f'bridge-rail-{i}', x, -2.5, x, 2.5)
    for i, x in enumerate((-2.6, 2.6)): r.rail(f'pit-rail-x{i}', x, -2.5, x, 2.5)
    for i, (x0, x1, z) in enumerate(((-2.5, -1.7, -2.6), (1.7, 2.5, -2.6), (-2.5, -1.7, 2.6), (1.7, 2.5, 2.6))): r.rail(f'pit-rail-z{i}', x0, z, x1, z)
    out = r.hide('cap-hollow', -5.2, 5.7, 'south', 'Bark', 'CapPlum')
    mushroom(r, 'giant', 5.3, 5.3, 3.8, 2.1, light=True); mushroom(r, 'elder', -4.7, -6.3, 2.5, 1.2, 'CapRose', light=True)
    for i in range(5): mushroom(r, f'choir-{i}', -6.7, -5.9+i*.85, 1.1+i*.32, .42, 'CapRose' if i % 2 else 'CapPlum', solid=False)
    r.blocker('choir-block', (-6.75, 1.2, -4.2), (.5, 2.4, 4.0))
    r.lathe('stool-stem', (5.5, 0, -5.5), [(.35, 0), (.25, .7), (.3, 1.3)], 'Stem', 12)
    r.box('stool-cap', (5.5, 1.4, -5.5), (1.7, .2, 1.7), 'CapPlum'); r.blocker('stool-block', (5.5, .65, -5.5), (.6, 1.3, .6))
    for i in range(9):
        a = i*2.4
        mushroom(r, f'deep-{i}', 1.9*math.sin(a), 1.9*math.cos(a*1.3), .5+.25*(i % 3), .3+.08*(i % 2), 'CapGlow', -3.2, False)
    r.light('pit-glow', (0, -1.6, 0), 'CapGlow', 2.2, 7)
    fireflies(r, 'spore', [(2.1, 1.4, 3.9), (-3.4, 2.2, -3.6), (4.9, 3.1, 1.2), (-1.2, 2.7, 4.6), (.9, 1.9, -4.9)], 2)
    for name, x, z in (('s-in', 0, -4.2), ('n-in', 0, 4.2), ('e-in', 4.6, 0), ('w-in', -4.6, 0), ('bridge-mid', 0, 0),
                       ('se', 4.4, -4.2), ('ne', 4.2, 4.0), ('nw', -4.4, 3.8), ('sw', -4.4, -4.2)): r.nav(name, x, 0, z)
    r.link('south', 's-in', 'bridge-mid', 'n-in', 'north'); r.link('east', 'e-in'); r.link('west', 'w-in')
    r.link('s-in', 'se', 'e-in', 'ne', 'n-in', 'nw', 'w-in', 'sw', 's-in'); r.link(out, 'nw')
    r.loot('Small', 5.5, 1.5, -5.5); r.loot('Medium', 5.7, 0, -3.0); r.loot('Big', -6.1, 0, 3.0); r.loot('Tall', 3.0, 0, 6.5)
    # The Small by the north wall leaves the east wall free: the only stretch where the dressing fits a second cover piece.
    r.loot('Tiny', -5.9, 0, 6.2); r.loot('Small', -2.9, 0, -6.6); r.loot('Medium', 2.9, 0, -6.4); r.loot('Small', 4.4, 0, 6.6)
    r.loot('Tiny', 6.7, 0, -2.6)
    _views(r)
    return r.finish()


def firefly_brook():
    r = _forest('FireflyBrook', 'Ручей светлячков', 'Passage', 104, [(-4.5, 4.5, -1.5, 1.5)])
    r.box('bridge', (-1.2, -.12, 0), (3.2, .24, 3.2), 'Oak', walk=True)
    for i in range(10): r.box(f'bridge-board-{i}', (-1.2, .008, -1.45+i*.32), (3.2, .016, .28), 'OakLight', collider=False)
    for i, x in enumerate((-2.9, .5)): r.rail(f'bridge-rail-{i}', x, -1.5, x, 1.5)
    for i, z in enumerate((-1.6, 1.6)):
        r.rail(f'bank-rail-w{i}', -4.5, z, -2.9, z); r.rail(f'bank-rail-e{i}', .5, z, 4.5, z)
    for i, x in enumerate((-4.6, 4.6)): r.rail(f'bank-end-{i}', x, -1.5, x, 1.5)
    r.box('water', (0, -2.5, 0), (9, .05, 3), 'Water', collider=False)
    r.light('water-glow', (1.5, -1.6, 0), 'Water', 2.0, 7)
    for i in range(7):
        x = -3.9+i*1.3
        r.tube(f'reed-{i}', [(x, -3.2, .9*math.sin(i*2.1)), (x+.1, -1.4, .9*math.sin(i*2.1)+.1)], .04, 'Leaf')
        r.box(f'stone-{i}', (x+.4, -3.05, -.8*math.cos(i*1.7)), (.7, .3, .6), 'Stone', collider=False, rot=(0, i*40, 0))
    out = r.hide('bank-hollow', 5.2, 5.7, 'south', 'Bark', 'Moss')
    lantern_post(r, 'lamp-a', -3.5, -2.4, 2.8); lantern_post(r, 'lamp-b', 1.1, 2.4, 2.8)
    tree(r, 'tree-nw', -6.0, 6.0); tree(r, 'tree-sw', -6.3, -6.6, radius=.35, lean=(.4, .2))
    mushroom(r, 'bank-shroom', -6.4, -5.8, 1.2, .7, 'CapRose', light=True)
    fireflies(r, 'firefly', [(2.4*math.sin(i*1.9), 1.2+.9*math.sin(i*.8)**2, 1.3*math.cos(i*2.3)) for i in range(14)], 5)
    for name, x, z in (('s-in', 0, -4.2), ('bridge-s', -1.2, -3.0), ('bridge-mid', -1.2, 0), ('bridge-n', -1.2, 3.0), ('n-in', 0, 4.2),
                       ('west-s', -5.9, -3.2), ('west-n', -5.9, 3.2), ('east-s', 5.9, -3.2), ('east-n', 5.9, 3.0)): r.nav(name, x, 0, z)
    r.link('south', 's-in', 'bridge-s', 'bridge-mid', 'bridge-n', 'n-in', 'north')
    r.link('s-in', 'west-s', 'west-n', 'n-in'); r.link('s-in', 'east-s', 'east-n', 'n-in'); r.link('east-n', out, 'n-in')
    r.loot('Medium', -6.4, 0, -4.4); r.loot('Wide', 3.6, 0, -6.6); r.loot('Small', -3.0, 0, -6.6); r.loot('Tall', -3.2, 0, 6.5)
    r.loot('Tiny', 5.9, 0, 6.2); r.loot('Small', 6.7, 0, 0); r.loot('Medium', -6.6, 0, 0.3); r.loot('Tiny', 1.4, 0, 6.7)
    _views(r)
    return r.finish()


def lost_picnic():
    r = _forest('LostPicnic', 'Забытый пикник', 'Normal', 105)
    checker(r, 'blanket', -3, 3, -3, 3, 4, 'Cloth', 'ClothCream')
    r.box('basket', (5.2, 1.1, 5.2), (3.0, 2.2, 3.0), 'OakLight')
    for i in range(5): r.box(f'basket-weave-{i}', (5.2, .3+i*.42, 5.2), (3.06, .1, 3.06), 'Oak', collider=False)
    r.box('basket-lid', (5.0, 2.45, 5.0), (3.1, .14, 3.1), 'Oak', False, (9, 0, -9))
    r.tube('basket-handle', [(3.9, 2.2, 5.2), (4.2, 3.6, 5.2), (5.2, 4.1, 5.2), (6.2, 3.6, 5.2), (6.5, 2.2, 5.2)], .12, 'Oak')
    cup(r, 'picnic-cup', -4.9, 4.9, .9, handle=200)
    out = r.cloth_hide('picnic-stool', -5.3, -5.2, 'east')
    r.box('cheese', (5.2, .6, -5.6), (2.6, 1.2, 1.6), 'Marzipan')
    for i, (x, z) in enumerate(((5.7, -5.2), (4.6, -5.9), (5.9, -6.0))): r.disc(f'cheese-hole-{i}', (x, 1.206, z), .35, .3, 'Biscuit')
    r.box('sugar-cube', (2.9, .25, -6.4), (.5, .5, .5), 'Icing')
    r.tube('fork', [(-2.6, .08, 5.9), (-.4, .1, 6.6), (.9, .16, 6.9)], .07, 'Brass')
    r.lathe('plate', (1.4, 0, 1.2), [(0, .02), (1.1, .02), (1.4, .1), (1.45, .14)], 'Porcelain', 24)
    r.lathe('jam-jar', (-6.3, 0, -2.9), [(.45, 0), (.5, .8), (.35, 1.0), (.38, 1.15)], 'RoseRed', 14)
    r.blocker('jam-block', (-6.3, .6, -2.9), (1.0, 1.2, 1.0))
    tree(r, 'tree-nw', -6.6, 6.7, radius=.3); lantern_post(r, 'lamp', -3.4, 3.4, 2.6, 'WarmGlass')
    r.light('basket-glow', (3.4, 1.6, 3.4), 'WarmGlass', 1.4, 6)
    fireflies(r, 'firefly', [(-1.4, 1.5, -2.2), (2.6, 2.1, .6), (-3.9, 2.6, 1.4), (.6, 1.8, 4.4)], 2)
    for name, x, z in (('centre', 0, 0), ('se', 3.4, -3.4), ('ne', 2.6, 2.8), ('nw', -2.6, 2.6), ('sw', -3.2, -3.2)): r.nav(name, x, 0, z)
    for side in ('north', 'east', 'south', 'west'): r.link(side, 'centre')
    r.link('south', 'se', 'east', 'ne', 'north', 'nw', 'west', 'sw', out, 'south')
    r.loot('Small', 5.2, 1.2, -5.0); r.loot('Medium', 6.3, 0, -3.2); r.loot('Tall', -6.5, 0, -1.2); r.loot('Big', -6.2, 0, 2.9)
    r.loot('Tiny', 2.9, .5, -6.4); r.loot('Small', 6.6, 0, 2.8); r.loot('Medium', 2.9, 0, 6.4); r.loot('Tiny', -2.9, 0, -6.7)
    _views(r)
    return r.finish()


def sugar_gate():
    r = _village('SugarGate', 'Сахарная застава', 'Passage', 107)
    for i, (x0, x1) in enumerate(((-7.2, -1.9), (1.9, 4.2), (6.2, 7.2))):
        r.box(f'gate-wall-{i}', ((x0+x1)/2, 2.3, 0), (x1-x0, 4.6, 1.2), 'Biscuit')
        r.box(f'gate-icing-{i}', ((x0+x1)/2, 4.68, 0), (x1-x0+.1, .16, 1.4), 'Icing', collider=False)
    r.box('gate-lintel', (0, 4.0, 0), (3.8, 1.2, 1.2), 'Biscuit'); r.box('postern-lintel', (5.2, 3.5, 0), (2.0, 2.2, 1.2), 'Biscuit')
    for i in range(11): r.box(f'merlon-{i}', (-6.6+i*1.32, 5.05, 0), (.7, .6, 1.2), 'IcingPink' if i % 2 else 'Icing', collider=False)
    for i in range(7): r.tube(f'portcullis-{i}', [(-1.5+i*.5, 3.4, 0), (-1.5+i*.5, 2.7, 0)], .05, 'Copper')
    r._add('gate-arch', 'arch', (0, 0, -.62), (3.8, 3.4, .2), 'Icing', collider=False, radius=.16)
    for i, s in enumerate((-1, 1)): candy_cane(r, f'cane-{i}', s*2.5, -1.1, 3.6, -s)
    out = r.hide('guard-booth', -5.0, 5.7, 'south', 'Biscuit', 'Roof')
    r.table('toll-table', -4.8, -4.2, 2.6, 1.2, .9, 'OakLight', 'OakDark')
    crate(r, 'sack-a', 5.9, -5.9, 1.0, 'Marzipan', 'Chocolate'); crate(r, 'sack-b', 4.8, -6.2, .7, 'Marzipan', 'Chocolate', yaw=20)
    lantern_post(r, 'lamp-s', -2.9, -3.0, 3.0, 'WarmGlass', 'Copper'); lantern_post(r, 'lamp-n', 2.9, 5.2, 3.0, 'WarmGlass', 'Copper')
    bunting(r, 'bunting', (-7, 5.2, -3.4), (7, 5.2, -3.4), ('Candy', 'Mint', 'IcingPink'), 13, .8)
    r.light('postern-light', (5.2, 2.0, 0), 'Candle', 1.2, 4)
    for name, x, z in (('gate-s', 0, -3), ('gate', 0, 0), ('gate-n', 0, 3), ('postern-s', 5.2, -2.6), ('postern', 5.2, 0), ('postern-n', 5.2, 2.6)):
        r.nav(name, x, 0, z)
    r.link('south', 'gate-s', 'gate', 'gate-n', 'north'); r.link('gate-s', 'postern-s', 'postern', 'postern-n', 'gate-n'); r.link(out, 'gate-n')
    r.loot('Small', -4.4, .9, -4.2); r.loot('Tiny', -5.5, .9, -4.2); r.loot('Medium', 3.0, 0, -6.4); r.loot('Big', -6.2, 0, -1.7)
    r.loot('Tall', 3.0, 0, 1.3); r.loot('Small', 5.9, 1.0, -5.9); r.loot('Tiny', -5.7, 0, 6.2); r.loot('Medium', 6.3, 0, 5.9)
    r.loot('Small', -6.6, 0, -6.5)
    _views(r)
    return r.finish()


def gingerbread_square():
    r = _village('GingerbreadSquare', 'Пряничная площадь', 'Normal', 108, [(-1.5, 1.5, -1.5, 1.5)], 4.0, [(0, 0)])
    for i, z in enumerate((-1.65, 1.65)): r.box(f'well-rim-z{i}', (0, .35, z), (3.6, .7, .3), 'Icing')
    for i, x in enumerate((-1.65, 1.65)): r.box(f'well-rim-x{i}', (x, .35, 0), (.3, .7, 3.0), 'Icing')
    for i, (x, z) in enumerate(((-1.65, -1.65), (1.65, -1.65), (-1.65, 1.65), (1.65, 1.65))):
        r.box(f'well-post-{i}', (x, 1.85, z), (.2, 2.3, .2), 'Chocolate')
    r.lathe('well-roof', (0, 3.0, 0), [(2.6, 0), (2.4, .15), (0, 1.5)], 'Roof', 4, (0, 45, 0))
    r.tube('well-rope', [(0, 3.1, 0), (0, .2, 0)], .03, 'Icing'); r.lathe('well-bucket', (0, .1, 0), [(.2, 0), (.28, .45)], 'Copper', 10)
    r.light('well-glow', (0, -1.2, 0), 'Mint', 2.0, 7)
    cottage(r, 'house-nw', -5.7, 5.7, 'south'); cottage(r, 'house-se', 5.7, -5.7, 'north', roof='Candy')
    out = r.hide('porch', -5.6, -5.4, 'east', 'Biscuit', 'Roof')
    r.box('cart', (5.6, .5, 5.9), (2.2, 1.0, 1.2), 'OakLight')
    for i, x in enumerate((4.8, 6.4)): r.lathe(f'cart-wheel-{i}', (x, .45, 5.2), [(0, 0), (.45, 0), (.45, .1), (0, .1)], 'Chocolate', 14, (90, 0, 0))
    lantern_post(r, 'lamp-e', 6.7, 2.6, 3.0, 'WarmGlass', 'Copper'); lantern_post(r, 'lamp-w', -6.7, -2.6, 3.0, 'WarmGlass', 'Copper')
    bunting(r, 'bunting-a', (-4.2, 4.4, 4.2), (4.2, 4.4, -4.2), ('Candy', 'Mint', 'IcingPink'), 11, .9)
    for name, x, z in (('n-in', 0, 4.2), ('e-in', 4.2, 0), ('s-in', 0, -4.2), ('w-in', -4.2, 0),
                       ('ne', 3.3, 3.3), ('nw', -3.3, 3.3), ('se', 3.3, -3.3), ('sw', -3.3, -3.3)): r.nav(name, x, 0, z)
    for side, inner in (('north', 'n-in'), ('east', 'e-in'), ('south', 's-in'), ('west', 'w-in')): r.link(side, inner)
    r.link('n-in', 'ne', 'e-in', 'se', 's-in', 'sw', 'w-in', 'nw', 'n-in'); r.link('sw', out, 's-in')
    r.loot('Tiny', 1.65, .7, 0); r.loot('Small', 5.6, 1.0, 5.9); r.loot('Medium', 2.9, 0, 6.4); r.loot('Big', 6.0, 0, 3.8)
    r.loot('Tall', -2.9, 0, 6.5); r.loot('Small', -6.6, 0, 3.0); r.loot('Medium', 2.9, 0, -6.4); r.loot('Tiny', -2.8, 0, -6.7)
    r.loot('Small', -6.2, 0, -5.4)
    _views(r)
    return r.finish()


def bakers_workshop():
    r = _village('BakersWorkshop', 'Мастерская пекаря', 'Normal', 109)
    approach, top = wall_stair(r, 'loft', -1, -1, 7.2, 'Oak', rail_gaps=((5.4, 7.2),))
    r.deck('loft-arm', -GALLERY_EDGE, -2.5, 5.4, 7.2, 2.7, 'Oak')
    r.rail('loft-arm-rail', -GALLERY_EDGE, 5.4, -2.5, 5.4, 2.7); r.rail('loft-arm-end', -2.5, 5.4, -2.5, 7.2, 2.7)
    for i, z in enumerate((-1.2, 2.2, 5.2)): r.box(f'loft-bracket-{i}', (-5.7, 2.3, z), (.9, .14, .14), 'OakDark', False, (0, 0, 28))
    r.box('oven', (5.9, 1.6, 5.3), (2.6, 3.2, 2.2), 'BiscuitDark'); r.box('oven-chimney', (6.3, 5.5, 5.6), (1.0, 4.6, 1.0), 'BiscuitDark', collider=False)
    r.box('oven-mouth', (4.58, 1.2, 5.3), (.06, 1.0, 1.3), 'Candle', collider=False); r.light('oven-fire', (4.2, 1.3, 5.3), 'Candle', 2.4, 7)
    r.table('kneading-table', .6, .4, 3.2, 1.8, .95, 'OakLight', 'OakDark')
    r.lathe('dough', (-.2, .95, .6), [(0, .3), (.4, .22), (.5, 0)], 'Marzipan', 12); r.tube('rolling-pin', [(.6, 1.02, .1), (1.9, 1.02, .5)], .07, 'Oak')
    out = r.cloth_hide('proofing-table', 5.4, -5.3, 'west', cloth='ClothCream')
    r.shelf('bread-rack', -4.0, 0, 6.84, 'north', 2.6, 2.3, 'OakDark', ('Icing', 'Biscuit', 'Marzipan', 'BiscuitDark'), 3)
    crate(r, 'flour-a', -6.5, 3.4, .9, 'ClothCream', 'Oak'); crate(r, 'flour-b', -6.6, 4.4, .7, 'ClothCream', 'Oak', yaw=15)
    for i in range(3): r.lathe(f'loaf-{i}', (-6.6, 2.7, 3.8+i*.7), [(0, .26), (.22, .2), (.28, 0)], 'Biscuit', 10)
    r.lamp('lamp-table', .6, 3.2, .4, 'WarmGlass', 'Copper', 2.0, 8); r.tube('lamp-table-chain', [(.6, 8, .4), (.6, 3.5, .4)], .02, 'Copper')
    r.lamp('lamp-loft', -6.3, 5.0, 3.0, 'WarmGlass', 'Copper', 1.4, 6)
    for name, x, z in (('a', -2.4, -1.8), ('b', 3.4, -1.8), ('c', 3.4, 2.8), ('d', -2.4, 2.8)): r.nav(name, x, 0, z)
    r.nav('loft-walk', -6.3, 2.7, 3.0); r.nav('loft-corner', -6.3, 2.7, 6.3); r.nav('loft-end', -3.6, 2.7, 6.3)
    r.link('south', 'a', 'west', 'd', 'north', 'c', 'east', 'b', 'south'); r.link('a', 'b', 'c', 'd', 'a')
    r.link(approach, 'a'); r.link(approach, 'south'); r.link(out, 'south'); r.link(out, 'b')
    r.link(top, 'loft-walk', 'loft-corner', 'loft-end')
    r.loot('Small', 1.8, .95, .9); r.loot('Tiny', -.6, .95, -.2); r.loot('Small', -3.0, 2.7, 6.8); r.loot('Tiny', -4.6, 2.7, 6.9)
    r.loot('Big', 5.8, 0, -2.9); r.loot('Tall', 2.9, 0, 6.5); r.loot('Medium', -2.9, 0, -6.2); r.loot('Medium', 6.4, 0, 2.9)
    r.loot('Tiny', 6.1, 0, -5.3)
    _views(r)
    return r.finish()


def candy_market():
    r = _village('CandyMarket', 'Конфетный рынок', 'Normal', 110)
    stalls = (('nw', -4.9, 5.9, 'Candy'), ('ne', 4.9, 5.9, 'Mint'), ('se', 4.9, -5.9, 'IcingPink'))
    for name, x, z, colour in stalls:
        s = 1 if z > 0 else -1
        r.box(f'stall-{name}', (x, .5, z), (2.6, 1.0, 1.0), 'OakLight')
        for i, dx in enumerate((-1.2, 1.2)):
            r.box(f'stall-{name}-post-{i}', (x+dx, 1.3, z-s*.4), (.12, 2.6, .12), 'Icing', collider=False)
        for i in range(5):
            r.box(f'stall-{name}-awning-{i}', (x-1.1+i*.55, 2.75, z-s*.2), (.56, .08, 1.9), colour if i % 2 else 'Icing', False, (s*14, 0, 0))
        for i in range(4):
            r.lathe(f'stall-{name}-jar-{i}', (x-.9+i*.6, 1.0, z+s*.25), [(.14, 0), (.16, .3), (.1, .36)], ('Candy', 'Mint', 'Marzipan', 'IcingPink')[i], 8)
    r.lathe('carousel-base', (0, 0, 0), [(.8, 0), (.8, .25), (.3, .4), (.22, 3.4)], 'Icing', 16)
    r.lathe('carousel-pop', (0, 3.4, 0), [(.3, 0), (1.3, .1), (1.3, .28), (.3, .4)], 'Candy', 20)
    for i in range(6): r.ring(f'carousel-swirl-{i}', (0, 3.63, 0), .25+i*.18, .03, 'Icing', segments=18)
    r.blocker('carousel-block', (0, 1.8, 0), (.8, 3.6, .8)); r.light('carousel-light', (0, 2.4, 1.0), 'Candle', 2.0, 9)
    out = r.hide('sweet-cellar', -5.6, -5.4, 'east', 'Biscuit', 'Roof')
    for i, (x, z) in enumerate(((2.6, 6.9), (-2.6, 6.9), (6.9, 2.6), (6.9, -2.6))): candy_cane(r, f'cane-{i}', x, z, 3.2, 1 if i % 2 else -1)
    bunting(r, 'bunting-a', (-7, 4.6, 2.9), (7, 4.6, 2.9), ('Candy', 'Mint', 'Marzipan'), 13, .8)
    bunting(r, 'bunting-b', (-7, 4.6, -2.9), (7, 4.6, -2.9), ('IcingPink', 'Mint', 'Candy'), 13, .8)
    lantern_post(r, 'lamp-w', -6.7, 2.7, 3.0, 'WarmGlass', 'Copper')
    for name, x, z in (('ne', 3.0, 3.0), ('nw', -3.0, 3.0), ('se', 3.0, -3.0), ('sw', -3.0, -3.0)): r.nav(name, x, 0, z)
    r.link('north', 'ne', 'east', 'se', 'south', 'sw', 'west', 'nw', 'north'); r.link('sw', out, 'south')
    r.loot('Small', -4.6, 1.0, 5.6); r.loot('Small', 4.6, 1.0, 5.6); r.loot('Small', 4.6, 1.0, -5.6); r.loot('Tiny', -5.7, 1.0, 5.6)
    r.loot('Tiny', 5.7, 1.0, -5.6); r.loot('Medium', 6.4, 0, 3.4); r.loot('Big', -6.1, 0, 4.0); r.loot('Tall', 2.9, 0, -6.6)
    r.loot('Medium', -2.9, 0, 6.2)
    _views(r)
    return r.finish()


def clockmaker():
    r = _village('Clockmaker', 'Дом часовщика', 'DeadEnd', 111)
    approach, top = wall_stair(r, 'gallery', 1, -1, 4.4, 'Oak', rail_gaps=((2.6, 4.4),))
    r.deck('gallery-north', -7.2, GALLERY_EDGE, 2.6, 4.4, 2.7, 'Oak', [(-3.6, 2.75), (0, 2.75), (3.6, 2.75)], 'OakDark')
    r.rail('gallery-north-rail', -7.2, 2.6, GALLERY_EDGE, 2.6, 2.7)
    # The mechanism lives behind a glazed partition.
    r.box('glass', (0, 4.0, 4.55), (14.4, 8.0, .1), 'Pane')
    for i in range(7): r.box(f'glass-mullion-{i}', (-6.6+i*2.2, 4.0, 4.48), (.12, 8.0, .08), 'Brass', collider=False)
    for i, y in enumerate((2.7, 5.2)): r.box(f'glass-transom-{i}', (0, y+.5, 4.48), (14.4, .1, .08), 'Brass', collider=False)
    gears = ((-3.8, 3.2, 1.9, 'Brass'), (-.6, 4.4, 2.6, 'Copper'), (3.2, 2.6, 1.6, 'Brass'), (4.8, 5.2, 1.3, 'Gold'), (-5.6, 5.9, 1.1, 'Copper'))
    for i, (x, y, rad, mat) in enumerate(gears):
        r.lathe(f'gear-{i}', (x, y, 5.6+.3*(i % 2)), [(.2, 0), (rad*.8, 0), (rad, .08), (rad*.8, .16), (.2, .16)], mat, 16, (90, 0, 0))
        for j in range(8):
            a = j*math.tau/8
            r.box(f'gear-{i}-tooth-{j}', (x+rad*math.cos(a), y+rad*math.sin(a), 5.52+.3*(i % 2)), (.3, .3, .16), mat, False, (0, 0, math.degrees(a)))
    r.tube('pendulum-rod', [(1.2, 7.4, 6.4), (1.6, 2.0, 6.4)], .05, 'Brass'); r.lathe('pendulum-bob', (1.6, 1.6, 6.4), [(0, 0), (.5, .1), (0, .2)], 'Gold', 16, (90, 0, 0))
    r.light('mechanism-light', (0, 4.2, 6.2), 'Candle', 2.6, 10)
    r.table('workbench', -6.3, -1.0, 1.4, 4.0, .95, 'OakLight', 'OakDark')
    for i in range(4): r.lathe(f'bench-gear-{i}', (-6.4+.2*(i % 2), .95, -2.4+i*.9), [(.05, 0), (.22, 0), (.22, .05), (.05, .05)], 'Brass', 10)
    r.box('grandfather', (3.0, 1.2, 2.25), (.8, 2.4, .5), 'OakDark'); r.disc('grandfather-face', (3.0, 2.0, 1.98), .5, .5, 'BookCream', (0, 0, 0))
    out = r.hide('clock-case', -5.6, -5.6, 'east', 'OakDark', 'Oak')
    r.lamp('bench-lamp', -5.4, 2.2, -1.0, 'WarmGlass', 'Brass', 1.6, 6); r.lamp('gallery-lamp', 6.3, 5.0, 1.0, 'WarmGlass', 'Brass', 1.4, 6)
    for name, x, y, z in (('m', 0, 0, -3.4), ('c', 0, 0, .4), ('bench', -4.6, 0, -1.0), ('east-floor', 4.2, 0, .2),
                          ('g-corner', 6.3, 2.7, 3.5), ('g-centre', 0, 2.7, 3.5), ('g-west', -5.0, 2.7, 3.5)): r.nav(name, x, y, z)
    r.link('south', 'm', 'c', 'bench'); r.link('m', out); r.link('m', approach); r.link('c', 'east-floor')
    r.link(top, 'g-corner', 'g-centre', 'g-west')
    r.loot('Small', -6.3, .95, -2.6); r.loot('Tiny', -6.5, .95, -.4); r.loot('Tiny', -6.2, .95, .6); r.loot('Medium', -6.5, 2.7, 3.5)
    r.loot('Small', 3.0, 2.7, 4.0); r.loot('Tall', -3.0, 0, 1.6); r.loot('Big', 6.2, 0, 1.0); r.loot('Medium', -2.9, 0, -6.5)
    r.loot('Small', 2.9, 0, -6.6); r.loot('Wide', 0, 0, 1.9)
    _views(r)
    return r.finish()


def marzipan_vault():
    r = _village('MarzipanVault', 'Марципановая кладовая', 'Extraction', 112)
    r.extraction(0, 2.2, 'south', 'Gold')
    pastel = ('Gold', 'Marzipan', 'IcingPink', 'Mint', 'Icing', 'Candy')
    for i, z in enumerate((1.2, 4.2)): r.shelf(f'rack-w-{i}', -6.84, 0, z, 'west', 2.6, 2.9, 'Chocolate', pastel)
    for i, z in enumerate((-4.6, -1.6, 1.4, 4.4)): r.shelf(f'rack-e-{i}', 6.84, 0, z, 'east', 2.6, 2.9, 'Chocolate', pastel)
    r.lathe('vault-door', (0, 3.1, 7.0), [(0, 0), (2.3, 0), (2.5, .12), (2.3, .25), (0, .25)], 'Copper', 28, (90, 0, 0))
    r.ring('vault-wheel', (0, 3.1, 6.7), .8, .07, 'Gold', segments=20)
    for i in range(6):
        a = i*math.tau/6
        r.tube(f'vault-spoke-{i}', [(0, 3.1, 6.72), (.8*math.cos(a), 3.1+.8*math.sin(a), 6.72)], .04, 'Gold')
    out = r.hide('sugar-nook', -5.4, -5.4, 'east', 'Biscuit', 'Roof')
    crate(r, 'block-a', 5.7, 6.4, 1.0, 'Marzipan', 'Chocolate'); crate(r, 'block-b', 4.6, 6.5, .8, 'IcingPink', 'Chocolate', yaw=10)
    for i, x in enumerate((-2.6, 2.6)): lantern_post(r, f'pad-lamp-{i}', x, 4.6, 3.0, 'WarmGlass', 'Copper', 1.6, 7)
    r.light('pad-glow', (0, 2.6, 2.2), 'Mint', 1.8, 7)
    for name, x, z in (('m', 0, -3.2), ('e', 4.4, -1.5), ('ne', 4.2, 3.6), ('w', -4.4, -1.0), ('nw', -4.4, 4.2)): r.nav(name, x, 0, z)
    r.link('south', 'm', 'extract'); r.link('m', out); r.link('m', 'e', 'ne', 'extract'); r.link('m', 'w', 'nw', 'extract')
    r.loot('Small', 2.9, 0, -6.6); r.loot('Medium', -2.9, 0, 6.5); r.loot('Big', 4.8, 0, -6.2); r.loot('Tiny', -6.0, 0, -5.4)
    r.loot('Small', -6.6, 0, -2.0); r.loot('Medium', 2.9, 0, 6.4); r.loot('Tiny', 5.7, 1.0, 6.4)
    _views(r)
    return r.finish()


ROOMS = [book_gate, mushroom_choir, firefly_brook, lost_picnic, sugar_gate, gingerbread_square, bakers_workshop, candy_market, clockmaker, marzipan_vault]
