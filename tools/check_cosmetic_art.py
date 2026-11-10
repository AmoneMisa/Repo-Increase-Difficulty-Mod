"""Validate shipped cosmetic/pet JSON, textures, pivots and catalogue parity (no Blender needed)."""
import json
import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'src' / 'Art'
failures = []
checks = 0


def check(ok, context):
    global checks
    checks += 1
    if not ok:
        failures.append(context)


def opening_clear(model):
    """Geometric sight-line fixture for the front eye region in the Hat slot (not a live rig test)."""
    points = [(side*.104+dx, .162+dy) for side in (-1,1)
              for dx,dy in ((0,0),(-.045,0),(.045,0),(0,-.055),(0,.055))]
    for part in model['parts']:
        origin=part['rest']['position'];vs=[tuple(v[c]+origin[c] for c in 'xyz') for v in part['vertices']]
        tri=part['triangles']
        for i in range(0,len(tri),3):
            a,b,c=(vs[tri[i+j]] for j in range(3))
            denominator=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
            if abs(denominator)<1e-10:continue
            for x,y in points:
                u=((b[1]-c[1])*(x-c[0])+(c[0]-b[0])*(y-c[1]))/denominator
                v=((c[1]-a[1])*(x-c[0])+(a[0]-c[0])*(y-c[1]))/denominator
                if u>=0 and v>=0 and u+v<=1:
                    z=u*a[2]+v*b[2]+(1-u-v)*c[2]
                    if .46<=z<=.75:return False
    return True


def validate(key, pet=False):
    file = ART / (key + '.mesh.json' if pet else 'v11_' + key + '.mesh.json')
    check(file.exists(), f'Missing {file.name}')
    if not file.exists(): return
    model = json.loads(file.read_text(encoding='utf-8'))
    pivot_names = {p['name' if pet else 'group'] for p in model.get('pivots', [])}
    check(len(pivot_names) == len(model.get('pivots', [])), f'{key}: duplicate pivots')
    if not pet:
        materials = model['materials']
        # character parts in fixed colours (faces, skin, hair) are registered as not tintable and must stay that way
        check(any(m.get('tint') for m in materials) != (key in fixed), f'{key}: tintable material does not match its registration')
        check((ART / f'cosmetic_{key}_icon.png').exists(), f'{key}: no menu icon')
        if key.startswith('wings_'):
            check(pivot_names == {'wing_l', 'wing_r'}, f'{key}: wing roots')
            check(not any(any(word in p['name'].lower() for word in ('membrane', 'web', 'covered feather roots', 'feather bed'))
                          for p in model['parts']), f'{key}: unwanted backing plate')
        for m in materials:
            for prop in ('texture', 'emissionTexture'):
                if m.get(prop): check((ART / m[prop]).exists(), f'{key}: missing {m[prop]}')
    channels = set()
    expressions = set()
    for part in model['parts']:
        n = len(part['vertices']); tris = part['triangles']
        check(n > 0 and n == len(part['normals']) == len(part['uv']), f'{key}/{part["name"]}: parallel arrays')
        check(len(tris) % 3 == 0 and min(tris, default=0) >= 0 and max(tris, default=0) < n, f'{key}: triangle indices')
        check(pet or n <= 65535, f'{key}: UInt16 vertex overflow')
        check(all(math.isfinite(c) for prop in ('vertices','normals','uv') for v in part[prop] for c in v.values()), f'{key}: nonfinite geometry')
        if pet:
            group, channel, _ = part['name'].split('|', 2)
            channels.add(channel)
            if group.startswith('eyes_'): expressions.add(group)
            check(group in pivot_names or group in ('body','eyes_open','eyes_happy','eyes_wide','eyes_sleepy','brows_sad','brows_focus'), f'{key}: unbound {group}')
            if part.get('texture'): check((ART / part['texture']).exists(), f'{key}: missing texture')
        else:
            check(part.get('group','static') in pivot_names | {'static'}, f'{key}: unbound group')
            check(0 <= part['material'] < len(model['materials']), f'{key}: material index')
    if pet:
        check({'main','second','accent'} <= channels, f'{key}: missing recolour channel')
        check(expressions == {'eyes_open','eyes_happy','eyes_wide','eyes_sleepy'}, f'{key}: missing expression')
        check((ART / f'{key}_normal.png').exists(), f'{key}: missing surface normals')


