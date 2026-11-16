"""Writes the compact room contract the mod embeds: src/FableRealm/fable_rooms.json.

The Unity prefab of a room carries geometry only. Everything the game side needs to bind vanilla components
(role, difficulty, special tag, anchors with their tag/size/links, the ids of the closed-exit variants and the
room's ambience) comes from the recipes, so the mod and the prefabs can never disagree about a room.

Run after build_level.py:  python tools/fable_realm/export_contract.py
"""
import json
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
ASSETS = REPO / 'unity' / 'FableRealm' / 'Assets'
SET = ASSETS / 'FableRealm' / 'RoomDefinitions' / 'FableRealm.room-set.json'
OUT = REPO / 'src' / 'FableRealm' / 'fable_rooms.json'


def vec(value):
    value = value or {}
    return [round(float(value.get(axis, 0)), 4) for axis in 'xyz']


def kill_zones(recipe):
    """One 'kill' anchor per pit: the box from the pit's bottom up to 1.3 m under the room floor, where the mod
    puts the death zone. Anyone standing on the rim or on a bridge across is above it."""
    zones = []
    for anchor in recipe['anchors']:
        if anchor['kind'] != 'pit': continue
        px, pz = anchor['position'].get('x', 0), anchor['position'].get('z', 0)
        best = None
        for e in recipe['elements']:
            if e['kind'] != 'box' or not e.get('collider', True) or e.get('variant'): continue
            pos, size = e['position'], e['size']
            top = pos['y'] + size['y'] / 2
            if top > -1.5 or min(size['x'], size['z']) < 1.5: continue
            if abs(px - pos['x']) > size['x'] / 2 or abs(pz - pos['z']) > size['z'] / 2: continue
            if best is None or top > best[0]: best = (top, pos, size)
        if best is None: raise SystemExit(f"{recipe['id']}: pit {anchor['id']} has no bottom to put a death zone on")
        top, pos, size = best
        zones.append(dict(id='kill-' + anchor['id'], kind='kill', p=[round(pos['x'], 4), round((top - 1.3) / 2, 4), round(pos['z'], 4)],
                          s=[round(size['x'] - .1, 4), round(-1.3 - top, 4), round(size['z'] - .1, 4)]))
    return zones


def main():
    room_set = json.loads(SET.read_text(encoding='utf-8'))
    rooms = []
    for entry in room_set['rooms']:
        recipe = json.loads((ASSETS.parent / entry['recipe']).read_text(encoding='utf-8'))
        anchors = []
        for anchor in recipe['anchors']:
            row = dict(id=anchor['id'], kind=anchor['kind'], p=vec(anchor.get('position')))
            if anchor.get('tag'): row['tag'] = anchor['tag']
            if any(vec(anchor.get('rotation'))): row['r'] = vec(anchor.get('rotation'))
            if any(vec(anchor.get('size'))): row['s'] = vec(anchor.get('size'))
            if anchor.get('connectsTo'): row['to'] = anchor['connectsTo']
            anchors.append(row)
        anchors += kill_zones(recipe)
        variants = sorted({e['variant'] for e in recipe['elements'] if e.get('variant')})
        # Only these elements are floor for the navmesh; every other collider is an obstacle (as in the editor bake).
        walkable = sorted({e['id'] for e in recipe['elements'] if e.get('navigation') == 'Walkable' and not e.get('variant')})
        rooms.append(dict(id=recipe['id'], role=entry['role'], difficulty=entry.get('difficulty', 1),
                          special=entry.get('special', ''), ambient=recipe.get('ambient', ''), fog=recipe.get('fog', ''),
                          fogDensity=recipe.get('fogDensity', 0), variants=variants, walkable=walkable, anchors=anchors))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(dict(schema=1, id=room_set['id'], rooms=rooms), ensure_ascii=False,
                              separators=(',', ':')) + '\n', encoding='utf-8')
    print(f'Wrote {len(rooms)} rooms, {sum(len(r["anchors"]) for r in rooms)} anchors, {OUT.stat().st_size // 1024} KB: {OUT}')


if __name__ == '__main__':
    main()
