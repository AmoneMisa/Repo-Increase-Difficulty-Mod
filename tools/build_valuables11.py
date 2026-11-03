"""Original porcelain cat/ingot and lighting/material revision of existing authored meshes."""
import bpy,math,json,sys,shutil
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
import rework_valuables08 as prev
from valuable_meshkit import box,cyl,ico,mesh,torus,bar,tube
ROOT=prev.ROOT;OUT=ROOT/('art/valuables/maneki-reference-13' if '--review' in sys.argv else 'art/valuables/lighting-collection-11');OUT.mkdir(exist_ok=True)

def load(key,revision):
    folder=ROOT/'art/valuables'/revision/key
    with bpy.data.libraries.load(str(folder/(key+'.blend')),link=False) as (src,dst):
        dst.objects=[n for n in src.objects if not n.startswith('Preview_')]
    def world(ob):return world(ob.parent)@ob.matrix_parent_inverse@ob.matrix_basis if ob.parent else ob.matrix_basis.copy()
    poses={o:world(o) for o in dst.objects if o and o.type=='MESH'}
    for o,w in poses.items():bpy.context.collection.objects.link(o);o.parent=None;o.matrix_world=w
    for o in list(bpy.data.objects):
        if o.type!='MESH':bpy.data.objects.remove(o,do_unlink=True)
    for image in bpy.data.images:
        if image.source=='FILE' and not image.packed_file:
            file=folder/Path(image.filepath).name
            if file.exists():image.filepath=str(file)

def sphere(name,loc,scale,mat,segments=24,rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=loc)
    ob=bpy.context.object;ob.name=name;ob.scale=scale;ob.data.materials.append(mat);return ob

def stamp(text,loc,size,mat,rotation=(0,0,0)):
    data=bpy.data.curves.new('Cast stamp','FONT');data.body=text;data.align_x='CENTER';data.align_y='CENTER';data.size=size;data.extrude=.00008;data.resolution_u=3
    ob=bpy.data.objects.new('Stamp '+text,data);bpy.context.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=rotation;data.materials.append(mat)
    bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.convert(target='MESH');ob.select_set(False);return bpy.context.object

def ingot():
    gold=prev.solid('Cast bullion gold',(.72,.43,.065),.27,.88);dark=prev.solid('Recessed gold stamp',(.23,.12,.022),.4,.72)
    vs=[(x,y,z) for z,w,l in [(0,.064,.150),(.020,.052,.128)] for x,y in [(-w/2,-l/2),(w/2,-l/2),(w/2,l/2),(-w/2,l/2)]]
    ob=mesh('Tapered cast ingot',vs,[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],gold)
    bpy.context.view_layer.objects.active=ob;mod=ob.modifiers.new('Soft cast edges','BEVEL');mod.width=.0014;mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name)
    for text,y,size in [('FINE',.039,.008),('GOLD',.022,.012),('999.9',.004,.008),('3 kg',-.038,.009)]:stamp(text,(0,y,.02015),size,dark)

def neko():
    from maneki_porcelain12 import make
    make(prev,sphere)


def recolor(key):
    load(key,'physics-rework-10' if key=='bomb' else 'interaction-rework-09')
    for mat in {m for o in bpy.context.scene.objects if o.type=='MESH' for m in o.data.materials}:
        p=mat.node_tree.nodes.get('Principled BSDF');tree=mat.node_tree
        if not p.inputs['Base Color'].is_linked:continue
        src=p.inputs['Base Color'].links[0].from_socket
        hsv=tree.nodes.new('ShaderNodeSeparateColor');hsv.mode='HSV';tree.links.new(src,hsv.inputs[0])
        lo=tree.nodes.new('ShaderNodeMath');lo.operation='GREATER_THAN';lo.inputs[1].default_value=.125;tree.links.new(hsv.outputs[0],lo.inputs[0])
        hi=tree.nodes.new('ShaderNodeMath');hi.operation='LESS_THAN';hi.inputs[1].default_value=.52;tree.links.new(hsv.outputs[0],hi.inputs[0])
        mask=tree.nodes.new('ShaderNodeMath');mask.operation='MULTIPLY';tree.links.new(lo.outputs[0],mask.inputs[0]);tree.links.new(hi.outputs[0],mask.inputs[1])
        gray=tree.nodes.new('ShaderNodeHueSaturation');gray.inputs['Saturation'].default_value=0;gray.inputs['Value'].default_value=.65;tree.links.new(src,gray.inputs['Color'])
        mix=tree.nodes.new('ShaderNodeMixRGB');tree.links.new(mask.outputs[0],mix.inputs[0]);tree.links.new(src,mix.inputs[1]);tree.links.new(gray.outputs[0],mix.inputs[2]);tree.links.new(mix.outputs[0],p.inputs['Base Color'])
    parts=prev.prepare_parts();bpy.context.view_layer.update();size=max((max((o.matrix_world@v.co)[i] for o in parts for v in o.data.vertices)-min((o.matrix_world@v.co)[i] for o in parts for v in o.data.vertices)) for i in range(3))
    folder=OUT/key;folder.mkdir(exist_ok=True);atlas=prev.bake_atlas(key,parts,size,folder)
    atlas.filepath_raw=str(folder/('v11_'+key+'_atlas.png'));atlas.save()
    for m in {m for o in parts for m in o.data.materials}:p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Metallic'].default_value=.72 if key=='bomb' else .65;p.inputs['Roughness'].default_value=.56 if key=='bomb' else .43

