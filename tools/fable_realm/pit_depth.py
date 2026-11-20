"""Turns every floor pit into a real pit: a shaft that drops out of sight instead of a lit cellar 3-4 m down.

The rooms build their pits three ways (the room kit, the library and the root hall recipes), but each one is a
bottom slab and thin walls. This pass, run on the finished recipe, works from those pieces:
- the walls continue down to VOID metres, darker by depth (the pit's own stone, then PitShade, then Void);
- the bottom slab moves down there and turns Void, with whatever lay on it (fallen books) moved along;
- lights inside the pit go: a glow at the bottom is what made it read as a shallow floor;
- roots hang down the walls from the rim, so the edge is not a clean cut.
The death zone follows on its own: export_contract.py puts it between the bottom and 1.3 m under the floor.
"""
import random
import zlib

from library_recipe import v
from room_kit import PALETTE, surface

VOID = 12.0          # depth of the pit bottom's top face, metres under the floor
SHADE = 2.5          # height of the PitShade band under the pit's own wall material


def _top(e): return e['position']['y'] + e['size']['y']/2
def _bottom(e): return e['position']['y'] - e['size']['y']/2
def _flat(e): return not any(abs(e.get('rotation', {}).get(c, 0)) > 1e-6 for c in 'xyz')


def _box(name, centre, size, mat):
    return dict(id=name, kind='box', position=v(*centre), size=v(*size), material=mat, rotation=v(), collider=True, navigation='Obstacle')


def _pit_bottom(elements, anchor):
    """The slab export_contract.py treats as the pit's bottom: the highest wide collider below -1.5 m under the probe."""
    px, pz = anchor['position'].get('x', 0), anchor['position'].get('z', 0)
    best = None
    for e in elements:
        if e['kind'] != 'box' or not e.get('collider', True) or e.get('variant'): continue
        p, s = e['position'], e['size']
        if _top(e) > -1.5 or min(s['x'], s['z']) < 1.5: continue
        if abs(px-p['x']) > s['x']/2 or abs(pz-p['z']) > s['z']/2: continue
        if best is None or _top(e) > _top(best): best = e
    return best


def footprints(recipe):
    """(x0, x1, z0, z1) of every pit, taken from its bottom slab."""
    out = []
    for anchor in recipe['anchors']:
        if anchor['kind'] != 'pit': continue
        b = _pit_bottom(recipe['elements'], anchor)
        if b is None: continue
        p, s = b['position'], b['size']
        out.append((p['x']-s['x']/2, p['x']+s['x']/2, p['z']-s['z']/2, p['z']+s['z']/2))
    return out


def deepen(recipe):
    elements = recipe['elements']
    ids = {e['id'] for e in elements}
    for anchor in [a for a in recipe['anchors'] if a['kind'] == 'pit']:
        bottom = _pit_bottom(elements, anchor)
        if bottom is None: raise SystemExit(f"{recipe['id']}: pit {anchor['id']} has no bottom")
        p, s = bottom['position'], bottom['size']
        x0, x1, z0, z1 = p['x']-s['x']/2, p['x']+s['x']/2, p['z']-s['z']/2, p['z']+s['z']/2
        cx, cz = p['x'], p['z']
        old = _top(bottom)
        def inside(e, grow): return x0-grow <= e['position']['x'] <= x1+grow and z0-grow <= e['position']['z'] <= z1+grow
        walls = [e for e in elements if e['kind'] == 'box' and e.get('collider', True) and not e.get('variant') and _flat(e)
                 and min(e['size']['x'], e['size']['z']) <= .3 and e['size']['y'] >= 1 and _top(e) <= .1
                 and abs(_bottom(e) - old) <= .7 and inside(e, .4)]
        if len(walls) < 4: raise SystemExit(f"{recipe['id']}: pit {anchor['id']} has {len(walls)} walls, expected 4")
        drop = -VOID - old
        rnd = random.Random(zlib.crc32((recipe['id'] + anchor['id']).encode()))
        for wall in walls:
            wp, ws = wall['position'], wall['size']
            for k, (top, low, mat) in enumerate(((_bottom(wall), _bottom(wall)-SHADE, 'PitShade'), (_bottom(wall)-SHADE, -VOID-.4, 'Void'))):
                name = f"{wall['id']}-deep-{k}"
                elements.append(_box(name, (wp['x'], (top+low)/2, wp['z']), (ws['x'], top-low, ws['z']), mat)); ids.add(name)
            # Roots from the rim, hanging just off the wall face on the pit side.
            across = 'x' if ws['x'] <= ws['z'] else 'z'
            along = 'z' if across == 'x' else 'x'
            inward = 1 if (cx if across == 'x' else cz) > wp[across] else -1
            face = wp[across] + inward*(ws[across]/2 + .06)
            length = ws[along]
            for i in range(max(1, int(length/1.6))):
                at = wp[along] + (i + .5 + rnd.uniform(-.3, .3))/max(1, int(length/1.6))*length - length/2
                drop_to = rnd.uniform(2.6, 5.2)
                points = []
                for j in range(7):
                    t = j/6
                    a = at + rnd.uniform(-.12, .12)*t
                    c = face + inward*(.05 + rnd.uniform(0, .12)*t)
                    pt = {along: a, across: c, 'y': -.3 - drop_to*t}
                    points.append(v(pt['x'], pt['y'], pt['z']))
                name = f"{wall['id']}-root-{i}"
                elements.append(dict(id=name, kind='tube', position=v(), size=v(1, 1, 1), material='Thorn', rotation=v(),
                                     collider=False, navigation='Ignore', points=points, radius=round(rnd.uniform(.035, .065), 3)))
                ids.add(name)
        # Whatever lay on the old bottom goes down with it; lights inside the pit go.
        for e in elements:
            if e is bottom or e in walls or e['kind'] == 'light' or e['id'].endswith(('-deep-0', '-deep-1')) or '-root-' in e['id']: continue
            if inside(e, 0) and old - .3 <= e['position']['y'] <= old + 1.2:
                e['position']['y'] += drop
        recipe['elements'] = elements = [e for e in elements if not (e['kind'] == 'light' and e['position']['y'] < 0 and inside(e, .5))]
        p['y'] += drop
        bottom['material'] = 'Void'
        anchor['size']['y'] = VOID
    have = {m['id'] for m in recipe['materials']}
    used = {e['material'] for e in recipe['elements']}
    recipe['materials'] += [surface(k, *PALETTE[k]) for k in ('PitShade', 'Void', 'Thorn') if k in used and k not in have]
    return recipe
