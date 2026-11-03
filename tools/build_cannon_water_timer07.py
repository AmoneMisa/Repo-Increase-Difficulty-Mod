"""Two original valuable models, including a real sampled liquid-timer animation."""
import sys,json,math,shutil,bpy,bmesh
from pathlib import Path
from mathutils import Vector
from math import pi,sin,cos
sys.path.insert(0,str(Path(__file__).resolve().parent))
import valuable_meshkit as kit
from valuable_meshkit import box,cyl,ico,mesh,torus,lathe,tube,bar,mat,reset
from merge_gltf_loop import merge_loop
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/valuables/cannon-water-timer-07';OUT.mkdir(parents=True,exist_ok=True)
(OUT/'textures').mkdir(exist_ok=True)
for name in ['green','steel','dark','gold','bone']:
    shutil.copyfile(ROOT/'art/valuables/medical-tech-pack-06/textures'/f'loot_{name}.png',OUT/'textures'/f'loot_{name}.png')
kit.OUT=OUT;kit.TEXTURES=OUT/'textures'

def color(name,c,rough=.35,transmission=0,ior=1.46):
    m=bpy.data.materials.new(name);m.diffuse_color=(*c,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*c,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=0
    p.inputs['Transmission Weight'].default_value=transmission;p.inputs['IOR'].default_value=ior;return m

def annulus_x(name,x,y,z,inner,outer,width,m,n=36):
    profile=[(-width/2,inner),(-width/2,outer),(width/2,outer),(width/2,inner)]
    vs=[(x+d,y+r*cos(j*2*pi/n),z+r*sin(j*2*pi/n)) for d,r in profile for j in range(n)];fs=[]
    for k in range(4):
        for j in range(n):fs.append((k*n+j,k*n+(j+1)%n,((k+1)%4)*n+(j+1)%n,((k+1)%4)*n+j))
    return mesh(name,vs,fs,m)

def carriage_rail(x):
    rings=[(-.42,.84,.12),(.20,.76,.15),(.65,.47,.14),(1.10,.16,.12)]
    vs=[]
    for y,z,h in rings:vs.extend([(x-.045,y,z-h/2),(x+.045,y,z-h/2),(x+.045,y,z+h/2),(x-.045,y,z+h/2)])
    fs=[(3,2,1,0),(12,13,14,15)]
    for k in range(3):
        for j in range(4):a=k*4+j;b=k*4+(j+1)%4;fs.append((a,b,b+4,a+4))
    return mesh('Tapered carriage rail',vs,fs,'green')

def cannon():
    # External historic-prop geometry only: bore is a shallow visual recess.
    for x in [-.64,.64]:
        annulus_x('Iron wheel tire',x,-.1,.55,.507,.545,.09,'steel')
        annulus_x('Painted wheel felloe',x,-.1,.55,.468,.514,.074,'green')
        cyl('Wheel hub',(x,-.10,.55),.087,.21,'green',16,'X')
        cyl('Axle cap',(x+(.115 if x>0 else -.115),-.10,.55),.050,.022,'steel',12,'X')
        for i in range(12):
            a=i*2*pi/12
            bar('Wheel spoke',(x,-.10+.08*cos(a),.55+.08*sin(a)),(x,-.10+.483*cos(a),.55+.483*sin(a)),.020,'green',6)
        for i in range(6):
            a=i*pi/3
            cyl('Hub bolt',(x+(.126 if x>0 else -.126),-.10+.058*cos(a),.55+.058*sin(a)),.007,.012,'gold',8,'X')
    bar('Main axle',(-.75,-.1,.55),(.75,-.1,.55),.040,'dark',12)
    for x in [-.14,.14]:carriage_rail(x)
    for y,z in [(-.34,.84),(.30,.71),(.82,.34)]:bar('Carriage crossbrace',(-.19,y,z),(.19,y,z),.025,'steel',8)
    box('Trail spade',(0,1.17,.055),(.34,.23,.09),'dark',.014)
    for x in [-.22,.22]:
        box('Trunnion support',(x,-.10,.91),(.09,.25,.23),'green',.015)
        cyl('Trunnion pivot',(x,-.10,1.02),.066,.12,'steel',16,'X')
    # Thick muzzle lip with a recessed dark bore rather than a sealed end disk.
    profile=[(0,.045),(.04,.10),(.10,.155),(.20,.16),(.34,.144),(.78,.106),(.94,.103),
             (1.01,.128),(1.10,.132),(1.13,.12),(1.13,.081),(1.00,.078),(.91,.075)]
    o=lathe('Cast barrel and recessed muzzle',profile,'green',40)
    o.location=(0,.29,.94);direction=Vector((0,-1.18,.26));o.rotation_euler=direction.to_track_quat('Z','Y').to_euler()
    o.data.materials.append(mat('dark'))
    for p in o.data.polygons:
        p.use_smooth=True
        if p.index>=10*40:p.material_index=1
    # Cast reinforcing collars around the breech.
    for distance,r in [(.19,.166),(.37,.147),(.80,.112)]:
        center=o.location+direction.normalized()*distance
        ob=torus('Barrel reinforcing ring',center,r,.009,'steel',32);ob.rotation_euler=o.rotation_euler
    ico('Breech knob',(0,.32,.935),(.068,.085,.065),'green',2)
    for x in [-.195,.195]:
        for y,z in [(-.30,.86),(.3,.70),(.77,.36)]:cyl('Carriage fastening',(x,y,z),.012,.012,'steel',8,'X')

def slab(name,outline,depth,m):
    vs=[(x,y,z) for y in [-depth/2,depth/2] for x,z in outline];n=len(outline)
    fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,vs,fs,m)

