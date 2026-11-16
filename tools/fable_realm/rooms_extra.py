"""Extra start rooms (25, 26) and extraction rooms (27-29): vanilla levels pick from several of each."""
import math

from library_recipe import v

from rooms_forest_village import _forest, _village, _views
from rooms_giant_castle import _giant, _castle, _views as _views_south
from fable_props import mushroom, tree, lantern_post, fireflies, cup, column, candy_cane, cottage, chandelier, crate

# (id, key, title, biome, role, hero model)
CATALOG = [
    ('25', 'CoachYard', 'Каретный двор', 'Village', 'Start', 'Cottage'),
    ('26', 'DrawbridgeYard', 'Двор у подъёмного моста', 'Castle', 'Start', 'Tower'),
    ('27', 'FairyRing', 'Ведьмин круг', 'Forest', 'Extraction', 'Mushroom'),
    ('28', 'TeaTray', 'Чайный поднос', 'Giant', 'Extraction', 'GiantCup'),
    ('29', 'CrownChapel', 'Коронная капелла', 'Castle', 'Extraction', 'Arch'),
    ('30', 'BeanstalkGrove', 'Роща великаньего боба', 'Giant', 'DeadEnd', 'Tree'),
]


def coach_yard():
    r = _village('CoachYard', 'Каретный двор', 'Start', 125)
    r.truck(0, -4.2, 'north'); r.extraction(-4.9, 2.6, 'east', 'Gold')
    cottage(r, 'coach-house', 5.7, 5.7, 'south', roof='Candy')
    out = r.hide('porch', 5.4, 1.4, 'west', 'Biscuit', 'Roof')
    for i, s in enumerate((-1, 1)): candy_cane(r, f'gate-cane-{i}', s*2.7, 6.8, 3.4, -s)
    # A sugar coach left beside the bay.
    r.box('coach-body', (-5.6, 1.2, -4.6), (1.8, 1.4, 2.6), 'IcingPink'); r.box('coach-roof', (-5.6, 2.0, -4.6), (2.0, .2, 2.8), 'Candy', False)
    for i, (x, z) in enumerate(((-4.6, -5.6), (-4.6, -3.6), (-6.6, -5.6), (-6.6, -3.6))):
        r.lathe(f'coach-wheel-{i}', (x, .5, z), [(0, 0), (.5, 0), (.5, .1), (0, .1)], 'Chocolate', 14, (0, 0, 90))
    r.blocker('coach-block', (-5.6, .3, -4.6), (2.2, .6, 2.8))
    crate(r, 'luggage-a', 3.6, -2.6, .8, 'Marzipan', 'Chocolate'); crate(r, 'luggage-b', 6.3, -5.9, 1.0, 'OakLight', 'OakDark', yaw=20)
    lantern_post(r, 'lamp-w', -2.8, 5.0, 3.0, 'WarmGlass', 'Copper'); lantern_post(r, 'lamp-e', 2.8, 3.2, 3.0, 'WarmGlass', 'Copper')
    r.light('extraction-glow', (-4.9, 2.4, 2.6), 'Mint', 1.6, 6)
    r.nav('mid', 0, 0, .8)
    r.link('truck-point', 'mid', 'north'); r.link('mid', 'extract'); r.link('mid', out)
    r.loot('Tiny', 3.6, .8, -2.6); r.loot('Small', 6.5, 0, -2.8); r.loot('Medium', -6.3, 0, 5.9); r.loot('Small', -6.5, 0, -1.6)
    _views(r, True)
    return r.finish()


