"""Validate mesh reimports, portable textures and the actual exported liquid-timer loop."""
import bpy,bmesh,json,math,struct,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/valuables/cannon-water-timer-07'
sys.path.insert(0,str(Path(__file__).resolve().parent))
from merge_gltf_loop import merge_loop
merge_loop(OUT/'water_timer/water_timer.glb')
rows=json.loads((OUT/'manifest.json').read_text());assert {r['asset'] for r in rows}=={'cannon','water_timer'}

# Correct the static OBJ pose independently of the animated GLB.
folder=OUT/'water_timer';bpy.ops.wm.open_mainfile(filepath=str(folder/'water_timer.blend'),load_ui=False);bpy.context.scene.frame_set(20)
bpy.ops.object.select_all(action='DESELECT');root=bpy.data.objects['water_timer'];root.select_set(True)
for ob in root.children:ob.select_set(True)
bpy.ops.wm.obj_export(filepath=str(folder/'water_timer.obj'),export_selected_objects=True,forward_axis='NEGATIVE_Z',up_axis='Y',export_uv=True,export_materials=True,export_normals=True,path_mode='RELATIVE')

for row in rows:
    name=row['asset'];folder=OUT/name;mtl=folder/(name+'.mtl');lines=[]
    for line in mtl.read_text().splitlines():
        if line.startswith('map_Kd '):line='map_Kd ../textures/'+Path(line[7:].replace('\\','/')).name
        lines.append(line)
    mtl.write_text('\n'.join(lines)+'\n');checks={}
    for kind in ['glb','obj']:
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        if kind=='glb':bpy.ops.import_scene.gltf(filepath=str(folder/(name+'.glb')))
        else:
            for line in mtl.read_text().splitlines():
                if line.startswith('map_Kd '):assert not Path(line[7:]).is_absolute() and (folder/line[7:]).is_file()
            bpy.ops.wm.obj_import(filepath=str(folder/(name+'.obj')),forward_axis='NEGATIVE_Z',up_axis='Y')
        bpy.context.view_layer.update();parts=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(parts)==row['mesh_parts']
        tri=0;points=[]
        for ob in parts:
            assert ob.data.uv_layers and all(m for m in ob.data.materials)
            tri+=sum(len(p.vertices)-2 for p in ob.data.polygons);points.extend(ob.matrix_world@v.co for v in ob.data.vertices)
            bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
            assert all(e.is_manifold for e in bm.edges),(name,kind,ob.name,'nonmanifold')
            assert all(f.calc_area()>1e-13 for f in bm.faces),(name,kind,ob.name,'degenerate');bm.free()
        assert tri==row['triangles'];assert all(math.isfinite(v) for p in points for v in p)
        dims=[max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)]
        assert all(abs(a-b)<3e-5 for a,b in zip(dims,row['bounds_m_xyz'])),(name,kind,dims)
        assert abs(min(p.z for p in points))<3e-5
        if kind=='glb' and name=='water_timer':
            animated=[o for o in parts if o.animation_data]
            assert len(animated)>=6,'Animation missing after GLB import'
            ob=next(o for o in animated if o.name.startswith('Timer_Drop_Blue_0'))
            a=ob.animation_data.action.frame_range;start,end=float(a[0]),float(a[1])
            bpy.context.scene.frame_set(int(start));p0=ob.location.copy();s0=ob.scale.copy()
            bpy.context.scene.frame_set(int(start+8));assert (ob.location-p0).length>.003,'Reimported droplet is static'
            bpy.context.scene.frame_set(int(end));assert (ob.location-p0).length<1e-5 and (ob.scale-s0).length<1e-5,'Imported loop seam'
        checks[kind]={'status':'PASS','triangles':tri,'mesh_parts':len(parts),'geometry_uv_materials_scale':'PASS'}
    bpy.ops.wm.open_mainfile(filepath=str(folder/(name+'.blend')),load_ui=False);assert name in bpy.data.objects
    if name=='water_timer':
        drops=[o for o in bpy.data.objects[name].children if o.animation_data];assert len(drops)==6
        poses={}
        for frame in [1,9,17,25,49,73,121,145]:
            bpy.context.scene.frame_set(frame);poses[frame]=[(o.location.copy(),o.scale.copy()) for o in drops]
            for o in drops:
                for corner in o.bound_box:
                    p=o.matrix_world@Vector(corner);assert -.052<p.x<.052 and -.020<p.y<.020 and .001<p.z<.259,'Drop leaves casing'
        assert any((a[0]-b[0]).length>.005 for a,b in zip(poses[1],poses[9]))
        assert all((a[0]-b[0]).length<1e-6 and (a[1]-b[1]).length<1e-6 for a,b in zip(poses[1],poses[145]))
        checks['source_animation']={'status':'PASS','droplets':6,'loop_seam':'PASS','sampled_casing_containment':'PASS'}
    row['checks']=checks;row['blend_reopen']='PASS';print('VALIDATED',name,flush=True)

path=OUT/'water_timer/water_timer.glb'
with path.open('rb') as f:
    magic,version,length=struct.unpack('<4sII',f.read(12));assert magic==b'glTF' and version==2 and length==path.stat().st_size
    size,kind=struct.unpack('<II',f.read(8));g=json.loads(f.read(size));size,kind=struct.unpack('<II',f.read(8));binary=f.read(size)
assert len(g.get('animations',[]))==1
def read_accessor(idx):
    a=g['accessors'][idx];v=g['bufferViews'][a['bufferView']];assert a['componentType']==5126
    n={'SCALAR':1,'VEC3':3,'VEC4':4}[a['type']];offset=v.get('byteOffset',0)+a.get('byteOffset',0);stride=v.get('byteStride',n*4)
    return [struct.unpack_from('<'+'f'*n,binary,offset+i*stride) for i in range(a['count'])]
changed=0;nodes=set();duration=0
for channel in g['animations'][0]['channels']:
    sampler=g['animations'][0]['samplers'][channel['sampler']];ts=read_accessor(sampler['input']);values=read_accessor(sampler['output'])
    duration=max(duration,ts[-1][0]);nodes.add(g['nodes'][channel['target']['node']]['name'])
    assert max(abs(a-b) for a,b in zip(values[0],values[-1]))<1e-5,'GLB animation endpoint mismatch'
    if any(any(abs(a-b)>1e-4 for a,b in zip(values[0],value)) for value in values[1:]):changed+=1
assert abs(duration-6)<1e-5 and len(nodes)==6 and changed>=12,(duration,len(nodes),changed)
next(r for r in rows if r['asset']=='water_timer')['glb_animation']={'status':'PASS','clips':1,'duration_seconds':duration,'animated_nodes':len(nodes),'changing_channels':changed,'endpoint_match':True,'reimport_motion':'PASS'}
(OUT/'manifest.json').write_text(json.dumps(rows,indent=2));print('ALL_CANNON_TIMER_CHECKS_PASSED',flush=True)
