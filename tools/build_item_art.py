"""Blender 5.2: --background --factory-startup --python tools/build_item_art.py -- --root PATH.
Original geometry only. Exports an editable scene, PNG textures, and sampled mesh/animation JSON.
Axes: Blender X/right Y/forward Z/up -> Unity X/right Y/up Z/forward (reverse winding).
"""
import bpy, bmesh, math, json, sys, argparse, random
from pathlib import Path
from mathutils import Vector, Matrix

args=argparse.ArgumentParser(); args.add_argument('--root', type=Path, required=True)
ROOT=args.parse_args(sys.argv[sys.argv.index('--')+1:]).root.resolve()
OUT=ROOT/'src'/'Art'; OUT.mkdir(parents=True,exist_ok=True)
ART=ROOT/'art'; ART.mkdir(parents=True,exist_ok=True)
POLISH=ART/'shape-polish-15'; POLISH.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=24
scene.render.resolution_x=1600; scene.render.resolution_y=1000; scene.render.resolution_percentage=100
scene.world.color=(.18,.18,.18); scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'; scene.render.fps=30
PALETTE={'ghost':(.67,.82,.91),'silencer':(.48,.55,.59),'link_a':(.96,.48,.1),
         'link_b':(.16,.72,.85),'repair':(.3,.82,.38),'defence':(.2,.48,.93),
         'protection':(.71,.58,.27),'poison':(.36,.43,.22),'ice':(.29,.43,.49),
         'sticky':(.47,.30,.35),'decoy':(.62,.36,.17)}

def symbol(k,x,y):
    # Deliberately chunky, monochrome emission masks for the vanilla icon sockets.
    if k=='ghost':
        body=((x*x+(y-.12)**2<.42**2 and y>.1) or (abs(x)<.42 and -.38<y<.18))
        eyes=(abs(abs(x)-.17)<.065 and .05<y<.23)
        hem=(y<-.25 and abs(x)<.09)
        return body and not (eyes or hem)
    if k=='repair': return (abs(x)<.12 and abs(y)<.43) or (abs(y)<.12 and abs(x)<.43)
    if k in ('defence','protection'):
        width=.4 if y>-.05 else .4*(y+.48)/.43
        shell=-.48<y<.4 and abs(x)<width
        inner=-.32<y<.26 and abs(x)<max(0,width-.11)
        mark=(abs(x)<.055 and abs(y)<.19) or (abs(y)<.055 and abs(x)<.19)
        return (shell and not inner) or (k=='protection' and mark)
    if k=='silencer':
        speaker=(-.4<x<-.24 and abs(y)<.14) or (-.24<x<-.04 and abs(y)<x+.46)
        slash=abs(y+x-.1)<.055 and -.35<x<.4
        return speaker or slash
    if k.startswith('link'):
        a=abs(math.hypot(x+.17,y-.08)-.22)<.065
        b=abs(math.hypot(x-.17,y+.08)-.22)<.065
        ticks=(.3<y<.42 and (abs(x+.1)<.035 or (k=='link_b' and abs(x-.1)<.035)))
        return a or b or ticks
    if k=='poison': return abs(x)<(.4-abs(y)*.6) and -.4<y<.4 and not (abs(x)<.07 and y>.0)
    if k=='ice': return (abs(x)<.055 or abs(y-x*.58)<.055 or abs(y+x*.58)<.055) and x*x+y*y<.2
    if k=='sticky': return x*x+y*y<.13 or any((x-a)**2+(y-b)**2<.012 for a,b in [(.33,.24),(-.32,.15),(.1,-.37)])
    return abs(math.hypot(x+.15,y)-.23)<.055 or abs(math.hypot(x+.15,y)-.4)<.05

