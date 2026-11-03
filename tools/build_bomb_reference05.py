"""Build an original low-poly visual prop using the user's museum photo as reference.
Run with Blender --background --factory-startup --python tools/build_bomb_reference05.py.
No game integration or weapon behavior is implemented.
"""
import bpy,bmesh,math,json,os
from math import pi,sin,cos
from mathutils import Vector
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art'/'valuables'/'heavy-bomb-reference-05';OUT.mkdir(parents=True,exist_ok=True)
NAME='Heavy_Bomb_Reference_05'
bpy.context.preferences.filepaths.file_preview_type='NONE'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1

def mat(name,color,metal=.2,rough=.48):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    return m
olive=mat('Olive painted casing',(.16,.17,.085),.22,.47)
yellow=mat('Yellow painted bands',(.92,.68,.025),.10,.50)
red=mat('Red paint blocks',(.56,.018,.035),.10,.53)
dark=mat('Dark circumferential seams',(.025,.030,.015),.24,.59)
finmat=mat('Olive tail fins',(.100,.113,.049),.26,.52)
capmat=mat('Ochre nose cap',(.78,.40,.016),.26,.36)
rim=mat('Nose rim and rivets',(.52,.30,.025),.38,.38)
Z=.85

def mesh(name,vs,fs,materials):
    me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.update()
    ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob)
    for m in materials:me.materials.append(m)
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
    return ob

def lathe(name,profile,materials,n=48,paint=None):
    # Axial X profile with closed solid end caps; shell paint is part of the mesh.
    vs=[(x,r*cos(2*pi*j/n),Z+r*sin(2*pi*j/n)) for x,r in profile for j in range(n)]
    fs=[];indices=[]
    for k in range(len(profile)-1):
        x=(profile[k][0]+profile[k+1][0])/2
        for j in range(n):
            a=k*n+j;b=k*n+(j+1)%n;fs.append((a,b,b+n,a+n))
            indices.append(paint(x,2*pi*(j+.5)/n) if paint else 0)
    fs.extend([tuple(reversed(range(n))),tuple(range((len(profile)-1)*n,len(profile)*n))])
    ob=mesh(name,vs,fs,materials)
    for i,p in enumerate(ob.data.polygons):
        p.use_smooth=i<len(indices)
        if i<len(indices):p.material_index=indices[i]
    return ob

profile=[(-1.40,.17),(-1.29,.27),(-1.14,.41),(-.98,.58),(-.82,.72),(-.67,.795),
         (-.48,.83),(-.20,.84),(.10,.84),(.37,.82),(.59,.74),(.76,.60),(.88,.43),(.94,.30)]
bands=[(-.86,-.79),(.47,.56)]
seams=[-.62,-.45,-.28,-.11,.06]
def radius(x):
    for (x0,r0),(x1,r1) in zip(profile,profile[1:]):
        if x0<=x<=x1:return r0+(r1-r0)*(x-x0)/(x1-x0)
    raise ValueError(x)
xs={x for x,r in profile}
for a,b in bands:xs.update([a,b])
for x in seams:xs.update([x-.0035,x+.0035])
def paint(x,a):
    if any(lo<x<hi for lo,hi in bands):
        return 2 if (.70*pi<a<.84*pi or 1.70*pi<a<1.84*pi) else 1
    if any(abs(x-s)<.0036 for s in seams):return 3
    return 0
lathe('Bomb_Barrel_And_Tapered_Rear',[(x,radius(x)) for x in sorted(xs)],[olive,yellow,red,dark],paint=paint)
lathe('Bomb_TailHub',[(-1.76,.15),(-1.72,.18),(-1.42,.18),(-1.35,.16)],[finmat],32)
for i in range(4):
    a=i*pi/2+pi/4;radial=Vector((0,cos(a),sin(a)));tangent=Vector((0,-sin(a),cos(a)))
    # Exposed swept stabilizer panels, without the earlier rectangular tail box.
    outline=[(-1.73,.17),(-1.73,.63),(-1.40,.78),(-.95,.54),(-1.12,.27)]
    vs=[tuple(Vector((x,0,Z))+radial*r+tangent*t) for t in [-.010,.010] for x,r in outline]
    n=len(outline);fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    fs.extend((j,(j+1)%n,(j+1)%n+n,j+n) for j in range(n))
    mesh('Bomb_Exposed_TailFin_%d'%i,vs,fs,[finmat])

