"""Restore the approved cut-quartz material and remove projecting supraorbital shelves."""
import bpy,math,json,sys,shutil
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
import rework_valuables08 as prev
ROOT=prev.ROOT;OUT=ROOT/'art/valuables/skull-quartz-10'

def create_skull():
    prev.load('crystal_skull')
    parts=[o for o in bpy.context.scene.objects if o.type=='MESH']
    head=next(o for o in parts if o.name=='Skull_Upper_Cranium_Face')
    inverse=head.matrix_world.inverted()
    for vertex in head.data.vertices:
        p=head.matrix_world@vertex.co
        # Reduce the anterior zygomatic knobs into a thin arch with a broad, seamless falloff.
        lateral=max(0,min(1,(abs(p.x)-.045)/.025))
        front=max(0,min(1,(.05-p.y)/.06))
        w=math.exp(-((p.z-.139)/.027)**4)*lateral*front
        p.x-=math.copysign(.018*w,p.x);p.y+=.020*w
        p.z=.139+(p.z-.139)*(1-.65*w)
        vertex.co=inverse@p
    # Recut larger, rounded-square orbits. This removes the old shelf volume itself,
    # including its lateral ends, instead of disguising it with shading or a vertex dent.
    for side in [-1,1]:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=28,ring_count=18,radius=1,location=(side*.045,-.065,.178))
        cutter=bpy.context.object;cutter.name='temporary orbital clearance'
        for vertex in cutter.data.vertices:
            x,y,z=vertex.co
            vertex.co=(math.copysign(abs(x)**.85,x)*.041,y*.084,math.copysign(abs(z)**.85,z)*.035)
        cutter.data.materials.append(head.data.materials[0])
        bpy.context.view_layer.objects.active=head;mod=head.modifiers.new('Remove projecting orbital shelf','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter;bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.data.objects.remove(cutter,do_unlink=True)
    for ob in parts:
        if ob.name.startswith(('Skull_Curved_Mandible','Skull_Lower_Tooth')):ob.location.z+=.004
        ob['group']='static'
    bpy.context.view_layer.update()
    low=min((o.matrix_world@v.co).z for o in parts for v in o.data.vertices)
    for o in parts:o.location.z-=low
    for o in parts:
        for poly in o.data.polygons:poly.use_smooth=False
        o.data.normals_split_custom_set([(0,0,0)]*len(o.data.loops));o.data.update()
    bpy.context.view_layer.update()
    return parts

def build():
    prev.kit.reset();OUT.mkdir(parents=True,exist_ok=True)
    create_skull();parts=prev.prepare_parts()
    for o in parts:
        for p in o.data.polygons:p.use_smooth=False
        o.data.update()
    parts=prev.merge_static(parts,'crystal_skull');root=bpy.data.objects.new('crystal_skull',None);bpy.context.collection.objects.link(root)
    for o in parts:
        o.parent=root
        # Boolean cuts can retain custom normals from the old source. Quartz uses exact facet normals.
        normals=[Vector((0,0,0)) for _ in o.data.loops]
        for poly in o.data.polygons:
            a,b,c=[o.data.vertices[i].co for i in poly.vertices[:3]];n=(b-a).cross(c-a).normalized()
            for li in poly.loop_indices:normals[li]=n
        o.data.normals_split_custom_set(normals);o.data.update()
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    root.select_set(True)
    data=prev.runtime_export('crystal_skull',parts,OUT)
    path=OUT/'v09_crystal_skull.mesh.json';(OUT/'v08_crystal_skull.mesh.json').replace(path)
    bpy.ops.export_scene.gltf(filepath=str(OUT/'crystal_skull.glb'),export_format='GLB',use_selection=True,export_yup=True,export_animations=False)
    bpy.ops.wm.obj_export(filepath=str(OUT/'crystal_skull.obj'),export_selected_objects=True,forward_axis='NEGATIVE_Z',up_axis='Y',export_materials=True,export_uv=True,export_normals=True,path_mode='RELATIVE')
    points=[o.matrix_world@v.co for o in parts for v in o.data.vertices]
    report={'asset':'crystal_skull','triangles':sum(len(o.data.polygons) for o in parts),'mesh_parts':len(parts),'runtime_parts':len(data['parts']),'bounds_m_xyz':[max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)],'groups':['static'],'revision':'quartz-10-cheek-refinement','material':'Approved colorless cut quartz; IOR 1.46, transmission 1, roughness 0.055','shape':'Slim, recessed cheek arches; flush orbital rims; nearly closed jaw; user-supplied shape reference'}
    cam,center,size=prev.present('crystal_skull',parts,OUT)
    scene=bpy.context.scene;scene.cycles.samples=64
    # Matching near-front views reveal shelves and cheek shape in both materials.
    cam.location=center+Vector((.75,-3, .48))*size;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(OUT/'quartz_shape_view.png');bpy.ops.render.render(write_still=True)
    original=[list(o.data.materials) for o in parts]
    clay=prev.solid('Preview only neutral clay',(.48,.52,.52),.72)
    for o in parts:o.data.materials.clear();o.data.materials.append(clay)
    scene.render.filepath=str(OUT/'clay_shape_view.png');bpy.ops.render.render(write_still=True)
    for o,mats in zip(parts,original):
        o.data.materials.clear()
        for m in mats:o.data.materials.append(m)
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'crystal_skull.blend'))
    (OUT/'manifest.json').write_text(json.dumps([report],indent=2))
    target=ROOT/'art/valuables/interaction-rework-09/crystal_skull'
    for name in ['crystal_skull.blend','crystal_skull.glb','crystal_skull.obj','crystal_skull.mtl','crystal_skull_front.png','crystal_skull_rear.png','v09_crystal_skull.mesh.json']:shutil.copyfile(OUT/name,target/name)
    shutil.copyfile(path,ROOT/'src/Art'/path.name)
    manifest=target.parent/'manifest.json';rows=json.loads(manifest.read_text());rows=[r for r in rows if r['asset']!='crystal_skull']+[report];manifest.write_text(json.dumps(rows,indent=2))
    print('SKULL_QUARTZ10_EXPORTED',report,flush=True)

if __name__=='__main__':build()
