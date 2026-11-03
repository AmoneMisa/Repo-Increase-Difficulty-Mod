"""Depth/material revision of the valuable collection, plus interactive-part exports.
Original art only; native materials/font files are never extracted into this pack.
"""
import bpy,bmesh,math,json,sys,shutil
from pathlib import Path
from mathutils import Vector,Matrix
from math import pi,sin,cos
sys.path.insert(0,str(Path(__file__).resolve().parent))
import valuable_meshkit as kit
from valuable_meshkit import box,cyl,ico,mesh,torus,lathe,bar,tube
from merge_gltf_loop import merge_loop
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/valuables/depth-rework-08';OUT.mkdir(parents=True,exist_ok=True)
kit.OUT=OUT
SOURCES={n:ROOT/f'art/valuables/medical-tech-pack-06/{n}/{n}.blend' for n in ['bomb','centrifuge','spritz','skeleton','graphics_card','ram_pair','robot_vacuum','bath_gift_set']}
SOURCES['cannon']=ROOT/'art/valuables/cannon-water-timer-07/cannon/cannon.blend'
SOURCES['crystal_skull']=ROOT/'art/references/valuable-skull-approved.blend'
SOURCES['monoblock_pc']=ROOT/'art/references/valuable-monoblock-source.blend'
PALETTE={'olive':(.105,.12,.045),'green':(.045,.105,.064),'yellow':(.65,.38,.022),'gold':(.34,.22,.057),'dark':(.018,.025,.028),'steel':(.18,.235,.24),'bone':(.53,.46,.30),'white':(.40,.46,.41),'pink':(.37,.08,.19),'purple':(.14,.075,.25),'cyan':(.045,.29,.30),'red':(.36,.025,.035),'orange':(.45,.16,.033)}

def solid(name,c,rough=.45,metal=0,trans=0,emit=0):
    m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*c,1)
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*c,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
    p.inputs['Transmission Weight'].default_value=trans;p.inputs['IOR'].default_value=1.46
    p.inputs['Emission Color'].default_value=(*c,1);p.inputs['Emission Strength'].default_value=emit
    return m

def load(name):
    with bpy.data.libraries.load(str(SOURCES[name]),link=False) as (src,dst):dst.objects=[n for n in src.objects if not n.startswith('Preview_')]
    def source_world(ob):
        return source_world(ob.parent) @ ob.matrix_parent_inverse @ ob.matrix_basis if ob.parent else ob.matrix_basis.copy()
    worlds={ob.name:source_world(ob) for ob in dst.objects if ob and ob.type=='MESH'}
    for ob in dst.objects:
        if not ob or ob.type!='MESH':continue
        bpy.context.collection.objects.link(ob);world=worlds[ob.name];ob.parent=None;ob.matrix_world=world
        if ob.animation_data:ob.animation_data_clear()
    # Remove any unrelated source roots; imported meshes are now self-contained.
    for ob in list(bpy.data.objects):
        if ob.type!='MESH':
            bpy.data.objects.remove(ob,do_unlink=True)

def loose_cannon_wheels():
    originals=[o for o in bpy.context.scene.objects if o.type=='MESH']
    for ob in originals:
        bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.separate(type='LOOSE');bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.update()
    for ob in bpy.context.scene.objects:
        if ob.type!='MESH':continue
        ps=[ob.matrix_world@v.co for v in ob.data.vertices];x=sum(p.x for p in ps)/len(ps)
        ob['group']='wheel_right' if x>.50 else 'wheel_left' if x<-.50 else 'static'

