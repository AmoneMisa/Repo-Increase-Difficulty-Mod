"""0.7.2 art: curved bomb details and rounded glass sample tubes, portable mesh previews."""
import sys,math,json,shutil,bpy
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
import rework_valuables08 as prev
import rework_valuables09 as rev9
import build_medical_tech_pack06 as source
from valuable_meshkit import cyl,lathe,torus,mesh
ROOT=prev.ROOT;OUT=ROOT/'art/valuables/physics-rework-10';OUT.mkdir(exist_ok=True)
def bomb():
    prev.load('bomb')
    print('BOMB_SOURCE_PARTS',[(o.name,len(o.data.polygons)) for o in bpy.context.scene.objects if o.type=='MESH'],flush=True)
    # Retain curved hull and fins as independent pieces for tight convex collision.
    for ob in list(bpy.context.scene.objects):
        if ob.type!='MESH':continue
        bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.separate(type='LOOSE');bpy.ops.object.mode_set(mode='OBJECT')
    steel=prev.solid('Worn strap steel',(.11,.13,.105),.62,.35)
    for x,r in [(-.80,.738),(.51,.77)]:torus('Curved casing band',(x,0,.84),r,.009,steel,40,(0,math.pi/2,0))
    # A curved inset panel follows the shell; no floating square repair blocks.
    vs=[]
    for x in [-.35,-.1,.15]:
        for i in range(9):
            a=-.26+i*.065;vs.append((x,.785*math.sin(a),.84+.785*math.cos(a)))
    fs=[]
    for j in range(2):
        for i in range(8):k=j*9+i;fs.append((k,k+1,k+10,k+9))
    panel=mesh('Curved service panel',vs,fs,steel)
    mod=panel.modifiers.new('Thin formed metal','SOLIDIFY');mod.thickness=.005;bpy.context.view_layer.objects.active=panel;bpy.ops.object.modifier_apply(modifier=mod.name)
    for ob in bpy.context.scene.objects:
        if ob.type=='MESH':ob['group']='hull_'+ob.name

def centrifuge():
    source.centrifuge()
    for ob in list(bpy.context.scene.objects):
        if ob.name.startswith(('Sample tube ','Tube collar ','Orange stopper ')):bpy.data.objects.remove(ob,do_unlink=True)
        elif ob.name.startswith('Lid'):
            ob['group']='lid'
            if ob.name.startswith('Lid smoky window'):
                ob.data.materials.clear();ob.data.materials.append(prev.solid('Clear smoked lid',(.18,.24,.25),.18,trans=.72))
    glass=prev.solid('Clear sample glass',(.53,.71,.77),.17,trans=.85)
    collar=prev.solid('Sample collar',(.42,.47,.43),.46)
    stopper=prev.solid('Orange rubber stopper',(.56,.12,.035),.65)
    liquids=[prev.solid('Sample liquid violet',(.18,.025,.38),.23,trans=.1,emit=.12),prev.solid('Sample liquid teal',(.025,.28,.23),.23,trans=.1,emit=.12)]
    for i in range(12):
        a=i*2*math.pi/12;center=Vector((.151*math.cos(a),.151*math.sin(a),.18))
        shell=lathe('Rounded glass tube %02d'%i,[(-.047,.001),(-.043,.007),(-.037,.0115),(.037,.0115),(.046,.012)],glass,20);shell.location=center
        liquid=cyl('Visible liquid %02d'%i,center+Vector((0,0,-.012)),.009,.055,liquids[i%2],20)
        lip=cyl('Collar %02d'%i,center+Vector((0,0,.044)),.014,.011,collar,20)
        cap=cyl('Rubber stopper %02d'%i,center+Vector((0,0,.055)),.013,.015,stopper,20)
        for ob in [shell,liquid,lip,cap]:
            ob['group']='tube_%02d'%i;ob['phase']=i/12
        for ob in [lip,cap]:
            bpy.context.view_layer.objects.active=ob;mod=ob.modifiers.new('Rounded rim','BEVEL');mod.width=.0015;mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name)
    prev.detail('centrifuge')

def build(name):
    prev.kit.reset();folder=OUT/name;folder.mkdir(exist_ok=True)
    globals()[name]()
    parts=prev.prepare_parts();bpy.context.view_layer.update()
    pts=[o.matrix_world@v.co for o in parts for v in o.data.vertices];size=max(max(p[i] for p in pts)-min(p[i] for p in pts) for i in range(3))
    rev9.paint(parts,size);atlas=prev.bake_atlas(name,parts,size,folder)
    if atlas:
        atlas.filepath_raw=str(folder/('v10_'+name+'_atlas.png'));atlas.save()
    # Preserve separate collision pieces; runtime grouping keeps each sample's glass/liquid/cap together.
    bpy.ops.object.select_all(action='DESELECT')
    for ob in parts:ob.select_set(True)
    for ob in parts:
        for m in ob.data.materials:
            for node in m.node_tree.nodes:
                if node.type=='TEX_IMAGE' and node.image and node.outputs['Color'].is_linked:
                    target=folder/Path(node.image.filepath).name
                    if not target.exists():node.image.save_render(str(target))
    data=prev.runtime_export(name,parts,folder)
    dest=folder/('v10_'+name+'.mesh.json');(folder/('v08_'+name+'.mesh.json')).replace(dest)
    shutil.copyfile(dest,ROOT/'src/Art'/dest.name)
    for m in data['materials']:
        if m['texture']:shutil.copyfile(folder/m['texture'],ROOT/'src/Art'/m['texture'])
    bpy.ops.export_scene.gltf(filepath=str(folder/(name+'.glb')),export_format='GLB',use_selection=True,export_yup=True)
    bpy.ops.wm.obj_export(filepath=str(folder/(name+'.obj')),export_selected_objects=True,forward_axis='NEGATIVE_Z',up_axis='Y',export_materials=True,export_uv=True,export_normals=True,path_mode='RELATIVE')
    mtl=folder/(name+'.mtl')
    mtl.write_text('\n'.join(line.split(' ')[0]+' '+Path(line.split(' ',1)[1]).name if line.startswith('map_') else line for line in mtl.read_text().splitlines())+'\n')
    bpy.ops.wm.save_as_mainfile(filepath=str(folder/(name+'.blend')))
    prev.present(name,parts,folder)
    print('V10_COMPLETE',name,len(parts),sum(len(o.data.polygons) for o in parts),flush=True)
for name in (sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['bomb','centrifuge']):build(name)
