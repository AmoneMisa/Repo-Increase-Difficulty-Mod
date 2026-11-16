"""Giant's garden (13-18) and ivory castle (19-24) rooms."""
import math

from library_recipe import v
from room_kit import Room
from fable_props import (mushroom, lantern_post, fireflies, cup, rose, column, cottage, checker, chandelier, crate, wall_stair, candle)


def _views(r):
    r.view('entrance', (0, 1.65, -6.3), (0, 1.9, 2.5), 74)
    r.view('overview', (6.2, r.height-2.6, -6.3), (-.5, .6, 1.0), 82)


def _sphere(radius, steps=8):
    return [(radius*math.sin(math.pi*i/steps), radius-radius*math.cos(math.pi*i/steps)) for i in range(steps+1)]


def _giant(key, title, role, seed, pits=(), depth=4.0, probes=None):
    r = Room(key, title, role, seed, 'Moss', 'Hedge', 'Limestone', 10.0, 'Night')
    r.floor(pits, depth, 'Earth', 'Earth', probes); r.shell(); r.stars(30)
    r.dressing = 'giant'; r.gate = ('OakLight', 'Copper')
    r.lighting('#24343a', '#0b1519', .022); r.clutter(('Leaf', 'Hedge', 'RoseRed', 'Earth', 'IcingPink'))
    r.light('moon', (0, 8.4, 0), 'MoonGlass', 2.6, 17, True)
    return r


def _castle(key, title, role, seed, pits=(), depth=4.0, probes=None, ground='Limestone'):
    r = Room(key, title, role, seed, ground, 'LimestoneDark', 'Marble', 9.0, 'Night')
    r.floor(pits, depth, 'LimestoneDark', 'Stone', probes); r.shell(); r.stars(22)
    r.dressing = 'castle'; r.gate = ('OakDark', 'Iron')
    r.lighting('#2a2e40', '#0f1220', .02); r.clutter(('LimestoneDark', 'Stone', 'BookCream', 'Velvet', 'Limestone'), 30, .06, .24)
    r.light('moon', (0, 7.6, 0), 'MoonGlass', 2.2, 16, True)
    return r


def beanstalk_court():
    r = _giant('BeanstalkCourt', 'Двор бобового стебля', 'Normal', 113, [(-2.5, 2.5, -2.5, 2.5)])
    r.box('bridge', (1.35, -.12, 0), (2.3, .24, 5.2), 'Oak', walk=True)
    for i in range(16): r.box(f'bridge-board-{i}', (1.35, .008, -2.4+i*.32), (2.3, .016, .28), 'OakLight', False)
    r.rail('bridge-rail', .1, -2.5, .1, 2.5); r.rail('pit-rail-w', -2.6, -2.5, -2.6, 2.5)
    for i, z in enumerate((-2.6, 2.6)): r.rail(f'pit-rail-{i}', -2.5, z, .1, z)
    stalk = [(-1.2+.45*math.cos(i*.9), -4+i*.75, .45*math.sin(i*.9)) for i in range(20)]
    r.tube('beanstalk', stalk, .55, 'Leaf')
    r.tube('beanstalk-twin', [(x+.5*math.cos(i*.9+2), y, z+.5*math.sin(i*.9+2)) for i, (x, y, z) in enumerate(stalk)], .22, 'Hedge')
    for i in range(6):
        x, y, z = stalk[4+i*2]; a = i*2.1
        r.disc(f'bean-leaf-{i}', (x+1.3*math.cos(a), y, z+1.3*math.sin(a)), 2.2, 1.3, 'Leaf', (90, math.degrees(a), 0))
        r.tube(f'bean-pod-{i}', [(x-.6*math.cos(a), y-.2, z-.6*math.sin(a)), (x-.9*math.cos(a), y-.9, z-.9*math.sin(a)),
                                 (x-.8*math.cos(a), y-1.5, z-.8*math.sin(a))], .14, 'MossGlow')
    r.light('pit-glow', (-1.2, -1.6, 1.2), 'MossGlow', 2.0, 8)
    approach, top = wall_stair(r, 'vine', -1, -1, 7.2, 'OakDark', rail_gaps=((-.9, .9),))
    r.deck('leaf', -5.4, -2.6, -.9, .9, 2.7, 'Leaf')
    r.tube('leaf-vein', [(-5.4, 2.72, 0), (-2.6, 2.72, 0)], .04, 'Hedge')
    out = r.hide('pod-hollow', 5.4, 5.7, 'south', 'Hedge', 'Leaf')
    lantern_post(r, 'lamp-se', 6.6, -6.6, 3.2, 'WarmGlass', 'Brass', 1.8, 8)
    for name, x, y, z in (('s-in', 0, 0, -4.2), ('n-in', 0, 0, 4.2), ('e-in', 4.6, 0, 0), ('w-in', -4.4, 0, 0), ('bridge-s', 1.35, 0, -3.4),
                          ('bridge-mid', 1.35, 0, 0), ('bridge-n', 1.35, 0, 3.4), ('se', 4.4, 0, -4.2), ('ne', 4.4, 0, 3.4),
                          ('nw', -4.2, 0, 4.2), ('sw', -4.2, 0, -4.4), ('leaf-root', -6.3, 2.7, 0), ('leaf-tip', -3.7, 2.7, 0),
                          ('loft-n', -6.3, 2.7, 6.2)): r.nav(name, x, y, z)
    r.link('south', 's-in', 'bridge-s', 'bridge-mid', 'bridge-n', 'n-in', 'north'); r.link('east', 'e-in'); r.link('west', 'w-in')
    r.link('s-in', 'se', 'e-in', 'ne', 'n-in', 'nw', 'w-in', 'sw', 's-in'); r.link(out, 'ne'); r.link('sw', approach)
    r.link(top, 'leaf-root', 'leaf-tip'); r.link('leaf-root', 'loft-n')
    r.loot('Small', -2.95, 2.7, 0); r.loot('Tiny', -6.7, 2.7, 6.8); r.loot('Medium', 5.9, 0, -5.9); r.loot('Big', 5.8, 0, -2.9)
    r.loot('Tall', -2.9, 0, 6.5); r.loot('Medium', 2.9, 0, 6.5); r.loot('Small', 2.9, 0, -6.6); r.loot('Tiny', -3.0, 0, -6.8)
    r.loot('Medium', -6.4, 0, 4.0)
    _views(r)
    return r.finish()