def detail(name):
    steel=solid('Detail dark steel',(.07,.095,.10),.35,.55);dark=solid('Detail recess',(.008,.014,.016),.8)
    light=solid('Detail worn lip',(.27,.30,.26),.5,.25)
    if name=='bomb':
        for x,r in [(-.80,.738),(.51,.77)]:
            ob=torus('Raised casing strap',(x,0,.84),r,.009,steel,48,(0,pi/2,0))
        for y in [-.34,.34]:
            o=box('Riveted repair plate',(-.12,y,.84+.77),(.20,.12,.013),steel,.006)
            for x in [-.19,-.05]:cyl('Repair rivet',(x,y,.84+.782),.009,.012,light,8)
    elif name=='centrifuge':
        for x in [-.12,.12]:
            box('Front casing seam',(x,-.22,.105),(.004,.012,.14),dark,.001)
        for i in range(7):box('Rear cooling recess',(-.066+i*.022,.226,.095),(.011,.008,.05),dark,.002)
        torus('Metal opening lip',(0,0,.207),.194,.004,steel,36)
    elif name=='graphics_card':
        for x in [-.106,0,.106]:
            torus('Milled fan socket',(x,0,.077),.045,.0025,steel,32)
            for a in [0,pi/2,pi,3*pi/2]:
                cyl('Fan mounting screw',(x+.048*cos(a),.048*sin(a),.073),.0025,.003,light,8)
        for i in range(8):box('Backplate vent',(-.12+i*.032,.073,.032),(.013,.004,.018),dark,.001)
    elif name=='ram_pair':
        for y in [-.022,.022]:
            for x in [-.045,.045]:box('Heat spreader inset',(x,y-.009,.028),(.019,.001,.016),steel,.002)
    elif name=='robot_vacuum':
        torus('Bumper lower rim',(0,0,.038),.174,.002,steel,48)
        for i in range(8):
            a=.3+i*.10;ob=box('Exhaust grille',(.17*cos(a),.17*sin(a),.049),(.003,.012,.014),dark,.001);ob.rotation_euler[2]=a
    elif name=='bath_gift_set':
        for x in [-.129,.129]:
            for z in [.025,.16]:cyl('Cardboard corner staple',(x,-.053,z),.002,.003,steel,6,'Y')
    elif name=='monoblock_pc':
        torus('Stand hinge',(0,.052,.185),.025,.006,steel,24,(pi/2,0,0))
        for i in range(7):box('Speaker opening',(-.09+i*.03,-.031,.177),(.018,.003,.002),dark)

def timer():
    glass=solid('Timer clear acrylic',(.75,.91,.95),.085,trans=1)
    rim=solid('Timer softly luminous rim',(.045,.28,.30),.24,trans=.4,emit=.35)
    blue=solid('Timer cobalt liquid',(.006,.025,.40),.16,trans=.12,emit=3.2)
    pink=solid('Timer rose liquid',(.42,.008,.11),.18,trans=.08,emit=3.2)
    outline=[(-.050,0),(.050,0),(.052,.012),(.047,.05),(.041,.108),(.042,.163),(.050,.226),(.052,.252),(.049,.260),(-.049,.260),(-.052,.252),(-.050,.226),(-.042,.163),(-.041,.108),(-.047,.05),(-.052,.012)]
    def slab(label,outline,depth,m):
        n=len(outline);vs=[(x,y,z) for y in [-depth/2,depth/2] for x,z in outline];fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
        return mesh(label,vs,fs,m)
    body=slab('Timer clear shaped casing',outline,.038,glass);body['group']='case'
    for z in [.006,.254]:
        ob=box('Timer light-catching end seal',(0,0,z),(.099,.038,.010),rim,.002);ob['group']='case'
        for x in [-.038,.038]:
            ob=ico('Timer cap fastening',(x,-.019,z),(.002,.001,.002),rim,1);ob['group']='case'
    # The orientation arrow moves with the case during a player-triggered flip.
    ob=tube('Timer orientation chevron',[(-.008,-.0195,.249),(0,-.0195,.245),(.008,-.0195,.249)],.0008,rim,5);ob['group']='case'
    for top,m in [(True,blue),(False,pink)]:
        pts=[(-.042,.239),(.042,.239),(.042,.215),(.033,.205),(.006,.194),(.002,.181),(-.002,.181),(-.006,.194),(-.033,.205),(-.042,.215)]
        if not top:pts=[(x,.26-z) for x,z in reversed(pts)]
        ob=slab('Timer_TopPool' if top else 'Timer_BottomPool',pts,.025,m);ob['group']='top_pool' if top else 'bottom_pool'
        bevel=ob.modifiers.new('Rounded liquid shoulder','BEVEL');bevel.width=.0018;bevel.segments=3;bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=bevel.name)
    drops=[]
    for i in range(5):
        ob=lathe('Timer_Drop_%02d'%i,[(-.008,.001),(-.006,.005),(-.002,.008),(.003,.0085),(.008,.006),(.013,.001)],blue.copy(),20)
        ob.location=((-1 if i%2 else 1)*.006,-.001,.15);ob['group']='drop';ob['phase']=i/5;drops.append(ob)
    for ob in bpy.context.scene.objects:
        if ob.type=='MESH' and not ob.get('group'):ob['group']='static'
    return drops

