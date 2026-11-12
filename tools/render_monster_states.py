"""Render representative animation targets without exporting or changing game assets.
blender -b --python tools/render_monster_states.py -- --root .
These are staged poses, not captures of Unity's interpolation, lighting or effects.
"""
from pathlib import Path
import sys, math, os
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
# Reactions every monster shares (MonsterVisual.Flinch / Struggle / the stun poses), frozen at one instant.
# The whole-body knock-back, thrash and wobble are applied to the model root in game and are not staged here.
def wave(count, amp, phase, step, skip=None):
    return {'tail' + str(i): unity(0, amp * math.sin(phase - abs(i - (skip or 0)) * step)) for i in range(count) if i != skip}
def neck(extra):
    p = wave(18, 6, .5, .65, 6)
    for i in range(6): p['tail' + str(i)] = unity(*extra)
    return p
reactions = {
 'pig': [
  ('hurt', {'head':unity(-20,14), 'wing_l':unity(0,0,-55), 'wing_r':unity(0,0,55)}),
  ('held', {'head':unity(10,16), 'wing_l':unity(0,0,22), 'wing_r':unity(0,0,14), 'leg-10.26':unity(-62), 'leg10.26':unity(8), 'leg-1-0.28':unity(-6), 'leg1-0.28':unity(64)}),
  ('stunned', {'head':unity(22,0,10), 'wing_l':unity(0,0,48), 'wing_r':unity(0,0,-48), 'leg-10.26':unity(-18,0,-12), 'leg10.26':unity(-18,0,12), 'leg-1-0.28':unity(18,0,-12), 'leg1-0.28':unity(18,0,12)}),
 ],
 'gorgon': [
  ('hurt', {'head':unity(-20,14), 'jaw':unity(16), 'leg-10.48':unity(-16), 'leg10.48':unity(-16), 'leg-1-0.65':unity(12), 'leg1-0.65':unity(12)}),
  ('held', {'head':unity(28,-14), 'jaw':unity(20), 'leg-10.48':unity(15,0,-9), 'leg10.48':unity(-15,0,9), 'leg-1-0.65':unity(-15,0,-9), 'leg1-0.65':unity(15,0,9)}),
  ('stunned', {'head':unity(24,16,10), 'jaw':unity(14), 'leg-10.48':unity(0,0,-9), 'leg10.48':unity(0,0,9), 'leg-1-0.65':unity(0,0,-9), 'leg1-0.65':unity(0,0,9)}),
 ],
 'witch': [
  ('hurt', {'arm_l':unity(-73,0,-20), 'arm_r':unity(-49,0,20), 'head':unity(-20,14)}),
  ('held', {'arm_l':unity(-58,0,-25), 'arm_r':unity(-104,0,25), 'head':unity(-10,16)}),
  ('stunned', {'arm_l':unity(15,0,-40), 'arm_r':unity(15,0,40), 'head':unity(26,0,10)}),
 ],
 'basilisk': [
  ('hurt', dict(neck((-7, 5)), jaw=unity(28), head=unity(-20,14))),
  ('held', dict(wave(18, 22, 1.0, .9, 6), jaw=unity(36), head=unity(8,18))),
  ('stunned', dict(wave(18, 5, 0, .5, 6), jaw=unity(10), head=unity(6,0,10))),
 ],
 'naga': [
  ('hurt', {'arm-10':unity(-38,24), 'arm-11':unity(-38,24), 'arm10':unity(-38,-24), 'arm11':unity(-38,-24), 'head':unity(-20,14)}),
  ('held', dict(wave(24, 12, .5, .6), **{'arm-10':unity(-88,0,18), 'arm-11':unity(-34,0,18), 'arm10':unity(-40,0,-18), 'arm11':unity(-90,0,-18), 'head':unity(10,16)})),
  ('stunned', {'arm-10':unity(60,0,25), 'arm10':unity(60,0,-25), 'arm-11':unity(35,0,15), 'arm11':unity(35,0,-15), 'head':unity(28,0,10)}),
 ],
 'fairy': [
  ('hurt', {'wing_l':unity(0,0,-36), 'wing_r':unity(0,0,36), 'arm_l':unity(-25,0,-20), 'arm_r':unity(-60,0,20), 'head':unity(-20,14)}),
  ('held', {'wing_l':unity(0,0,20), 'wing_r':unity(0,0,12), 'arm_l':unity(-9,0,-25), 'arm_r':unity(-116,0,25), 'head':unity(10,16)}),
  ('stunned', {'wing_l':unity(0,0,55), 'wing_r':unity(0,0,-55), 'arm_l':unity(20,0,-50), 'arm_r':unity(20,0,50), 'head':unity(30,0,10)}),
 ],
 'twins': [
  ('hurt', {'head_h':unity(-22,10,-16), 'head_g':unity(-22,10,16), 'arm_h_out':unity(-30), 'arm_g_out':unity(-30)}),
  ('held', {'head_h':unity(-10,18,-16), 'head_g':unity(-10,-16,16), 'arm_h_out':unity(-66,0,6), 'arm_g_out':unity(-134,0,-6),
            'foot_h_l':unity(36), 'foot_h_r':unity(-36), 'foot_g_l':unity(-36), 'foot_g_r':unity(36)}),
  ('stunned', {'head_h':unity(34,0,-38), 'head_g':unity(30,0,24), 'arm_h_out':unity(12), 'arm_g_out':unity(12)}),
 ],
}
only_states = [x for x in os.environ.get('IHM_STATES', '').split(',') if x]   # e.g. IHM_STATES=hurt,held,stunned
for key, maker, hover, states in [
 ('witch','witch',.1,witch), ('basilisk','basilisk',0,[('crawl',snake()),('warning',snake(True)),('constrict',snake(coil=True))]),
 ('gorgon','gorgon',0,gorgon), ('naga','naga',0,naga), ('pig','pig',.3,pig), ('fairy','fairy',.3,[]), ('twins','twins',0,[]),
]:
    if ns['args'].only and key not in ns['args'].only.split(','): continue
    for state, target in states + reactions[key]:
        if only_states and state not in only_states: continue
        bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
        ns.update(parts=[], pivots={'body':(0,0,0)}, parent={}, pose={}, posed={}, render_only=[])
        ns[maker](); bpy.context.view_layer.update()
        ns['pose']=dict(target)
        if key=='pig':
            for obj in ns['parts']:
                group=obj.name.split('|')[0]
                if state=='landed' and group.startswith('wing'): obj.hide_render=True
                if group.startswith('leg') and state!='landed' and group not in ns['pose']: ns['pose'][group]=unity(-30 if '-0.' not in group else 30)
        ns['apply_pose']()
        # Spell particles and shader bloom belong to the live game, not this pose render.
        ns['render'](key+'_'+state,0 if state=='landed' else hover)
        print('STATE_RENDERED',key,state,flush=True)
