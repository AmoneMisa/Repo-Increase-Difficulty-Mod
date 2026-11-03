import bpy, bmesh, json, os, math, struct
from mathutils import Vector

root=os.path.abspath('art/valuables/heavy-bomb-reference-05')
with open(os.path.join(root,'validation.json')) as f: reports=json.load(f)
for report in reports:
    name=report['asset']; folder=root; imports={}
    for ext in ['glb','obj']:
        bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
        path=os.path.join(folder,name+'.'+ext)
        if ext=='glb':
            with open(path,'rb') as f:
                magic,version,length=struct.unpack('<4sII',f.read(12))
                assert magic==b'glTF' and version==2 and length==os.path.getsize(path)
            bpy.ops.import_scene.gltf(filepath=path)
        else:bpy.ops.wm.obj_import(filepath=path,forward_axis='NEGATIVE_Z',up_axis='Y')
        bpy.context.view_layer.update()
        meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
        assert meshes and not any('Preview' in o.name for o in meshes)
        assert len(meshes)==report['mesh_parts'],(name,ext,len(meshes),report['mesh_parts'])
        tri=0; nonman=0; degenerate=0
        points=[]
        for ob in meshes:
            assert ob.data.uv_layers and ob.data.materials
            assert all(m is not None for m in ob.data.materials)
            bm=bmesh.new(); bm.from_mesh(ob.data)
            # GLB splits vertices at normals/UV seams: weld positions for topology check.
            bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
            nonman+=sum(not e.is_manifold for e in bm.edges)
            degenerate+=sum(f.calc_area()<1e-11 for f in bm.faces)
            tri+=sum(len(p.vertices)-2 for p in ob.data.polygons)
            points.extend(ob.matrix_world@v.co for v in ob.data.vertices)
            bm.free()
        assert all(math.isfinite(v) for p in points for v in p)
        dimensions=[max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)]
        assert all(abs(a-b)<2e-5 for a,b in zip(dimensions,report['bounds_m_xyz'])),(name,ext,dimensions)
        assert tri==report['triangles'],(tri,report['triangles'])
        assert nonman==0 and degenerate==0,(name,ext,nonman,degenerate)
        assert abs(min(p.z for p in points))<2e-5
        imports[ext]={'status':'PASS','triangles':tri,'mesh_parts':len(meshes),'dimensions_m_xyz':[round(v,5) for v in dimensions],'non_manifold_edges_after_seam_weld':nonman,'degenerate_faces':degenerate,'materials_and_uvs_present':True,'ground_pivot_check':True}
        print('IMPORT_PASS',name,ext,flush=True)
    bpy.ops.wm.open_mainfile(filepath=os.path.join(folder,name+'.blend'),load_ui=False)
    assert name in bpy.data.objects
    source_parts=[o for o in bpy.data.objects[name].children if o.type=='MESH']
    assert len(source_parts)==report['mesh_parts']
    report['blend_reopen']={'status':'PASS','asset_root_present':True,'mesh_parts':len(source_parts)}
    report['round_trip_imports']=imports
with open(os.path.join(root,'validation.json'),'w') as f:json.dump(reports,f,indent=2)
print('ALL_IMPORT_CHECKS_PASSED',flush=True)