def painted_materials(parts,size):
    """Bake art-directed normal shading and local AO, not repeated checker/noise tiles."""
    touched=set()
    for ob in parts:
        for m in ob.data.materials:
            if not m or m.name in touched or not m.use_nodes:continue
            touched.add(m.name);p=m.node_tree.nodes.get('Principled BSDF')
            if not p:continue
            if p.inputs['Transmission Weight'].default_value>.15 or p.inputs['Emission Strength'].default_value>.01:continue
            key=m.name.split('|')[-1].strip().split('.')[0]
            keep=any(n.type=='TEX_IMAGE' and n.image and ('fairy' in n.image.name or 'winx' in n.image.name.lower()) for n in m.node_tree.nodes)
            old=p.inputs['Base Color'];old_link=old.links[0].from_socket if old.is_linked and keep else None
            if not keep:
                for link in list(old.links):m.node_tree.links.remove(link)
            base=PALETTE.get(key,tuple(old.default_value[:3]))
            ao=m.node_tree.nodes.new('ShaderNodeAmbientOcclusion');ao.inputs['Distance'].default_value=max(.005,size*.045);ao.samples=16
            mix=m.node_tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.52;mix.inputs[1].default_value=(*base,1)
            if old_link:m.node_tree.links.new(old_link,mix.inputs[1])
            m.node_tree.links.new(ao.outputs['Color'],mix.inputs[2])
            geo=m.node_tree.nodes.new('ShaderNodeNewGeometry');sep=m.node_tree.nodes.new('ShaderNodeSeparateXYZ');ramp=m.node_tree.nodes.new('ShaderNodeMapRange')
            m.node_tree.links.new(geo.outputs['Normal'],sep.inputs[0]);m.node_tree.links.new(sep.outputs['Z'],ramp.inputs['Value'])
            ramp.inputs['From Min'].default_value=-1;ramp.inputs['From Max'].default_value=1;ramp.inputs['To Min'].default_value=.62;ramp.inputs['To Max'].default_value=1.18
            shade=m.node_tree.nodes.new('ShaderNodeMixRGB');shade.blend_type='MULTIPLY';shade.inputs[0].default_value=.60
            m.node_tree.links.new(mix.outputs[0],shade.inputs[1]);m.node_tree.links.new(ramp.outputs['Result'],shade.inputs[2]);m.node_tree.links.new(shade.outputs[0],old)
            p.inputs['Roughness'].default_value=.55 if key in ['steel','gold'] else .74
            p.inputs['Metallic'].default_value=.35 if key in ['steel','gold'] else .035

def bake_atlas(name,parts,size,folder):
    opaque=[]
    for ob in parts:
        if all(m.node_tree.nodes.get('Principled BSDF') and m.node_tree.nodes.get('Principled BSDF').inputs['Transmission Weight'].default_value<=.15 and m.node_tree.nodes.get('Principled BSDF').inputs['Emission Strength'].default_value<=.01 for m in ob.data.materials):opaque.append(ob)
    if not opaque:return None
    bpy.ops.object.select_all(action='DESELECT')
    for ob in opaque:ob.select_set(True)
    for ob in opaque:
        source=ob.data.uv_layers.active
        if source:source.name='SourceUV'
        target=ob.data.uv_layers.new(name='AtlasUV');ob.data.uv_layers.active=target;target.active_render=True
        for m in ob.data.materials:
            uv=m.node_tree.nodes.new('ShaderNodeUVMap');uv.uv_map='SourceUV'
            for node in list(m.node_tree.nodes):
                if node.type=='TEX_IMAGE' and node.image:m.node_tree.links.new(uv.outputs['UV'],node.inputs['Vector'])
    bpy.context.view_layer.objects.active=opaque[0];bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.01);bpy.ops.uv.pack_islands(margin=.008);bpy.ops.object.mode_set(mode='OBJECT')
    atlas=bpy.data.images.new('v08_'+name+'_atlas',width=1024,height=1024,alpha=True)
    for ob in opaque:
        for m in ob.data.materials:
            node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=atlas;m.node_tree.nodes.active=node
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=8;scene.render.bake.margin=8
    bpy.ops.object.bake(type='DIFFUSE',pass_filter={'COLOR'},use_clear=True,margin=8)
    atlas.filepath_raw=str(folder/('v08_'+name+'_atlas.png'));atlas.file_format='PNG';atlas.save();atlas.pack()
    target=solid('Painted atlas '+name,(1,1,1),.62,.08);p=target.node_tree.nodes.get('Principled BSDF');tex=target.node_tree.nodes.new('ShaderNodeTexImage');tex.image=atlas;tex.interpolation='Linear';target.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color'])
    for ob in opaque:
        ob.data.materials.clear();ob.data.materials.append(target)
        for poly in ob.data.polygons:poly.material_index=0
        source=ob.data.uv_layers.get('SourceUV')
        if source:ob.data.uv_layers.remove(source)
    return atlas