lathe('Bomb_Yellow_Nose_Rim',[(.916,.302),(.932,.345),(.963,.345),(.981,.310)],[rim])
lathe('Bomb_Yellow_Rounded_Nose',[(.965,.302),(.995,.299),(1.035,.275),(1.072,.222),(1.098,.135),(1.107,.025)],[capmat])
for i in range(8):
    a=(i+.5)*2*pi/8
    bpy.ops.mesh.primitive_uv_sphere_add(segments=8,ring_count=4,radius=1,location=(.971,.326*cos(a),Z+.326*sin(a)))
    ob=bpy.context.object;ob.name='Bomb_Nose_Rivet_%02d'%i;ob.scale=(.006,.008,.008);ob.data.materials.append(rim)

parts=[o for o in scene.objects if o.type=='MESH'];bpy.context.view_layer.update()
low=min((o.matrix_world@v.co).z for o in parts for v in o.data.vertices)
for ob in parts:ob.location.z-=low
bpy.context.view_layer.update();bpy.ops.object.select_all(action='DESELECT')
triangles=vertices=0
for ob in parts:
    ob.select_set(True);bpy.context.view_layer.objects.active=ob
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    mod=ob.modifiers.new('Export triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.02);bpy.ops.object.mode_set(mode='OBJECT')
    bm=bmesh.new();bm.from_mesh(ob.data)
    assert all(e.is_manifold for e in bm.edges),ob.name
    assert all(f.calc_area()>1e-11 for f in bm.faces),ob.name
    triangles+=len(bm.faces);vertices+=len(bm.verts);bm.free();ob.select_set(False)
points=[ob.matrix_world@v.co for ob in parts for v in ob.data.vertices]
bounds=[max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)]
root=bpy.data.objects.new(NAME,None);bpy.context.collection.objects.link(root)
for ob in parts:ob.parent=root;ob.select_set(True)
root.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(OUT/(NAME+'.glb')),export_format='GLB',use_selection=True,export_yup=True)
bpy.ops.wm.obj_export(filepath=str(OUT/(NAME+'.obj')),export_selected_objects=True,forward_axis='NEGATIVE_Z',up_axis='Y',export_uv=True,export_materials=True,export_normals=True)
stats={'asset':NAME,'triangles':triangles,'vertices':vertices,'mesh_parts':len(parts),'materials':7,'bounds_m_xyz':bounds,
       'geometry_checks':'PASS: closed components, no degenerate triangles, UVs present','reference':'User-supplied museum photograph; external appearance only. Inferred side/rear proportions.',
       'scope':'Visual game prop only; no gameplay implementation.'}
(OUT/'validation.json').write_text(json.dumps([stats],indent=2))

def look(ob,target):ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
floor=mat('Preview | dark studio',(.06,.07,.08),0,.8)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.006));bpy.context.object.name='Preview_Ground';bpy.context.object.data.materials.append(floor)
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True
scene.render.resolution_x=1300;scene.render.resolution_y=1050;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.world.color=(.20,.20,.20)
target=Vector((-.30,0,bounds[2]*.47))
for loc,power,size in [((-3,-4,5),700,4),((4,-1,3),500,3),((0,4,4),850,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc);ob=bpy.context.object;ob.name='Preview_Softbox';ob.data.energy=power;ob.data.shape='DISK';ob.data.size=size;look(ob,target)
bpy.ops.object.camera_add();cam=bpy.context.object;cam.name='Preview_Camera';cam.data.type='ORTHO';cam.data.ortho_scale=3.6;cam.data.clip_end=1000;scene.camera=cam
for label,direction in [('threequarter',(2.8,-4.5,2.0)),('side',(0,-5,.65)),('rear',(-3,4.4,1.9))]:
    cam.location=target+Vector(direction);look(cam,target)
    scene.render.filepath=str(OUT/(NAME+'_'+label+'.png'));bpy.ops.render.render(write_still=True)
cam.location=target+Vector((2.8,-4.5,2.0));look(cam,target)
bpy.ops.object.select_all(action='DESELECT');root.select_set(True);bpy.context.view_layer.objects.active=root
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(NAME+'.blend')))
print('BOMB_REFERENCE_05_BUILT',json.dumps(stats),flush=True)