def texture(key,icon=False):
    n=256; img=bpy.data.images.new(key+('_icon' if icon else '_skin'),width=n,height=n,alpha=True)
    col=PALETTE[key]; pixels=[]; rng=random.Random(92)
    for v in range(n):
        for u in range(n):
            x=(u+.5)/n*2-1; y=(v+.5)/n*2-1
            if icon:
                c=(1,1,1,1) if symbol(key,x,y) else (0,0,0,1)
            else:
                noise=rng.uniform(-.018,.018)
                c=tuple(max(0,min(1,a*.72+noise)) for a in col)
                if u<8 or u>247 or v<8 or v>247: c=(.075,.085,.08)
                if 16<v<32: c=(.12,.14,.13) if (u+v)//16%2 else tuple(a*.85 for a in col)
                if 222<v<229 and 24<u<232: c=(.65,.65,.53)
                if symbol(key,x*1.25,y*1.25): c=(.86,.88,.74)
                if (u*71+v*139)%1543<4: c=(.23,.22,.17)
                if any((u-a)**2+(v-b)**2<9 for a in (16,239) for b in (45,239)): c=(.055,.065,.06)
                c=(*c,1)
            pixels.extend(c)
    img.pixels.foreach_set(pixels); img.filepath_raw=str(OUT/(img.name+'.png')); img.file_format='PNG'; img.save(); img.pack()
    return img

textures={k:texture(k) for k in PALETTE}
for k in ('ghost','silencer','link_a','link_b','repair','defence','protection'): texture(k,True)

# Low contrast tiling coatings preserve the vanilla UVs and animated emission masks.
# Function emblems are separate, so they are never fragmented by the donor atlas.
for key in ('ghost','silencer','link_a','link_b','repair','defence','protection'):
    img=bpy.data.images.new(key+'_coat',width=128,height=128,alpha=True); pixels=[]; rng=random.Random(104)
    for v in range(128):
        for u in range(128):
            shade=.56+rng.uniform(-.045,.045)
            if u%32<2 or v%64<2: shade*=.64
            if (u*31+v*53)%787<3: shade= .82
            if 8<v%64<13 and (u+v)//8%2: shade*=.70
            pixels.extend((*[c*shade for c in PALETTE[key]],1))
    img.pixels.foreach_set(pixels); img.filepath_raw=str(OUT/(key+'_coat.png')); img.file_format='PNG'; img.save(); img.pack()

# Shared neutral wear atlas: all metal/paint surfaces, not just the labels, use this in-game.
wear=bpy.data.images.new('workshop_wear',width=128,height=128,alpha=True); pixels=[]; rng=random.Random(318)
for v in range(128):
    for u in range(128):
        shade=.78+rng.uniform(-.06,.06)
        if (u*7+v*43)%179<4: shade=.38
        if u%32<2 or v%32<2: shade=1.0
        if (u+v//3)%37<2 and v%19<7: shade=.98
        pixels.extend((shade,shade*.98,shade*.92,1))
wear.pixels.foreach_set(pixels); wear.filepath_raw=str(OUT/'workshop_wear.png'); wear.file_format='PNG'; wear.save(); wear.pack()

# Effect textures used by the existing game's particle and surface shaders.
for kind in ('poison_puff','ice_frost'):
    image=bpy.data.images.new(kind,width=128,height=128,alpha=True); pixels=[]
    for v in range(128):
        for u in range(128):
            x=(u+.5)/64-1; y=(v+.5)/64-1
            noise=(math.sin(u*.31+math.sin(v*.2)*2)+math.sin(v*.27+u*.09))*.08
            if kind=='poison_puff':
                alpha=max(0,min(1,(1-math.hypot(x,y)+noise)*1.7))**2
                pixels.extend((.53,.66,.29,alpha*.60))
            else:
                crack=abs(math.sin(u*.105+math.sin(v*.08)*1.3))<.09 or abs(math.sin(v*.137-u*.07))<.055
                c=.95 if crack else .43+noise
                pixels.extend((c*.60,c*.84,c,1))
    image.pixels.foreach_set(pixels); image.filepath_raw=str(OUT/(kind+'.png')); image.file_format='PNG'; image.save(); image.pack()

def material(name,color,metal=0,texture_image=None):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*(c**2.2 for c in color),1)
    p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=.7
    if texture_image:
        t=m.node_tree.nodes.new('ShaderNodeTexImage'); t.image=texture_image; t.interpolation='Closest'
        m.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color'])
    elif name!='Backdrop':
        t=m.node_tree.nodes.new('ShaderNodeTexImage'); t.image=wear; t.interpolation='Closest'
        mix=m.node_tree.nodes.new('ShaderNodeMixRGB'); mix.blend_type='MULTIPLY'; mix.inputs[0].default_value=1
        mix.inputs[1].default_value=(*(c**2.2 for c in color),1)
        m.node_tree.links.new(t.outputs['Color'],mix.inputs[2]); m.node_tree.links.new(mix.outputs[0],p.inputs['Base Color'])
    return m
