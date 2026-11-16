"""Writes every Fable Realm room recipe and the level room set.

Run from any directory:  python tools/fable_realm/build_level.py
Unity then builds and validates them: Fable Realm -> Build level room set.
"""
import json
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from library_recipe import ROOT, v, library, navigation_ready  # noqa: E402
from root_hall_recipe import root_hall  # noqa: E402
from room_kit import LOOT, PALETTE, Room, surface  # noqa: E402
import rooms_forest_village  # noqa: E402
import rooms_giant_castle  # noqa: E402
import rooms_extra  # noqa: E402
import loot_fill  # noqa: E402

CATALOG = ROOT.parent / 'room-catalog.json'
# Vanilla keeps three difficulty pools per module type; harder pools get pits and upper routes.
DIFFICULTY = {'BookGate': 1, 'RootHall': 2, 'MushroomChoir': 2, 'FireflyBrook': 1, 'LostPicnic': 1, 'HollowLibrary': 2,
              'SugarGate': 1, 'GingerbreadSquare': 1, 'BakersWorkshop': 2, 'CandyMarket': 1, 'Clockmaker': 2, 'MarzipanVault': 1,
              'BeanstalkCourt': 3, 'GiantTea': 2, 'PorcelainWalk': 2, 'RoseMaze': 2, 'ForgottenToy': 1, 'GardenStore': 2,
              'ThornGate': 3, 'MirrorCourt': 3, 'SilentBallroom': 3, 'SleepingObservatory': 3, 'StoryArchive': 3, 'RoyalTreasury': 3,
              'GiantPantry': 1, 'TrophyHall': 2, 'CoachYard': 2, 'DrawbridgeYard': 3, 'FairyRing': 1, 'TeaTray': 2, 'CrownChapel': 3, 'BeanstalkGrove': 3}


SPECIAL = {'BeanstalkGrove': 'bean-puzzle'}


def _loot(recipe, items):
    recipe['anchors'] = [a for a in recipe['anchors'] if a['kind'] != 'loot']
    for i, (kind, x, y, z) in enumerate(items, 1):
        recipe['anchors'].append(dict(id=f'loot-{i:02d}', kind='loot', tag=kind, position=v(x, y, z), rotation=v(), size=v(*LOOT[kind])))


def _surfaces(recipe):
    """Older recipes carry flat colours; give them the shared patterns, roughness and forest lighting."""
    upgraded = []
    for m in recipe['materials']:
        base = PALETTE.get(m['id'], (m['color'], m.get('grain', 0), m.get('smoothness', .2), m.get('metallic', 0), m.get('emission', 0)))
        # Glowing materials take the shared colour and strength, so lamps keep their hue instead of clipping to white.
        known = m['id'] in PALETTE
        upgraded.append(surface(m['id'], base[0] if known else m['color'], base[1], base[2], m.get('metallic', 0),
                                base[4] if known else m.get('emission', 0)))
    recipe['materials'] = upgraded + [surface('Vista', *PALETTE['Vista']), surface('SkyView', *PALETTE['SkyView'])]
    for e in recipe['elements']:
        # Windows show a painted view instead of flat glowing glass.
        if e['kind'] == 'disc' and e['material'] == 'MoonGlass': e['material'] = 'SkyView' if e['rotation']['x'] == 90 else 'Vista'
    recipe.update(ambient='#27393b', fog='#0d1a1a', fogDensity=.03)
    for e in recipe['elements']:
        # Door arches sit in rectangular openings: fill the corners with the wall material.
        if e['kind'] == 'arch' and (e['id'].startswith('door-arch') or e['id'] == 'entrance-arch'): e['palette'] = ['Bark']


def _connect(recipe, node, yaw):
    anchor = next(a for a in recipe['anchors'] if a['id'] == node)
    anchor['tag'] = 'connect'; anchor['rotation'] = v(0, yaw, 0)


