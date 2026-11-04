"""Real Blender round-trip checks of approval geometry (not gameplay certification)."""
import bpy, bmesh, json, math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/valuables/maneki-reference-14'
(OUT/'validation.json').unlink(missing_ok=True)
results={}

def measure(objects):
    points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
    return [[min(p[k] for p in points) for k in range(3)], [max(p[k] for p in points) for k in range(3)]]

def inspect(objects):
    rows=[]
    for o in objects:
        assert o.data.uv_layers, f'Missing UV: {o.name}'
        assert all(-1e-5<=v<=1.00001 for loop in o.data.uv_layers.active.data for v in loop.uv),f'UV wrap can repeat paint: {o.name}'
        assert all(math.isfinite(v) for vert in o.data.vertices for v in vert.co),o.name
        assert all(math.isfinite(v) for p in o.data.polygons for v in p.normal),o.name
        assert all(p.area>1e-12 for p in o.data.polygons), f'Degenerate face: {o.name}'
        bm=bmesh.new();bm.from_mesh(o.data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
        bm.normal_update()
        bad=sum(not e.is_manifold for e in bm.edges)
        vol=bm.calc_volume(signed=True)
        rows.append({'object':o.name,'triangles':sum(len(p.vertices)-2 for p in o.data.polygons),'nonmanifold_edges_after_uv_weld':bad,'signed_volume_m3':vol})
        assert bad==0, f'Nonmanifold surface: {o.name}: {bad}'
        assert vol>0, f'Inverted surface: {o.name}'
        bm.free()
    return {'bounds_xyz_m':measure(objects),'meshes':rows,'triangles':sum(r['triangles'] for r in rows)}

bpy.ops.wm.open_mainfile(filepath=str(OUT/'maneki_review.blend'),load_ui=False)
source=[o for o in bpy.context.scene.objects if o.type=='MESH' and 'stage' in o]
results['blend']=inspect(source)
assert len(source)==10
assert len([i for i in bpy.data.images if i.packed_file])==3
assert min(p[2] for p in [o.matrix_world@v.co for o in source for v in o.data.vertices])>=-1e-6

for kind in ['glb','obj']:
    bpy.ops.wm.open_mainfile(filepath=str(OUT/'maneki_review.blend'),load_ui=False)
    for o in list(bpy.context.scene.objects):
        if 'stage' in o or o.type=='EMPTY':bpy.data.objects.remove(o,do_unlink=True)
    before=set(bpy.context.scene.objects)
    if kind=='glb':bpy.ops.import_scene.gltf(filepath=str(OUT/'maneki_review.glb'))
    else:bpy.ops.wm.obj_import(filepath=str(OUT/'maneki_review.obj'),forward_axis='NEGATIVE_Z',up_axis='Y')
    imported=[o for o in bpy.context.scene.objects if o not in before and o.type=='MESH']
    bpy.context.view_layer.update()
    results[kind]=inspect(imported)
    error=max(abs(a-b) for aa,bb in zip(results['blend']['bounds_xyz_m'],results[kind]['bounds_xyz_m']) for a,b in zip(aa,bb))
    results[kind]['bounds_error_m']=error
    assert error<.0001, (kind,error)
    assert results[kind]['triangles']==results['blend']['triangles']
    images=set()
    for ob in imported:
        for mat in ob.data.materials:
            assert mat and mat.use_nodes
            for n in mat.node_tree.nodes:
                if n.type=='TEX_IMAGE' and n.image:
                    assert n.image.size[0]>0, (kind,n.image.name)
                    images.add(n.image.name)
    assert len(images)==3,(kind,images)
    results[kind]['loaded_texture_count']=len(images)
    scene=bpy.context.scene;scene.cycles.samples=32
    scene.render.resolution_percentage=70
    scene.render.filepath=str(OUT/f'reimport_{kind}.png')
    bpy.ops.render.render(write_still=True)

results['status']='PASS: static geometry and material round trips only'
results['not_checked']=['approved appearance','animation sweep and shoulder joint','runtime colliders and centre of mass','native car size comparison','Unity integration','in-game shaders, audio, spawning, host/client and late join']
(OUT/'validation.json').write_text(json.dumps(results,indent=2))
print(json.dumps(results,indent=2))
