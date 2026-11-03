"""Blender helper functions for the medical/tech valuables pack."""
import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector,Matrix
from math import pi,sin,cos
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/valuables/medical-tech-pack-06'
TEXTURES=OUT/'textures'
def reset():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
    bpy.context.preferences.filepaths.file_preview_type='NONE';bpy.context.preferences.filepaths.save_version=0
def mat(key):
    name='Loot | '+key
    if bpy.data.materials.get(name):return bpy.data.materials[name]
    m=bpy.data.materials.new(name);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Roughness'].default_value=.84;p.inputs['Metallic'].default_value=.04
    t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(TEXTURES/f'loot_{key}.png'),check_existing=True);t.interpolation='Closest'
    m.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color']);m.diffuse_color=(.5,.5,.5,1)
    return m
def finish(o,name,m):o.name=name;o.data.materials.clear();o.data.materials.append(mat(m) if isinstance(m,str) else m);return o
def box(name,loc,size,m,bev=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bev:
        mod=o.modifiers.new('Worn corners','BEVEL');mod.width=min(bev,min(size)*.35);mod.segments=1;bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(o,name,m)
def cyl(name,loc,r,h,m,n=16,axis='Z'):
    bpy.ops.mesh.primitive_cylinder_add(vertices=n,radius=r,depth=h,location=loc);o=bpy.context.object
    if axis=='X':o.rotation_euler[1]=pi/2
    elif axis=='Y':o.rotation_euler[0]=pi/2
    return finish(o,name,m)
def ico(name,loc,size,m,sub=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub,radius=1,location=loc);o=bpy.context.object;o.scale=size;return finish(o,name,m)
def mesh(name,vs,fs,m):
    me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.update();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);return finish(o,name,m)
def torus(name,loc,r,t,m,n=20,rot=None):
    bpy.ops.mesh.primitive_torus_add(major_segments=n,minor_segments=6,major_radius=r,minor_radius=t,location=loc);o=bpy.context.object
    if rot:o.rotation_euler=rot
    return finish(o,name,m)
def lathe(name,profile,m,n=24):
    vs=[(r*cos(j*2*pi/n),r*sin(j*2*pi/n),z) for z,r in profile for j in range(n)];fs=[]
    for k in range(len(profile)-1):
        for j in range(n):a=k*n+j;b=k*n+(j+1)%n;fs.append((a,b,b+n,a+n))
    fs.extend([tuple(reversed(range(n))),tuple(range((len(profile)-1)*n,len(profile)*n))]);return mesh(name,vs,fs,m)
def tube(name,path,r,m,n=6):
    ps=[Vector(p) for p in path];vs=[]
    for i,p in enumerate(ps):
        t=(ps[min(i+1,len(ps)-1)]-ps[max(i-1,0)]).normalized();a=t.cross(Vector((0,0,1)))
        if a.length<.1:a=t.cross(Vector((0,1,0)))
        a.normalize();b=t.cross(a).normalized()
        for j in range(n):vs.append(tuple(p+r*(a*cos(2*pi*j/n)+b*sin(2*pi*j/n))))
    fs=[tuple(reversed(range(n))),tuple(range((len(ps)-1)*n,len(ps)*n))]
    for k in range(len(ps)-1):
        for j in range(n):a=k*n+j;b=k*n+(j+1)%n;fs.append((a,b,b+n,a+n))
    return mesh(name,vs,fs,m)
def bar(name,a,b,r,m,n=8):
    a,b=Vector(a),Vector(b);o=cyl(name,(a+b)/2,r,(b-a).length,m,n);o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o
def bone(name,a,b,r=.015):
    a,b=Vector(a),Vector(b);length=(b-a).length
    o=lathe(name,[(0,r*.8),(.03*length,r*1.35),(.13*length,r),(length*.40,r*.64),(length*.83,r*.75),(length*.96,r*1.35),(length,r*.8)],'bone',8)
    o.location=a;o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o