def giant_tea():
    r = _giant('GiantTea', 'Чаепитие великана', 'Normal', 114)
    legs = [(-2.9, -1.9), (2.9, -1.9), (-2.9, 2.5), (2.9, 2.5)]
    r.deck('table-top', -3.2, 3.2, -2.2, 2.8, 2.7, 'OakLight', legs, 'OakDark')
    r.box('table-cloth', (0, 2.708, .3), (4.6, .016, 3.2), 'ClothCream', False)
    r.stairs('book-stair', -4.1, -4.0, 'north', 1.8, 2.7, 4.4, 'BookWine')
    for i in range(3): r.box(f'book-stack-{i}', (-4.1, .25+i*.5, -2.6+i*.3), (1.9, .5, 1.3), ('BookTeal', 'BookOchre', 'BookInk')[i], False)
    r.deck('landing', -5.0, -3.2, -1.8, .2, 2.7, 'BookCream')
    r.rail('landing-west', -5.0, -1.8, -5.0, .2, 2.7, 'Brass'); r.rail('landing-north', -5.0, .2, -3.2, .2, 2.7, 'Brass')
    cup(r, 'tea-cup', 1.2, .6, .8, 2.7, handle=30)
    r.lathe('sugar-bowl', (-1.9, 2.7, .9), [(.3, 0), (.45, .3), (.35, .5)], 'PorcelainBlue', 14)
    r.tube('spoon', [(-2.9, 2.76, -1.9), (-1.2, 2.78, -1.7), (-.2, 2.9, -1.9)], .06, 'Brass')
    r.lathe('teapot', (-5.2, 0, 5.0), [(.7, 0), (1.1, .5), (1.15, 1.2), (.7, 1.9), (.3, 2.0), (.3, 2.2), (0, 2.3)], 'PorcelainBlue', 22)
    r.tube('teapot-spout', [(-4.3, 1.0, 4.6), (-3.6, 1.5, 4.2), (-3.3, 2.1, 4.1)], .16, 'PorcelainBlue')
    r.blocker('teapot-block', (-5.2, 1.1, 5.0), (2.2, 2.2, 2.2))
    for i, (x, z) in enumerate(((4.6, 4.6), (6.6, 4.6), (4.6, 6.6), (6.6, 6.6))): r.box(f'chair-leg-{i}', (x, 2.1, z), (.6, 4.2, .6), 'OakDark')
    r.box('chair-seat', (5.6, 4.35, 5.6), (2.9, .3, 2.9), 'Oak'); r.box('chair-back', (5.6, 7.0, 6.9), (2.9, 5.0, .3), 'Oak', False)
    out = r.hide('chair-nook', 5.4, -5.6, 'west', 'Oak', 'Cloth')
    r.box('sugar-cube', (2.8, .3, -6.8), (.6, .6, .6), 'Icing')
    r.lamp('table-lamp', -1.0, 5.2, .4, 'WarmGlass', 'Brass', 2.2, 9); r.tube('table-lamp-chain', [(-1.0, 10, .4), (-1.0, 5.5, .4)], .02, 'Brass')
    r.light('under-table', (0, 1.6, .3), 'Candle', 1.2, 6)
    fireflies(r, 'firefly', [(4.2, 2.2, -1.2), (-5.6, 3.4, 1.9), (2.4, 4.6, 4.9)], 2)
    for name, x, y, z in (('centre', 0, 0, .3), ('stair-foot', -4.1, 0, -6.4), ('stair-mid', -4.1, 1.4, -4.0), ('landing', -4.1, 2.7, -1.6),
                          ('table-w', -2.2, 2.7, -.8), ('table-n', -1.6, 2.7, 2.0), ('table-s', 1.0, 2.7, -1.5)): r.nav(name, x, y, z)
    for side in ('north', 'east', 'south', 'west'): r.link(side, 'centre')
    r.link('south', 'stair-foot', 'stair-mid', 'landing', 'table-w', 'table-n'); r.link('table-w', 'table-s'); r.link(out, 'south')
    r.loot('Small', -2.6, 2.7, 2.3); r.loot('Tiny', -.6, 2.7, -1.9); r.loot('Tiny', 2.8, 2.7, -1.7); r.loot('Medium', 5.6, 0, 5.6)
    r.loot('Medium', 5.9, 0, 2.9); r.loot('Tall', 2.9, 0, 6.5); r.loot('Medium', -2.9, 0, 6.5); r.loot('Big', -6.1, 0, -3.0)
    r.loot('Small', 2.8, .6, -6.8)
    _views(r)
    return r.finish()


