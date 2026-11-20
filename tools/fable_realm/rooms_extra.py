"""Extra start rooms (25, 26) and extraction rooms (27-29): vanilla levels pick from several of each."""
import math

from library_recipe import v
from room_kit import PALETTE

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
    ('31', 'GiantPantry', 'Кладовая великана', 'Giant', 'Normal', 'GiantCup'),
    ('32', 'TrophyHall', 'Трофейный зал', 'Castle', 'Normal', 'Arch'),
]


# The beanstalk's cloud bank: walkable top at CLOUD_Y, on the west side of the grove (the stalk stands at x 0).
# The castle gate faces the stalk; the Golden Egg appears in its passage. BeanPuzzleRules.LowerDistance in the mod
# must equal BEANSTALK_SINK: everything named 'beanstalk-' is hidden that far under the floor before it grows.
CLOUD_Y = 28.0
BEANSTALK_SINK = 29.8
CASTLE_X = -4.6      # front face of the gate wall
SKY_TOP = CLOUD_Y+9  # room_finish raises the grove's painted sky above the castle


def _puff(r, name, x, y, z, radius, squash=.55):
    """One cloud puff: a flattened ball resting at height y."""
    steps = 7
    profile = [(radius*math.sin(math.pi*i/steps), radius*squash*(1-math.cos(math.pi*i/steps))) for i in range(steps+1)]
    r.lathe(name, (x, y-radius*squash, z), profile, 'Cloud', 14)