def _dress_legacy(recipe, role, biome, seed, pits, walls):
    """Run the shared dressing pass over a hand-written recipe: hiding spots, high and tight places, clutter.
    The room is mirrored into a kit Room for placement checks; only the new pieces are added to the recipe."""
    room = Room(recipe['id'], recipe['title'], role, seed, 'Earth', 'Bark', 'OakDark')
    room.dressing, room.pits = biome, list(pits)
    for e in recipe['elements']:
        e = json.loads(json.dumps(e)); e.setdefault('navigation', 'Obstacle' if e.get('collider', True) else 'Ignore')
        if e['id'].startswith(walls): e['id'] = 'wall-' + e['id']
        if e.get('collider', True) and e['kind'] == 'tube':
            # Solid trunks: the pre-check knows boxes, so stand a box around the tube.
            xs, ys, zs = ([p[c] for p in e['points']] for c in 'xyz'); rad = e.get('radius', .3)
            e = dict(id=e['id'], kind='blocker', material='Bark', rotation=v(), collider=True, navigation='Obstacle',
                     position=v((min(xs)+max(xs))/2, (min(ys)+max(ys))/2, (min(zs)+max(zs))/2),
                     size=v(max(xs)-min(xs)+2*rad, max(ys)-min(ys), max(zs)-min(zs)+2*rad))
        elif e.get('collider', True) and e['kind'] not in ('box', 'blocker', 'rail', 'stairs', 'shelf', 'light'): e['collider'] = False
        room.elements.append(e); room.ids.add(e['id'])
    for a in recipe['anchors']:
        if a['kind'] != 'nav': room.anchors.append(a); continue
        p = a['position']; room.nodes[a['id']] = dict(pos=(p['x'], p['y'], p['z']), tag=a.get('tag', ''), yaw=None)
        for b in a.get('connectsTo', []):
            if (a['id'], b) not in room.edges and (b, a['id']) not in room.edges: room.edges.append((a['id'], b))
    room.loot_count = sum(1 for a in recipe['anchors'] if a['kind'] == 'loot')
    first_element, first_anchor = len(room.elements), len(room.anchors)
    room._dress()
    recipe['elements'] += room.elements[first_element:]; recipe['anchors'] += room.anchors[first_anchor:]
    have = {m['id'] for m in recipe['materials']}
    used = {e['material'] for e in recipe['elements']} | {m for e in recipe['elements'] for m in e.get('palette', [])}
    recipe['materials'] += [surface(k, *PALETTE[k]) for k in PALETTE if k in used and k not in have]


def _lanterns(recipe):
    """Swap the first room's block lamps for the shared caged lantern; their lights stay as they are."""
    kit = Room(recipe['id'], '', 'DeadEnd', 0, 'Earth', 'Bark', 'OakDark')
    for e in recipe['elements']:
        if e['kind'] == 'box' and e['id'].endswith('-glass'): kit.lamp(e['id'][:-6], *(e['position'][c] for c in 'xyz'))
    names = {e['id'][:-len(part)] for e in kit.elements for part in ('-glass',) if e['id'].endswith(part)}
    old = {n+part for n in names for part in ('-mount', '-glass', '-cap')}
    recipe['elements'] = [e for e in recipe['elements'] if e['id'] not in old] + [e for e in kit.elements if e['kind'] != 'light']
    recipe['materials'].append(surface('LampGlass', *PALETTE['LampGlass']))


def legacy_library():
    """The first detailed room, brought onto the module contract: connect point and valuable volumes."""
    recipe = navigation_ready(library()); _surfaces(recipe)
    _lanterns(recipe)
    _connect(recipe, 'entry', 180)
    _loot(recipe, [('Medium', -2.9, 0, -6.2), ('Small', 1.2, 0, -6.6), ('Tall', -1.4, 0, 5.6), ('Big', 3.0, 0, -2.0),
                   ('Tiny', 4.35, .7, -5.6), ('Medium', 5.6, 0, 3.0), ('Small', 5.8, 3, 4.4), ('Tiny', -6.0, 3, 5.9),
                   ('Small', 0, 3, 6.2), ('Medium', 5.6, 3, 1.2)])
    _dress_legacy(recipe, 'DeadEnd', 'forest', 106, [(-2.6, 2.6, -.6, 3.6)], ('rear-wall', 'side-wall', 'front-wall'))
    return recipe


