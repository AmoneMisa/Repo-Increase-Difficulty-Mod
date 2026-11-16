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
