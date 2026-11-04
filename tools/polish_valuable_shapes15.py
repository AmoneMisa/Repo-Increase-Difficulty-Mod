"""Round manufactured edges of the current collection, preserving runtime bindings.

Uses existing approved source shapes/atlases; no gameplay settings are modified.
An explicit immutable pre-pass runtime snapshot makes regeneration idempotent.
"""
import bpy, bmesh, json, math, shutil, sys
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/shape-polish-15/valuables';OUT.mkdir(parents=True,exist_ok=True)
BASE=ROOT/'work/shape-polish-15'
C=Matrix(((1,0,0),(0,0,1),(0,1,0)))
SOURCES={
 'cannon':('v11','lighting-collection-11'), 'bomb':('v11','lighting-collection-11'),
 'robot_vacuum':('v11','lighting-collection-11'), 'graphics_card':('v11','lighting-collection-11'),
 'gold_ingot':('v11','lighting-collection-11'), 'centrifuge':('v10','physics-rework-10'),
 'ram_pair':('v09','interaction-rework-09'), 'monoblock_pc':('v09','interaction-rework-09'),
 'bath_gift_set':('v09','interaction-rework-09'), 'skeleton':('v09','interaction-rework-09'),
 'spritz':('v09','interaction-rework-09'), 'water_timer':('v09','interaction-rework-09')}

def xyz(v):return {'x':float(v[0]),'y':float(v[1]),'z':float(v[2])}
def v3(v):return Vector(tuple(v[c] for c in 'xyz'))
def bounds(objects):
    p=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
    return [[min(v[k] for v in p) for k in range(3)],[max(v[k] for v in p) for k in range(3)]]

def round_wheel_rim(ob):
    """A real 80-section circular rim, with the old atlas transferred spatially."""
    me=ob.data;me.calc_loop_triangles()
    verts=[v.co.copy() for v in me.vertices]
    triangles=[tuple(t.vertices) for t in me.loop_triangles]
    uvs=[[Vector((*me.uv_layers.active.data[li].uv,0)) for li in t.loops] for t in me.loop_triangles]
    tree=BVHTree.FromPolygons(verts,triangles,all_triangles=True)
    low=Vector([min(v[k] for v in verts) for k in range(3)]);high=Vector([max(v[k] for v in verts) for k in range(3)])
    center=(low+high)/2;half=(high.x-low.x)/2
    radii=[math.hypot(v.y-center.y,v.z-center.z) for v in verts]
    inner,outer=min(radii),max(radii);middle=(inner+outer)/2;radial=(outer-inner)/2
    bevel=min(half*.32,radial*.40);profile=[]
    for cx,cy,start in [(half-bevel,radial-bevel,0),(-half+bevel,radial-bevel,90),(-half+bevel,-radial+bevel,180),(half-bevel,-radial+bevel,270)]:
        for j in range(4):
            a=math.radians(start+j*30);profile.append((cx+bevel*math.cos(a),middle+cy+bevel*math.sin(a)))
    n=80;q=len(profile)
    newverts=[tuple(center+Vector((x,r*math.cos(i*math.tau/n),r*math.sin(i*math.tau/n)))) for i in range(n) for x,r in profile]
    faces=[(i*q+j,((i+1)%n)*q+j,((i+1)%n)*q+(j+1)%q,i*q+(j+1)%q) for i in range(n) for j in range(q)]
    new=bpy.data.meshes.new(me.name+' circular rim');new.from_pydata(newverts,[],faces);new.update()
    for m in me.materials:new.materials.append(m)
    layer=new.uv_layers.new(name='UVMap')
    for poly in new.polygons:
        for li in poly.loop_indices:
            p=new.vertices[new.loops[li].vertex_index].co;nearest,_,idx,_=tree.find_nearest(p)
            uv=barycentric_transform(nearest,*(verts[v] for v in triangles[idx]),*uvs[idx]);layer.data[li].uv=uv.xy
    bm=bmesh.new();bm.from_mesh(new);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(new);bm.free()
    ob.data=new

