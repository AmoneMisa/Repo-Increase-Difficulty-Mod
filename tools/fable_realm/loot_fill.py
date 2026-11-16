"""Tops up the valuable volumes of a finished room recipe to vanilla-like numbers.

Measured in the game's Manor modules (average volumes per module, by type):
    normal    Tiny 28  Small 12  Medium 4.2  Big 3.0  Wide 2.2  Tall 2.0  VeryTall 2.3
    passage   Tiny 20  Small  6  Medium 2.2  Big 1.7  Wide 1.1  Tall 1.1  VeryTall 1.4
    dead end  Tiny 20  Small  9  Medium 2.8  Big 1.6  Wide 0.9  Tall 0.9  VeryTall 1.3
Vanilla start and extraction modules carry none, so those roles are left as the recipe wrote them.

Hand-placed volumes stay. New ones go on bare ground-level floor: supported, clear of every collider and of
the decoration that becomes solid in game, off doorways, level-point routes, pads, hiding spots and puzzle
sockets, and within a short clear walk of a level point. The room's own pre-check has the last word.
"""
import math
import random

from room_kit import LOOT, colliders, precheck, _Obb, _mat

TARGET = {
    'Normal': dict(Tiny=14, Small=9, Medium=4, Big=3, Wide=2, Tall=2, VeryTall=2),
    'Passage': dict(Tiny=11, Small=7, Medium=3, Big=2, Wide=1, Tall=1, VeryTall=1),
    'DeadEnd': dict(Tiny=12, Small=9, Medium=3, Big=2, Wide=1, Tall=1, VeryTall=1),
}
ORDER = ('VeryTall', 'Wide', 'Big', 'Tall', 'Medium', 'Small', 'Tiny')  # the hardest to fit go first
IDENTITY = _mat(0, 0, 0)


def _xyz(d): return tuple((d or {}).get(k, 0) for k in 'xyz')


def _decor(recipe):
    """Rough boxes around decoration without recipe collision: the mod makes it solid (FableModuleBuilder.Solidify)."""
    out = []
    for e in recipe['elements']:
        if e.get('collider', True) or e['kind'] == 'light': continue
        pos, size = _xyz(e.get('position')), _xyz(e.get('size'))
        if e['kind'] == 'box':
            if max(size) < .3 or (size[1] < .06 and pos[1] < .2): continue
            out.append(_Obb(e['id'], pos, tuple(s/2 for s in size), _mat(*_xyz(e.get('rotation')))))
        elif e['kind'] == 'lathe' and e.get('points'):
            radius = max(p['x'] for p in e['points']); ys = [p['y'] for p in e['points']]
            out.append(_Obb(e['id'], (pos[0], pos[1]+(min(ys)+max(ys))/2, pos[2]), (radius, (max(ys)-min(ys))/2, radius), IDENTITY))
        elif e['kind'] == 'tube' and e.get('points'):
            pts = [tuple(pos[i]+_xyz(p)[i] for i in range(3)) for p in e['points']]; radius = e.get('radius', .05)
            for a, b in zip(pts, pts[1:]):
                steps = max(1, math.ceil(math.dist(a, b)/.3))
                for i in range(steps+1):
                    c = tuple(a[k]+(b[k]-a[k])*i/steps for k in range(3))
                    out.append(_Obb(e['id'], c, (radius+.16,)*3, IDENTITY))
    return out


def _mirror(recipe):
    """The two hand-written rooms give colliders to tubes and arches, which the box model does not know.
    As in build_level._dress_legacy: a solid tube becomes the box around it, other shapes count as decoration."""
    elements, legacy = [], False
    for e in recipe['elements']:
        if e.get('collider', True) and e['kind'] not in ('box', 'blocker', 'rail', 'stairs', 'shelf', 'light'):
            legacy = True; e = dict(e)
            if e['kind'] == 'tube' and e.get('points'):
                pos = _xyz(e.get('position')); rad = e.get('radius', .3)
                lo = [min(pos[i]+_xyz(p)[i] for p in e['points']) for i in range(3)]
                hi = [max(pos[i]+_xyz(p)[i] for p in e['points']) for i in range(3)]
                e.update(kind='blocker', rotation=dict(x=0, y=0, z=0), points=None,
                         position=dict(x=(lo[0]+hi[0])/2, y=(lo[1]+hi[1])/2, z=(lo[2]+hi[2])/2),
                         size=dict(x=hi[0]-lo[0]+2*rad, y=max(.1, hi[1]-lo[1]), z=hi[2]-lo[2]+2*rad))
            else: e['collider'] = False
        elements.append(e)
    return dict(recipe, elements=elements), legacy