def drawbridge_yard():
    r = _castle('DrawbridgeYard', 'Двор у подъёмного моста', 'Start', 126)
    r.truck(0, -4.2, 'north'); r.extraction(4.9, 2.6, 'west', 'Gold')
    for i, s in enumerate((-1, 1)):
        r.lathe(f'gate-tower-{i}', (s*3.7, 0, 6.2), [(1.0, 0), (.9, .4), (.85, 5.2), (1.05, 5.4), (1.05, 5.9), (0, 5.9)], 'Limestone', 16)
        r.lathe(f'gate-tower-roof-{i}', (s*3.7, 5.9, 6.2), [(1.2, 0), (0, 2.0)], 'Roof', 16)
        r.blocker(f'gate-tower-block-{i}', (s*3.7, 3, 6.2), (1.8, 6, 1.8))
        r.tube(f'bridge-chain-{i}', [(s*1.9, 3.6, 7.0), (s*2.6, 4.6, 6.4), (s*3.0, 5.2, 6.2)], .04, 'Iron')
        r.box(f'banner-{i}', (s*3.7, 3.4, 5.16), (.7, 1.8, .04), 'Velvet', False)
    out = r.hide('guard-niche', -5.4, 1.6, 'east', 'LimestoneDark', 'Roof')
    for i, (x, z) in enumerate(((5.9, -5.6), (-5.9, -5.6))): column(r, f'yard-column-{i}', x, z, 5.0, .35, 'Marble', 'Gold')
    crate(r, 'supply-a', 3.6, -2.6, .8, 'OakLight', 'Iron'); crate(r, 'supply-b', -3.8, 4.6, .9, 'OakLight', 'Iron', yaw=15)
    lantern_post(r, 'lamp-w', -2.8, 3.2, 3.2, 'Candle', 'Iron', 1.8, 8); lantern_post(r, 'lamp-e', 2.8, 5.0, 3.2, 'Candle', 'Iron', 1.8, 8)
    r.light('extraction-glow', (4.9, 2.4, 2.6), 'MoonGlass', 1.6, 6)
    r.nav('mid', 0, 0, .8)
    r.link('truck-point', 'mid', 'north'); r.link('mid', 'extract'); r.link('mid', out)
    r.loot('Tiny', 3.6, .8, -2.6); r.loot('Small', 6.5, 0, -2.6); r.loot('Medium', -6.3, 0, 5.4); r.loot('Small', -6.5, 0, -2.4)
    _views(r, True)
    return r.finish()


def fairy_ring():
    r = _forest('FairyRing', 'Ведьмин круг', 'Extraction', 127)
    r.extraction(0, 1.6, 'south', 'CapGlow')
    for i in range(12):
        if i % 3 == 0: continue  # gaps where the three approaches cross the ring
        a = i*math.tau/12+math.pi/12
        mushroom(r, f'ring-{i}', 3.3*math.cos(a), 1.6+3.3*math.sin(a), .5+.2*(i % 2), .34, 'CapGlow' if i % 2 else 'CapRose', solid=False)
    r.ring('ring-glow', (0, .03, 1.6), 3.3, .03, 'CapGlow', segments=40); r.light('ring-light', (0, 2.2, 1.6), 'CapGlow', 2.0, 8)
    out = r.hide('root-hollow', -5.4, -5.2, 'east', 'Bark', 'Moss')
    tree(r, 'tree-ne', 5.9, 5.9, lean=(-.4, -.3)); tree(r, 'tree-nw', -5.9, 6.0, radius=.38)
    mushroom(r, 'elder', 5.6, -5.4, 2.6, 1.3, light=True)
    fireflies(r, 'firefly', [(2.6*math.cos(i*1.3), 1.2+.8*math.sin(i)**2, 1.6+2.6*math.sin(i*1.3)) for i in range(10)], 4)
    for name, x, z in (('m', 0, -3.6), ('w', -4.6, 1.2), ('e', 4.6, 1.2), ('n', 0, 5.4)): r.nav(name, x, 0, z)
    r.link('south', 'm', 'extract', 'n'); r.link('m', out, 'w', 'extract'); r.link('m', 'e', 'extract')
    r.loot('Small', 2.9, 0, -6.6); r.loot('Medium', -2.9, 0, 6.4); r.loot('Tall', 3.0, 0, 6.5); r.loot('Tiny', -6.0, 0, -5.2)
    r.loot('Medium', 6.3, 0, -2.6); r.loot('Small', -6.5, 0, 3.4)
    _views_south(r)
    return r.finish()