def cloud_castle(r, cx, cz):
    back, front = -7.2, -2.35
    length, half = front-back, 3.5
    # The bank itself: one solid slab under rolling puffs. Its front edge is open where the bridge leaf arrives.
    r.box('cloud-deck', ((back+front)/2, CLOUD_Y-.2, cz), (length, .4, 2*half), 'Cloud')
    for i, (x, z, rad) in enumerate(((-3.0, cz-3.0, 1.0), (-4.6, cz-3.2, 1.2), (-6.4, cz-3.0, 1.1), (-6.9, cz+.2, 1.0),
                                     (-6.3, cz+3.1, 1.2), (-4.4, cz+3.3, 1.1), (-2.7, cz+2.8, .9), (-2.5, cz-2.2, .8),
                                     (-2.5, cz+2.0, .8))):
        _puff(r, f'cloud-rim-{i}', x, CLOUD_Y+.35, z, rad, .45)
    for i, (x, z, rad) in enumerate(((-3.6, cz-1.8, 1.5), (-5.6, cz-2.2, 1.8), (-6.2, cz+1.8, 1.7), (-3.9, cz+2.0, 1.4), (-4.9, cz, 1.9))):
        _puff(r, f'cloud-under-{i}', x, CLOUD_Y-.4-rad*.5, z, rad, .5)  # below the deck top: nothing to trip on
    # Low cloud walls on the open sides, so a dropped egg does not roll off: everything but the arrival gap.
    for i, (x, z, sx, sz) in enumerate((((back+front)/2, cz-half+.2, length, .35), ((back+front)/2, cz+half-.2, length, .35),
                                        (front+.2, cz-2.3, .35, 2.4), (front+.2, cz+2.3, .35, 2.4))):
        r.box(f'cloud-wall-{i}', (x, CLOUD_Y+.35, z), (sx, .7, sz), 'Cloud')
    # Loose clouds around the stalk on the way up and above the bank; none within reach of the climb.
    for i, (x, y, z, rad) in enumerate(((4.6, 12.5, -4.4, 1.6), (-4.8, 15.0, -4.6, 1.8), (4.4, 18.5, 4.0, 2.0), (-5.2, 21.0, 4.6, 1.7),
                                        (4.4, 24.0, -3.6, 1.9), (3.6, 27.2, 4.4, 2.2), (5.2, 30.5, -2.4, 2.0), (-1.0, 33.5, -5.2, 2.4),
                                        (2.8, 34.5, 3.2, 2.1), (-6.0, 35.0, -1.8, 1.8))):
        _puff(r, f'cloud-drift-{i}', x, y, z, rad, .5)
        _puff(r, f'cloud-drift-{i}-b', x+rad*.7, y-.2, z+rad*.4, rad*.7, .5)
    # The castle gate: a curtain wall with an arched entrance, two round towers and a short vaulted passage behind.
    wall_h, open_w, open_h, thick = 4.6, 2.8, 3.4, .5
    gx = CASTLE_X-thick/2
    side = half-.3-open_w/2
    for i, s in enumerate((-1, 1)):
        r.box(f'castle-wall-{i}', (gx, CLOUD_Y+wall_h/2, cz+s*(open_w/2+side/2)), (thick, wall_h, side), 'WhiteStone')
    r.box('castle-lintel', (gx, CLOUD_Y+open_h+(wall_h-open_h)/2, cz), (thick, wall_h-open_h, open_w), 'WhiteStone')
    r._add('castle-arch', 'arch', (CASTLE_X+.02, CLOUD_Y, cz), (open_w, open_h, .3), 'Marble', (0, 90, 0), False, nav='Ignore',
           radius=.16, palette=['WhiteStone'])
    for i in range(7):
        r.box(f'castle-merlon-{i}', (gx, CLOUD_Y+wall_h+.3, cz-half+.6+i*(2*half-1.2)/6), (thick+.1, .6, .55), 'WhiteStone', False)
    for i, s in enumerate((-1, 1)):
        tz = cz+s*(half-.4)
        r.lathe(f'castle-tower-{i}', (gx, CLOUD_Y, tz), [(1.0, 0), (.95, .3), (.9, 6.2), (1.1, 6.4), (1.1, 6.9), (0, 6.9)], 'WhiteStone', 18)
        r.lathe(f'castle-tower-roof-{i}', (gx, CLOUD_Y+6.9, tz), [(1.25, 0), (0, 2.6)], 'Roof', 18)
        r.blocker(f'castle-tower-block-{i}', (gx, CLOUD_Y+3.45, tz), (1.8, 6.9, 1.8))
        r.box(f'castle-banner-{i}', (gx+1.0, CLOUD_Y+4.2, tz-s*.1), (.04, 1.8, .7), 'Velvet', False)
        r.tube(f'castle-flag-pole-{i}', [(gx, CLOUD_Y+9.4, tz), (gx, CLOUD_Y+10.6, tz)], .03, 'Gold')
        r.box(f'castle-flag-{i}', (gx, CLOUD_Y+10.35, tz+.35), (.03, .4, .7), 'Velvet', False)
    # Passage: side walls, vault and a back wall with the castle door, closed: the egg waits on its threshold.
    inner = CASTLE_X-thick
    deep = inner-(back+.15)
    for i, s in enumerate((-1, 1)):
        r.box(f'castle-passage-wall-{i}', ((inner+back+.15)/2, CLOUD_Y+open_h/2+.1, cz+s*(open_w/2+.15)), (deep, open_h+.2, .3), 'WhiteStone')
    r.box('castle-passage-vault', ((inner+back+.15)/2, CLOUD_Y+open_h+.25, cz), (deep, .3, open_w+.6), 'WhiteStone')
    r.box('castle-passage-back', (back+.3, CLOUD_Y+open_h/2+.1, cz), (.3, open_h+.2, open_w+.6), 'WhiteStone')
    r.box('castle-door', (back+.47, CLOUD_Y+1.4, cz), (.06, 2.8, 1.8), 'OakDark', False)
    for i, y in enumerate((.6, 2.2)):
        r.box(f'castle-door-band-{i}', (back+.51, CLOUD_Y+y, cz), (.03, .12, 1.8), 'Iron', False)
    r.box('castle-carpet', ((back+.45+front)/2, CLOUD_Y+.01, cz), (front-back-.45, .02, 1.3), 'Velvet', False)
    # Light: torches at the gate, a lamp in the passage and a high sun over the clouds.
    for i, s in enumerate((-1, 1)):
        r.lamp(f'castle-torch-{i}', CASTLE_X+.25, CLOUD_Y+2.6, cz+s*(open_w/2+.45), 'Candle', 'Iron', 1.8, 7)
    r.light('castle-passage-light', (inner-deep/2, CLOUD_Y+2.6, cz), 'Candle', 1.6, 5)
    r.light('cloud-sun', (cx, CLOUD_Y+7, cz), 'Sun', 2.4, 22)

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
    then a beanstalk grows out of the dais and the Golden Egg appears on its crown.
    Markers for the game side (kind 'surface'): bean-socket-<colour>, giant-bean-landing (the dais), golden-egg-spawn.
    Moving piece: every element whose id starts with 'beanstalk-' rises BEANSTALK_SINK out of the floor.
    The cloud bank and the castle gate on it stand there from the start; the egg appears inside the gate."""
    r = _giant('BeanstalkGrove', 'Роща великаньего боба', 'DeadEnd', 130)
    cx, cz = 0.0, .6
    # Dais the bean lands on.
    r.lathe('dais', (cx, 0, cz), [(0, .03), (1.75, .03), (1.9, 0)], 'Limestone', 28)
    r.ring('dais-rim', (cx, .035, cz), 1.75, .05, 'Gold', segments=40); r.ring('dais-glow', (cx, .04, cz), 1.2, .03, 'GiantBean', segments=32)
    # The beanstalk that grows out of the dais once the five beans lie on their plates. The game keeps every
    # 'beanstalk-' element sunk BEANSTALK_SINK under the floor until then and raises it; the recipe shows it fully grown.
    # It is as tall as Swiftbroom Academy's Great Tree (Module - Wizard - DE - 2 - Great Tree: floor to crown about
    # 30 m): it climbs to a cloud bank at CLOUD_Y. Leaves wind around the trunk as steps, half a metre each, eight to a
    # turn; every second turn ends on a wide resting leaf. The last leaf and a bridge leaf lead west onto the clouds.
    rise, top_leaf = .5, CLOUD_Y-.5
    leaves = int(round((top_leaf-.5)/rise))+1
    first = 180-45*(leaves-1)  # chosen so the last leaf points west (180 degrees), toward the cloud bank
    r.tube('beanstalk-trunk', [(cx+(.12 if k % 2 else -.1)*(k % 3 > 0), CLOUD_Y*k/10, cz+(.1 if k % 2 else -.12)*(k % 3 > 0))
                               for k in range(11)], .4, 'Leaf')
    r.tube('beanstalk-twin', [(cx+.32*math.cos(k*1.1), CLOUD_Y*k/10, cz+.32*math.sin(k*1.1)) for k in range(11)], .18, 'Hedge')
    r.blocker('beanstalk-trunk-block', (cx, CLOUD_Y/2, cz), (.7, CLOUD_Y, .7))
    for i in range(leaves):
        a = math.radians(first+45*i); top = .5+rise*i
        rest = i > 0 and i % 16 == 0
        reach = 1.45 if rest else 1.3
        lx, lz = cx+reach*math.cos(a), cz+reach*math.sin(a)
        size = (1.3, .14, 1.6) if rest else (.8, .12, 1.05)
        r.box(f'beanstalk-leaf-{i}', (lx, top-size[1]/2, lz), size, 'Leaf' if i % 2 else 'Hedge', rot=(0, -math.degrees(a), 0))
        r.tube(f'beanstalk-stem-{i}', [(cx, top-.25, cz), (lx, top-.1, lz)], .06, 'Hedge')
    # Bridge leaf between the last step and the edge of the cloud bank.
    r.box('beanstalk-bridge', (cx-2.15, CLOUD_Y-.25-.06, cz), (1.5, .12, 1.3), 'Leaf')
    r.tube('beanstalk-bridge-stem', [(cx, CLOUD_Y-.5, cz), (cx-2.15, CLOUD_Y-.32, cz)], .07, 'Hedge')
    r.lathe('beanstalk-crown', (cx, CLOUD_Y, cz), [(0, 0), (.55, .02), (.82, .1), (.78, .16), (0, .15)], 'Leaf', 14)
    for k in range(5):
        a = k*math.tau/5+.4
        r.tube(f'beanstalk-tendril-{k}', [(cx, CLOUD_Y+.1, cz), (cx+.7*math.cos(a), CLOUD_Y+.9, cz+.7*math.sin(a)),
                                         (cx+1.1*math.cos(a+.6), CLOUD_Y+1.6, cz+1.1*math.sin(a+.6))], .05, 'Hedge')
    r.light('beanstalk-light', (cx, CLOUD_Y*.55, cz-1.6), 'GiantBean', 2.4, 14)
    r.light('beanstalk-light-high', (cx+1.6, CLOUD_Y*.85, cz), 'GiantBean', 2.2, 12)
    cloud_castle(r, cx, cz)
    # The room volume reaches the castle, so players and the egg up there are still in this room.
    room = next(a for a in r.anchors if a['kind'] == 'volume' and a['id'] == 'room')
    room['position'] = v(0, SKY_TOP/2, 0); room['size'] = v(14.8, SKY_TOP, 14.8)
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
    r.anchors.append(dict(id='giant-bean-landing', kind='surface', position=v(cx, 0, cz-2.3)))  # bare ground at the foot of the dais
    r.anchors.append(dict(id='golden-egg-spawn', kind='surface', position=v(CASTLE_X-1.45, CLOUD_Y+.05, cz)))  # inside the castle gate
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


def _big_loot(r):
    """Open-floor rooms for the large valuables: four big, two wide, two very tall, two tall and four medium spots,
    one group per quarter of the room so the routes through the middle stay clear."""
    for sx, sz in ((1, 1), (-1, 1), (1, -1), (-1, -1)): r.loot('Big', 3.5*sx, 0, 3.5*sz)
    r.loot('VeryTall', 5.9, 0, 2.3); r.loot('VeryTall', -5.9, 0, -2.3)
    r.loot('Wide', -5.7, 0, 2.7, 90); r.loot('Wide', 5.7, 0, -2.7, 90)
    r.loot('Tall', 3.3, 0, 6.3); r.loot('Tall', -3.3, 0, -6.3)
    r.loot('Medium', -3.3, 0, 6.3); r.loot('Medium', 3.3, 0, -6.3); r.loot('Medium', 1.7, 0, 1.7); r.loot('Medium', -1.7, 0, -1.7)


def giant_pantry():
    """The giant's larder: an open floor between a few huge stores pushed into the corners."""
    r = _giant('GiantPantry', 'Кладовая великана', 'Normal', 131)
    r.lathe('jar', (-6.1, 0, 6.1), [(.55, 0), (.95, .5), (1.0, 1.9), (.7, 2.5), (.72, 2.8), (0, 2.85)], 'PorcelainBlue', 20)
    r.blocker('jar-block', (-6.1, 1.4, 6.1), (1.9, 2.8, 1.9))
    crate(r, 'crate-a', 6.2, 6.2, 1.7); crate(r, 'crate-b', 6.4, 6.3, 1.1, y=1.7, yaw=20)
    r.box('cheese', (6.1, .55, -6.1), (1.8, 1.1, 1.8), 'Butter' if 'Butter' in PALETTE else 'BookOchre', rot=(0, 30, 0))
    out = r.hide('sack-nook', -5.6, -5.6, 'east', 'Cloth', 'OakLight')
    r.lamp('pantry-lamp', 0, 6.4, 0, 'WarmGlass', 'Copper', 2.4, 11); r.tube('pantry-lamp-chain', [(0, 10, 0), (0, 6.7, 0)], .02, 'Copper')
    fireflies(r, 'firefly', [(4.4, 2.4, .4), (-4.2, 3.0, 1.2), (.6, 3.6, -4.4)], 2)
    r.nav('centre', 0, 0, 0)
    for side in ('north', 'east', 'south', 'west'): r.link(side, 'centre')
    r.link(out, 'south')
    _big_loot(r)
    _views_south(r)
    return r.finish()


def trophy_hall():
    """A bare castle hall kept for the largest trophies: four columns, a chandelier and room to carry."""
    r = _castle('TrophyHall', 'Трофейный зал', 'Normal', 132)
    for i, (x, z) in enumerate(((-6.2, 6.2), (6.2, 6.2), (6.2, -6.2))):
        column(r, f'column-{i}', x, z, 6.5, .4, 'Marble', 'Gold')
    out = r.hide('alcove', -5.6, -5.6, 'east', 'LimestoneDark', 'Velvet')
    chandelier(r, 'chandelier', 0, 6.0, 0, 1.5, 6, top=9.0)
    r.nav('centre', 0, 0, 0)
    for side in ('north', 'east', 'south', 'west'): r.link(side, 'centre')
    r.link(out, 'south')
    _big_loot(r)
    _views_south(r)
    return r.finish()


ROOMS = [coach_yard, drawbridge_yard, fairy_ring, tea_tray, crown_chapel, beanstalk_grove, giant_pantry, trophy_hall]
