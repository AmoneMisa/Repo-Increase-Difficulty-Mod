"""Check the actual delivered mesh files, atlases, pivots, and runtime resources."""
import bpy,bmesh,json,math,struct,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/valuables/interaction-rework-09'
sys.path.insert(0,str(Path(__file__).resolve().parent))
from merge_gltf_loop import merge_loop
rows=json.loads((OUT/'manifest.json').read_text());assert len(rows)==12
merge_loop(OUT/'water_timer/water_timer.glb','Water_Watch_Slow_Mixing')
for row in rows:
    name=row['asset'];folder=OUT/name;checks={}
    for kind in ['glb','obj']:
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        if kind=='glb':bpy.ops.import_scene.gltf(filepath=str(folder/(name+'.glb')))
        else:
            for line in (folder/(name+'.mtl')).read_text().splitlines():
                if line.startswith('map_Kd '):assert not Path(line[7:]).is_absolute() and (folder/line[7:]).is_file(),line
            bpy.ops.wm.obj_import(filepath=str(folder/(name+'.obj')),forward_axis='NEGATIVE_Z',up_axis='Y')
        bpy.context.view_layer.update();parts=[o for o in bpy.context.scene.objects if o.type=='MESH']
        assert len(parts)==row['mesh_parts'],(name,kind,'part count',len(parts),row['mesh_parts'])
        points=[];tri=0
        for ob in parts:
            assert ob.data.uv_layers and all(m for m in ob.data.materials)
            points.extend(ob.matrix_world@v.co for v in ob.data.vertices);tri+=sum(len(p.vertices)-2 for p in ob.data.polygons)
            bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
            assert all(e.is_manifold for e in bm.edges),(name,kind,ob.name,'nonmanifold')
            assert all(f.calc_area()>1e-13 for f in bm.faces),(name,kind,ob.name,'degenerate')
            bm.free()
        assert tri==row['triangles'];assert all(math.isfinite(c) for p in points for c in p)
        dims=[max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)]
        assert all(abs(a-b)<5e-5 for a,b in zip(dims,row['bounds_m_xyz'])),(name,kind,dims,row['bounds_m_xyz'])
        checks[kind]={'status':'PASS','triangles':tri,'parts':len(parts),'materials_uvs_scale_topology':'PASS'}
    bpy.ops.wm.open_mainfile(filepath=str(folder/(name+'.blend')),load_ui=False)
    assert name in bpy.data.objects;row['blend_reopen']='PASS'
    runtime=json.loads((folder/('v09_'+name+'.mesh.json')).read_text());assert len(runtime['parts'])==row['runtime_parts']
    for part in runtime['parts']:
        assert len(part['vertices'])==len(part['normals'])==len(part['uv'])
        if part['texture']:assert (folder/part['texture']).is_file()
        for v in part['vertices']:assert all(math.isfinite(c) for c in v.values())
        for i in range(0,len(part['triangles']),3):
            ids=part['triangles'][i:i+3];vs=[Vector(tuple(part['vertices'][j][c] for c in ['x','y','z'])) for j in ids]
            n=Vector(tuple(part['normals'][ids[0]][c] for c in ['x','y','z']))
            assert (vs[1]-vs[0]).cross(vs[2]-vs[0]).dot(n)>0,(name,part['name'],'runtime winding')
    if name=='cannon':
        assert sorted(p['group'] for p in runtime['parts'])==['static','wheel_left','wheel_right']
        for part in runtime['parts']:
            if not part['group'].startswith('wheel'):continue
            pivot=part['rest']['position'];assert abs(abs(pivot['x'])-.64)<1e-5 and abs(pivot['y']-.545)<1e-5
        # Catch assembly-transform regressions, not merely a self-consistent bad export.
        assert all(abs(a-b)<.003 for a,b in zip(row['bounds_m_xyz'],[1.544,2.12435,1.3006]))
        checks['wheel_pivots']='PASS'
    if name=='spritz':
        assert runtime['frames']==12 and len([p for p in runtime['parts'] if p['samples']])==2
        assert abs(max(v['z'] for p in runtime['parts'] for v in p['vertices']))<1e-5
        assert all(abs(a-b)<.001 for a,b in zip(row['bounds_m_xyz'],[.147,.526,.062]))
        checks['muzzle_and_plunger']='PASS'
    if name=='water_timer':
        assert len([p for p in runtime['parts'] if p['group']=='drop'])==5
        assert all(m['emission']>0 for m in runtime['materials'] if 'liquid' in m['name'].lower())
        assert any(m['transmission']>.9 for m in runtime['materials'])
        checks['glow_glass_and_moving_parts']='PASS'
    if name=='ram_pair':
        assert len([p for p in runtime['parts'] if p['group'].startswith('rgb_')])==2
        assert len(runtime['parts'])==3
        assert all(m['emission']>0 and m['texture']=='v09_rgb_gradient.png' for m in runtime['materials'] if 'diffuser' in m['name'].lower())
        checks['two_continuous_emissive_bars']='PASS'
    if name=='centrifuge':
        assert sorted(p['group'] for p in runtime['parts'] if p['group'].startswith('tube_'))==['tube_%02d'%i for i in range(12)]
        checks['twelve_independent_samples']='PASS'
    if name=='skeleton':
        expected={'pelvis','torso','head'}|{a+'_'+s for a in ['upper_arm','forearm','hand','thigh','shin','foot'] for s in ['left','right']}
        assert expected<={p['group'] for p in runtime['parts']}
        assert len([p for p in runtime['parts'] if p['group'].startswith('caster_')])==5
        assert row['triangles']<22000
        checks['fifteen_joint_bodies_and_five_casters']='PASS'
    if name=='crystal_skull':assert row['triangles']<10000
    for material in runtime['materials']:
        if material['texture']:assert (ROOT/'src/Art'/material['texture']).is_file()
    assert (ROOT/'src/Art'/('v09_'+name+'.mesh.json')).read_bytes()==(folder/('v09_'+name+'.mesh.json')).read_bytes()
    row['checks']=checks;print('V09_PASS',name,flush=True)