def porcelain_walk():
    r = _giant('PorcelainWalk', 'Фарфоровая галерея', 'Passage', 115, [(-3.5, 3.5, -3.5, 3.5)])
    r.box('bridge', (0, -.12, 0), (3.2, .24, 7.2), 'Porcelain', walk=True)
    for i in range(9): r.box(f'bridge-inlay-{i}', (0, .008, -3.2+i*.8), (3.0, .016, .12), 'PorcelainBlue', False)
    for i, x in enumerate((-1.7, 1.7)): r.rail(f'bridge-rail-{i}', x, -3.5, x, 3.5, mat='Gold')
    for i, x in enumerate((-3.6, 3.6)): r.rail(f'pit-rail-x{i}', x, -3.5, x, 3.5, mat='Gold')
    for i, (x0, x1, z) in enumerate(((-3.5, -1.7, -3.6), (1.7, 3.5, -3.6), (-3.5, -1.7, 3.6), (1.7, 3.5, 3.6))): r.rail(f'pit-rail-z{i}', x0, z, x1, z, mat='Gold')
    for i in range(5):
        r.lathe(f'plate-{i}', (-2.5, -4+i*.22, -1.2), [(0, 0), (.9-i*.04, 0), (1.3-i*.04, .12), (1.34-i*.04, .16)], 'PorcelainBlue' if i % 2 else 'Porcelain', 22)
    cup(r, 'fallen-cup', 2.4, .4, 1.0, -4.0, solid=False)
    r.lathe('shard', (2.3, -4, -2.4), [(0, 0), (.9, .05), (.7, .5)], 'Porcelain', 5)
    r.light('pit-glow', (0, -1.8, 0), 'MoonGlass', 1.8, 8)
    cup(r, 'walk-cup', 5.7, 5.7, .7, handle=210)
    out = r.hide('cupboard', -5.4, 5.7, 'south', 'Porcelain', 'PorcelainBlue')
    for i, (x, y, z, s) in enumerate(((7.1, 3.2, -4.4, 1.6), (7.1, 4.6, -1.0, 2.2), (7.1, 3.0, 2.4, 1.4), (-7.1, 3.4, -4.6, 1.8), (-7.1, 4.8, -.4, 2.4), (-7.1, 3.0, 2.6, 1.5))):
        r.lathe(f'wall-plate-{i}', (x, y, z), [(0, 0), (s*.34, 0), (s*.5, .06), (s*.52, .1)], 'PorcelainBlue' if i % 2 else 'Porcelain', 20, (0, 0, 90 if x > 0 else -90))
    lantern_post(r, 'lamp-sw', -6.6, -6.6, 3.2, 'WarmGlass', 'Gold'); lantern_post(r, 'lamp-se', 6.6, -3.0, 3.2, 'WarmGlass', 'Gold')
    for name, x, z in (('s-in', 0, -4.8), ('n-in', 0, 4.8), ('bridge-mid', 0, 0), ('sw', -5.0, -4.8), ('w-mid', -5.2, 0),
                       ('se', 5.2, -4.8), ('e-mid', 5.2, 0), ('ne', 5.2, 3.9)): r.nav(name, x, 0, z)
    r.link('south', 's-in', 'bridge-mid', 'n-in', 'north'); r.link('s-in', 'sw', 'w-mid', out, 'n-in'); r.link('s-in', 'se', 'e-mid', 'ne', 'n-in')
    r.loot('Small', 6.5, 0, -6.4); r.loot('Medium', -5.6, 0, -6.4); r.loot('Tall', 6.7, 0, 1.0); r.loot('Medium', -6.6, 0, -.5)
    r.loot('Tiny', 3.0, 0, 6.8); r.loot('Small', -3.0, 0, -6.7); r.loot('Tiny', -6.0, 0, 6.3); r.loot('Medium', 2.9, 0, -6.5)
    _views(r)
    return r.finish()


def rose_maze():
    r = _giant('RoseMaze', 'Розовый лабиринт', 'Normal', 116)
    hedges = (('a', -2.6, -1.6, .7, 6.0), ('b', 2.6, 1.6, .7, 6.0), ('c', -4.75, 3.5, 4.9, .7), ('d', 4.75, -3.5, 4.9, .7))
    for name, x, z, w, d in hedges:
        r.box(f'hedge-{name}', (x, 1.35, z), (w, 2.7, d), 'Hedge')
        r.box(f'hedge-{name}-top', (x, 2.76, z), (w+.16, .14, d+.16), 'Leaf', False)
        n = int(max(w, d)/1.1)
        for i in range(n):
            t = (i+.5)/n-.5
            rose(r, f'rose-{name}-{i}', x+(w-.5)*t*(w > d), z+(d-.5)*t*(d > w), .55, .7, 2.76, 'RoseRed' if i % 2 else 'IcingPink')
    r._add('rose-arch', 'arch', (0, 0, 0), (4.4, 3.7, .3), 'Thorn', collider=False, radius=.12)
    for i in range(7):
        a = math.pi*(i+.5)/7
        rose(r, f'arch-rose-{i}', 2.2*math.cos(a), 0, 0, .55, 1.5+2.2*math.sin(a))
    r.lamp('arch-lamp', 0, 3.0, 0, 'WarmGlass', 'Brass', 2.0, 9)
    out = r.hide('bower', -5.4, 5.6, 'east', 'Hedge', 'Leaf')
    rose(r, 'giant-rose', 6.5, -6.4, 4.2, 3.0); r.blocker('giant-rose-block', (6.5, 2, -6.4), (.5, 4, .5))
    r.light('rose-glow', (5.6, 3.6, -5.6), 'RoseRed', 1.6, 7); r.light('west-glow', (-5.2, 2.2, -2.4), 'MossGlow', 1.4, 7)
    r.light('east-glow', (5.2, 2.2, 2.6), 'MossGlow', 1.4, 7)
    fireflies(r, 'firefly', [(-4.6, 1.8, -5.2), (4.4, 2.4, 5.4), (0, 2.6, -4.4), (-1.2, 2.0, 5.2), (4.9, 1.6, .2)], 3)
    for name, x, z in (('centre', 0, 0), ('centre-n', 0, 2.3), ('centre-s', 0, -2.3), ('west-lane', -4.8, 0), ('west-turn', -4.6, 2.3),
                       ('west-gap', -2.6, 2.3), ('west-s', -4.8, -5.8), ('east-lane', 4.8, 0), ('east-turn', 4.6, -2.3),
                       ('east-gap', 2.6, -2.3), ('east-n', 4.8, 5.8), ('rose-pocket', 4.6, -5.6)): r.nav(name, x, 0, z)
    r.link('north', 'centre-n', 'centre', 'centre-s', 'south')
    r.link('west', 'west-lane', 'west-turn', 'west-gap', 'centre-n'); r.link('west-lane', 'west-s', 'south')
    r.link('east', 'east-lane', 'east-turn', 'east-gap', 'centre-s'); r.link('east-lane', 'east-n', 'north')
    r.link('north', out); r.link('south', 'rose-pocket')
    r.loot('Tall', -6.5, 0, -6.4); r.loot('Medium', 6.4, 0, 6.4); r.loot('Big', 5.4, 0, -5.4); r.loot('Small', -6.6, 0, 2.6)
    r.loot('Tiny', -6.0, 0, 5.6); r.loot('Small', 1.6, 0, 3.9); r.loot('Medium', -1.5, 0, -4.0); r.loot('Tiny', 6.7, 0, -2.7)
    _views(r)
    return r.finish()


