"""Authored behavior previews, not game footage. Mirrors the runtime flip/flow/roll rules."""
import bpy,math,json,sys
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/valuables/depth-rework-08'
only=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['water_timer','cannon']
def lerp(a,b,t):return tuple(x+(y-x)*t for x,y in zip(a,b))
for name in only:
    folder=OUT/name;bpy.ops.wm.open_mainfile(filepath=str(folder/(name+'.blend')),load_ui=False)
    scene=bpy.context.scene;scene.frame_set(1)
    # This preview drives the runtime rules; source GLB animation must not override it at render evaluation.
    for ob in scene.objects:
        if ob.animation_data:ob.animation_data_clear()
    for material in bpy.data.materials:
        if material.node_tree and material.node_tree.animation_data:material.node_tree.animation_data_clear()
    cam=scene.camera;scene.render.resolution_x=480;scene.render.resolution_y=600;scene.cycles.samples=20
    root=bpy.data.objects[name];parts=[o for o in root.children if o.type=='MESH'];rest={o.name:o.matrix_world.copy() for o in parts}
    bounds={o.name:[o.matrix_world@Vector(c) for c in o.bound_box] for o in parts}
    if name=='water_timer':
        center=Vector((0,0,.13));cam.location=center+Vector((.32,-1.0,.20));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=.34
    else:
        ps=[p for vals in bounds.values() for p in vals];center=Vector((0,.18,.65));cam.location=center+Vector((3,-4,2));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=3.0
    dest=folder/'interaction_frames';dest.mkdir(exist_ok=True)
    for i in range(72):
        t=i/6;scene.frame_set(1)
        if name=='water_timer':
            flipped=t>=5;f=max(0,min(1,(t-5)/1.2)) if flipped else 0;f=f*f*(3-2*f);epoch=6.2 if flipped else 0
            progress=max(0,min(1,(t-epoch)/32));visibility=max(0,min(1,abs(f-.5)*2)) if flipped and t<epoch else 1
            for o in parts:
                group=o.get('group','static');base=rest[o.name];ps=bounds[o.name];mid=(Vector(tuple(min(p[j] for p in ps) for j in range(3)))+Vector(tuple(max(p[j] for p in ps) for j in range(3))))/2
                if group=='case':o.matrix_world=Matrix.Translation(Vector((0,0,.13)))@Matrix.Rotation(-math.pi*f,4,'Y')@Matrix.Translation(Vector((0,0,-.13)))@base
                elif group in ['drop','top_pool','bottom_pool']:
                    phase=float(o.get('phase',0));new=mid.copy();scale=Vector((1,1,1))
                    if group=='drop':
                        q=((t-epoch)/6+phase)%1;s=max(.001,min(q/.12,(1-q)/.13,1))*visibility;new.z=.179-.132*q;scale=Vector((s,s,s*(1+.12*math.sin(math.pi*q))))
                        c=lerp(lerp((.025,.13,.85),(.9,.025,.28),(progress+phase)%1),(.04,.65,.65),.16+.15*math.sin(math.pi*q))
                    else:
                        s=((1-.88*progress) if group=='top_pool' else (.15+1.10*progress))*visibility
                        half=(max(p.z for p in ps)-min(p.z for p in ps))/2;new.z=mid.z-half+half*s;scale.z=max(.001,s)
                        c=lerp((.025,.13,.85) if group=='top_pool' else (.9,.025,.28),lerp((.025,.13,.85),(.9,.025,.28),progress),.65)
                    o.matrix_world=Matrix.Translation(new)@Matrix.Diagonal((*scale,1))@Matrix.Translation(-mid)@base
                    p=o.data.materials[0].node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*c,1);p.inputs['Emission Color'].default_value=(*c,1);p.inputs['Emission Strength'].default_value=1.6
        else:
            # Drag forward on the floor, lift without spinning, then lower and drag back.
            if t<4:travel=.18*t;height=0;angle=travel/.545
            elif t<8:travel=.72;height=.34*math.sin((t-4)/4*math.pi);angle=.72/.545
            else:travel=.72-.18*(t-8);height=0;angle=travel/.545
            for o in parts:
                M=rest[o.name];group=o.get('group','static')
                if group in ['wheel_left','wheel_right']:
                    pivot=Vector((-.64 if group=='wheel_left' else .64,-.1,.545));M=Matrix.Translation(pivot)@Matrix.Rotation(-angle,4,'X')@Matrix.Translation(-pivot)@M
                o.matrix_world=Matrix.Translation(Vector((0,travel,height)))@M
        bpy.context.view_layer.update();scene.render.filepath=str(dest/f'{i:03d}.png');bpy.ops.render.render(write_still=True)
    print('V08_INTERACTION_PREVIEW',name,flush=True)