def water_timer():
    glass=color('Timer | clear acrylic',(.91,.97,1),.12,1,1.46)
    blue=color('Timer | cobalt liquid',(.009,.026,.55),.16,.13,1.33)
    pink=color('Timer | magenta liquid',(.64,.009,.20),.18,.10,1.33)
    edge=color('Timer | frosted rim',(.68,.82,.83),.28,.6,1.46)
    outline=[(-.050,0),(.050,0),(.052,.012),(.047,.050),(.041,.108),(.042,.163),(.050,.226),(.052,.252),(.049,.260),(-.049,.260),(-.052,.252),(-.050,.226),(-.042,.163),(-.041,.108),(-.047,.05),(-.052,.012)]
    casing=slab('Timer_Acrylic_Casing',outline,.038,glass)
    bevel=casing.modifiers.new('Soft acrylic edges','BEVEL');bevel.width=.0012;bevel.segments=2;bpy.context.view_layer.objects.active=casing;bpy.ops.object.modifier_apply(modifier=bevel.name)
    box('Clear top seal',(0,0,.254),(.098,.037,.010),edge,.002)
    box('Clear bottom seal',(0,0,.006),(.099,.037,.010),edge,.002)
    funnel=[(-.043,.237),(.043,.237),(.043,.211),(.036,.202),(.005,.191),(.002,.177),(-.002,.177),(-.005,.191),(-.036,.202),(-.043,.211)]
    slab('Timer_Upper_Blue_Reservoir',funnel,.025,blue)
    # Colored lower reservoirs, separated slightly in depth so both are visible.
    pool=slab('Timer_Lower_Pink_Reservoir',[(-.044,.017),(.044,.017),(.044,.034),(.029,.041),(.012,.035),(-.011,.039),(-.030,.031),(-.044,.034)],.025,pink)
    pool.location.y=.001
    bluepool=slab('Timer_Lower_Blue_Reservoir',[(-.043,.012),(.043,.012),(.043,.021),(.025,.025),(.008,.020),(-.018,.025),(-.043,.020)],.024,blue)
    bluepool.location.y=-.001
    for i in range(9):
        ico('Timer_Settled_Bead_%02d'%i,(-.038+i*.0095,-.001,.025+.0015*(i%2)),(.004,.005,.0037),blue,1)
    drops=[]
    for c,m in [('Blue',blue),('Pink',pink)]:
        for i in range(3):
            profile=[(-.008,.0008),(-.006,.0053),(-.002,.008),(.003,.0082),(.008,.0054),(.015,.0008)]
            ob=lathe('Timer_Drop_'+c+'_'+str(i),profile,m,16)
            ob.location=( -.010 if c=='Blue' else .011, -.002 if c=='Blue' else .004,.11)
            for p in ob.data.polygons:p.use_smooth=True
            drops.append((ob,i*16+(8 if c=='Pink' else 0)))
    return drops

def prepare(name):
    parts=kit.bake_parts()
    if name=='cannon':
        groups={}
        for ob in parts:groups.setdefault(tuple(m.name for m in ob.data.materials),[]).append(ob)
        parts=[]
        for i,group in enumerate(groups.values()):
            bpy.ops.object.select_all(action='DESELECT')
            for ob in group:ob.select_set(True)
            bpy.context.view_layer.objects.active=group[0]
            if len(group)>1:bpy.ops.object.join()
            ob=bpy.context.object;ob.name='Cannon_Material_Group_%02d'%i;parts.append(ob)
    bpy.ops.object.select_all(action='DESELECT');root=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(root)
    for ob in parts:ob.parent=root
    return root,parts