def forgotten_toy():
    r = _giant('ForgottenToy', 'Забытая игрушечная', 'DeadEnd', 117)
    r.box('stage', (4.1, .8, 4.7), (6.2, 1.6, 5.0), 'OakLight', walk=True)
    for i in range(6): r.box(f'stage-stripe-{i}', (1.5+i*1.04, .8, 2.19), (.52, 1.6, .02), 'ToyRed' if i % 2 else 'ToyYellow', False)
    r.stairs('block-stair', 4.0, .8, 'north', 2.0, 1.6, 2.8, 'ToyYellow')
    r.rail('stage-rail-a', 1.0, 2.2, 3.0, 2.2, 1.6, 'ToyBlue'); r.rail('stage-rail-b', 5.0, 2.2, 7.2, 2.2, 1.6, 'ToyBlue')
    r.rail('stage-rail-w', 1.0, 2.2, 1.0, 7.2, 1.6, 'ToyBlue')
    cottage(r, 'dollhouse', 4.0, 6.2, 'south', 2.8, 2.0, 2.4, 'IcingPink', 'ToyRed', 'Icing', y=1.6)
    out = r.hide('tent', -5.4, 5.7, 'south', 'ToyRed', 'ToyYellow')
    r.box('horse-body', (-4.6, 1.2, -3.9), (1.7, .6, .55), 'ToyRed', False); r.box('horse-head', (-3.7, 1.75, -3.9), (.5, .8, .4), 'ToyRed', False, (0, 0, -25))
    for i, x in enumerate((-5.2, -4.0)): r.box(f'horse-leg-{i}', (x, .6, -3.9), (.18, 1.0, .4), 'ToyYellow', False)
    for i, z in enumerate((-4.15, -3.65)):
        r.tube(f'horse-rocker-{i}', [(-5.8, .45, z), (-5.2, .12, z), (-4.6, .06, z), (-4.0, .12, z), (-3.4, .45, z)], .07, 'ToyBlue')
    r.blocker('horse-block', (-4.6, .9, -3.9), (2.4, 1.8, .8))
    r.lathe('ball', (2.2, 0, -4.6), _sphere(.6), 'ToyGreen', 16); r.blocker('ball-block', (2.2, .6, -4.6), (1.0, 1.2, 1.0))
    crate(r, 'block-a', -2.8, -6.8, .6, 'ToyRed', 'ToyYellow'); crate(r, 'block-b', 2.8, -6.8, .6, 'ToyBlue', 'ToyYellow')
    r.box('train-engine', (-6.5, .5, -.6), (1.0, 1.0, 1.8), 'ToyGreen'); r.box('train-car', (-6.5, .4, 1.2), (.9, .8, 1.4), 'ToyRed')
    r.lathe('train-funnel', (-6.5, 1.0, -1.1), [(.14, 0), (.14, .4), (.24, .55)], 'ToyYellow', 10)
    for i, z in enumerate((-1.2, -.1, .8, 1.7)): r.lathe(f'train-wheel-{i}', (-5.97, .25, z), [(0, 0), (.25, 0), (.25, .06), (0, .06)], 'ToyYellow', 12, (0, 0, -90))
    for i in range(4):
        r.box(f'soldier-{i}', (6.8, .7, -5.6+i*.9), (.36, 1.4, .36), 'ToyRed' if i % 2 else 'ToyBlue')
        r.box(f'soldier-hat-{i}', (6.8, 1.6, -5.6+i*.9), (.3, .4, .3), 'Iron', False)
    r.lamp('nursery-lamp', -1.2, 4.4, 1.0, 'WarmGlass', 'Brass', 2.2, 10); r.tube('nursery-lamp-chain', [(-1.2, 10, 1.0), (-1.2, 4.7, 1.0)], .02, 'Brass')
    r.light('stage-light', (4.0, 4.2, 4.0), 'Candle', 1.6, 7)
    for name, x, y, z in (('m', 0, 0, -3.6), ('c', -1.2, 0, .6), ('nw', -1.2, 0, 5.4), ('stair-foot', 4.0, 0, -.8), ('stair-mid', 4.0, .85, .8),
                          ('stage-top', 4.0, 1.6, 2.4), ('stage-c', 4.0, 1.6, 4.0), ('stage-w', 2.0, 1.6, 4.2), ('stage-e', 6.2, 1.6, 4.0)):
        r.nav(name, x, y, z)
    r.link('south', 'm', 'c', 'nw'); r.link('c', out); r.link('m', 'stair-foot', 'stair-mid', 'stage-top', 'stage-c', 'stage-w'); r.link('stage-c', 'stage-e')
    r.loot('Small', 6.6, 1.6, 2.9); r.loot('Tiny', 1.6, 1.6, 6.6); r.loot('Medium', 6.4, 1.6, 5.4); r.loot('Big', -5.9, 0, -5.9)
    r.loot('Tall', 5.6, 0, -5.9); r.loot('Medium', 6.3, 0, -1.6); r.loot('Small', -2.8, .6, -6.8); r.loot('Tiny', 2.8, .6, -6.8)
    r.loot('Wide', -1.4, 0, 6.6); r.loot('Small', -6.5, .8, 1.2); r.loot('Tiny', -6.0, 0, 6.3)
    _views(r)
    return r.finish()