def fill(recipe, role):
    """Adds loot anchors to the recipe in place. Returns {type: added}."""
    target = TARGET.get(role)
    if not target: return {}
    mirror, legacy = _mirror(recipe)
    base, _ = colliders(mirror)
    solid = base + _decor(mirror)
    rng = random.Random(recipe.get('seed', 0)*31+7)
    anchors = recipe['anchors']
    low = [_xyz(a['position']) for a in anchors if a['kind'] == 'nav' and _xyz(a['position'])[1] < 1.0]
    byid = {a['id']: a for a in anchors}
    edges = [(_xyz(a['position']), _xyz(byid[t]['position'])) for a in anchors if a['kind'] == 'nav'
             for t in a.get('connectsTo', []) if t in byid and a['id'] < t
             and max(_xyz(a['position'])[1], _xyz(byid[t]['position'])[1]) < 1.0]
    keep = []  # (x, z, radius) circles that stay free
    for a in anchors:
        x, y, z = _xyz(a['position'])
        if a['kind'] == 'portal': keep.append((x, z, 2.3))
        elif a['kind'] == 'hide': keep.append((x, z, 1.5))
        elif a['kind'] == 'surface': keep.append((x, z, 1.1))
        elif a['kind'] == 'extraction': keep.append((x, z, 2.7))
        elif a['kind'] == 'truck': keep.append((x, z, 3.9))
        elif a['kind'] == 'spawn': keep.append((x, z, .8))
    taken = [(_xyz(a['position'])[0], _xyz(a['position'])[2], max(_xyz(a['size'])[0], _xyz(a['size'])[2])/2)
             for a in anchors if a['kind'] == 'loot' and _xyz(a['position'])[1] < 1.0]

    def segment(px, pz, a, b):
        ux, uz = b[0]-a[0], b[2]-a[2]; length = ux*ux+uz*uz
        t = 0 if length < 1e-9 else max(0, min(1, ((px-a[0])*ux+(pz-a[2])*uz)/length))
        return math.hypot(px-a[0]-ux*t, pz-a[2]-uz*t)

    def floor(x, z):
        for b in base:
            t = b.ray((x, .1, z), (0, -1, 0), .3)
            if t is not None and abs(t-.1) < .04: return True
        return False

    def clear(x, z, hx, hy, hz):
        near = [b for b in solid if b.distance((x, hy+.06, z)) < math.sqrt(hx*hx+hy*hy+hz*hz)+.2]
        if not near: return True
        nx, ny, nz = (max(2, math.ceil(2*h/.22)+1) for h in (hx, hy, hz))
        for i in range(nx):
            for j in range(ny):
                for k in range(nz):
                    p = (x-hx+2*hx*i/(nx-1), .1+(2*hy-.04)*j/(ny-1), z-hz+2*hz*k/(nz-1))
                    if any(b.distance(p) < .09 for b in near): return False
        return True

    def reachable(x, z):
        for n in sorted(low, key=lambda n: math.hypot(n[0]-x, n[2]-z))[:3]:
            length = math.hypot(n[0]-x, n[2]-z)
            if length > 6: break
            steps = max(1, math.ceil(length/.25)); ok = True
            for i in range(steps):  # stops short of the volume itself
                px, pz = n[0]+(x-n[0])*i/steps, n[2]+(z-n[2])*i/steps
                if not floor(px, pz) or any(b.distance((px, y, pz)) < .22 for b in solid for y in (.45, .95)): ok = False; break
            if ok: return True
        return False

    grid = [(-6.75+.25*i, -6.75+.25*j) for i in range(55) for j in range(55)]
    added, new = {}, []
    for kind in ORDER:
        want = target[kind]-sum(1 for a in anchors if a['kind'] == 'loot' and a.get('tag') == kind)
        if want <= 0: continue
        sx, sy, sz = LOOT[kind]
        rng.shuffle(grid)
        for x, z in grid:
            if want <= 0: break
            yaw = rng.choice((0, 90)) if sx != sz else 0
            hx, hz = (sz/2, sx/2) if yaw else (sx/2, sz/2)
            # A long volume is judged by its mean half-size: as a full circle it would fit almost nowhere.
            r = (hx+hz)/2; route = .3 if r < .3 else .4
            if abs(x)+hx > 6.95 or abs(z)+hz > 6.95: continue
            if any(math.hypot(x-kx, z-kz) < r+kr for kx, kz, kr in keep): continue
            if any(math.hypot(x-tx, z-tz) < r+tr+.25 for tx, tz, tr in taken): continue
            if any(math.hypot(x-n[0], z-n[2]) < r+route for n in low): continue
            if any(segment(x, z, a, b) < r+route for a, b in edges): continue
            if not all(floor(x+dx*(hx-.05), z+dz*(hz-.05)) for dx, dz in ((0, 0), (-1, -1), (-1, 1), (1, -1), (1, 1))): continue
            if not clear(x, z, hx, sy/2, hz) or not reachable(x, z): continue
            anchor = dict(id=f'loot-fill-{len(new):02d}', kind='loot', tag=kind, position=dict(x=round(x, 3), y=0, z=round(z, 3)),
                          rotation=dict(x=0, y=yaw, z=0), size=dict(x=sx, y=sy, z=sz))
            anchors.append(anchor); new.append(anchor); taken.append((x, z, r)); want -= 1
            added[kind] = added.get(kind, 0)+1
    if legacy: return {k: n for k, n in added.items() if n}  # hand-written rooms are checked by Unity only
    # The pre-check names the anchor in every loot complaint: drop whatever it rejects.
    problems = precheck(recipe, role)
    rejected = [a for a in new if any(a['id'] in p for p in problems)]
    for a in rejected:
        anchors.remove(a); added[a['tag']] -= 1
    left = precheck(recipe, role) if rejected else problems
    if left: raise RuntimeError(f"{recipe['id']} after loot fill:\n  " + '\n  '.join(left))
    return {k: n for k, n in added.items() if n}
