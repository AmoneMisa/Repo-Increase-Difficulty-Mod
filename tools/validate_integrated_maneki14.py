"""Round-trip the animated final cat and compare its phase/scale with runtime geometry."""
import bpy,json,math,wave,struct
from pathlib import Path
from mathutils import Vector,Matrix,Quaternion
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/valuables/maneki-integrated-14'
data=json.loads((OUT/'v11_maneki_neko.mesh.json').read_text())
assert (OUT/'v11_maneki_neko.mesh.json').read_bytes()==(ROOT/'src/Art/v11_maneki_neko.mesh.json').read_bytes()
C=Matrix(((1,0,0),(0,0,1),(0,1,0)))
def vector(v):return Vector(tuple(v[c] for c in 'xyz'))
def bounds(points):return [min(p[k] for p in points) for k in range(3)]+[max(p[k] for p in points) for k in range(3)]
pivot=vector(data['beckonPivot']);count=0
height_ref=json.loads((OUT/'player-height-reference.json').read_text())
ys=[v['y'] for p in data['parts'] for v in p['vertices']]
assert abs((max(ys)-min(ys))*2-height_ref['height_m'])<.00001
for part in data['parts']:
    assert len(part['vertices'])==len(part['normals'])==len(part['uv'])
    count+=len(part['triangles'])//3
    for i in range(0,len(part['triangles']),3):
        ids=part['triangles'][i:i+3];a,b,c=[vector(part['vertices'][j]) for j in ids];normal=(b-a).cross(c-a)
        assert normal.length>1e-13
        for j in ids:assert normal.dot(vector(part['normals'][j]))>0
for mat in data['materials']:
    if mat['texture']:assert (OUT/mat['texture']).read_bytes()==(ROOT/'src/Art'/mat['texture']).read_bytes()
physics=data['physics'];assert len(physics['colliders'])==3
assert sum(c['moving'] for c in physics['colliders'])==1
assert all(len(c['triangles'])//3<=255 for c in physics['colliders'])

bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.scene.render.fps=30
bpy.ops.import_scene.gltf(filepath=str(OUT/'maneki_neko.glb'))
scene=bpy.context.scene;objects=[o for o in scene.objects if o.type=='MESH']
assert sum(sum(len(p.vertices)-2 for p in ob.data.polygons) for ob in objects)==count
actions={o.animation_data.action for o in scene.objects if o.animation_data and o.animation_data.action}
assert actions
start=min(a.frame_range[0] for a in actions);end=max(a.frame_range[1] for a in actions)
assert abs((end-start)/30-8/3)<.0001
errors=[]
for sample in [0,20,40,60,80]:
    scene.frame_set(round(start+sample));bpy.context.view_layer.update()
    actual=bounds([ob.matrix_world@v.co for ob in objects for v in ob.data.vertices])
    angle=3-15*math.cos(math.tau*sample/80);rotation=Quaternion(Vector((1,0,0)),math.radians(angle));points=[]
    for part in data['parts']:
        for v in part['vertices']:
            p=vector(v)
            if part['group']=='beckoning_paw':p=pivot+rotation@(p-pivot)
            points.append(C@p*2)
    expected=bounds(points);error=max(abs(a-b) for a,b in zip(actual,expected));errors.append(error)
    assert error<.0002,('GLB and runtime differ',sample,error)
    lo,hi=vector(physics['motionMin']),vector(physics['motionMax'])
    for hull in physics['colliders']:
        for v in hull['vertices']:
            p=vector(v)
            if hull['moving']:p=pivot+rotation@p
            assert all(lo[k]<=p[k]<=hi[k] for k in range(3)),('collider sweep',sample)

bpy.ops.wm.read_factory_settings(use_empty=True)
for line in (OUT/'maneki_neko.mtl').read_text().splitlines():
    if line.startswith('map_'):assert (OUT/line.split()[-1]).is_file(),line
bpy.ops.wm.obj_import(filepath=str(OUT/'maneki_neko.obj'),forward_axis='NEGATIVE_Z',up_axis='Y')
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert sum(sum(len(p.vertices)-2 for p in ob.data.polygons) for ob in objects)==count
for ob in objects:
    assert ob.data.uv_layers
    for mat in ob.data.materials:
        for node in mat.node_tree.nodes:
            if node.type=='TEX_IMAGE' and node.image:assert node.image.size[0]>0
for cue in ['porcelain','porcelain_break']:
    with wave.open(str(ROOT/'src/Audio'/('valuable_'+cue+'.wav')),'rb') as f:
        assert (f.getnchannels(),f.getsampwidth(),f.getframerate())==(1,2,22050)
        values=struct.unpack('<'+'h'*f.getnframes(),f.readframes(f.getnframes()))
        assert max(map(abs,values))<30000 and abs(values[0])<10 and abs(values[-1])<100
report={'status':'PASS','triangles':count,'height_m':height_ref['height_m'],'mass_kg':height_ref['mass_kg'],'glb_obj_reimport':'PASS','cycle_seconds':8/3,'max_glb_runtime_bounds_error_m':max(errors),'collider_sweep':'PASS','audio_format_and_peaks':'PASS','live_game_validation':'pending'}
(OUT/'final-validation.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