def garden_store():
    r = _giant('GardenStore', 'Садовая сокровищница', 'Extraction', 118)
    r.extraction(0, 1.8, 'south', 'Gold')
    for i, (x, z) in enumerate(((-2.3, -.3), (2.3, -.3), (-2.3, 3.9), (2.3, 3.9))): r.box(f'pergola-post-{i}', (x, 1.7, z), (.3, 3.4, .3), 'OakLight')
    for i, x in enumerate((-2.3, 2.3)): r.box(f'pergola-beam-{i}', (x, 3.5, 1.8), (.24, .24, 5.4), 'OakLight', False)
    for i in range(6): r.box(f'pergola-lath-{i}', (0, 3.68, -.6+i*.96), (5.6, .12, .16), 'Oak', False)
    for i in range(5):
        x = -2.2+i*1.1
        r.tube(f'pergola-vine-{i}', [(x, 3.6, -.5+i*.9), (x+.2, 3.0-.2*(i % 2), -.3+i*.9), (x+.1, 2.5-.3*(i % 3), -.4+i*.9)], .04, 'Leaf')
        rose(r, f'pergola-rose-{i}', x+.1, -.4+i*.9, .2, .5, 2.3-.3*(i % 3), 'RoseRed' if i % 2 else 'IcingPink')
    r.table('potting-bench', 6.3, -2.0, 1.4, 3.6, .95, 'OakLight', 'OakDark')
    for i in range(3): r.lathe(f'bench-pot-{i}', (6.5, .95, -3.3+i*.5), [(.1, 0), (.16, .2), (.18, .24)], 'Copper', 8)
    r.lathe('giant-pot', (-5.4, 0, 5.4), [(.8, 0), (1.15, 1.5), (1.3, 1.6), (1.3, 1.8), (1.0, 1.75)], 'Copper', 20)
    r.blocker('giant-pot-block', (-5.4, .9, 5.4), (2.0, 1.8, 2.0)); rose(r, 'pot-rose', -5.4, 5.4, 3.4, 2.4, 1.6)
    r.lathe('watering-can', (5.4, 0, 5.4), [(1.0, 0), (1.0, 1.7), (.8, 1.9), (.5, 1.9)], 'PorcelainBlue', 18)
    r.tube('can-spout', [(4.5, .9, 5.0), (3.6, 1.7, 4.4), (3.3, 2.2, 4.2)], .12, 'PorcelainBlue')
    r.tube('can-handle', [(5.4, 1.9, 4.6), (5.4, 2.6, 5.0), (5.4, 2.7, 5.6), (5.4, 1.9, 6.2)], .08, 'PorcelainBlue')
    r.blocker('can-block', (5.4, .95, 5.4), (2.0, 1.9, 2.0))
    out = r.hide('shed', -5.5, -4.6, 'east', 'OakLight', 'Roof')
    for i, x in enumerate((-1.9, 1.9)): r.lamp(f'pergola-lamp-{i}', x, 2.9, 1.8, 'WarmGlass', 'Brass', 1.6, 7)
    fireflies(r, 'firefly', [(-3.6, 1.6, -2.4), (3.4, 2.2, -4.6), (-4.4, 2.8, 2.2), (4.6, 3.0, 2.0)], 2)
    for name, x, z in (('m', 0, -3.4), ('n', 0, 5.2), ('e', 4.4, -2.0), ('ne', 4.2, 2.0), ('w', -4.4, .6)): r.nav(name, x, 0, z)
    r.link('south', 'm', 'extract', 'n'); r.link('m', out, 'w', 'extract'); r.link('m', 'e', 'ne', 'extract')
    r.loot('Small', 6.2, .95, -2.6); r.loot('Tiny', 6.2, .95, -1.0); r.loot('Medium', 2.9, 0, 6.4); r.loot('Tall', -2.9, 0, 6.5)
    r.loot('Big', 5.6, 0, -5.8); r.loot('Medium', -6.4, 0, 2.6); r.loot('Tiny', -6.1, 0, -4.6)
    _views(r)
    return r.finish()


def thorn_gate():
    r = _castle('ThornGate', 'Терновые ворота', 'Passage', 119, [(-4.5, 4.5, -2.5, 2.5)])
    r.box('bridge', (1.1, -.12, 0), (3.2, .24, 5.2), 'Limestone', walk=True)
    for i, x in enumerate((-.6, 2.8)): r.rail(f'bridge-rail-{i}', x, -2.5, x, 2.5, mat='Iron')
    for i, z in enumerate((-2.6, 2.6)):
        r.rail(f'moat-rail-w{i}', -4.5, z, -.6, z, mat='Iron'); r.rail(f'moat-rail-e{i}', 2.8, z, 4.5, z, mat='Iron')
    for i, x in enumerate((-4.6, 4.6)): r.rail(f'moat-end-{i}', x, -2.5, x, 2.5, mat='Iron')
    for i, x in enumerate((-1.0, 3.2)):
        r.box(f'gate-pillar-{i}', (x, 2.2, 3.4), (.7, 4.4, .7), 'Marble'); r.box(f'gate-cap-{i}', (x, 4.5, 3.4), (.95, .25, .95), 'Limestone', False)
    r.tube('gate-arch', [(-1.0+2.1*(1-math.cos(math.pi*i/12)), 4.5+1.5*math.sin(math.pi*i/12), 3.4) for i in range(13)], .22, 'Marble')
    for i in range(9):
        a = i*.7
        r.tube(f'thorn-{i}', [(-4.2+i*1.05, -4, 1.6*math.sin(a)), (-4.0+i*1.05, -1.6, 1.2*math.cos(a)), (-4.4+i*1.05, .4+.5*(i % 3), 1.9*math.sin(a+1)),
                               (-3.9+i*1.05, 1.2+.4*(i % 2), 2.2*math.sin(a+2))], .09, 'Thorn')
    for i in range(6):
        t = math.pi*(i+.5)/6
        rose(r, f'gate-rose-{i}', 1.1-2.1*math.cos(t), 3.4, .3, .7, 4.45+1.5*math.sin(t))
    r.tube('gate-vine-w', [(-1.0, 0, 3.0), (-1.3, 1.6, 3.2), (-.8, 3.2, 3.0), (-1.2, 4.6, 3.3)], .07, 'Thorn')
    r.tube('gate-vine-e', [(3.2, 0, 3.0), (3.5, 1.4, 3.2), (3.0, 3.0, 3.0), (3.4, 4.6, 3.3)], .07, 'Thorn')
    r.light('moat-glow', (0, -2.0, 0), 'RoseRed', 1.8, 8)
    out = r.hide('gatehouse', -5.4, -5.4, 'east', 'LimestoneDark', 'Roof')
    lantern_post(r, 'lamp-a', -1.1, -3.2, 3.2, 'Candle', 'Iron', 1.8, 8); lantern_post(r, 'lamp-b', 3.3, -3.2, 3.2, 'Candle', 'Iron', 1.8, 8)
    for name, x, z in (('s-in', .6, -4.6), ('bridge-mid', 1.1, 0), ('bridge-n', 1.1, 5.0), ('west-s', -5.6, -3.1), ('west-n', -5.6, 4.4),
                       ('east-s', 5.8, -3.6), ('east-n', 5.8, 4.6)): r.nav(name, x, 0, z)
    r.link('south', 's-in', 'bridge-mid', 'bridge-n', 'north'); r.link('s-in', out)
    r.link('s-in', 'west-s', 'west-n', 'bridge-n'); r.link('s-in', 'east-s', 'east-n', 'bridge-n')
    r.loot('Medium', -6.3, 0, 6.3); r.loot('Small', 6.5, 0, 6.5); r.loot('Tall', 6.7, 0, 0); r.loot('Medium', -6.7, 0, .4)
    r.loot('Big', 5.8, 0, -5.9); r.loot('Small', 3.0, 0, -6.7); r.loot('Tiny', -6.0, 0, -5.4); r.loot('Tiny', -2.8, 0, 6.8)
    r.loot('Small', 2.9, 0, 6.6)
    _views(r)
    return r.finish()