def tea_tray():
    r = _giant('TeaTray', 'Чайный поднос', 'Extraction', 128)
    r.extraction(0, 1.8, 'south', 'Gold')
    r.lathe('tray', (0, 0, 1.8), [(0, .015), (2.9, .015), (3.1, .06), (3.15, .1)], 'Brass', 32)
    for i, s in enumerate((-1, 1)): r.tube(f'tray-handle-{i}', [(s*3.1, .1, 1.2), (s*3.5, .22, 1.5), (s*3.5, .22, 2.1), (s*3.1, .1, 2.4)], .06, 'Gold')
    cup(r, 'tray-cup', 5.5, 5.5, .8, handle=220)
    r.lathe('sugar-bowl', (-5.4, 0, 5.4), [(.7, 0), (1.1, .6), (1.0, 1.3), (.6, 1.4)], 'PorcelainBlue', 20); r.blocker('sugar-bowl-block', (-5.4, .7, 5.4), (2.0, 1.4, 2.0))
    for i, (x, z) in enumerate(((-4.6, 4.2), (-5.9, 3.9), (-4.3, 5.9))): r.box(f'sugar-lump-{i}', (x, 1.6+.02*i, z), (.4, .4, .4), 'Icing', False, (0, i*25, 0))
    r.tube('teaspoon', [(4.2, .08, -5.9), (5.6, .1, -5.2), (6.6, .2, -4.9)], .08, 'Brass')
    out = r.hide('napkin-fold', -5.5, -4.6, 'east', 'ClothCream', 'Cloth')
    for i, x in enumerate((-2.2, 2.2)): lantern_post(r, f'pad-lamp-{i}', x, 5.0, 3.2, 'WarmGlass', 'Gold', 1.7, 8)
    fireflies(r, 'firefly', [(-3.6, 1.6, -2.4), (3.4, 2.2, -3.6), (4.4, 2.8, 1.2)], 2)
    for name, x, z in (('m', 0, -3.4), ('n', 0, 5.6), ('e', 4.6, -1.6), ('w', -4.6, .8)): r.nav(name, x, 0, z)
    r.link('south', 'm', 'extract', 'n'); r.link('m', out, 'w', 'extract'); r.link('m', 'e', 'extract')
    r.loot('Small', 2.9, 0, -6.6); r.loot('Medium', 2.9, 0, 6.4); r.loot('Tall', -2.9, 0, 6.5); r.loot('Big', 5.8, 0, -2.9)
    r.loot('Medium', -6.4, 0, 2.2); r.loot('Tiny', -6.1, 0, -4.6)
    _views_south(r)
    return r.finish()


def crown_chapel():
    r = _castle('CrownChapel', 'Коронная капелла', 'Extraction', 129, ground='Marble')
    r.extraction(0, 2.0, 'south', 'Gold')
    r.disc('rose-window', (0, 5.6, 7.12), 3.4, 3.4, 'Vista', (0, 0, 0))
    for i in range(8):
        a = i*math.tau/8
        r.tube(f'window-ray-{i}', [(0, 5.6, 7.05), (1.7*math.cos(a), 5.6+1.7*math.sin(a), 7.05)], .04, 'Gold')
    r.tube('window-rim', [(1.7*math.cos(i*math.tau/24), 5.6+1.7*math.sin(i*math.tau/24), 7.05) for i in range(25)], .08, 'Gold')
    r.light('window-light', (0, 5.2, 5.6), 'MoonGlass', 2.4, 11)
    for i, z in enumerate((-4.8, -3.2, -1.6)):
        for j, s in enumerate((-1, 1)):
            r.box(f'pew-{i}-{j}', (s*4.9, .45, z), (2.6, .9, .5), 'OakDark'); r.box(f'pew-back-{i}-{j}', (s*4.9, 1.05, z-.2), (2.6, .5, .1), 'OakDark', False)
    r.box('altar', (0, .55, 6.2), (2.4, 1.1, 1.0), 'MarbleDark'); r.box('altar-cloth', (0, 1.11, 6.2), (1.2, .02, 1.04), 'Velvet', False)
    r.lathe('crown', (0, 1.12, 6.2), [(.22, 0), (.24, .12), (.3, .24)], 'Gold', 10)
    for i, s in enumerate((-1, 1)): column(r, f'chapel-column-{i}', s*3.4, 4.6, 7.0, .36, 'Marble', 'Gold')
    out = r.hide('confessional', 5.5, 5.6, 'south', 'OakDark', 'Velvet')
    chandelier(r, 'chandelier', 0, 5.6, -1.6, 1.5, 6, 9.0, intensity=2.2, reach=12)
    for name, x, z in (('m', 0, -3.6), ('n', 0, 4.8), ('ne', 4.4, 2.8)): r.nav(name, x, 0, z)
    r.link('south', 'm', 'extract', 'n'); r.link('extract', 'ne', out)
    r.loot('Small', 0, 1.1, 5.9); r.loot('Tall', -5.8, 0, 5.6); r.loot('Medium', -6.3, 0, 1.6); r.loot('Medium', 6.3, 0, .6)
    r.loot('Small', 2.9, 0, -6.6); r.loot('Tiny', 6.1, 0, 6.2); r.loot('Small', -2.9, 0, -6.6)
    _views_south(r)
    return r.finish()


