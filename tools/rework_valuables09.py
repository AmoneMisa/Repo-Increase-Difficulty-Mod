"""Revision 09: independently articulated props and consistent original painted albedos."""
import bpy,bmesh,math,json,sys,shutil
from pathlib import Path
from mathutils import Vector,Matrix
from math import pi,sin,cos
sys.path.insert(0,str(Path(__file__).resolve().parent))
import rework_valuables08 as prev
import build_medical_tech_pack06 as source
from valuable_meshkit import box,cyl,ico,mesh,torus,lathe,bar,tube,label
ROOT=prev.ROOT;OUT=ROOT/'art/valuables/interaction-rework-09';OUT.mkdir(parents=True,exist_ok=True)
prev.OUT=OUT;prev.kit.OUT=OUT
GROUPS={}

def clear_object(ob):bpy.data.objects.remove(ob,do_unlink=True)
def skull(bone=False):
    if not bone:
        from skull_quartz10 import create_skull
        return create_skull()
    material=source.mat('bone') if bone else prev.solid('Carved clear crystal',(.59,.78,.84),.13,trans=.93)
    def uv(name,loc,scale):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=14,location=loc);o=bpy.context.object;o.name=name;o.scale=scale;o.data.materials.append(material)
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return o
    head=uv('Anatomical cranial vault',(0,.012,.205),(.091,.080,.101))
    facial=[head,box('Upper dental arch',(0,-.059,.099),(.083,.041,.027),material,.009)]
    for side in [-1,1]:
        facial.append(uv('Connected maxillary bone',(side*.026,-.037,.134),(.031,.044,.047)))
        facial.append(uv('Connected zygomatic cheek',(side*.062,-.034,.145),(.023,.027,.030)))
    facial.append(uv('Nasal bridge',(0,-.047,.164),(.014,.023,.037)))
    bpy.ops.object.select_all(action='DESELECT')
    for ob in facial:ob.select_set(True)
    bpy.context.view_layer.objects.active=head;bpy.ops.object.join()
    remesh=head.modifiers.new('Continuous carved skull volume','REMESH');remesh.mode='VOXEL';remesh.voxel_size=.0020;bpy.ops.object.modifier_apply(modifier=remesh.name)
    dec=head.modifiers.new('Low-poly craniofacial surface','DECIMATE');dec.ratio=.06;bpy.ops.object.modifier_apply(modifier=dec.name)
    # Real openings cut into the face, rather than faceted bulges over the eyes.
    for side in [-1,1]:
        cutter=uv('temporary eye socket',(side*.041,-.058,.166),(.030,.060,.029))
        bpy.context.view_layer.objects.active=head;mod=head.modifiers.new('Deep orbital cavity','BOOLEAN');mod.operation='DIFFERENCE';mod.object=cutter;bpy.ops.object.modifier_apply(modifier=mod.name);clear_object(cutter)
    nose=mesh('temporary triangular nasal opening',[(-.012,-.13,.113),(.012,-.13,.113),(0,-.13,.145),(-.012,.015,.113),(.012,.015,.113),(0,.015,.145)],[(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)],material)
    bpy.context.view_layer.objects.active=head;mod=head.modifiers.new('Nasal aperture','BOOLEAN');mod.operation='DIFFERENCE';mod.object=nose;bpy.ops.object.modifier_apply(modifier=mod.name);clear_object(nose)
    jaws=[]
    for side in [-1,1]:
        jaws.append(bar('Mandible ascending ramus',(side*.065,-.012,.128),(side*.057,-.043,.050),.011,material,10))
    jaws.append(tube('U-shaped lower mandible',[(-.057,-.043,.05),(-.047,-.068,.037),(-.023,-.082,.033),(0,-.085,.032),(.023,-.082,.033),(.047,-.068,.037),(.057,-.043,.05)],.011,material,10))
    bpy.ops.object.select_all(action='DESELECT')
    for ob in jaws:ob.select_set(True)
    bpy.context.view_layer.objects.active=jaws[-1];bpy.ops.object.join();jaw=bpy.context.object
    remesh=jaw.modifiers.new('Continuous mandibular bone','REMESH');remesh.mode='VOXEL';remesh.voxel_size=.0015;bpy.ops.object.modifier_apply(modifier=remesh.name)
    dec=jaw.modifiers.new('Low-poly jaw','DECIMATE');dec.ratio=.06;bpy.ops.object.modifier_apply(modifier=dec.name)
    for row,z in [('Upper',.083),('Lower',.050)]:
        for i in range(10):
            x=(i-4.5)*.008;y=-.083+.016*(abs(x)/.04)**2
            box(row+' tooth',(x,y,z),(.0065,.009,.011),material,.0018)
    return [o for o in bpy.context.scene.objects if o.type=='MESH']