dark=material('Graphite rubber',(.055,.068,.066)); steel=material('Worn steel',(.28,.32,.3),.55)
bone=material('Bone labels',(.75,.77,.63)); black=material('Inset void',(.012,.02,.018))
bow_paint=material('Worn ochre bow limbs',(.67,.59,.24),.2)
mats={k:material(k,PALETTE[k],.15) for k in PALETTE}
skinmats={k:material(k+'_label',(1,1,1),0,textures[k]) for k in PALETTE}
parts=[]

def mesh_object(name,verts,faces,mat):
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update()
    bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
    o=bpy.data.objects.new(name,mesh); scene.collection.objects.link(o)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.smart_project(island_margin=.025); bpy.ops.object.mode_set(mode='OBJECT')
    return finish(o,name,mat)

def interpolated(rows,steps=4):
    result=[]
    for i in range(len(rows)-1):
        a,b,c,d=rows[max(0,i-1)],rows[i],rows[i+1],rows[min(len(rows)-1,i+2)]
        for j in range(steps):
            t=j/steps
            result.append(tuple(max(min(b[k],c[k]),min(max(b[k],c[k]),.5*(2*b[k]+(-a[k]+c[k])*t+(2*a[k]-5*b[k]+4*c[k]-d[k])*t*t+(-a[k]+3*b[k]-3*c[k]+d[k])*t*t*t))) for k in range(len(b))))
    return result+[rows[-1]]

def shell(name,rings,mat,sides=24):
    """Tapered elliptical sections along Y: (y, half-width, half-height, center-z)."""
    rings=interpolated(rings);sides=max(sides,24)
    verts=[(w*math.cos(j*2*math.pi/sides),y,z+h*math.sin(j*2*math.pi/sides)) for y,w,h,z in rings for j in range(sides)]
    faces=[]
    for i in range(len(rings)-1):
        for j in range(sides):
            a=i*sides+j; d=i*sides+(j+1)%sides; faces.append((a,a+sides,d+sides,d))
    faces.extend([tuple(range(sides)),tuple(range((len(rings)-1)*sides,len(rings)*sides))])
    return mesh_object(name,verts,faces,mat)

def limb(name,side,mat,inset=False):
    path=[(.035,.245),(.10,.22),(.18,.165),(.25,.085),(.305,-.015),(.333,-.11),(.33,-.175),(.353,-.21)]
    path=interpolated(path,4);verts=[]
    section=[(-.8,-1),(.8,-1),(1,-.65),(1,.65),(.8,1),(-.8,1),(-1,.65),(-1,-.65)]
    for i,(x,y) in enumerate(path):
        before=Vector(path[max(0,i-1)]); after=Vector(path[min(len(path)-1,i+1)])
        tangent=(after-before).normalized(); normal=Vector((-tangent.y,tangent.x))
        width=(.035-i/(len(path)-1)*.0105)*(.50 if inset else 1)
        for across,up in section:
            v=Vector((x,y))+normal*width*across
            verts.append((side*v.x,v.y,.10+(.012 if inset else 0)+up*(.003 if inset else .013)))
    faces=[tuple(range(8))]
    for i in range(len(path)-1):
        for j in range(8): faces.append((i*8+j,i*8+(j+1)%8,(i+1)*8+(j+1)%8,(i+1)*8+j))
    faces.append(tuple(range(len(verts)-8,len(verts))))
    return mesh_object(name,verts,faces,mat)