def label(name,loc,size,key):
    o=box(name,loc,size,key)
    uv=o.data.uv_layers.active or o.data.uv_layers.new(name='UVMap');uv.active_render=True
    for p in o.data.polygons:
        for li in p.loop_indices:
            v=o.data.vertices[o.data.loops[li].vertex_index].co;uv.data[li].uv=(v.x/size[0]+.5,v.z/size[2]+.5)
    return o
def bake_parts():
    parts=[o for o in bpy.context.scene.objects if o.type=='MESH'];bpy.context.view_layer.update()
    low=min((o.matrix_world@v.co).z for o in parts for v in o.data.vertices)
    for o in parts:o.location.z-=low
    bpy.context.view_layer.update();bpy.ops.object.select_all(action='DESELECT')
    for o in parts:
        o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
        bpy.ops.object.material_slot_remove_unused()
        bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
        mod=o.modifiers.new('Export triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
        if not o.data.uv_layers:
            bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.025);bpy.ops.object.mode_set(mode='OBJECT')
        bm=bmesh.new();bm.from_mesh(o.data)
        assert all(e.is_manifold for e in bm.edges),(o.name,'nonmanifold')
        assert all(f.calc_area()>1e-12 for f in bm.faces),(o.name,'degenerate')
        bm.free();o.select_set(False)
    return parts
def render(parts,folder,name):
    scene=bpy.context.scene;points=[o.matrix_world@v.co for o in parts for v in o.data.vertices]
    low=Vector(tuple(min(p[i] for p in points) for i in range(3)));high=Vector(tuple(max(p[i] for p in points) for i in range(3)));center=(low+high)/2;size=max(high-low)
    floor=bpy.data.materials.new('Preview floor');floor.diffuse_color=(.045,.055,.06,1)
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.005));bpy.context.object.name='Preview_Ground';bpy.context.object.data.materials.append(floor)
    for loc,power,width in [((-2,-3,4),350,3),((3,-1,2),180,2),((0,3,4),400,3)]:
        bpy.ops.object.light_add(type='AREA',location=center+Vector(loc)*max(size,.5));o=bpy.context.object;o.name='Preview_Light';o.data.energy=power*max(size,.5)**2;o.data.size=width*max(size,.5);o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add();cam=bpy.context.object;cam.name='Preview_Camera';cam.data.type='ORTHO';cam.data.clip_end=1000;cam.data.ortho_scale=size*1.4;scene.camera=cam
    scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True;scene.world.color=(.14,.14,.14);scene.view_settings.view_transform='AgX'
    scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    for view,direction in [('front',(1.5,-2.5,1.5)),('rear',(-1.8,2.5,1.3))]:
        cam.location=center+Vector(direction)*max(size,.4);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(folder/f'{name}_{view}.png');bpy.ops.render.render(write_still=True)
    return center,size,cam