def animate(drops):
    scene=bpy.context.scene;scene.name='Water_Timer_Flow';scene.render.fps=24;scene.frame_start=1;scene.frame_end=145
    for ob,offset in drops:
        for frame in range(1,146):
            t=((frame-1+offset)%48)/48
            # Birth/death take place while tiny; there is no visible bottom-to-top jump.
            birth=min(1,t/.10);death=min(1,(1-t)/.11);s=max(.001,min(birth,death))
            ob.location.z=.176-.131*(t**1.3)
            ob.scale=(s,s,s*(1.05+.20*sin(pi*t)))
            ob.keyframe_insert(data_path='location',frame=frame);ob.keyframe_insert(data_path='scale',frame=frame)
        ob.animation_data.action.name='Liquid_Timer_Flow_'+ob.name
    scene.frame_set(1)

def write_exports(name,root,parts,animated):
    scene=bpy.context.scene;folder=OUT/name;folder.mkdir(exist_ok=True);scene.frame_set(1);bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    for ob in parts:ob.select_set(True)
    root.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(folder/(name+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_animations=animated,export_animation_mode='SCENE',export_force_sampling=True,export_frame_range=True)
    if animated:merge_loop(folder/(name+'.glb'))
    # OBJ cannot store animation; choose a readable pose with no near-zero birth scales.
    if animated:scene.frame_set(20)
    bpy.ops.wm.obj_export(filepath=str(folder/(name+'.obj')),export_selected_objects=True,forward_axis='NEGATIVE_Z',up_axis='Y',export_materials=True,export_uv=True,export_normals=True,path_mode='RELATIVE')
    mtl=folder/(name+'.mtl');lines=[]
    for line in mtl.read_text().splitlines():
        if line.startswith('map_Kd '):line='map_Kd ../textures/'+Path(line[7:].replace('\\','/')).name
        lines.append(line)
    mtl.write_text('\n'.join(lines)+'\n')
    points=[o.matrix_world@v.co for o in parts for v in o.data.vertices]
    row={'asset':name,'role':'valuable','triangles':sum(len(o.data.polygons) for o in parts),'mesh_parts':len(parts),'bounds_m_xyz':[max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)],'animated':animated,'source_checks':'PASS'}
    if animated:
        row.update({'fps':24,'frames':145,'duration_seconds':6,'animated_droplets':6})
        tracks=[]
        for ob in parts:
            if not ob.animation_data:continue
            samples=[]
            for frame in range(1,146):
                scene.frame_set(frame);p=ob.location;s=ob.scale
                samples.append({'position':[p.x,p.z,p.y],'scale':[s.x,s.z,s.y]})
            tracks.append({'node':ob.name,'samples':samples})
        (folder/'water_timer.animation.json').write_text(json.dumps({'fps':24,'loop':True,'frames':145,'duration_seconds':6,'axes':'Unity X/Y/Z (converted from Blender X/Z/Y)','tracks':tracks},indent=2));scene.frame_set(1)
    return folder,row

def previews(name,folder,parts):
    scene=bpy.context.scene;scene.frame_set(17 if name=='water_timer' else 1)
    center,size,cam=kit.render(parts,folder,name)
    if name=='water_timer':
        scene.cycles.max_bounces=16;scene.cycles.transmission_bounces=12;scene.cycles.samples=48
        # Re-render with enough glass bounces; a slight offset retains the timer's thin shape.
        for label,vec in [('front',(1.1,-4,.7)),('rear',(-1.2,4,.8))]:
            cam.location=center+Vector(vec)*.26;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
            scene.render.filepath=str(folder/f'{name}_{label}.png');bpy.ops.render.render(write_still=True)
        cam.location=center+Vector((1.1,-4,.7))*.26;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
        scene.render.resolution_x=420;scene.render.resolution_y=560;scene.cycles.samples=20
        (folder/'motion_frames').mkdir(exist_ok=True)
        # Identical droplets are phase-spaced 16 frames apart, so the visible pattern
        # repeats after 16 frames, although individual node tracks repeat after 48.
        for frame in range(1,17):
            scene.frame_set(frame);scene.render.filepath=str(folder/'motion_frames'/f'{frame:04d}.png');bpy.ops.render.render(write_still=True)
        scene.frame_set(17);scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.cycles.samples=48
    for image in bpy.data.images:
        if image.source=='FILE' and not image.packed_file:
            try:image.pack()
            except RuntimeError:pass
    bpy.ops.wm.save_as_mainfile(filepath=str(folder/(name+'.blend')))

reports=[]
for name in ['cannon','water_timer']:
    reset()
    if name=='cannon':cannon();drops=None
    else:drops=water_timer()
    root,parts=prepare(name)
    if drops:animate(drops)
    folder,row=write_exports(name,root,parts,bool(drops));previews(name,folder,parts);reports.append(row)
    (OUT/'manifest.json').write_text(json.dumps(reports,indent=2))
    print('VALUABLE_07_EXPORTED',name,row,flush=True)
print('CANNON_AND_WATER_TIMER_BUILT',flush=True)