def legacy_root_hall():
    """The second detailed room: connect points, closable exits and valuable volumes."""
    recipe = root_hall(); _surfaces(recipe)
    for k, side in enumerate(('north', 'east', 'south', 'west')):
        _connect(recipe, side, k*90)
        a = k*math.pi/2
        x, z = 7.3*math.sin(a), 7.3*math.cos(a)
        recipe['elements'].append(dict(id=f'plug-{side}', kind='box', position=v(x, 1.9, z), size=v(4.5, 3.8, .3), material='Bark',
                                       rotation=v(0, k*90, 0), collider=True, navigation='Obstacle', variant=f'closed-door-{side}'))
        for i, (offset, tilt) in enumerate(((-.9, 28), (.9, -28))):
            bx, bz = 7.1*math.sin(a)+offset*math.cos(a), 7.1*math.cos(a)-offset*math.sin(a)
            recipe['elements'].append(dict(id=f'plug-{side}-root-{i}', kind='box', position=v(bx, 1.9, bz), size=v(.28, 4.3, .12),
                                           material='OakDark', rotation=v(0, k*90, tilt), collider=False, navigation='Ignore',
                                           variant=f'closed-door-{side}'))
    _loot(recipe, [('Small', -3.0, 0, -6.3), ('Medium', -3.0, 0, -4.2), ('Tiny', 4.2, 0, -4.2), ('Small', 3.2, 0, 6.3),
                   ('Tall', 6.5, 0, -3.4), ('Medium', -6.4, 0, 3.0), ('Small', 5.9, 3, 2.2), ('Tiny', 6.2, 3, 5.5)])
    _dress_legacy(recipe, 'Normal', 'forest', 102, [(-4.5, 4.5, -1.5, 1.5)], ())
    return recipe


def main():
    ROOT.mkdir(parents=True, exist_ok=True)
    recipes = {r['id']: r for r in [legacy_library(), legacy_root_hall()]}
    for module in (rooms_forest_village, rooms_giant_castle, rooms_extra):
        for build in module.ROOMS:
            recipe = build(); recipes[recipe['id']] = recipe
    catalog = json.loads(CATALOG.read_text(encoding='utf-8'))
    known = {room['key'] for room in catalog['rooms']}
    for number, key, title, biome, role, hero in rooms_extra.CATALOG:
        # Extra start and extraction rooms join the catalog after the original 24.
        if key not in known:
            catalog['rooms'].append(dict(id=number, key=key, title=title, biome=biome, role=role, hero=hero, width=15, depth=15,
                                         doorClearance=3.2, pitDepth=0, upperRoute=False, shelterType='alcove'))
    entries = []
    for room in catalog['rooms']:
        recipe = recipes.pop(room['key'])
        loot_fill.fill(recipe, room['role'])  # vanilla-like numbers of valuable volumes for every size
        path = ROOT / (room['key'] + '.room.json')
        path.write_text(json.dumps(recipe, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        entries.append(dict(recipe='Assets/FableRealm/RoomDefinitions/' + path.name, role=room['role'], difficulty=DIFFICULTY[room['key']]))
        if room['key'] in SPECIAL: entries[-1]['special'] = SPECIAL[room['key']]  # at most one per level; the game side runs its puzzle
        room['status'] = 'authored-recipe-gameplay-anchors'
        room['layoutSource'] = 'RoomDefinitions/' + path.name
        count = lambda kind: sum(1 for a in recipe['anchors'] if a['kind'] == kind)
        sizes = ' '.join(f"{kind[0]}{'T' if kind == 'VeryTall' else ''}{sum(1 for a in recipe['anchors'] if a['kind'] == 'loot' and a.get('tag') == kind)}"
                         for kind in ('Tiny', 'Small', 'Medium', 'Big', 'Wide', 'Tall', 'VeryTall'))
        print(f"{room['id']} {room['key']:<20} {room['role']:<10} {len(recipe['elements']):>4} elements  "
              f"{count('nav'):>2} level points  {count('loot'):>2} loot ({sizes})")
    if recipes: raise SystemExit('Recipes missing from the catalog: ' + ', '.join(recipes))
    (ROOT / 'FableRealm.room-set.json').write_text(json.dumps(dict(
        schemaVersion=1, id='FableRealm', title='Страна забытых сказок', gameplayContract=True, rooms=entries),
        ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    CATALOG.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'Wrote {len(entries)} room recipes and FableRealm.room-set.json')


if __name__ == '__main__':
    main()