def soften_component(ob,key,asset_size):
    bpy.context.view_layer.objects.active=ob
    dims=sorted(ob.dimensions)
    if not dims[0] or dims[0]<.0004:return
    if key=='cannon' and ob.dimensions.x<.12 and ob.dimensions.y>.9 and ob.dimensions.z>.9:
        round_wheel_rim(ob);return
    bm=bmesh.new();bm.from_mesh(ob.data)
    # Only remove diagonal edges where both geometry AND atlas are continuous.
    bmesh.ops.dissolve_limit(bm,angle_limit=.001,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV','MATERIAL','SEAM'})
    bm.to_mesh(ob.data);bm.free()
    if len(ob.data.polygons)<4:return
    width=min(dims[0]*.16,dims[1]*.045,.018 if key=='cannon' else .008)
    if key=='bath_gift_set':width=min(width,.0018)
    if key=='water_timer':width=min(width,.001)
    # Sub-pixel bevels on contacts, screws and small bones waste triangles.
    if width<max(.00035,asset_size*.0012):return
    bevel=ob.modifiers.new('Rounded physical edge radius','BEVEL')
    bevel.width=width;bevel.segments=3 if key=='cannon' else 2;bevel.limit_method='ANGLE';bevel.angle_limit=math.radians(28)
    bevel.use_clamp_overlap=True;bevel.loop_slide=True
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    ob.data.validate();ob.data.update()

def polish_object(ob,key,asset_size):
    name=ob.name;group=ob.get('group','static')
    # Liquid shape sampling is part of an existing motion system, not a hard case.
    if key=='water_timer' and group in ('drop','top_pool','bottom_pool'):return
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.separate(type='LOOSE');bpy.ops.object.mode_set(mode='OBJECT')
    pieces=list(bpy.context.selected_objects)
    for p in pieces:
        bpy.ops.object.select_all(action='DESELECT');p.select_set(True)
        if not (key=='skeleton' and group=='head'):soften_component(p,key,asset_size)
        bm=bmesh.new();bm.from_mesh(p.data)
        bmesh.ops.dissolve_degenerate(bm,dist=1e-7,edges=list(bm.edges))
        bmesh.ops.triangulate(bm,faces=list(bm.faces),quad_method='BEAUTY',ngon_method='BEAUTY')
        # Clipped bevels can leave long, sub-micron slivers: unstable after float32 export.
        tiny=[f for f in bm.faces if f.calc_area()<max(1e-13,asset_size*asset_size*1e-8)]
        if tiny:bmesh.ops.delete(bm,geom=tiny,context='FACES_ONLY')
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(p.data);bm.free();p.data.update()
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(52),keep_sharp_edges=False)
        if key not in ('cannon','skeleton','bomb','spritz','robot_vacuum','centrifuge'):
            normal=p.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL');normal.keep_sharp=True;normal.weight=40
            bpy.ops.object.modifier_apply(modifier=normal.name)
    bpy.ops.object.select_all(action='DESELECT')
    for p in pieces:p.select_set(True)
    bpy.context.view_layer.objects.active=ob
    if len(pieces)>1:bpy.ops.object.join()
    ob.name=name;ob['group']=group

def render(key,parts,folder):
    scene=bpy.context.scene
    # Retain original presentation lights/camera for consistent before/after framing.
    scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
    scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    scene.render.film_transparent=False
    b=bounds(parts);lo,hi=map(Vector,b);center=(lo+hi)/2;size=max(hi-lo)
    ground=bpy.data.objects.get('Preview_Ground')
    if ground:ground.location.z=lo.z-.014
    cam=scene.camera
    if not cam:
        bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam
    cam.data.type='ORTHO';cam.data.ortho_scale=size*1.3;cam.data.clip_end=1000
    cam.location=center+Vector((1.4,-3,1.1))*size
    cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(folder/(key+'_preview.png'));bpy.ops.render.render(write_still=True)