def make_ram():
    source.ram()
    for o in list(bpy.context.scene.objects):
        if o.name.startswith('RGB diffuser'):clear_object(o)
    for i,y in enumerate([-.022,.022]):
        m=prev.solid('Continuous RGB diffuser '+str(i),(.25,.5,.7),.30,emit=.9)
        p=m.node_tree.nodes.get('Principled BSDF');tex=m.node_tree.nodes.new('ShaderNodeTexCoord');sep=m.node_tree.nodes.new('ShaderNodeSeparateXYZ');r=m.node_tree.nodes.new('ShaderNodeValToRGB')
        colors=[(.7,.025,.02,1),(.8,.24,.015,1),(.65,.7,.03,1),(.03,.6,.19,1),(.02,.4,.75,1),(.26,.04,.7,1)]
        r.color_ramp.elements.remove(r.color_ramp.elements[1]);r.color_ramp.elements[0].color=colors[0]
        for j,c in enumerate(colors[1:]):r.color_ramp.elements.new((j+1)/5).color=c
        m.node_tree.links.new(tex.outputs['Generated'],sep.inputs[0]);m.node_tree.links.new(sep.outputs['X'],r.inputs[0]);m.node_tree.links.new(r.outputs[0],p.inputs['Base Color']);m.node_tree.links.new(r.outputs[0],p.inputs['Emission Color'])
        # Bake the continuous gradient into a portable texture, retaining one solid diffuser per stick.
        import numpy as np
        im=bpy.data.images.new('v09_rgb_gradient',width=256,height=16,alpha=True)
        pixels=np.empty((16,256,4),dtype=np.float32)
        for x in range(256):
            f=x/255*5;j=min(4,int(f));t=f-j;pixels[:,x,:]=[(1-t)*colors[j][k]+t*colors[j+1][k] for k in range(4)]
        im.pixels.foreach_set(pixels.ravel());folder=OUT/'ram_pair';folder.mkdir(exist_ok=True);im.filepath_raw=str(folder/'v09_rgb_gradient.png');im.file_format='PNG';im.save();im.pack()
        image=m.node_tree.nodes.new('ShaderNodeTexImage');image.image=im
        m.node_tree.links.new(image.outputs['Color'],p.inputs['Base Color']);m.node_tree.links.new(image.outputs['Color'],p.inputs['Emission Color'])
        ob=box('Seamless luminous light bar',(0,y,.053),(.140,.009,.006),m,.002)
        uv=ob.data.uv_layers.active
        for poly in ob.data.polygons:
            for li in poly.loop_indices:
                v=ob.data.vertices[ob.data.loops[li].vertex_index].co;uv.data[li].uv=(v.x/.140+.5,.5)
        ob['group']='rgb_'+str(i)

def make_centrifuge():
    source.centrifuge()
    for ob in bpy.context.scene.objects:
        if ob.type!='MESH':continue
        if ob.name.startswith('Lid smoky window'):
            ob.data.materials.clear();ob.data.materials.append(prev.solid('Smoked clear polycarbonate',(.18,.24,.25),.18,trans=.72))
        if ob.name.startswith(('Sample tube ','Tube collar ','Orange stopper ')):
            ob['group']='tube_'+ob.name.split()[2].split('.')[0].zfill(2)
        elif ob.name.startswith('Lid'):ob['group']='lid'
    prev.detail('centrifuge')

def make_vacuum():
    source.vacuum();prev.detail('robot_vacuum')
    for ob in bpy.context.scene.objects:
        if ob.name.startswith('Drive wheel'):ob['group']='drive_'+('left' if ob.location.x<0 else 'right')
        elif ob.name.startswith('Side brush'):ob['group']='brush'
        elif ob.name.startswith('Lidar'):ob['group']='lidar'

