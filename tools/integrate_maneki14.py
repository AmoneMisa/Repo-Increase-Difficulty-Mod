"""Approved reference-14 model: shared-clock motion, collision hulls and runtime data.

Portable files are metres. Runtime vertices are half size because the current
project deliberately scales the maneki prefab by 2; net physical size is identical.
"""
import bpy,bmesh,math,json,shutil
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[1];SOURCE=ROOT/'art/valuables/maneki-reference-14'
OUT=ROOT/'art/valuables/maneki-integrated-14';OUT.mkdir(parents=True,exist_ok=True)
C=Matrix(((1,0,0),(0,0,1),(0,1,0)));S=.5
def v3(v):return {'x':float(v[0]),'y':float(v[1]),'z':float(v[2])}
def unity(v):return v3(C@v*S)
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'maneki_review.blend'),load_ui=False)
bpy.context.preferences.filepaths.save_version=0;bpy.context.preferences.filepaths.file_preview_type='NONE'
scene=bpy.context.scene
parts=[o for o in scene.objects if o.type=='MESH' and 'stage' in o]
arm=next(o for o in parts if o.name.startswith('Raised right arm'))
body=next(o for o in parts if o.name.startswith('Porcelain body'))
head=next(o for o in parts if o.name.startswith('Porcelain head'))
pivot=arm.parent;pivot.name='Beckoning paw pivot'
bpy.context.view_layer.update()
worlds={o:[o.matrix_world@v.co for v in o.data.vertices] for o in parts}
# Twelve millimetres of shoulder clearance removes a hidden cheek intersection
# at the back of the sweep without changing the approved paw/arm proportions.
for point in worlds[arm]:point.x+=.012
pivot.location.x+=.012
for ob,points in worlds.items():
    ob.parent=None;ob.matrix_world=Matrix.Identity(4)
    for vert,point in zip(ob.data.vertices,points):vert.co=point
    ob.data.update();ob['stage']='Appearance approved 2026-11-04; integrated runtime model'
hinge=pivot.location.copy()
for vert in arm.data.vertices:vert.co-=hinge
arm.parent=pivot;arm.matrix_parent_inverse=Matrix.Identity(4);arm.location=(0,0,0)
bpy.context.view_layer.update()

def surface_tree(ob,world=False):
    me=ob.data;me.calc_loop_triangles()
    return BVHTree.FromPolygons([ob.matrix_world@v.co if world else v.co for v in me.vertices],[tuple(t.vertices) for t in me.loop_triangles],all_triangles=True)

headtree=surface_tree(head,True);bodytree=surface_tree(body,True)
mins=Vector((float('inf'),)*3);maxs=Vector((-float('inf'),)*3)
minimum_embedded=10**9;overlaps=[]
for frame in range(161):
    angle=math.radians(-(3-15*math.cos(math.tau*frame/160)))
    pivot.rotation_euler.x=angle;bpy.context.view_layer.update()
    for ob in parts:
        for vert in ob.data.vertices:
            p=ob.matrix_world@vert.co
            for k in range(3):mins[k]=min(mins[k],p[k]);maxs[k]=max(maxs[k],p[k])
    armtree=surface_tree(arm,True);hits=headtree.overlap(armtree)
    if hits:overlaps.append({'sample':frame,'intersecting_triangle_pairs':len(hits)})
    embedded=0
    for vert in arm.data.vertices:
        p=arm.matrix_world@vert.co
        if (p-hinge).length>.15:continue
        close,normal,_,_=bodytree.find_nearest(p)
        if (p-close).dot(normal)<-.001:embedded+=1
    minimum_embedded=min(minimum_embedded,embedded)
assert not overlaps,('Paw intersects head',overlaps[:5])
assert minimum_embedded>=8,('Shoulder connection not maintained',minimum_embedded)
pivot.rotation_euler.x=0;bpy.context.view_layer.update()

# A convex support hull for each real main volume; decorations never get phantom boxes.
def hull(ob,moving):
    pts=[v.co.copy() if moving else ob.matrix_world@v.co for v in ob.data.vertices]
    directions=[Vector(s).normalized() for s in [(1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)]]
    for i in range(80):
        z=1-2*(i+.5)/80;a=i*math.pi*(3-math.sqrt(5));r=math.sqrt(1-z*z);directions.append(Vector((r*math.cos(a),r*math.sin(a),z)))
    selected={max(range(len(pts)),key=lambda i:pts[i].dot(d)) for d in directions}
    bm=bmesh.new()
    for i in selected:bm.verts.new(pts[i])
    bmesh.ops.convex_hull(bm,input=list(bm.verts),use_existing_faces=False)
    bmesh.ops.triangulate(bm,faces=list(bm.faces));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.verts.ensure_lookup_table();bm.verts.index_update()
    result={'name':ob.name,'moving':moving,'vertices':[unity(v.co) for v in bm.verts],'triangles':[v.index for f in bm.faces for v in reversed(list(f.verts))]}
    assert len(bm.faces)<=255;bm.free();return result
colliders=[hull(body,False),hull(head,False),hull(arm,True)]