def mirror_court():
    r = _castle('MirrorCourt', 'Зеркальный двор', 'Normal', 120, ground='Marble')
    approach, top = wall_stair(r, 'gallery', 1, 1, -7.2, 'Limestone', 'Iron', rail_gaps=((-7.2, -5.4),))
    r.deck('gallery-arm', 2.5, 5.4, -7.2, -5.4, 2.7, 'Limestone')
    r.rail('gallery-arm-rail', 2.5, -5.4, 5.4, -5.4, 2.7, 'Iron'); r.rail('gallery-arm-end', 2.5, -7.2, 2.5, -5.4, 2.7, 'Iron')
    for i, (x, z) in enumerate(((-2.6, -2.5), (2.8, .5))):
        r.box(f'mirror-{i}', (x, 1.6, z), (.3, 3.2, 3.0), 'Mirror')
        for j, (dy, dz, sy, sz) in enumerate(((1.65, 0, .14, 3.2), (-1.55, 0, .14, 3.2), (0, 1.55, 3.3, .14), (0, -1.55, 3.3, .14))):
            r.box(f'mirror-{i}-frame-{j}', (x, 1.6+dy, z+dz), (.4, sy, sz), 'Gold', False)
    for i, z in enumerate((-4.6, 4.6)):
        r.box(f'wall-mirror-{i}', (-7.17, 2.3, z), (.05, 2.6, 1.6), 'Mirror', False)
        r.box(f'wall-mirror-frame-{i}', (-7.19, 2.3, z), (.04, 2.9, 1.9), 'Gold', False)
    r.box('plinth', (-6.6, .45, 2.9), (.8, .9, .8), 'MarbleDark')
    r.box('statue-plinth', (-5.9, .5, -5.9), (1.2, 1.0, 1.2), 'MarbleDark')
    r.lathe('statue', (-5.9, 1.0, -5.9), [(.42, 0), (.26, .9), (.18, 1.2), (.24, 1.45), (.12, 1.62), (.17, 1.85), (0, 2.0)], 'Marble', 12)
    checker(r, 'tiles', -4.8, 4.8, -4.8, 4.8, 6, 'MarbleDark', 'Marble')
    out = r.hide('mirror-niche', -5.4, 5.7, 'south', 'Marble', 'Velvet')
    chandelier(r, 'chandelier', 0, 5.4, 0, 1.5, 6, 9.0)
    r.lamp('gallery-lamp', 6.3, 5.0, -3.0, 'Candle', 'Gold', 1.5, 7); r.light('niche-glow', (-3.4, 2.4, 4.0), 'MoonGlass', 1.2, 6)
    for name, x, y, z in (('centre', 0, 0, 0), ('ne', 4.4, 0, 3.6), ('se', 4.4, 0, -3.4), ('sw', -4.4, 0, -3.6), ('nw', -4.6, 0, 2.4),
                          ('g-east', 6.3, 2.7, -3.0), ('g-corner', 6.3, 2.7, -6.3), ('g-south', 3.6, 2.7, -6.3)): r.nav(name, x, y, z)
    r.link('north', 'centre', 'south'); r.link('east', 'ne', 'north'); r.link('east', 'se', 'south')
    r.link('west', 'sw', 'south'); r.link('west', 'nw', 'north'); r.link(out, 'nw'); r.link(approach, 'ne'); r.link(approach, 'north')
    r.link(top, 'g-east', 'g-corner', 'g-south')
    r.loot('Small', 3.0, 2.7, -6.8); r.loot('Tiny', 6.8, 2.7, -.5); r.loot('Tall', -4.2, 0, -6.5); r.loot('Big', 6.0, 0, -4.4)
    r.loot('Medium', -6.3, 0, -2.9); r.loot('Medium', -2.4, 0, -6.5); r.loot('Small', 2.9, 0, 6.6); r.loot('Tiny', -6.6, .9, 2.9)
    _views(r)
    return r.finish()


def silent_ballroom():
    r = _castle('SilentBallroom', 'Безмолвный бальный зал', 'Normal', 121, ground='Parquet')
    west_approach, west_top = wall_stair(r, 'west-gallery', -1, -1, 7.2, 'Parquet', 'Gold')
    east_approach, east_top = wall_stair(r, 'east-gallery', 1, 1, -7.2, 'Parquet', 'Gold')
    # Refuge under the upper half of the west stair.
    r.box('nook-back', (-6.3, 1.2, -2.12), (1.8, 2.4, .12), 'Velvet'); r.box('nook-front', (-6.3, .6, -3.95), (1.8, 1.2, .12), 'Velvet')
    r.anchors.append(dict(id='stair-nook', kind='hide', position=v(-6.3, 1, -3.05), rotation=v(0, 270, 0)))
    checker(r, 'parquet', -5.2, 5.2, -5.2, 5.2, 6, 'ParquetDark', 'Parquet')
    gown = [(.55, 0), (.3, .9), (.18, 1.2), (.23, 1.45), (.12, 1.6), (.17, 1.82), (0, 1.96)]
    for i, (x, z) in enumerate(((-2.2, 1.8), (2.2, -1.8), (-1.8, -2.8), (1.8, 2.8))):
        r.lathe(f'dancer-{i}', (x, 0, z), gown, 'Marble' if i % 2 else 'IcingPink', 12); r.blocker(f'dancer-{i}-block', (x, 1.0, z), (.7, 2.0, .7))
        r.lathe(f'partner-{i}', (x+.5, 0, z+.3), [(.3, 0), (.22, 1.0), (.26, 1.5), (.13, 1.66), (.17, 1.9), (0, 2.04)], 'MarbleDark', 10)
        r.blocker(f'partner-{i}-block', (x+.5, 1.0, z+.3), (.5, 2.0, .5))
    chandelier(r, 'chandelier', 0, 5.6, 0, 1.9, 8, 9.0, intensity=2.8, reach=13)
    for i, z in enumerate((-4.2, 4.2)):
        r.ring(f'side-chandelier-{i}', (0, 6.2, z), 1.0, .05, 'Gold'); r.tube(f'side-chain-{i}', [(0, 9, z), (0, 6.2, z)], .025, 'Gold')
        r.light(f'side-light-{i}', (0, 5.9, z), 'Candle', 1.4, 8)
    for i, (x, z) in enumerate(((-7.1, 4.6), (7.1, -4.6))):
        r.box(f'drape-{i}', (x, 5.2, z), (.12, 4.4, 2.2), 'Velvet', False)
    for name, x, y, z in (('centre', 0, 0, 0), ('nook-out', -4.4, 0, -3.05), ('east-floor', 4.4, 0, 3.05),
                          ('west-walk', -6.3, 2.7, 6.3), ('east-walk', 6.3, 2.7, -6.3)): r.nav(name, x, y, z)
    for side in ('north', 'east', 'south', 'west'): r.link(side, 'centre')
    r.link('centre', 'nook-out', west_approach, 'south'); r.link('east', 'east-floor', east_approach, 'north')
    r.link(west_top, 'west-walk'); r.link(east_top, 'east-walk')
    r.loot('Small', -6.6, 2.7, 6.8); r.loot('Tiny', -6.9, 2.7, 3.0); r.loot('Small', 6.6, 2.7, -6.8); r.loot('Tiny', 6.9, 2.7, -3.0)
    r.loot('Big', -6.2, 0, 4.6); r.loot('Medium', 6.3, 0, -4.6); r.loot('Tall', -2.9, 0, 6.5); r.loot('Medium', 2.9, 0, -6.5)
    _views(r)
    return r.finish()