path=OUT/'water_timer/water_timer.glb'
with path.open('rb') as f:
    magic,version,length=struct.unpack('<4sII',f.read(12));assert magic==b'glTF' and version==2 and length==path.stat().st_size
    size,kind=struct.unpack('<II',f.read(8));g=json.loads(f.read(size));size,kind=struct.unpack('<II',f.read(8));binary=f.read(size)
def accessor(index):
    a=g['accessors'][index];v=g['bufferViews'][a['bufferView']];n={'SCALAR':1,'VEC3':3,'VEC4':4}[a['type']]
    assert a['componentType']==5126;offset=v.get('byteOffset',0)+a.get('byteOffset',0);stride=v.get('byteStride',n*4)
    return [struct.unpack_from('<'+'f'*n,binary,offset+i*stride) for i in range(a['count'])]
assert len(g['animations'])==1;animation=g['animations'][0];node_changes=material_changes=0;duration=0
for channel in animation['channels']:
    s=animation['samplers'][channel['sampler']];times=accessor(s['input']);values=accessor(s['output']);duration=max(duration,times[-1][0])
    changed=any(max(abs(a-b) for a,b in zip(values[0],v))>1e-4 for v in values[1:])
    assert max(abs(a-b) for a,b in zip(values[0],values[-1]))<1e-4,'Loop endpoints'
    if changed:
        if 'node' in channel['target']:node_changes+=1
        else:material_changes+=1
assert abs(duration-18)<1e-4 and node_changes>=10 and material_changes>=5,(duration,node_changes,material_changes)
next(r for r in rows if r['asset']=='water_timer')['glb_animation']={'status':'PASS','duration_seconds':duration,'changing_transform_channels':node_changes,'changing_material_channels':material_changes,'material_extension':'KHR_animation_pointer','loop_seam':'PASS'}
(OUT/'manifest.json').write_text(json.dumps(rows,indent=2));print('ALL_V09_ASSET_CHECKS_PASSED',flush=True)
