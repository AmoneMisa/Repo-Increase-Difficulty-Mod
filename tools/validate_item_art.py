"""Check Blender-to-Unity exports without requiring Blender or a game install."""
import json, math, struct, wave
from pathlib import Path

root=Path(__file__).resolve().parents[1]
assets=root/'src/Art'
def xyz(v): return tuple(v[c] for c in 'xyz')
def sub(a,b): return tuple(x-y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
total=0
for key in ('poison','ice','sticky','decoy'):
    model=json.loads((assets/(key+'.mesh.json')).read_text())
    assert model['version']==1 and model['fps']==30 and model['frames']==30
    moving=triangles=0
    for part in model['parts']:
        vs=part['vertices']; ns=part['normals']; ts=part['triangles']
        assert len(vs)==len(ns)==len(part['uv']) and len(ts)%3==0
        assert all(math.isfinite(c) for v in vs for c in v.values())
        assert all(0<=t<len(vs) for t in ts)
        for i in range(0,len(ts),3):
            a,b,c=[xyz(vs[t]) for t in ts[i:i+3]]
            area=cross(sub(b,a),sub(c,a)); n=xyz(ns[ts[i]])
            assert sum(x*y for x,y in zip(area,n))>1e-10, (key,part['name'],'winding/degenerate')
        triangles+=len(ts)//3
        if part['texture']: assert (assets/part['texture']).is_file()
        poses=[part['rest']]+part['samples']
        for pose in poses:
            assert all(math.isfinite(c) for channel in pose.values() for c in channel.values())
            assert abs(sum(c*c for c in pose['rotation'].values())-1)<1e-4
            assert all(s>0 for s in pose['scale'].values())
        if part['samples']:
            assert len(part['samples'])==model['frames']
            assert part['samples'][0]==part['rest'], (key,part['name'],'start pose')
            assert any(p!=part['rest'] for p in part['samples'])
            if key!='decoy': assert part['samples'][-1]==part['rest'], (key,part['name'],'return to idle')
            moving+=1
    assert moving>0
    print(f'{key}: {triangles} triangles, {moving} animated parts')
    total+=triangles
for key in ('ghost','silencer','link_a','link_b','repair','defence','protection'):
    for suffix in ('icon','coat','skin'): assert (assets/f'{key}_{suffix}.png').is_file()
for key in ('poison','ice','sticky','decoy'): assert (assets/f'{key}_inventory.png').is_file()
for png in assets.glob('*.png'):
    data=png.read_bytes(); assert data[:8]==b'\x89PNG\r\n\x1a\n'
    expected_sizes=((256,16),) if png.name=='v09_rgb_gradient.png' else ((1024,1024),) if png.name.startswith(('v08_','v09_')) else ((128,128),(256,256))
    assert struct.unpack('>II',data[16:24]) in expected_sizes
assert (root/'art/ihm_items.blend').is_file()
for name in ('workshop_wear','poison_puff','ice_frost'):
    assert (assets/(name+'.png')).is_file()
audio=root/'src/Audio'
expected={'poison_shot','ice_shot','sticky_shot','sticky_hit','ice_hit','poison_hit','power_down','spritz_shot'}
expected.update(k+'_activate' for k in ('ghost','silencer','link_a','link_b','repair','defence','protection','decoy'))
# Other modules (for example ping feedback) share this embedded Audio folder.
# Validate every known item cue without rejecting those unrelated resources.
assert expected <= {p.stem for p in audio.glob('*.wav')}
for name in sorted(expected):
    with wave.open(str(audio/(name+'.wav')),'rb') as sound:
        assert sound.getnchannels()==1 and sound.getsampwidth()==2 and sound.getframerate()==22050
        samples=struct.unpack('<'+'h'*sound.getnframes(),sound.readframes(sound.getnframes()))
        assert .15<len(samples)/22050<1
        assert max(abs(s) for s in samples)<30000, (name,'clipping')
        assert math.sqrt(sum(s*s for s in samples)/len(samples))>300, (name,'silent')
        assert abs(samples[0])<10 and abs(samples[-1])<100, (name,'edge click')
print(f'PASS: {len(expected)} mono audio cues; sample formats, duration, no clipping or silent files.')
print(f'PASS: {total} triangles; texture coverage, finite transforms, winding, idle resets, animation tracks.')