def prepare_parts():
    parts=[o for o in bpy.context.scene.objects if o.type=='MESH'];bpy.ops.object.select_all(action='DESELECT');bpy.context.view_layer.update()
    for ob in parts:
        ob.select_set(True);bpy.context.view_layer.objects.active=ob;bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
        bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free()
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35),keep_sharp_edges=True)
        if not ob.data.uv_layers:
            bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.01);bpy.ops.object.mode_set(mode='OBJECT')
        if any(len(p.vertices)>3 for p in ob.data.polygons):
            mod=ob.modifiers.new('Export triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
        # Tight concave cuts can average a smooth corner normal behind its own face.
        for poly in ob.data.polygons:
            if any(ob.data.corner_normals[li].vector.dot(poly.normal)<=0 for li in poly.loop_indices):poly.use_smooth=False
        ob.data.update()
        ob.select_set(False)
    return parts

def merge_static(parts,name):
    groups={}
    for ob in parts:
        group=ob.get('group','static')
        if name=='spritz':continue
        if group in ['drop','top_pool','bottom_pool']:continue
        groups.setdefault((group,tuple(m.name for m in ob.data.materials)),[]).append(ob)
    removed=set();made=[]
    for (group,_),objects in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for ob in objects:ob.select_set(True);removed.add(id(ob))
        bpy.context.view_layer.objects.active=objects[0]
        if len(objects)>1:bpy.ops.object.join()
        ob=bpy.context.object;ob['group']=group;ob.name=name+'_'+group+'_'+str(len(made));made.append(ob)
    return [o for o in parts if id(o) not in removed]+made

def runtime_export(name,parts,folder):
    C=Matrix(((1,0,0),(0,0,1),(0,1,0)));origin=Vector((0,0,0))
    if name=='spritz':origin=Vector((0,.27,next(o for o in parts if o.name=='Spritz_Needle').location.z))
    vec=lambda v:{'x':float(v.x),'y':float(v.y),'z':float(v.z)}
    mats=[];indices={};records=[]
    for ob in parts:
        group=ob.get('group','static');points=[ob.matrix_world@v.co-origin for v in ob.data.vertices]
        center=(Vector(tuple(min(v[i] for v in points) for i in range(3)))+Vector(tuple(max(v[i] for v in points) for i in range(3))))/2
        if group in ['wheel_left','wheel_right']:center=Vector((-.64 if group=='wheel_left' else .64,-.1,.545))
        if name=='spritz':center=Vector((0,0,0))
        for slot,m in enumerate(ob.data.materials):
            if m.name not in indices:
                p=m.node_tree.nodes.get('Principled BSDF');base=p.inputs['Base Color'].default_value[:3]
                tex=next((n.image for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and n.outputs['Color'].is_linked),None)
                texture=Path(tex.filepath).name if tex else ''
                indices[m.name]=len(mats);mats.append({'name':m.name,'texture':texture,'color':{'r':float(base[0]),'g':float(base[1]),'b':float(base[2]),'a':1},'transmission':float(p.inputs['Transmission Weight'].default_value),'emission':float(p.inputs['Emission Strength'].default_value),'roughness':float(p.inputs['Roughness'].default_value)})
            verts=[];normals=[];uv=[];tri=[]
            for poly in ob.data.polygons:
                if poly.material_index!=slot:continue
                start=len(verts)
                for li in poly.loop_indices:
                    vi=ob.data.loops[li].vertex_index;verts.append(vec(C@(ob.matrix_world@ob.data.vertices[vi].co-origin-center)))
                    normals.append(vec((C@ob.data.corner_normals[li].vector).normalized()));u=ob.data.uv_layers.active.data[li].uv;uv.append({'x':float(u.x),'y':float(u.y)})
                tri.extend([start,start+2,start+1])
            if not verts:continue
            color={'r':1,'g':1,'b':1,'a':1} if mats[indices[m.name]]['texture'] else mats[indices[m.name]]['color']
            pose={'position':vec(C@center),'rotation':{'x':0,'y':0,'z':0,'w':1},'scale':{'x':1,'y':1,'z':1}}
            records.append({'name':ob.name,'group':group,'phase':float(ob.get('phase',0)),'material':indices[m.name],'vertices':verts,'normals':normals,'uv':uv,'triangles':tri,'color':color,'texture':mats[indices[m.name]]['texture'],'rest':pose,'samples':[]})
    data={'version':1,'fps':24,'frames':1,'materials':mats,'parts':records}
    if name=='spritz':
        import copy
        data['fps']=30;data['frames']=12
        for record in records:
            if record['name'] not in ['Thumb ring','Plunger shaft']:continue
            for frame in range(12):
                pose=copy.deepcopy(record['rest']);t=min(1,frame/6);pose['position']['z']+=.037*t*t*(3-2*t);record['samples'].append(pose)
    dest=folder/('v08_'+name+'.mesh.json');dest.write_text(json.dumps(data,separators=(',',':')))
    if name in ['cannon','water_timer','spritz']:
        key='spritz' if name=='spritz' else 'v08_'+name
        (ROOT/'src/Art'/(key+'.mesh.json')).write_text(json.dumps(data,separators=(',',':')))
        for m in mats:
            if m['texture'] and (folder/m['texture']).is_file():shutil.copyfile(folder/m['texture'],ROOT/'src/Art'/m['texture'])
    return data

def timer_keys(parts):
    scene=bpy.context.scene;scene.render.fps=24;scene.frame_start=1;scene.frame_end=433
    for ob in parts:
        group=ob.get('group')
        if group not in ['drop','top_pool','bottom_pool']:continue
        original=ob.location.copy();phase=float(ob.get('phase',0));p=ob.data.materials[0].node_tree.nodes.get('Principled BSDF')
        for frame in range(1,434):
            t=(frame-1)/24
            if group=='drop':
                f=(t/6+phase)%1;s=max(.001,min(f/.12,(1-f)/.13,1));ob.location.z=.179-.132*f;ob.scale=(s,s,s*(1.0+.12*sin(pi*f)))
                ob.keyframe_insert(data_path='location',frame=frame);ob.keyframe_insert(data_path='scale',frame=frame)
                mix=.5+.5*sin(2*pi*(t/18+phase));c=(.008+.32*mix,.03+.08*(1-mix),.40-.20*mix)
                p.inputs['Base Color'].default_value=(*c,1);p.inputs['Emission Color'].default_value=(*c,1)
                p.inputs['Base Color'].keyframe_insert(data_path='default_value',frame=frame);p.inputs['Emission Color'].keyframe_insert(data_path='default_value',frame=frame)
        ob.location=original
    scene.frame_set(61)

def present(name,parts,folder,samples=48):
    scene=bpy.context.scene;points=[o.matrix_world@v.co for o in parts for v in o.data.vertices]
    lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)));center=(hi+lo)/2;size=max(hi-lo)
    floor=solid('Preview neutral charcoal',(.95,.95,.95) if name=='maneki_neko' else (.028,.037,.039),.85);box('Preview_Ground',(0,0,-.014),(200,200,.02),floor)
    scene.render.engine='CYCLES';scene.cycles.samples=samples;scene.cycles.use_denoising=True;scene.cycles.max_bounces=16;scene.cycles.transmission_bounces=12
    scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    if name=='water_timer':
        tree=bpy.data.node_groups.new('Water watch soft glow','CompositorNodeTree');scene.compositing_node_group=tree
        scene.use_nodes=True;scene.render.use_compositing=True
        tree.interface.new_socket(name='Image',in_out='OUTPUT',socket_type='NodeSocketColor')
        layers=tree.nodes.new('CompositorNodeRLayers');glare=tree.nodes.new('CompositorNodeGlare');output=tree.nodes.new('NodeGroupOutput')
        glare.inputs['Type'].default_value='Fog Glow';glare.inputs['Quality'].default_value='High';glare.inputs['Threshold'].default_value=.55;glare.inputs['Strength'].default_value=.38;glare.inputs['Size'].default_value=.08
        tree.links.new(layers.outputs['Image'],glare.inputs['Image']);tree.links.new(glare.outputs['Image'],output.inputs['Image'])
    else:scene.compositing_node_group=None
    scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.8,.8,.8,1) if name=='maneki_neko' else (.10,.14,.16,1);scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.4
    for loc,power,scale,col in [((-2,-3,4),320,2,(1,.80,.58)),((3,-1,2),100,2,(.52,.75,1)),((0,3,4),380,2,(.66,.82,1))]:
        bpy.ops.object.light_add(type='AREA',location=center+Vector(loc)*max(size,.4));ob=bpy.context.object;ob.name='Preview_Light';ob.data.energy=power*max(size,.4)**2;ob.data.size=scale*max(size,.4);ob.data.color=col;ob.rotation_euler=(center-ob.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add();cam=bpy.context.object;cam.name='Preview_Camera';cam.data.type='ORTHO';cam.data.ortho_scale=size*(1.10 if name=='maneki_neko' else 1.35);cam.data.clip_end=1000;scene.camera=cam
    for label,v in [('front',(.15,-3,.27) if name=='maneki_neko' else (1.4,-3,1.1)),('rear',(-1.7,3,1.0))]:
        cam.location=center+Vector(v)*max(size,.4);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(folder/f'{name}_{label}.png');bpy.ops.render.render(write_still=True)
    if name=='spritz':
        scene.render.resolution_x=256;scene.render.resolution_y=256;scene.render.film_transparent=True
        bpy.data.objects['Preview_Ground'].hide_render=True;scene.render.filepath=str(ROOT/'src/Art/spritz_inventory.png');bpy.ops.render.render(write_still=True)
    return cam,center,size

def build(name):
    kit.reset();folder=OUT/name;folder.mkdir(exist_ok=True)
    if name=='water_timer':timer()
    else:load(name)
    if name=='cannon':loose_cannon_wheels()
    if name not in ['water_timer','crystal_skull']:detail(name)
    parts=prepare_parts();bpy.context.view_layer.update();points=[o.matrix_world@v.co for o in parts for v in o.data.vertices];size=max(max(p[i] for p in points)-min(p[i] for p in points) for i in range(3))
    if name not in ['water_timer','crystal_skull']:
        painted_materials(parts,size);bake_atlas(name,parts,size,folder)
    parts=merge_static(parts,name)
    bpy.ops.object.select_all(action='DESELECT');root=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(root)
    for ob in parts:ob.parent=root;ob.select_set(True)
    root.select_set(True);runtime=runtime_export(name,parts,folder)
    if name=='water_timer':timer_keys(parts)
    points=[o.matrix_world@v.co for o in parts for v in o.data.vertices];bounds=[max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)]
    bpy.ops.export_scene.gltf(filepath=str(folder/(name+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_animations=name=='water_timer',export_animation_mode='SCENE',export_force_sampling=True,export_pointer_animation=True)
    bpy.ops.wm.obj_export(filepath=str(folder/(name+'.obj')),export_selected_objects=True,forward_axis='NEGATIVE_Z',up_axis='Y',export_materials=True,export_uv=True,export_normals=True,path_mode='RELATIVE')
    # The atlases are next to the mesh, not in a machine-specific source folder.
    p=folder/(name+'.mtl');lines=[]
    for line in p.read_text().splitlines():
        if line.startswith('map_Kd '):
            basename=Path(line[7:].replace('\\','/')).name
            source=next((n.image for ob in parts for m in ob.data.materials for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and Path(n.image.filepath).name==basename),None)
            if source and not (folder/basename).exists():source.save_render(str(folder/basename))
            line='map_Kd '+basename
        lines.append(line)
    p.write_text('\n'.join(lines)+'\n')
    cam,center,size=present(name,parts,folder)
    for image in bpy.data.images:
        if image.source=='FILE' and not image.packed_file:
            try:image.pack()
            except RuntimeError:pass
    bpy.ops.wm.save_as_mainfile(filepath=str(folder/(name+'.blend')))
    row={'asset':name,'triangles':sum(len(o.data.polygons) for o in parts),'mesh_parts':len(parts),'bounds_m_xyz':bounds,'atlas':name not in ['water_timer','crystal_skull'],'runtime_parts':len(runtime['parts'])}
    manifest=OUT/'manifest.json';rows=json.loads(manifest.read_text()) if manifest.exists() else [];rows=[r for r in rows if r['asset']!=name]+[row];manifest.write_text(json.dumps(rows,indent=2));print('V08_COMPLETE',row,flush=True)

if __name__=='__main__':
    only=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    for name in (only or ['cannon','water_timer','bomb','centrifuge','spritz','graphics_card','ram_pair','robot_vacuum','bath_gift_set','skeleton','monoblock_pc','crystal_skull']):build(name)