def make_skeleton():
    source.skeleton()
    for ob in list(bpy.context.scene.objects):
        if ob.type=='MESH' and ob.name.startswith('Skull_'):clear_object(ob)
    old=set(bpy.context.scene.objects);skull(True)
    for ob in set(bpy.context.scene.objects)-old:
        ob.location=ob.location*.65+Vector((0,0,1.49));ob.scale*=.65;ob['group']='head'
    bpy.context.view_layer.update()
    for ob in bpy.context.scene.objects:
        if ob.type!='MESH' or ob.get('group'):continue
        n=ob.name;center=sum((ob.matrix_world@v.co for v in ob.data.vertices),Vector())/len(ob.data.vertices);side='left' if center.x<0 else 'right'
        if n.startswith('Caster wheel'):ob['group']='caster_'+str(len([o for o in bpy.context.scene.objects if o.get('group','').startswith('caster_')]))
        elif n.startswith(('Humerus','Shoulder joint')):ob['group']='upper_arm_'+side
        elif n.startswith(('Radius','Ulna','Elbow')):ob['group']='forearm_'+side
        elif n.startswith(('Wrist','Metacarpal','Finger')):ob['group']='hand_'+side
        elif n.startswith('Femur'):ob['group']='thigh_'+side
        elif n.startswith(('Tibia','Fibula','Knee')):ob['group']='shin_'+side
        elif n.startswith(('Heel','Metatarsal','Toe')):ob['group']='foot_'+side
        elif n.startswith(('Rib','Sternum','Shoulder blade','Clavicle')) or n.startswith(('Vertebra','Intervertebral','Spinal')) and center.z>1.08:ob['group']='torso'
        elif n.startswith(('Pelvic','Hip socket','Pubic','Vertebra','Spinal','Intervertebral')):ob['group']='pelvis'

def winx():
    source.bath_set();prev.detail('bath_gift_set')
    path=OUT/'textures/winx_packaging.png';assert path.is_file(),'Winx label texture must be generated first'
    im=bpy.data.images.load(str(path));im.pack()
    def printmat(name):
        m=prev.solid(name,(1,1,1),.76);p=m.node_tree.nodes.get('Principled BSDF');node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=im;m.node_tree.links.new(node.outputs['Color'],p.inputs['Base Color']);return m
    for ob in bpy.context.scene.objects:
        if not ob.name.startswith('Original fairy label'):continue
        i=round((ob.location.x+.086)/.086);ob.data.materials.clear();ob.data.materials.append(printmat('Winx fairy label '+str(i)))
        uv=ob.data.uv_layers.active
        for v in uv.data:v.uv=(i/3+v.uv.x/3,.26+v.uv.y*.74)
    ob=label('Winx printed gift header',(0,-.052,.177),(.260,.001,.023),printmat('Winx gift header'))
    for v in ob.data.uv_layers.active.data:v.uv=(v.uv.x,v.uv.y*.26)

def paint(parts,size):
    prev.painted_materials(parts,size)
    for m in {m for o in parts for m in o.data.materials}:
        p=m.node_tree.nodes.get('Principled BSDF')
        if p.inputs['Transmission Weight'].default_value>.15 or p.inputs['Emission Strength'].default_value>.01:continue
        if 'Winx' in m.name:continue
        # Low contrast, broad wear variation rather than tiled coloured speckles.
        tree=m.node_tree;old=p.inputs['Base Color'].links[0].from_socket
        noise=tree.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=32;noise.inputs['Detail'].default_value=0
        ramp=tree.nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.28;ramp.color_ramp.elements[0].color=(.78,.75,.69,1);ramp.color_ramp.elements[1].position=.72;ramp.color_ramp.elements[1].color=(1,1,.96,1)
        tree.links.new(noise.outputs['Fac'],ramp.inputs[0]);mix=tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.34;tree.links.new(old,mix.inputs[1]);tree.links.new(ramp.outputs[0],mix.inputs[2]);tree.links.new(mix.outputs[0],p.inputs['Base Color'])

