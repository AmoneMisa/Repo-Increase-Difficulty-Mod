"""Round-trip Blender checks of the delivered mesh files and embedded Spritz resource."""
import bpy,bmesh,json,math,struct
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/valuables/medical-tech-pack-06'
rows=json.loads((OUT/'manifest.json').read_text());assert len(rows)==8
for row in rows:
    name=row['asset'];folder=OUT/name;results={}
    for kind in ['glb','obj']:
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        path=folder/(name+'.'+kind)
        if kind=='glb':
            with path.open('rb') as f:
                magic,version,length=struct.unpack('<4sII',f.read(12));assert magic==b'glTF' and version==2 and length==path.stat().st_size
                size,tag=struct.unpack('<II',f.read(8));assert tag==0x4e4f534a;g=json.loads(f.read(size))
            assert all('bufferView' in im for im in g.get('images',[])),'GLB must have embedded textures'
            assert len(g.get('images',[]))==len(row['textures'])
            bpy.ops.import_scene.gltf(filepath=str(path))
        else:
            # Exported MTLs must continue to resolve after the pack is moved.
            for line in (folder/(name+'.mtl')).read_text().splitlines():
                if line.startswith('map_Kd '):
                    p=line[7:].strip();assert not Path(p).is_absolute(),p
                    assert (folder/p).is_file(),p
            bpy.ops.wm.obj_import(filepath=str(path),forward_axis='NEGATIVE_Z',up_axis='Y')
        bpy.context.view_layer.update();parts=[o for o in bpy.context.scene.objects if o.type=='MESH']
        assert len(parts)==row['mesh_parts'],(name,kind,'part count')
        points=[];tri=0;nonman=0;deg=0
        for ob in parts:
            assert ob.data.uv_layers and all(m for m in ob.data.materials)
            points.extend(ob.matrix_world@v.co for v in ob.data.vertices)
            tri+=sum(len(p.vertices)-2 for p in ob.data.polygons)
            bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
            nonman+=sum(not e.is_manifold for e in bm.edges);deg+=sum(f.calc_area()<1e-12 for f in bm.faces);bm.free()
        assert all(math.isfinite(x) for p in points for x in p)
        dims=[max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)]
        assert tri==row['triangles'],(name,kind,tri,row['triangles'])
        assert all(abs(a-b)<3e-5 for a,b in zip(dims,row['bounds_m_xyz']))
        assert abs(min(p.z for p in points))<3e-5
        assert nonman==0 and deg==0,(name,kind,nonman,deg)
        results[kind]={'status':'PASS','triangles':tri,'mesh_parts':len(parts),'nonmanifold_edges_after_seam_weld':nonman,'degenerate_faces':deg,'materials_uv_textures':'PASS','scale_ground_finite':'PASS'}
    bpy.ops.wm.open_mainfile(filepath=str(folder/(name+'.blend')),load_ui=False)
    assert name in bpy.data.objects
    assert len([o for o in bpy.data.objects[name].children if o.type=='MESH'])==row['mesh_parts']
    row['round_trip']=results;row['blend_reopen']='PASS'
    print('ROUND_TRIP_PASS',name,flush=True)
runtime=json.loads((ROOT/'src/Art/spritz.mesh.json').read_text());assert runtime['version']==1 and runtime['frames']==12
maxforward=-10;minforward=10;tri=0
for p in runtime['parts']:
    assert len(p['vertices'])==len(p['normals'])==len(p['uv'])
    assert (ROOT/'src/Art'/p['texture']).is_file()
    for v in p['vertices']:
        assert all(math.isfinite(x) for x in v.values());maxforward=max(maxforward,v['z']);minforward=min(minforward,v['z'])
    for i in range(0,len(p['triangles']),3):
        ids=p['triangles'][i:i+3];vs=[Vector(tuple(p['vertices'][j][k] for k in ['x','y','z'])) for j in ids]
        n=Vector(tuple(p['normals'][ids[0]][k] for k in ['x','y','z']))
        assert (vs[1]-vs[0]).cross(vs[2]-vs[0]).dot(n)>1e-14,'runtime winding/normals'
        tri+=1
    if p['samples']:assert len(p['samples'])==12
assert abs(maxforward)<1e-5 and minforward<-.50,'Needle tip must be at the native muzzle, body behind it'
assert tri==next(r for r in rows if r['asset']=='spritz')['triangles']
next(r for r in rows if r['asset']=='spritz')['runtime_mesh_checks']={'status':'PASS','tip_at_muzzle':True,'winding_normals':'PASS','frames':12,'triangles':tri}
(OUT/'manifest.json').write_text(json.dumps(rows,indent=2));print('ALL_MEDICAL_TECH_ASSET_CHECKS_PASSED',flush=True)