def draw_cable(side):
    a=Vector((side*.353,-.21,.10)); b=Vector((0,-.27,.145))
    o=bar('Draw cable',a,b,.0045,dark); length=(b-a).length
    for frame,y in ((1,-.27),(3,.18),(9,.18),(25,-.27),(30,-.27)):
        b=Vector((0,y,.145)); o.location=(a+b)/2
        o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler(); o.scale.z=(b-a).length/length
        for channel in ('location','rotation_euler','scale'): o.keyframe_insert(data_path=channel,frame=frame)
    scene.frame_set(1)

def finish(o,name,mat):
    o.name=name; o.data.materials.append(mat); parts.append(o); return o
def box(name,loc,size,mat,bevel=.004):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc); o=bpy.context.object; o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Rounded manufactured edges','BEVEL'); mod.width=bevel; mod.segments=4
        bpy.ops.object.modifier_apply(modifier=mod.name)
    # Small reproducible irregularities in casing corners; functional anchors stay unchanged.
    if name in ('Receiver','Cooling block','Pressure body','Speaker housing'):
        for vertex in o.data.vertices:
            vertex.co.x += .003*math.sin(vertex.co.y*41+vertex.co.z*29)
            vertex.co.z += .002*math.sin(vertex.co.x*53)
    return finish(o,name,mat)
def cylinder(name,loc,r,depth,mat,axis='Z',verts=10):
    bpy.ops.mesh.primitive_cylinder_add(vertices=max(24 if r>.025 else 12,verts),radius=r,depth=depth,location=loc)
    o=bpy.context.object
    if axis=='Y': o.rotation_euler.x=math.pi/2
    if axis=='X': o.rotation_euler.y=math.pi/2
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    mod=o.modifiers.new('Turned rim radius','BEVEL');mod.width=min(r*.12,depth*.20,.004);mod.segments=3
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(o,name,mat)
def bar(name,a,b,r,mat):
    mid=(Vector(a)+Vector(b))/2; o=cylinder(name,mid,r,(Vector(b)-Vector(a)).length,mat,verts=6)
    o.rotation_euler=(Vector(b)-Vector(a)).to_track_quat('Z','Y').to_euler(); return o
def label(key,loc,size):
    # A single UV mapped plaque, vertical and facing the presentation camera.
    bpy.ops.mesh.primitive_plane_add(size=1,location=loc); o=bpy.context.object
    o.rotation_euler=(math.pi/2,0,math.pi/2); o.scale=(size,size,1)
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    return finish(o,'Specification plate',skinmats[key])
def keys(o, frames):
    base=o.location.copy()
    for frame,offset,scale in frames:
        o.location=base+Vector(offset); o.scale=scale
        o.keyframe_insert(data_path='location',frame=frame); o.keyframe_insert(data_path='scale',frame=frame)
    scene.frame_set(1)
def kick(o,amount=.05): keys(o,[(1,(0,0,0),(1,1,1)),(3,(0,-amount,0),(1,1,1)),(9,(0,-amount*.6,0),(1,1,1)),(24,(0,0,0),(1,1,1))])

def grip():
    g=box('Angled grip',(0,-.12,-.08),(.085,.11,.23),dark,.025); g.rotation_euler.x=-.28
    for z in (-.16,-.11,-.06):
        tread=box('Grip tread',(0,-.174+.28*(z+.08),z),(.088,.014,.014),steel,.003)
        tread.rotation_euler.x=-.28
    curve=bpy.data.curves.new('Continuous curved trigger guard','CURVE');curve.dimensions='3D';curve.resolution_u=5;curve.bevel_depth=.011;curve.bevel_resolution=2
    spline=curve.splines.new('BEZIER');spline.bezier_points.add(4)
    for p,co in zip(spline.bezier_points,[(0,-.075,-.085),(0,-.050,-.125),(0,.037,-.130),(0,.079,-.080),(0,.065,-.025)]):
        p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
    guard=bpy.data.objects.new('Curved trigger guard',curve);scene.collection.objects.link(guard)
    bpy.ops.object.select_all(action='DESELECT');guard.select_set(True);bpy.context.view_layer.objects.active=guard;bpy.ops.object.convert(target='MESH');finish(guard,guard.name,steel)
    box('Trigger',(0,.007,-.028),(.022,.025,.07),bone,.003)