def build(name):
    prev.kit.reset();folder=OUT/name;folder.mkdir(exist_ok=True)
    if name=='crystal_skull':skull()
    elif name=='centrifuge':make_centrifuge()
    elif name=='ram_pair':make_ram()
    elif name=='robot_vacuum':make_vacuum()
    elif name=='skeleton':make_skeleton()
    elif name=='bath_gift_set':winx()
    elif name=='water_timer':prev.timer()
    else:
        prev.load(name)
        if name=='cannon':prev.loose_cannon_wheels()
        prev.detail(name)
    parts=prev.prepare_parts();bpy.context.view_layer.update();points=[o.matrix_world@v.co for o in parts for v in o.data.vertices];size=max(max(p[i] for p in points)-min(p[i] for p in points) for i in range(3))
    if name=='crystal_skull':
        for ob in parts:
            for poly in ob.data.polygons:poly.use_smooth=False
            ob.data.update()
    if name not in ['water_timer','crystal_skull']:
        paint(parts,size);prev.bake_atlas(name,parts,size,folder)
    parts=prev.merge_static(parts,name)
    bpy.ops.object.select_all(action='DESELECT');root=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(root)
    for ob in parts:ob.parent=root;ob.select_set(True)
    for ob in parts:
        for m in ob.data.materials:
            for n in m.node_tree.nodes:
                if n.type=='TEX_IMAGE' and n.image and n.outputs['Color'].is_linked:
                    target=folder/Path(n.image.filepath).name
                    if not target.exists():n.image.save_render(str(target))
    root.select_set(True);runtime=prev.runtime_export(name,parts,folder)
    # Versioned runtime descriptors retain loader compatibility; all collection items are embedded now.
    old=folder/('v08_'+name+'.mesh.json');dest=folder/('v09_'+name+'.mesh.json');old.replace(dest)
    shutil.copyfile(dest,ROOT/'src/Art'/dest.name)
    for m in runtime['materials']:
        if m['texture']:shutil.copyfile(folder/m['texture'],ROOT/'src/Art'/m['texture'])
    if name=='water_timer':prev.timer_keys(parts)
    bpy.ops.export_scene.gltf(filepath=str(folder/(name+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_animations=name=='water_timer',export_animation_mode='SCENE',export_force_sampling=True,export_pointer_animation=True)
    bpy.ops.wm.obj_export(filepath=str(folder/(name+'.obj')),export_selected_objects=True,forward_axis='NEGATIVE_Z',up_axis='Y',export_materials=True,export_uv=True,export_normals=True,path_mode='RELATIVE')
    path=folder/(name+'.mtl');lines=[]
    for line in path.read_text().splitlines():
        if line.startswith('map_Kd '):line='map_Kd '+Path(line[7:].replace('\\','/')).name
        lines.append(line)
    path.write_text('\n'.join(lines)+'\n')
    # Export any unbaked RGB texture beside its mesh.
    for ob in parts:
        for m in ob.data.materials:
            for n in m.node_tree.nodes:
                if n.type=='TEX_IMAGE' and n.image:
                    target=folder/Path(n.image.filepath).name
                    if not target.exists():n.image.save_render(str(target))
    prev.present(name,parts,folder)
    for image in bpy.data.images:
        if image.source=='FILE' and not image.packed_file:
            try:image.pack()
            except RuntimeError:pass
    bpy.ops.wm.save_as_mainfile(filepath=str(folder/(name+'.blend')))
    bpy.context.view_layer.update();points=[o.matrix_world@v.co for o in parts for v in o.data.vertices]
    row={'asset':name,'triangles':sum(len(o.data.polygons) for o in parts),'mesh_parts':len(parts),'runtime_parts':len(runtime['parts']),'bounds_m_xyz':[max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)],'groups':sorted({p['group'] for p in runtime['parts']})}
    path=OUT/'manifest.json';rows=json.loads(path.read_text()) if path.exists() else [];rows=[r for r in rows if r['asset']!=name]+[row];path.write_text(json.dumps(rows,indent=2));print('V09_COMPLETE',name,flush=True)

if __name__=='__main__':
    only=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    for name in only or ['crystal_skull','ram_pair','centrifuge','robot_vacuum','skeleton','bomb','cannon','graphics_card','monoblock_pc','spritz','water_timer','bath_gift_set']:build(name)