# The five bean colours of the grove puzzle, in socket order around the dais (starting north, going west).
BEAN_COLOURS = (('red', 'BeanRed'), ('yellow', 'BeanYellow'), ('blue', 'BeanBlue'), ('green', 'BeanGreen'), ('violet', 'BeanViolet'))


def beanstalk_grove():
    """Special puzzle room, one per level. Five coloured beans found elsewhere go on the five matching plates;
    then the giant bean comes down onto the dais and leaves the Golden Egg.
    Markers for the game side (kind 'surface'): bean-socket-<colour>, giant-bean-landing, golden-egg-spawn.
    Moving piece: every element whose id starts with 'giant-bean-' hangs 5.2 m above its landing spot."""
    r = _giant('BeanstalkGrove', 'Роща великаньего боба', 'DeadEnd', 130)
    cx, cz = 0.0, .6
    # Dais the bean lands on.
    r.lathe('dais', (cx, 0, cz), [(0, .03), (1.75, .03), (1.9, 0)], 'Limestone', 28)
    r.ring('dais-rim', (cx, .035, cz), 1.75, .05, 'Gold', segments=40); r.ring('dais-glow', (cx, .04, cz), 1.2, .03, 'GiantBean', segments=32)
    # The giant bean, hanging by three tendrils from the stalk that fills the north-east corner.
    r.lathe('giant-bean-body', (cx, 5.2, cz), [(0, 0), (.75, .25), (1.1, .95), (1.05, 1.75), (.65, 2.45), (0, 2.7)], 'GiantBean', 16)
    r.lathe('giant-bean-seam', (cx, 5.2, cz), [(1.12, .9), (1.14, 1.35), (1.07, 1.8)], 'Gold', 16)
    r.tube('giant-bean-eye', [(cx-.25, 6.3, cz-1.08), (cx, 6.55, cz-1.12), (cx+.25, 6.3, cz-1.08)], .05, 'OakDark')
    r.light('giant-bean-light', (cx, 4.6, cz), 'GiantBean', 2.4, 9)
    for i, a in enumerate((.4, 2.5, 4.6)):
        r.tube(f'tendril-{i}', [(cx+.5*math.cos(a), 7.8, cz+.5*math.sin(a)), (cx+1.4*math.cos(a), 8.9, cz+1.4*math.sin(a)),
                                (cx+2.6*math.cos(a)+1.2, 9.7, cz+2.6*math.sin(a)+1.2), (5.0, 9.9, 5.2)], .09, 'Leaf')
    sx, sz = 5.3, 5.5
    r.tube('stalk', [(sx, 0, sz), (sx-.5, 2.2, sz+.3), (sx+.3, 4.4, sz-.4), (sx-.4, 6.6, sz+.2), (sx+.1, 8.6, sz-.3), (sx-.6, 10, sz-.6)], .55, 'Leaf')
    r.tube('stalk-twin', [(sx+.5, 0, sz-.5), (sx+.6, 2.4, sz-.2), (sx-.4, 4.8, sz+.3), (sx+.3, 7.2, sz), (sx-.2, 10, sz+.2)], .3, 'Hedge')
    r.blocker('stalk-block', (sx, 2.5, sz), (1.7, 5, 1.7))
    for i in range(7):
        a = i*2.4; y = 1.6+i*1.1
        r.box(f'stalk-leaf-{i}', (sx-.2+1.1*math.cos(a), y, sz+1.1*math.sin(a)), (1.3, .05, .7), 'Leaf' if i % 2 else 'Hedge', False, (18, math.degrees(-a), 12))
    for i in range(4):
        a = i*math.tau/4+.5
        r.tube(f'stalk-root-{i}', [(sx, .9, sz), (sx+1.0*math.cos(a), .3, sz+1.0*math.sin(a)), (sx+1.9*math.cos(a), .04, sz+1.9*math.sin(a))], .2, 'Leaf')
    # Five plates around the dais, each watched by a standing stone that wears its colour.
    for i, (colour, mat) in enumerate(BEAN_COLOURS):
        a = math.radians(90+72*i); c, s = math.cos(a), math.sin(a)
        px, pz = cx+3.4*c, cz+3.4*s; mx, mz = cx+4.45*c, cz+4.45*s; yaw = (0, 90-math.degrees(a), 0)
        r.lathe(f'plate-{colour}', (px, 0, pz), [(0, .02), (.34, .02), (.42, .05), (.5, .05), (.56, 0)], 'Limestone', 18)
        r.ring(f'plate-{colour}-ring', (px, .055, pz), .46, .035, mat, segments=24)
        r.lathe(f'plate-{colour}-mark', (px, .022, pz), [(0, .004), (.16, .004)], mat, 12)
        r.box(f'stone-{colour}', (mx, .85, mz), (.7, 1.7, .32), 'Stone', rot=yaw)
        r.box(f'stone-{colour}-band', (mx, 1.25, mz), (.74, .14, .36), mat, False, yaw)
        r.lathe(f'stone-{colour}-bean', (mx, 1.72, mz), [(0, 0), (.1, .03), (.15, .14), (.1, .25), (0, .28)], mat, 10)
        r.light(f'stone-{colour}-light', (mx-.5*c, 1.6, mz-.5*s), mat, 1.1, 3.6)
        r.tube(f'plate-{colour}-vine', [(mx-.2*c, .03, mz-.2*s), (cx+2.6*c+.2*s, .03, cz+2.6*s-.2*c), (cx+1.85*c, .03, cz+1.85*s)], .03, 'Hedge')
        r.anchors.append(dict(id=f'bean-socket-{colour}', kind='surface', position=v(px, 0, pz), rotation=v(*yaw)))
    r.anchors.append(dict(id='giant-bean-landing', kind='surface', position=v(cx, 0, cz)))
    r.anchors.append(dict(id='golden-egg-spawn', kind='surface', position=v(cx, 0, cz-1.1)))
    out = r.hide('root-hollow', -5.5, -5.3, 'east', 'Bark', 'Moss')
    cup(r, 'grove-cup', 5.4, -5.3, .7, handle=140)
    lantern_post(r, 'lamp-w', -4.3, 6.4, 3.0, 'WarmGlass', 'Copper'); lantern_post(r, 'lamp-e', 6.5, -3.0, 3.0, 'WarmGlass', 'Copper')
    fireflies(r, 'firefly', [(cx+2.4*math.cos(i*1.7), 2.0+1.6*math.sin(i*.9)**2, cz+2.4*math.sin(i*1.7)) for i in range(12)], 4)
    for name, x, z in (('m', 0, -4.9), ('front', 0, -1.9), ('sw', -4.1, -2.9), ('se', 4.6, -3.5), ('w', -5.9, -.6), ('nw', -5.6, 4.6), ('e', 5.9, -.6), ('e2', 5.9, 2.6)): r.nav(name, x, 0, z)
    r.link('south', 'm', 'front'); r.link('m', out); r.link(out, 'sw', 'w', 'nw'); r.link('m', 'se', 'e', 'e2')
    r.loot('Medium', -6.4, 0, 1.4); r.loot('Small', -6.5, 0, 6.3); r.loot('Tall', -2.6, 0, 6.5); r.loot('Small', 2.4, 0, 6.6)
    r.loot('Tiny', 6.5, 0, 3.6); r.loot('Medium', 6.4, 0, .8); r.loot('Small', 2.9, 0, -6.6); r.loot('Small', -2.9, 0, -6.6)
    r.loot('Tiny', -6.6, 0, -2.6); r.loot('Wide', 0, 0, 6.6)
    _views_south(r)
    return r.finish()


ROOMS = [coach_yard, drawbridge_yard, fairy_ring, tea_tray, crown_chapel, beanstalk_grove]