def crossbow():
    grip(); shell('Tapered rail',[(-.30,.036,.025,.07),(-.22,.053,.035,.07),(.20,.040,.024,.09),(.29,.028,.020,.10)],steel,8)
    shell('Receiver',[(-.29,.038,.035,.075),(-.24,.075,.050,.075),(-.10,.072,.048,.075),(-.02,.047,.032,.08)],mats['poison'])
    box('Rail groove',(0,.05,.121),(.034,.4,.012),black,.002)
    for side in (-1,1):
        limb('Swept recurve limb',side,bow_paint)
        limb('Limb spring inset',side,steel,True)
        draw_cable(side)
    bolt=bar('Loaded poison bolt',(0,-.21,.145),(0,.23,.145),.012,bone)
    keys(bolt,[(1,(0,0,0),(1,1,1)),(2,(0,.25,0),(1,1,1)),(3,(0,.5,0),(.001,.001,.001)),(18,(0,-.08,0),(.001,.001,.001)),(20,(0,-.08,0),(1,1,1)),(30,(0,0,0),(1,1,1))])
    cylinder('Poison cartridge',(.13,-.11,.09),.046,.2,mats['poison'],'Y')
    for y in (-.2,-.02): cylinder('Cartridge collar',(.13,y,.09),.052,.025,steel,'Y')
    label('poison',(.076,-.17,.075),.075)
    for y in (-.22,-.07): cylinder('Receiver rivet',(.073,y,.075),.009,.012,steel,'X',6)
    return (0,.30,.145),30

def ice():
    grip(); shell('Cooling block',[(-.23,.043,.045,.08),(-.18,.088,.080,.08),(-.06,.105,.085,.08),(.075,.080,.068,.08),(.12,.068,.055,.08)],mats['ice'])
    cylinder('Pulse barrel',(0,.19,.08),.084,.24,steel,'Y',12)
    for y in (.1,.15,.2,.25): cylinder('Cooling fin',(0,y,.08),.092,.018,steel,'Y',8)
    cylinder('Muzzle recess',(0,.315,.08),.071,.012,black,'Y',12)
    lens=cylinder('Cold emitter',(0,.324,.08),.044,.012,bone,'Y',8); kick(lens,.02)
    slide=shell('Reciprocating top',[(-.18,.028,.014,.16),(-.12,.048,.024,.17),(.02,.048,.024,.17),(.06,.025,.012,.16)],steel,8); kick(slide,.08)
    for x in (-.115,.115):
        for y in (-.13,-.07,-.01): box('Side vent',(x*.88,y,.08),(.008,.014,.045),dark,.002)
    label('ice',(.106,-.065,.08),.085)
    return (0,.335,.08),30

def sticky():
    grip(); shell('Pressure body',[(-.23,.042,.045,.065),(-.18,.083,.074,.065),(-.02,.096,.077,.065),(.10,.064,.049,.08)],mats['sticky'])
    cylinder('Adhesive drum',(0,-.08,.23),.084,.21,mats['sticky'],'Y',10)
    for y in (-.19,.03): cylinder('Drum cap',(0,y,.23),.09,.028,steel,'Y',10)
    piston=cylinder('Pressure piston',(0,.11,.09),.055,.21,steel,'Y'); kick(piston,.055)
    cylinder('Nozzle',(0,.24,.09),.093,.07,dark,'Y',10)
    cylinder('Nozzle face',(0,.281,.09),.077,.012,mats['sticky'],'Y',10)
    cylinder('Nozzle bore',(0,.29,.09),.032,.013,black,'Y')
    bar('Feed pipe',(.11,-.06,.22),(.11,.13,.1),.018,steel)
    for y,x in ((-.14,.087),(-.08,.092),(-.02,.097)): box('Gauge tick',(x,y,.06),(.009,.02,.035),bone,.001)
    label('sticky',(.096,-.07,.075),.075)
    box('Reservoir strap',(0,-.09,.316),(.14,.035,.014),dark,.001)
    box('Pressure warning',(0,.02,.153),(.12,.025,.012),bone,.001)
    return (0,.30,.09),30