def export(name,role):
    folder=OUT/name;folder.mkdir(parents=True,exist_ok=True);parts=bake_parts();points=[o.matrix_world@v.co for o in parts for v in o.data.vertices]
    bounds=[max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)]
    if role=='valuable':
        # Static props share renderers by material; keep the centrifuge lid separate for authoring.
        groups={}
        for o in parts:
            key=tuple(m.name for m in o.data.materials)+(('lid',) if name=='centrifuge' and o.name.startswith('Lid') else ('body',))
            groups.setdefault(key,[]).append(o)
        merged=[]
        for i,objects in enumerate(groups.values()):
            bpy.ops.object.select_all(action='DESELECT')
            for ob in objects:ob.select_set(True)
            bpy.context.view_layer.objects.active=objects[0]
            if len(objects)>1:bpy.ops.object.join()
            ob=bpy.context.object;ob.name=name+'_MaterialGroup_%02d'%i;merged.append(ob)
        parts=merged
    bpy.ops.object.select_all(action='DESELECT')
    root=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(root)
    for o in parts:o.parent=root;o.select_set(True)
    root.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(folder/(name+'.glb')),export_format='GLB',use_selection=True,export_yup=True)
    bpy.ops.wm.obj_export(filepath=str(folder/(name+'.obj')),export_selected_objects=True,forward_axis='NEGATIVE_Z',up_axis='Y',export_uv=True,export_materials=True,export_normals=True,path_mode='RELATIVE')
    # Packed Blender images can retain a source scene's relative directory. Resolve
    # this portable sidecar explicitly against this pack's shared texture folder.
    mtl=folder/(name+'.mtl');lines=[]
    for line in mtl.read_text().splitlines():
        if line.startswith('map_Kd '):line='map_Kd ../textures/'+Path(line[7:].replace('\\','/')).name
        lines.append(line)
    mtl.write_text('\n'.join(lines)+'\n')
    result={'asset':name,'role':role,'triangles':sum(len(o.data.polygons) for o in parts),'mesh_parts':len(parts),'bounds_m_xyz':bounds,'materials':len({m.name for o in parts for m in o.data.materials if m}),'textures':sorted({Path(n.image.filepath).name for o in parts for m in o.data.materials if m and m.use_nodes for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image}),'source_checks':'PASS'}
    if name=='spritz':export_spritz(parts)
    center,size,cam=render(parts,folder,name)
    if name=='spritz':
        scene=bpy.context.scene;scene.render.resolution_x=256;scene.render.resolution_y=256;scene.render.film_transparent=True
        for o in scene.objects:
            if o.name.startswith('Preview_Ground'):o.hide_render=True
        cam.location=center+Vector((1.5,-2.5,1.5))*max(size,.4);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(ROOT/'src/Art/spritz_inventory.png');bpy.ops.render.render(write_still=True)
    for o in bpy.data.images:
        if o.source=='FILE' and not o.packed_file:
            try:o.pack()
            except RuntimeError:pass
    bpy.ops.wm.save_as_mainfile(filepath=str(folder/(name+'.blend')))
    print('ASSET_EXPORTED',name,result,flush=True);return result
def export_spritz(parts):
    # Existing ItemArt schema; muzzle/needle tip at origin, forward Blender +Y -> Unity +Z.
    from mathutils import Matrix
    C=Matrix(((1,0,0),(0,0,1),(0,1,0)));needle=next(o for o in parts if o.name=='Spritz_Needle');origin=Vector((0,.27,needle.location.z));data={'version':1,'fps':30,'frames':12,'parts':[]}
    vec=lambda v:{'x':float(v.x),'y':float(v.y),'z':float(v.z)}
    for ob in parts:
        # World coordinates are converted directly, so all runtime part transforms are identity.
        verts=[];normals=[];uvs=[];idx=[];mw=ob.matrix_world;N=mw.to_3x3().inverted().transposed()
        for p in ob.data.polygons:
            start=len(verts)
            for li in p.loop_indices:
                loop=ob.data.loops[li];v=ob.data.vertices[loop.vertex_index]
                verts.append(vec(C@(mw@v.co-origin)));normals.append(vec((C@(N@p.normal)).normalized()))
                u=ob.data.uv_layers.active.data[li].uv;uvs.append({'x':float(u.x),'y':float(u.y)})
            idx.extend([start,start+2,start+1])
        texture=next(n.image for n in ob.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE')
        texname=Path(texture.filepath).name
        pose={'position':{'x':0,'y':0,'z':0},'rotation':{'x':0,'y':0,'z':0,'w':1},'scale':{'x':1,'y':1,'z':1}}
        samples=[]
        if ob.name in ['Plunger shaft','Thumb ring']:
            import copy
            for frame in range(12):
                p=copy.deepcopy(pose);t=min(1,frame/6);p['position']['z']=.037*t*t*(3-2*t);samples.append(p)
        data['parts'].append({'name':ob.name,'vertices':verts,'normals':normals,'uv':uvs,'triangles':idx,'color':{'r':1,'g':1,'b':1,'a':1},'texture':texname,'rest':pose,'samples':samples})
        import shutil
        shutil.copyfile(TEXTURES/texname,ROOT/'src/Art'/texname)
    (ROOT/'src/Art/spritz.mesh.json').write_text(json.dumps(data,separators=(',',':')))
