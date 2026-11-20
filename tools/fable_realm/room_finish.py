"""Last pass over every finished room recipe: real door frames and open-air rooms.

Door frames. The game side hangs a pair of vanilla hinge doors (the Swiftbroom Academy door) in every
portal, so a doorway has to be a plain rectangle the leaves can swing through. The rounded door arches
narrowed the opening to a round top with infill corners, which the leaves would hit; here every arch at
a portal becomes two square posts and a header inside the wall's own thickness (7.2-7.5 m from the
centre), so nothing sticks out into the neighbouring room's doorway.
  Clear opening: 3.9 m wide (posts at +-1.95), 3.5 m high. The game side hinges the leaves at +-1.85.

Open air. Some rooms lose their ceiling and stand under a painted daytime sky: a sky box just above the
wall tops, a sun lamp instead of the moon, and pale stone and meadow instead of the dark shell colours.
"""
import math
import re

from library_recipe import v
from room_kit import PALETTE, surface
import rooms_extra

FRAME_DEPTH, POST, HEADER, OPENING_HEIGHT = .34, .3, .3, 3.5
WALL_GAP = 2.25  # half-width of the gap every recipe leaves in its wall at a portal

# Rooms that stand under the sky. The bean ritual grove is the one that asked for it.
OPEN_SKY = ('BeanstalkGrove', 'FairyRing', 'RoseMaze', 'LostPicnic', 'BeanstalkCourt', 'GardenStore')
# A sky that reaches higher than 3.4 m over the wall tops: the grove's beanstalk climbs to a castle in the clouds.
SKY_TOP = {'BeanstalkGrove': rooms_extra.SKY_TOP}
_CEILING = re.compile(r'^(ceiling$|star-|drop-|canopy-branch-)')
_NIGHT_LIGHTS = ('moon', 'moonlight', 'dusk')
_SHELL = re.compile(r'^(wall|lintel|plug|panel|patch)')
_TRIM = re.compile(r'^(rib|cornice|plinth|doorframe)')


def _xz(e): return e['position']['x'], e['position']['z']


def door_frames(recipe):
    portals = [a for a in recipe['anchors'] if a['kind'] == 'portal']
    for portal in portals:
        px, pz = portal['position']['x'], portal['position']['z']
        length = math.hypot(px, pz) or 1.0
        ox, oz = px/length, pz/length                  # outward
        rx, rz = oz, -ox                               # along the wall
        yaw = round(math.degrees(math.atan2(ox, oz))) % 360
        arches = [e for e in recipe['elements'] if e['kind'] == 'arch' and math.hypot(_xz(e)[0]-px, _xz(e)[1]-pz) < .8]
        material = arches[0]['material'] if arches else 'OakDark'
        recipe['elements'] = [e for e in recipe['elements'] if e not in arches]
        cx, cz = px-ox*(FRAME_DEPTH/2), pz-oz*(FRAME_DEPTH/2)
        name = f"doorframe-{portal['id']}"
        for i, s in enumerate((-1, 1)):
            post = WALL_GAP-POST/2
            recipe['elements'].append(dict(id=f'{name}-post-{i}', kind='box', position=v(cx+rx*s*post, OPENING_HEIGHT/2, cz+rz*s*post),
                                           size=v(POST, OPENING_HEIGHT, FRAME_DEPTH), material=material, rotation=v(0, yaw, 0),
                                           collider=True, navigation='Obstacle'))
        recipe['elements'].append(dict(id=f'{name}-header', kind='box', position=v(cx, OPENING_HEIGHT+HEADER/2, cz),
                                       size=v(WALL_GAP*2, HEADER, FRAME_DEPTH), material=material, rotation=v(0, yaw, 0),
                                       collider=True, navigation='Obstacle'))
    _materials(recipe)


def open_sky(recipe):
    height = recipe['bounds']['y']-4
    kept = []
    for e in recipe['elements']:
        if _CEILING.match(e['id']) or (e['kind'] == 'light' and e['id'] in _NIGHT_LIGHTS): continue
        if e['id'].startswith('floor-'): e['material'] = 'PaleMeadow'
        elif _SHELL.match(e['id']): e['material'] = 'WhiteStone'
        elif _TRIM.match(e['id']): e['material'] = 'Marble'
        kept.append(e)
    recipe['elements'] = kept
    # Sky box: a lid 3.4 m above the wall tops and four sides from just under the wall tops up to the lid.
    top, bottom, reach = SKY_TOP.get(recipe['id'], height+3.4), height-.5, 7.6
    recipe['elements'].append(dict(id='sky-top', kind='box', position=v(0, top, 0), size=v(2*reach+.2, .1, 2*reach+.2), material='DaySky',
                                   rotation=v(), collider=False, navigation='Ignore'))
    for k, side in enumerate(('north', 'east', 'south', 'west')):
        a = k*math.pi/2
        recipe['elements'].append(dict(id=f'sky-{side}', kind='box', position=v(reach*math.sin(a), (top+bottom)/2, reach*math.cos(a)),
                                       size=v(2*reach+.2, top-bottom, .1), material='DaySky', rotation=v(0, k*90, 0),
                                       collider=False, navigation='Ignore'))
    recipe['elements'].append(dict(id='sun', kind='light', position=v(1.5, height+2.0, -1.0), material='Sun', size=v(1, 1, 1), rotation=v(),
                                   collider=False, navigation='Ignore', intensity=2.6, range=24.0, shadows=True))
    recipe['elements'].append(dict(id='sky-fill', kind='light', position=v(0, height-1.5, 0), material='DaySky', size=v(1, 1, 1), rotation=v(),
                                   collider=False, navigation='Ignore', intensity=1.1, range=16.0, shadows=False))
    recipe.update(ambient='#9aa4ad', fog='#c8d4dc', fogDensity=.01)
    _materials(recipe)


def _materials(recipe):
    have = {m['id'] for m in recipe['materials']}
    used = {e['material'] for e in recipe['elements']} | {m for e in recipe['elements'] for m in e.get('palette', [])}
    recipe['materials'] = [m for m in recipe['materials'] if m['id'] in used or m['id'] in ('Vista', 'SkyView')]
    recipe['materials'] += [surface(k, *PALETTE[k]) for k in PALETTE if k in used and k not in have]


def finish(recipe):
    door_frames(recipe)
    if recipe['id'] in OPEN_SKY: open_sky(recipe)