def decoy():
    casing=shell('Rounded beacon casing',[(-.13,.055,.055,0),(-.10,.093,.093,0),(-.06,.106,.106,0),(.06,.106,.106,0),(.11,.084,.084,0),(.13,.05,.05,0)],mats['decoy'],16)
    casing.rotation_euler.x=math.pi/2
    for z in (-.12,.12): cylinder('Rubber bumper',(0,0,z),.122,.035,dark,'Z',10)
    box('Speaker housing',(0,.085,0),(.15,.065,.16),steel,.026)
    for z in (-.055,-.025,.005,.035,.065): box('Speaker grille',(0,.123,z),(.128,.012,.012),black,.001)
    cylinder('Beacon lamp',(0,0,.16),.046,.05,bone,'Z',8)
    mast=cylinder('Telescoping antenna',(.065,0,.195),.012,.13,steel)
    keys(mast,[(1,(0,0,0),(1,1,1)),(8,(0,0,.05),(1,1,1.6)),(30,(0,0,.05),(1,1,1.6))])
    cap=cylinder('Antenna cap',(.065,0,.27),.022,.022,dark)
    keys(cap,[(1,(0,0,0),(1,1,1)),(8,(0,0,.09),(1,1,1)),(30,(0,0,.09),(1,1,1))])
    label('decoy',(.111,0,0),.13)
    return (0,0,0),30

C=Matrix(((1,0,0),(0,0,1),(0,1,0)))
def vec(v): return {'x':round(v[0],6),'y':round(v[1],6),'z':round(v[2],6)}
def pose(o,origin):
    p,q,s=o.matrix_local.decompose(); q=(C@q.to_matrix()@C).to_quaternion()
    return {'position':vec(C@(p-Vector(origin))),'rotation':{'x':q.x,'y':q.y,'z':q.z,'w':q.w},'scale':vec(C@s)}