# Hollow porcelain: area-weighted shell centroid, independent of donor/body bounds.
centroid=Vector();area_sum=0
for ob in (head,body,arm):
    ob.data.calc_loop_triangles()
    for t in ob.data.loop_triangles:
        a,b,c=[ob.matrix_world@ob.data.vertices[i].co for i in t.vertices];area=(b-a).cross(c-a).length*.5
        centroid+=(a+b+c)/3*area;area_sum+=area
centroid/=area_sum
materials=[];matindices={};records=[]
for ob in parts:
    me=ob.data;me.calc_loop_triangles();nm=ob.matrix_world.to_3x3().inverted().transposed()
    for slot,mat in enumerate(me.materials):
        if mat.name not in matindices:
            shader=mat.node_tree.nodes.get('Principled BSDF');tex=next((n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image),None)
            filename=(Path(tex.filepath).name or tex.name.split('.png')[0]+'.png') if tex else ''
            texture='v14_maneki_'+filename if tex else ''
            if tex:
                source=SOURCE/'textures'/filename
                shutil.copyfile(source,OUT/texture);shutil.copyfile(source,ROOT/'src/Art'/texture);tex.filepath=str(OUT/texture)
            col=shader.inputs['Base Color'].default_value
            matindices[mat.name]=len(materials)
            materials.append({'name':mat.name,'texture':texture,'color':{'r':col[0],'g':col[1],'b':col[2],'a':1},'roughness':shader.inputs['Roughness'].default_value,'metallic':shader.inputs['Metallic'].default_value,'transmission':0,'emission':0})
        vs=[];ns=[];uv=[];indices=[]
        for tri in me.loop_triangles:
            if tri.material_index!=slot:continue
            for li in reversed(tri.loops):
                vs.append(unity(ob.matrix_world@me.vertices[me.loops[li].vertex_index].co))
                ns.append(v3((C@nm@me.corner_normals[li].vector).normalized()));p=me.uv_layers.active.data[li].uv;uv.append({'x':p.x,'y':p.y});indices.append(len(indices))
        if not vs:continue
        records.append({'name':ob.name,'group':'beckoning_paw' if ob==arm else 'static','phase':0,'material':matindices[mat.name],'vertices':vs,'normals':ns,'uv':uv,'triangles':indices,'rest':{'position':v3(Vector()),'rotation':{'x':0,'y':0,'z':0,'w':1},'scale':{'x':1,'y':1,'z':1}},'samples':[]})

padding=Vector((.008,)*3)
physics={'centerOfMass':unity(centroid),'motionMin':unity(mins-padding),'motionMax':unity(maxs+padding),'colliders':colliders}
# Swapping Blender Y/Z preserves componentwise minima/maxima.
data={'version':1,'fps':30,'frames':81,'parts':records,'materials':materials,'beckonPivot':unity(hinge),'physics':physics}
resource='v11_maneki_neko.mesh.json';(OUT/resource).write_text(json.dumps(data,separators=(',',':')));shutil.copyfile(OUT/resource,ROOT/'src/Art'/resource)

bpy.ops.object.select_all(action='DESELECT')
for ob in parts:ob.select_set(True)
pivot.select_set(True)
bpy.ops.wm.obj_export(filepath=str(OUT/'maneki_neko.obj'),export_selected_objects=True,path_mode='RELATIVE',forward_axis='NEGATIVE_Z',up_axis='Y')
mtl=OUT/'maneki_neko.mtl';mtl.write_text('\n'.join('map_Kd '+Path(line.split()[-1].replace('\\','/')).name if line.startswith('map_Kd ') else line for line in mtl.read_text().splitlines())+'\n')
scene.render.fps=30;scene.frame_start=1;scene.frame_end=81
for frame in range(1,82):
    pivot.rotation_euler.x=math.radians(-(3-15*math.cos(math.tau*(frame-1)/80)))
    pivot.keyframe_insert(data_path='rotation_euler',frame=frame)
# Export frame samples as linear interpolation of the smooth shared-clock curve.
for layer in pivot.animation_data.action.layers:
    for strip in layer.strips:
        for bag in strip.channelbags:
            for f in bag.fcurves:
                for key in f.keyframe_points:key.interpolation='LINEAR'
scene.frame_set(1)
bpy.ops.export_scene.gltf(filepath=str(OUT/'maneki_neko.glb'),use_selection=True,export_format='GLB',export_animations=True,export_animation_mode='SCENE',export_force_sampling=True)
scene['approval_status']='User approved appearance 2026-11-04. Shared-clock fore/aft cycle; playtest still required.'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'maneki_neko.blend'))
scene.cycles.samples=24;scene.render.resolution_percentage=70
for frame in [1,21,41,61]:
    scene.frame_set(frame);scene.render.filepath=str(OUT/f'motion_{frame:02d}.png');bpy.ops.render.render(write_still=True)
report={'triangles':sum(len(p['triangles'])//3 for p in records),'period_seconds':80/30,'unity_angles_deg':[-12,18],'tested_cycle_samples':161,'head_intersections':len(overlaps),'minimum_embedded_shoulder_vertices':minimum_embedded,'physical_size_xyz_m':list(maxs-mins),'runtime_geometry_scale':S,'current_prefab_scale':2,'center_of_mass_method':'uniform hollow porcelain shell area centroid','collider_triangle_counts':[len(c['triangles'])//3 for c in colliders],'status':'geometry/motion checks passed; native game validation pending'}
(OUT/'motion-validation.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