wings_source = (ROOT/'src/Cosmetics/WingCosmetics.cs').read_text(encoding='utf-8')
legacy = re.findall(r'new\("([a-z_]+)",', wings_source)
fixed = set(re.findall(r'new\("([a-z_]+)",[^\n]*, false, "', wings_source))
catalogue = (ROOT/'src/Cosmetics/MascotCosmetics.cs').read_text(encoding='utf-8')
sets = re.findall(r'\("([a-z]+)", "[^"]+"\)', catalogue)
pieces = re.findall(r'Part\(set, "([a-z_]+)"', catalogue)
pieces = [p for p in pieces if p not in ('face', 'neck')]   # only some sets have a face and a jaw
new = [s + '_' + p for s in sets for p in pieces]
check(len(sets) == 13 and len(new) == 91, 'Thirteen seven-part costume sets')
face_sets = re.findall(r'"([a-z]+)"', re.search(r'FaceSets = new\(\)\s*\{([^}]*)\}', catalogue).group(1))
extra = [s + '_' + p for s in face_sets for p in ('face', 'neck')] + re.findall(r'new WingCosmetics\.Definition\("([a-z_]+)"', catalogue)
fixed |= set(re.findall(r'new WingCosmetics\.Definition\("([a-z_]+)"', catalogue)) | set(re.findall(r'"([a-z_]+)"', re.search(r'Fixed = new\(\)\s*\{([^}]*)\}', catalogue).group(1)))
check(len(extra) == 13, 'Six faces with jaws, and lips')
for key in legacy + new + extra: validate(key)
for kind in sets:
    model=json.loads((ART/f'v11_{kind}_hood.mesh.json').read_text(encoding='utf-8'))
    check(opening_clear(model), f'{kind}: eye-region sight line blocked by hood/detail')
    for side in ('r','l'):
        boot=json.loads((ART/f'v11_{kind}_boot_{side}.mesh.json').read_text(encoding='utf-8'))
        vertices=[{axis:v[axis]+p['rest']['position'][axis] for axis in 'xyz'} for p in boot['parts'] for v in p['vertices']]
        sole=[v for v in vertices if v['y']<-.53]
        width=max(v['x'] for v in sole)-min(v['x'] for v in sole)
        depth=max(v['z'] for v in sole)-min(v['z'] for v in sole)
        check(depth>width*1.3, f'{kind}/{side}: footwear needs an extended toe/heel silhouette')
        glove=json.loads((ART/f'v11_{kind}_glove_{side}.mesh.json').read_text(encoding='utf-8'))
        reach=max(abs(v['x']+p['rest']['position']['x']) for p in glove['parts'] for v in p['vertices'])
        check(reach>.545, f'{kind}/{side}: native arm tip may protrude through glove')
look = (ROOT/'src/Courier/CourierLook.cs').read_text(encoding='utf-8')
animals = re.findall(r'"([a-z]+)"', re.search(r'Keys = \{([^}]+)', look).group(1))
check(animals[:5] == ['penguin','chicken','cat','owl','beaver'], 'Existing save IDs must remain unchanged')
check(len(animals) == 12, 'Twelve courier species')
for key in animals: validate('courier_' + key, True)
if failures:
    raise SystemExit('\n'.join(failures))
print(f'PASS: {checks} art checks; {len(legacy)+len(new)+len(extra)} cosmetics, {len(animals)} pets. All referenced assets exist.')