models={}; offset={'poison':(-.6,.35,.38),'ice':(.45,.4,.38),'sticky':(-.55,-.5,.38),'decoy':(.5,-.45,.25)}
for key,builder in [('poison',crossbow),('ice',ice),('sticky',sticky),('decoy',decoy)]:
    parts=[]; origin,last=builder(); scene.frame_set(1)
    data={'version':1,'fps':30,'frames':last,'parts':[]}
    for o in parts:
        bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
        if o.name!='Specification plate' and not o.name.startswith('Specification plate.'):
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(50),keep_sharp_edges=False)
            normal=o.modifiers.new('Weighted manufacturing normals','WEIGHTED_NORMAL');normal.keep_sharp=True;normal.weight=30
            bpy.ops.object.modifier_apply(modifier=normal.name)
        if not o.data.uv_layers:
            bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.02);bpy.ops.object.mode_set(mode='OBJECT')
        mesh=o.data; mesh.calc_loop_triangles(); vertices=[]; normals=[]; uv=[]; indices=[]
        for tri in mesh.loop_triangles:
            for li in reversed(tri.loops):
                loop=mesh.loops[li]; vertices.append(vec(C@mesh.vertices[loop.vertex_index].co))
                normals.append(vec(C@mesh.corner_normals[li].vector)); p=mesh.uv_layers.active.data[li].uv if mesh.uv_layers.active else (0,0)
                uv.append({'x':float(p[0]),'y':float(p[1])}); indices.append(len(indices))
        mat=mesh.materials[0]; texture_name=key+'_skin.png' if mat==skinmats[key] else 'workshop_wear.png'
        col=mat.diffuse_color
        record={'name':o.name,'vertices':vertices,'normals':normals,'uv':uv,'triangles':indices,'color':{'r':col[0],'g':col[1],'b':col[2],'a':1},'texture':texture_name,'rest':pose(o,origin),'samples':[]}
        if o.animation_data:
            for frame in range(1,last+1): scene.frame_set(frame); record['samples'].append(pose(o,origin))
        data['parts'].append(record); scene.frame_set(1)
    (OUT/(key+'.mesh.json')).write_text(json.dumps(data,separators=(',',':')),encoding='utf8')
    models[key]=list(parts)
    # Portable meshes use the exact same parts and animation, with the muzzle at zero.
    folder=POLISH/key;folder.mkdir(exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    export_root=bpy.data.objects.new(key+' muzzle origin',None);scene.collection.objects.link(export_root);export_root.location=-Vector(origin)
    for o in parts:o.parent=export_root;o.select_set(True)
    export_root.select_set(True)
    scene.frame_start=1;scene.frame_end=last
    bpy.ops.export_scene.gltf(filepath=str(folder/(key+'.glb')),use_selection=True,export_format='GLB',export_animations=True,export_force_sampling=True)
    bpy.ops.wm.obj_export(filepath=str(folder/(key+'.obj')),export_selected_objects=True,path_mode='COPY',forward_axis='NEGATIVE_Z',up_axis='Y')
    # OBJ needs explicit companions because packed Blender image paths may be relative.
    import shutil
    for image in [wear,textures[key]]:shutil.copyfile(OUT/(image.name+'.png'),folder/(image.name+'.png'))
    mtl=folder/(key+'.mtl');mtl.write_text('\n'.join('map_Kd '+Path(line.split()[-1].replace('\\','/')).name if line.startswith('map_Kd ') else line for line in mtl.read_text().splitlines())+'\n')
    for o in parts:o.parent=None
    bpy.data.objects.remove(export_root,do_unlink=True)
    # Presentation offsets on parents are excluded from the Unity export above.
    parent=bpy.data.objects.new(key.upper()+' / animation 1-30',None); scene.collection.objects.link(parent); parent.location=offset[key]
    for o in parts: o.parent=parent

# Texture contact sheet in the Blender scene; these are swatches, not replacement drone geometry.
for i,k in enumerate(('ghost','silencer','link_a','link_b','repair','defence','protection')):
    bpy.ops.mesh.primitive_plane_add(size=.24,location=(-.90+i*.30,.97,.02))
    bpy.context.object.name=k+' skin swatch'; bpy.context.object.data.materials.append(skinmats[k])

floor=material('Backdrop',(.024,.035,.039)); box('Presentation floor',(0,0,-.13),(3.4,3,.12),floor,.025)
def area(name,loc,power,size):
    bpy.ops.object.light_add(type='AREA',location=loc); o=bpy.context.object; o.name=name; o.data.energy=power; o.data.shape='DISK'; o.data.size=size; o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
area('Large softbox',(1,-2,4),420,4); area('Cool fill',(-3,0,2),270,3); area('Edge',(0,3,3),420,2)
bpy.ops.object.camera_add(location=(2.4,3.4,3.6)); camera=bpy.context.object
camera.rotation_euler=(Vector((0,.16,.2))-camera.location).to_track_quat('-Z','Y').to_euler(); camera.data.type='ORTHO'; camera.data.ortho_scale=3.65; scene.camera=camera
scene.frame_end=30; scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'ihm_items.blend'))
shutil.copyfile(ART/'ihm_items.blend',POLISH/'weapons.blend')
scene.render.filepath=str(ART/'item_lineup.png'); bpy.ops.render.render(write_still=True)

# Square transparent inventory renders, with the same geometry as the runtime export.
scene.render.resolution_x=256; scene.render.resolution_y=256; scene.render.film_transparent=True
meshes=[o for o in scene.objects if o.type=='MESH']
for key,objects in models.items():
    for obj in meshes: obj.hide_render=obj not in objects
    center=Vector(offset[key])+Vector((0,0,.03))
    camera.location=center+Vector((1,1.5,1)); camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale=.94 if key!='decoy' else .65
    scene.render.filepath=str(OUT/(key+'_inventory.png')); bpy.ops.render.render(write_still=True)
    scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.film_transparent=False
    scene.world.color=(.20,.22,.24)
    scene.render.filepath=str(POLISH/key/(key+'_preview.png'));bpy.ops.render.render(write_still=True)
    scene.render.resolution_x=256;scene.render.resolution_y=256;scene.render.film_transparent=True
print('IHM_ART_OK',sum(len(json.loads((OUT/(k+'.mesh.json')).read_text())['parts']) for k in models),'parts')