def sleeping_observatory():
    r = _castle('SleepingObservatory', 'Спящая обсерватория', 'Normal', 122, [(1.5, 4.5, -2.5, 1.5)])
    approach, top = wall_stair(r, 'loft', -1, -1, 7.2, 'Oak', 'Brass', rail_gaps=((5.4, 7.2),))
    r.deck('loft-arm', -5.4, -2.5, 5.4, 7.2, 2.7, 'Oak')
    r.rail('loft-arm-rail', -5.4, 5.4, -2.5, 5.4, 2.7, 'Brass'); r.rail('loft-arm-end', -2.5, 5.4, -2.5, 7.2, 2.7, 'Brass')
    for i, x in enumerate((1.4, 4.6)): r.rail(f'well-rail-x{i}', x, -2.5, x, 1.5, mat='Brass')
    for i, z in enumerate((-2.6, 1.6)): r.rail(f'well-rail-z{i}', 1.5, z, 4.5, z, mat='Brass')
    # Orrery asleep in the star well.
    r.tube('orrery-axis', [(3.0, -4, -.5), (3.0, -.6, -.5)], .06, 'Brass'); r.lathe('orrery-sun', (3.0, -1.6, -.5), _sphere(.45), 'Candle', 14)
    for i, (rad, tilt) in enumerate(((1.0, 0), (1.35, 1))):
        r.ring(f'orrery-ring-{i}', (3.0, -1.2-.5*i, -.5), rad, .035, 'Brass', .8 if tilt else 1.0)
        a = 1.3+i*2.2
        r.lathe(f'orrery-planet-{i}', (3.0+rad*math.cos(a), -1.36-.5*i, -.5+rad*(.8 if tilt else 1.0)*math.sin(a)), _sphere(.16), 'MoonGlass', 10)
    r.light('well-glow', (3.0, -1.2, -.5), 'Candle', 2.0, 8)
    r.box('telescope-base', (-1.6, .6, 2.6), (1.4, 1.2, 1.4), 'MarbleDark')
    r.tube('telescope', [(-1.9, 1.5, 3.0), (.5, 5.2, .6)], .42, 'Brass'); r.ring('telescope-lens', (.56, 5.3, .54), .46, .06, 'Gold')
    r.tube('telescope-finder', [(-1.2, 2.9, 2.6), (-.3, 4.3, 1.7)], .1, 'Gold')
    r.disc('oculus', (1.0, 8.98, 0), 5.0, 5.0, 'SkyView'); r.ring('oculus-rim', (1.0, 8.95, 0), 2.5, .14, 'Brass')
    for i, rad in enumerate((1.3, 2.2)): r.ring(f'star-chart-{i}', (-.4, .02, -.4), rad, .025, 'Brass', segments=40)
    out = r.hide('star-closet', 5.4, 5.7, 'south', 'LimestoneDark', 'Velvet')
    r.shelf('chart-shelf', -4.0, 0, 6.84, 'north', 2.6, 2.3, 'OakDark')
    r.lamp('loft-lamp', -6.3, 5.0, 3.0, 'Candle', 'Brass', 1.5, 7); r.lamp('floor-lamp', -3.4, 2.4, -3.4, 'Candle', 'Brass', 1.4, 7)
    for name, x, y, z in (('centre', 0, 0, -.4), ('n-in', .4, 0, 4.8), ('e-in', 5.9, 0, 0), ('se', 5.9, 0, -4.4),
                          ('loft-n', -6.3, 2.7, 6.3), ('loft-arm', -3.6, 2.7, 6.3)): r.nav(name, x, y, z)
    r.link('north', 'n-in', 'centre', 'south'); r.link('west', 'centre'); r.link('east', 'e-in', 'se', 'south')
    r.link('e-in', out, 'n-in'); r.link('south', approach, 'centre'); r.link(top, 'loft-n', 'loft-arm')
    r.loot('Small', -2.9, 2.7, 6.8); r.loot('Tiny', -6.9, 2.7, 2.6); r.loot('Medium', 6.4, 0, -6.3); r.loot('Tall', 2.9, 0, 6.5)
    r.loot('Big', -6.2, 0, 4.4); r.loot('Medium', 2.9, 0, -3.6); r.loot('Small', -6.6, 0, -2.6); r.loot('Tiny', 6.8, 0, 2.6)
    _views(r)
    return r.finish()