def build(key):
    rev,source=SOURCES[key];resource=rev+'_'+key+'.mesh.json'
    baseline=ROOT/f'art/valuables/{source}/{key}'/resource
    if not baseline.exists():raise RuntimeError('Missing archived source '+str(baseline))
    data=json.loads(baseline.read_text());folder=OUT/key;folder.mkdir(exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/f'art/valuables/{source}/{key}/{key}.blend'),load_ui=False)
    bpy.context.preferences.filepaths.save_version=0;bpy.context.preferences.filepaths.file_preview_type='NONE'
    bpy.context.scene.frame_set(1)
    names={p['name'] for p in data['parts']}
    parts=[bpy.data.objects[n] for n in names]
    bpy.context.view_layer.update();before=bounds(parts)
    poses={o:o.matrix_world.copy() for o in parts}
    # Runtime handles motion by group; keep component-local origins unchanged in JSON.
    for ob in parts:
        ob.parent=None;ob.matrix_world=poses[ob]
        if key!='water_timer' and ob.animation_data:ob.animation_data_clear()
    if key=='spritz':
        # The item exporter subtracts the syringe needle origin; the source blend does not.
        runtime_points=[C@v3(p['rest']['position'])+C@v3(v) for p in data['parts'] for v in p['vertices']]
        target=Vector([(min(p[k] for p in runtime_points)+max(p[k] for p in runtime_points))*.5 for k in range(3)])
        origin=(Vector(before[0])+Vector(before[1]))*.5-target
        for ob in parts:ob.location-=origin
        bpy.context.view_layer.update();before=bounds(parts)
    asset_size=max(Vector(before[1])-Vector(before[0]))
    for ob in list(parts):polish_object(ob,key,asset_size)
    bpy.context.view_layer.update();after=bounds(parts)
    for record in data['parts']:
        if key=='water_timer' and record['group'] in ('drop','top_pool','bottom_pool'):continue
        ob=bpy.data.objects[record['name']]
        source_mat=data['materials'][record['material']]['name']
        slot=next(i for i,m in enumerate(ob.data.materials) if m.name==source_mat)
        me=ob.data;me.calc_loop_triangles();normal_matrix=ob.matrix_world.to_3x3().inverted().transposed()
        vs=[];ns=[];uv=[];ts=[];center=v3(record['rest']['position'])
        for tri in me.loop_triangles:
            if tri.material_index!=slot:continue
            for li in reversed(tri.loops):
                vs.append(xyz(C@(ob.matrix_world@me.vertices[me.loops[li].vertex_index].co)-center))
                n=(C@normal_matrix@me.corner_normals[li].vector).normalized()
                # Concave bevel junctions must not leave a smoothed normal behind the face.
                if me.corner_normals[li].vector.dot(tri.normal)<=0:n=(C@normal_matrix@tri.normal).normalized()
                ns.append(xyz(n));p=me.uv_layers.active.data[li].uv;uv.append({'x':float(p.x),'y':float(p.y)});ts.append(len(ts))
        assert vs,(key,record['name'],source_mat)
        record.update(vertices=vs,normals=ns,uv=uv,triangles=ts)
    for material in data['materials']:
        if material['texture']:shutil.copyfile(ROOT/'src/Art'/material['texture'],folder/material['texture'])
    for ob in parts:
        for mat in ob.data.materials:
            expected=next((m['texture'] for m in data['materials'] if m['name']==mat.name),'')
            for node in mat.node_tree.nodes:
                if node.type=='TEX_IMAGE' and node.image:
                    if expected:
                        node.image=bpy.data.images.load(str(folder/expected),check_existing=True)
                    name=Path(node.image.filepath.replace('\\','/')).name
                    if (folder/name).exists():node.image.filepath=str(folder/name)
                    if not node.image.packed_file:node.image.pack()
    (folder/resource).write_text(json.dumps(data,separators=(',',':')))
    shutil.copyfile(folder/resource,ROOT/'src/Art'/resource)
    if key=='spritz':shutil.copyfile(folder/resource,ROOT/'src/Art/spritz.mesh.json')
    bpy.ops.object.select_all(action='DESELECT')
    for ob in parts:ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(folder/(key+'.glb')),use_selection=True,export_format='GLB',export_animations=key=='water_timer',export_force_sampling=True)
    bpy.ops.wm.obj_export(filepath=str(folder/(key+'.obj')),export_selected_objects=True,path_mode='RELATIVE',forward_axis='NEGATIVE_Z',up_axis='Y')
    mtl=folder/(key+'.mtl');lines=[];current=''
    texture_names={m['name'].replace(' ','_'):m['texture'] for m in data['materials']}
    for line in mtl.read_text().splitlines():
        if line.startswith('newmtl '):current=line[7:]
        if line.startswith('map_Kd '):line='map_Kd '+(texture_names.get(current) or Path(line.split()[-1].replace('\\','/')).name)
        elif line.startswith('map_'):line=line.split()[0]+' '+Path(line.split()[-1].replace('\\','/')).name
        lines.append(line)
    mtl.write_text('\n'.join(lines)+'\n')
    render(key,parts,folder)
    bpy.ops.wm.save_as_mainfile(filepath=str(folder/(key+'.blend')))
    row={'asset':key,'runtime':resource,'triangles_before':sum(len(p['triangles'])//3 for p in json.loads(baseline.read_text())['parts']),'triangles_after':sum(len(p['triangles'])//3 for p in data['parts']),'bounds_before_xyz':before,'bounds_after_xyz':after,'groups':sorted({p['group'] for p in data['parts']}),'runtime_bindings_preserved':True}
    (folder/'changes.json').write_text(json.dumps(row,indent=2));print('POLISH_COMPLETE',json.dumps(row),flush=True)

if __name__=='__main__':
    keys=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else list(SOURCES)
    for key in keys:build(key)
