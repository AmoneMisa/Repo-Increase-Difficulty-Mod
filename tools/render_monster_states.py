"""Render representative animation targets without exporting or changing game assets.
blender -b --python tools/render_monster_states.py -- --root .
These are staged poses, not captures of Unity's interpolation, lighting or effects.
"""
from pathlib import Path
import sys, math
import bpy

source = Path(__file__).with_name('build_monsters.py')
ns = {'__file__': str(source), '__name__': 'monster_preview'}
definitions = source.read_text(encoding='utf8').split('CLOSEUPS =')[0]
definitions = definitions.replace('(1.0 if closeup else .95)', '(1.0 if closeup else 1.1)')
exec(compile(definitions, str(source), 'exec'), ns)
ns['preview'] = ns['root'] / 'art' / 'monsters' / 'states'
ns['preview'].mkdir(parents=True, exist_ok=True)

# Unity +X pitch/+Y yaw/+Z roll -> Blender -X/-Z/-Y, respectively.
def unity(x=0, y=0, z=0): return (-x, -z, -y)
witch = [
 ('idle', {'arm_l':unity(-38), 'arm_r':unity(-14)}),
 ('fire_prep', {'arm_l':unity(-38), 'arm_r':unity(-88,0,12), 'head':unity(-6)}),
 ('electric', {'arm_l':unity(-165,0,-14), 'arm_r':unity(-165,0,14), 'head':unity(-12)}),
]
gorgon = [
 ('idle', {}),
 ('charge', {'head':unity(34), 'leg-10.48':unity(32), 'leg10.48':unity(42), 'leg-1-0.65':unity(-38), 'leg1-0.65':unity(-25)}),
 ('stare', {'head':unity(-16), 'jaw':unity(6)}),
]
naga = [
 ('idle', {}),
 ('combo', {'arm-10':unity(-20), 'arm10':unity(88,-18), 'head':unity(6)}),
 ('gaze', {'arm-10':unity(0,-20,35), 'arm10':unity(0,20,-35), 'arm-11':unity(0,-40,20), 'arm11':unity(0,40,-20), 'head':unity(9)}),
]
def snake(raised=False, coil=False):
    p={}; rise=[28,26,4,-20,-26,-26]
    for i in range(18):
        if i==6: continue
        d=abs(i-6)
        yaw = (-360/18 if i<6 else 360/18*(1+d*.07)) if coil else math.sin(.5-d*.65)*13*(.6 if i<6 else 1)
        pitch = -rise[i] if raised and i<6 else 0
        p['tail'+str(i)] = unity(pitch,yaw)
    p['jaw']=unity(28 if raised else 0)
    return p
pig = [
 ('flying', {'wing_l':unity(0,0,-32),'wing_r':unity(0,0,32)}),
 ('eating', {'head':unity(38),'wing_l':unity(0,0,18),'wing_r':unity(0,0,-18)}),
 ('landed', {}),
]
for key, maker, hover, states in [
 ('witch','witch',.1,witch), ('basilisk','basilisk',0,[('crawl',snake()),('warning',snake(True)),('constrict',snake(coil=True))]),
 ('gorgon','gorgon',0,gorgon), ('naga','naga',0,naga), ('pig','pig',.3,pig),
]:
    if ns['args'].only and key not in ns['args'].only.split(','): continue
    for state, target in states:
        bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
        ns.update(parts=[], pivots={'body':(0,0,0)}, parent={}, pose={}, posed={}, render_only=[])
        ns[maker](); bpy.context.view_layer.update()
        ns['pose']=target
        if key=='pig':
            for obj in ns['parts']:
                group=obj.name.split('|')[0]
                if state=='landed' and group.startswith('wing'): obj.hide_render=True
                if group.startswith('leg') and state!='landed': ns['pose'][group]=unity(-30 if '-0.' not in group else 30)
        ns['apply_pose']()
        # Spell particles and shader bloom belong to the live game, not this pose render.
        ns['render'](key+'_'+state,0 if state=='landed' else hover)
        print('STATE_RENDERED',key,state,flush=True)