def story_archive():
    r = _castle('StoryArchive', 'Архив недописанных сказок', 'DeadEnd', 123, ground='Parquet')
    for i, z in enumerate((-4.0, -1.6)): r.shelf(f'row-w-{i}', -3.2, 0, z, 'west')
    for i, z in enumerate((.6, 3.0, 5.4)): r.shelf(f'row-e-{i}', 2.6, 0, z, 'east')
    for i, x in enumerate((-2.6, -.2)): r.shelf(f'wall-n-{i}', x, 0, 6.84, 'north')
    for i, z in enumerate((-1.0, 1.4, 3.8)): r.shelf(f'wall-e-{i}', 6.84, 0, z, 'east')
    for i, z in enumerate((-.6, 1.8)): r.shelf(f'wall-w-{i}', -6.84, 0, z, 'west')
    r.table('desk', 4.6, -4.8, 3.0, 1.4, .9, 'OakLight', 'OakDark')
    for i in range(5): r.box(f'manuscript-{i}', (3.6+i*.25, .91+i*.012, -4.9+.1*(i % 2)), (.5, .012, .7), 'BookCream', False, (0, i*13-20, 0))
    r.tube('quill', [(5.6, .9, -4.6), (5.75, 1.3, -4.5)], .015, 'Icing'); candle(r, 'desk-candle', 5.9, .9, -5.2, .28)
    r.light('desk-light', (5.6, 1.6, -4.9), 'Candle', 1.8, 7)
    r.box('rose-plinth', (-1.2, .5, 2.2), (.9, 1.0, .9), 'MarbleDark'); rose(r, 'last-rose', -1.2, 2.2, .9, 1.0, 1.0)
    r.lathe('rose-dome', (-1.2, 1.0, 2.2), [(.42, 0), (.42, .9), (.3, 1.25), (0, 1.4)], 'MoonGlass', 16); r.light('rose-light', (-1.2, 2.0, 2.2), 'RoseRed', 1.6, 6)
    for i in range(12):
        a = i*2.4
        r.box(f'page-{i}', (3.6*math.sin(a), 3.2+1.6*math.sin(i*1.3)**2, 3.0*math.cos(a*1.7)-.6), (.3, .01, .42), 'BookCream', False, (i*23 % 60-30, i*47, i*31 % 50-25))
    r.tube('ladder-a', [(1.9, 0, 5.6), (2.2, 2.65, 5.6)], .035, 'Oak'); r.tube('ladder-b', [(1.9, 0, 4.9), (2.2, 2.65, 4.9)], .035, 'Oak')
    for i in range(6): r.tube(f'ladder-rung-{i}', [(1.93+i*.045, .3+i*.4, 5.6), (1.93+i*.045, .3+i*.4, 4.9)], .025, 'Oak')
    out = r.hide('reading-nook', -5.4, 5.7, 'south', 'OakDark', 'Velvet')
    chandelier(r, 'chandelier', 0, 5.6, -1.6, 1.3, 6, 9.0, intensity=2.0, reach=11)
    for name, x, z in (('m', 0, -5.2), ('c', -.3, -.6), ('cn', 1.0, 3.6), ('n-in', -1.6, 4.6), ('w-s', -5.4, -6.1), ('w-n', -5.4, .6),
                       ('d', 2.0, -3.0), ('e-s', 4.2, -1.4), ('e-n', 4.7, 4.6)): r.nav(name, x, 0, z)
    r.link('south', 'm', 'c', 'cn', 'n-in'); r.link('m', 'w-s', 'w-n', out, 'n-in'); r.link('m', 'd', 'e-s', 'e-n')
    r.loot('Tiny', 4.2, .9, -4.6); r.loot('Small', 5.2, .9, -4.6); r.loot('Medium', 4.7, 0, 6.3); r.loot('Tall', -6.5, 0, -3.6)
    r.loot('Medium', 5.9, 0, -2.9); r.loot('Small', -6.6, 0, 3.6); r.loot('Tiny', -6.0, 0, 6.3); r.loot('Small', 1.9, 0, -6.7)
    r.loot('Tiny', 6.8, 0, -6.7); r.loot('Small', -2.2, 0, -6.7); r.loot('Medium', .9, 0, 5.9)
    _views(r)
    return r.finish()


def royal_treasury():
    r = _castle('RoyalTreasury', 'Королевская сокровищница', 'Extraction', 124, ground='Marble')
    r.extraction(0, 1.6, 'south', 'Gold')
    out_w = r.hide('alcove-w', -5.6, 1.0, 'east', 'Marble', 'Velvet'); out_e = r.hide('alcove-e', 5.6, 1.0, 'west', 'Marble', 'Velvet')
    for i, (x, z) in enumerate(((-3.2, -4.4), (3.2, -4.4), (-3.2, 4.8), (3.2, 4.8))): column(r, f'column-{i}', x, z, 7.0, .4, 'Marble', 'Gold')
    r.box('chest', (0, .5, 6.4), (2.0, 1.0, 1.2), 'OakDark')
    for i, x in enumerate((-.7, 0, .7)): r.box(f'chest-band-{i}', (x, .5, 6.4), (.12, 1.04, 1.24), 'Gold', False)
    r.lathe('chest-lid', (0, 1.0, 6.4), [(0, 0), (.62, 0), (.62, .02)], 'Gold', 4, (0, 45, 0))
    for i, x in enumerate((-2.6, 2.6)): r.box(f'arch-pillar-{i}', (x, 2.5, 5.7), (.6, 5.0, .6), 'Marble')
    r.tube('royal-arch', [(2.6*math.cos(math.pi*i/14), 5.0+1.6*math.sin(math.pi*i/14), 5.7) for i in range(15)], .26, 'Gold')
    r.disc('crest', (0, 5.6, 7.12), 1.8, 1.8, 'Gold', (0, 0, 0))
    for i, (x, z) in enumerate(((-6.5, 6.5), (6.5, 6.5), (-6.6, -6.6), (6.6, -6.6))):
        r.lathe(f'coins-{i}', (x, 0, z), [(.75, 0), (.5, .35), (.2, .6), (0, .66)], 'Gold', 14); r.blocker(f'coins-{i}-block', (x, .3, z), (1.0, .6, 1.0))
    checker(r, 'tiles', -4.8, 4.8, -6.0, 4.0, 5, 'MarbleDark', 'Marble')
    r.box('carpet', (0, .012, -3.4), (2.6, .012, 6.0), 'Velvet', False)
    chandelier(r, 'chandelier', 0, 5.8, 1.6, 1.7, 8, 9.0, intensity=2.6, reach=13)
    r.light('gold-glow', (0, 2.2, 5.2), 'Candle', 1.6, 7)
    for name, x, z in (('m', 0, -3.6), ('sw', -4.4, -2.6), ('se', 4.4, -2.6), ('n', 0, 4.4)): r.nav(name, x, 0, z)
    r.link('south', 'm', 'extract', 'n'); r.link('m', 'sw', out_w, 'extract'); r.link('m', 'se', out_e, 'extract')
    r.loot('Small', 0, 1.0, 6.4); r.loot('Tall', -5.6, 0, 5.0); r.loot('Big', 5.4, 0, 4.8); r.loot('Medium', -5.6, 0, -5.4)
    r.loot('Medium', 5.6, 0, -5.4); r.loot('Tiny', -6.2, 0, 1.0); r.loot('Tiny', 6.2, 0, 1.0)
    _views(r)
    return r.finish()


ROOMS = [beanstalk_court, giant_tea, porcelain_walk, rose_maze, forgotten_toy, garden_store,
         thorn_gate, mirror_court, silent_ballroom, sleeping_observatory, story_archive, royal_treasury]
