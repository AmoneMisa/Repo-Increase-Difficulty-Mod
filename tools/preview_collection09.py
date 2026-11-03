"""Authored motion studies for review. These are NOT live Unity physics captures."""
import bpy,math,json,sys
from pathlib import Path
from mathutils import Vector,Matrix
OUT=Path(__file__).resolve().parents[1]/'art/valuables/interaction-rework-09'
def pivot(p,axis,angle):return Matrix.Translation(Vector(p))@Matrix.Rotation(angle,4,axis)@Matrix.Translation(-Vector(p))
only=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
for name in only or ['centrifuge','robot_vacuum','skeleton']:
    folder=OUT/name;bpy.ops.wm.open_mainfile(filepath=str(folder/(name+'.blend')),load_ui=False)
    root=bpy.data.objects[name];parts=[o for o in root.children if o.type=='MESH'];rest={o.name:o.matrix_world.copy() for o in parts}
    scene=bpy.context.scene;scene.cycles.samples=12;scene.render.resolution_x=480;scene.render.resolution_y=600
    for ob in scene.objects:
        if ob.animation_data:ob.animation_data_clear()
    centers={o.name:sum((o.matrix_world@Vector(p) for p in o.bound_box),Vector())/8 for o in parts}
    dest=folder/'motion_frames';dest.mkdir(exist_ok=True)
    if name=='centrifuge':scene.camera.data.ortho_scale=1.04
    definitions={'pelvis':('',(0,.015,1.04)),'torso':('pelvis',(0,.04,1.09)),'head':('torso',(0,.02,1.49))}
    for side,s in [('left',-1),('right',1)]:
        definitions.update({'upper_arm_'+side:('torso',(s*.19,0,1.395)),'forearm_'+side:('upper_arm_'+side,(s*.235,-.01,1.10)),'hand_'+side:('forearm_'+side,(s*.26,-.02,.85)),'thigh_'+side:('pelvis',(s*.095,0,1.02)),'shin_'+side:('thigh_'+side,(s*.095,-.009,.68)),'foot_'+side:('shin_'+side,(s*.095,-.01,.26))})
    for i in range(36):
        t=i/6;tilt=max(0,min(1,(t-.5)/1.5));tilt=tilt*tilt*(3-2*tilt)
        matrices={};travel=.12*math.sin(math.pi*t/6)
        if name=='skeleton':
            for g,(parent,p) in definitions.items():
                elapsed=max(0,t-1.5);amplitude=.12 if g in ['pelvis','torso','head'] else .35
                angle=amplitude*math.sin(elapsed*7+(0 if 'left' in g else .6))*math.exp(-elapsed*.55) if elapsed>0 else 0
                matrices[g]=matrices.get(parent,Matrix.Identity(4))@pivot(p,'X',angle)
        for ob in parts:
            g=ob.get('group','static');M=rest[ob.name]
            if name=='centrifuge':
                M=pivot((0,0,.32),'X',tilt*math.pi)@M
                if g.startswith('tube_') and t>2.25:
                    f=t-2.25;idx=int(g.split('_')[1]);M=pivot((0,0,.32),'X',math.pi)@rest[ob.name]
                    p=M.translation.copy();z=max(.065,p.z-4.905*f*f);p.x+=.16*math.cos(idx*math.pi/6)*min(f,1);p.y+=.16*math.sin(idx*math.pi/6)*min(f,1);p.z=z
                    c=M.translation.copy();M=Matrix.Translation(p-c)@pivot(c,'X',min(f,.6)*(idx%3+1)*2)@M
            elif name=='robot_vacuum':
                if g.startswith('drive_'):M=pivot(centers[ob.name],'X',-travel/.029)@M
                elif g in ['lidar','brush']:M=pivot(centers[ob.name],'Z',t*math.pi)@M
                M=Matrix.Translation(Vector((0,travel,0)))@M
            else:
                if g.startswith('caster_'):M=pivot(centers[ob.name],'X',-travel/.025)@M
                M=Matrix.Translation(Vector((0,travel,0)))@matrices.get(g,Matrix.Identity(4))@M
            ob.matrix_world=M
        bpy.context.view_layer.update();scene.render.filepath=str(dest/f'{i:03d}.png');bpy.ops.render.render(write_still=True)
    print('V09_MOTION_STUDY',name,flush=True)