def existing_light(key):
    load(key,'interaction-rework-09')
    cyan=prev.solid('Cyan light guide',(.025,.40,.48),.3,emit=2.2)
    if key=='graphics_card':
        for y in [-.077,.077]:
            ob=box('Continuous GPU light guide',(0,y,.050),(.316,.0025,.0035),cyan,.0007);ob['group']='rgb_gpu'
    else:
        ob=torus('Underside scanner emitter',(0,0,.017),.149,.002,cyan,48);ob['group']='scanner_emitter'

def build(key):
    prev.kit.reset();folder=OUT/key;folder.mkdir(exist_ok=True)
    if key=='gold_ingot':ingot()
    elif key=='maneki_neko':neko()
    elif key in ['bomb','cannon']:recolor(key)
    else:existing_light(key)
    parts=prev.prepare_parts()
    if key=='maneki_neko':
        for ob in parts:
            if any('porcelain' in m.name.lower() for m in ob.data.materials):
                for poly in ob.data.polygons:poly.use_smooth=True
    if key=='maneki_neko':
        # Keep a small material-batched model while retaining the complete moving paw.
        parts=prev.merge_static(parts,key)
    if key=='gold_ingot':parts=prev.merge_static(parts,key)
    for ob in parts:
        ob.data.update()
        for poly in ob.data.polygons:
            if any(ob.data.corner_normals[li].vector.dot(poly.normal)<=0 for li in poly.loop_indices):poly.use_smooth=False
        ob.data.update()
    for o in parts:
        for m in o.data.materials:
            for node in m.node_tree.nodes:
                if node.type=='TEX_IMAGE' and node.image and node.outputs['Color'].is_linked:
                    path=folder/Path(node.image.filepath).name
                    if not path.exists():node.image.save_render(str(path))
    runtime=prev.runtime_export(key,parts,folder)
    mats={m.name:m for o in parts for m in o.data.materials}
    for m in runtime['materials']:m['metallic']=float(mats[m['name']].node_tree.nodes['Principled BSDF'].inputs['Metallic'].default_value)
    if key=='maneki_neko':
        from maneki_porcelain12 import PAW_PIVOT
        runtime['beckonPivot']={'x':PAW_PIVOT[0],'y':PAW_PIVOT[2],'z':PAW_PIVOT[1]}
    path=folder/('v11_'+key+'.mesh.json');path.write_text(json.dumps(runtime,separators=(',',':')));(folder/('v08_'+key+'.mesh.json')).unlink()
    if '--review' not in sys.argv:shutil.copyfile(path,ROOT/'src/Art'/path.name)
    for m in runtime['materials']:
        if m['texture'] and '--review' not in sys.argv:shutil.copyfile(folder/m['texture'],ROOT/'src/Art'/m['texture'])
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    if key=='maneki_neko':
        pivot=bpy.data.objects.new('Beckoning paw pivot',None);bpy.context.collection.objects.link(pivot);pivot.location=PAW_PIVOT
        bpy.context.view_layer.update()
        for ob in parts:
            if ob.get('group')=='beckoning_paw':world=ob.matrix_world.copy();ob.parent=pivot;ob.matrix_world=world
        for frame,angle in [(1,-12),(33,18),(65,-12)]:pivot.rotation_euler.x=math.radians(angle);pivot.keyframe_insert(data_path='rotation_euler',frame=frame)
        bpy.context.scene.render.fps=24;bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=65;pivot.select_set(True)
        bpy.context.scene.frame_set(17)
    bpy.ops.export_scene.gltf(filepath=str(folder/(key+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_animations=True)
    bpy.ops.wm.obj_export(filepath=str(folder/(key+'.obj')),export_selected_objects=True,forward_axis='NEGATIVE_Z',up_axis='Y',export_materials=True,export_uv=True,export_normals=True,path_mode='RELATIVE')
    mtl=folder/(key+'.mtl');mtl.write_text('\n'.join(s.split(' ')[0]+' '+Path(s.split(' ',1)[1]).name if s.startswith('map_') else s for s in mtl.read_text().splitlines())+'\n')
    if '--no-preview' not in sys.argv:
        cam,center,size=prev.present(key,parts,folder,samples=24)
        if key=='maneki_neko':
            for frame in [1,33]:bpy.context.scene.frame_set(frame);bpy.context.scene.render.filepath=str(folder/('maneki_neko_paw_'+str(frame)+'.png'));bpy.ops.render.render(write_still=True)
    for im in bpy.data.images:
        if im.source=='FILE' and not im.packed_file:
            try:im.pack()
            except RuntimeError:pass
    bpy.ops.wm.save_as_mainfile(filepath=str(folder/(key+'.blend')))
    print('V11_COMPLETE',key,'parts',len(runtime['parts']),'triangles',sum(len(p['triangles'])//3 for p in runtime['parts']),flush=True)

if __name__=='__main__':
    for key in ([s for s in sys.argv[sys.argv.index('--')+1:] if not s.startswith('--')] if '--' in sys.argv else ['gold_ingot','maneki_neko','bomb','cannon','graphics_card','robot_vacuum']):build(key)
