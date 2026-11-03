"""Validate portable files, actual runtime meshes, metals and the exported paw animation."""
import bpy,bmesh,json,math,struct,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];review='--review' in sys.argv
OUT=ROOT/('art/valuables/maneki-reference-13' if review else 'art/valuables/lighting-collection-11');rows=[]
def glb_json(path):
    raw=path.read_bytes();length,kind=struct.unpack_from('<II',raw,12);assert kind==0x4E4F534A
    return json.loads(raw[20:20+length])
for key in (['maneki_neko'] if review else ['gold_ingot','maneki_neko','bomb','cannon','graphics_card','robot_vacuum']):
    folder=OUT/key;path=folder/('v11_'+key+'.mesh.json');data=json.loads(path.read_text());triangles=0;points=[]
    if not review:assert path.read_bytes()==(ROOT/'src/Art'/path.name).read_bytes()
    for part in data['parts']:
        assert len(part['vertices'])==len(part['normals'])==len(part['uv'])
        assert len(part['triangles'])%3==0;triangles+=len(part['triangles'])//3
        rest=part['rest']['position']
        for v in part['vertices']:assert all(math.isfinite(v[c]) for c in ['x','y','z']);points.append([v[c]+rest[c] for c in ['x','y','z']])
        for i in range(0,len(part['triangles']),3):
            ids=part['triangles'][i:i+3];assert all(0<=j<len(part['vertices']) for j in ids)
            v=[Vector(tuple(part['vertices'][j][c] for c in ['x','y','z'])) for j in ids]
            for j in ids:
                n=Vector(tuple(part['normals'][j][c] for c in ['x','y','z']))
                assert .98<n.length<1.02
                assert (v[1]-v[0]).cross(v[2]-v[0]).dot(n)>0,(key,part['name'],'winding/normal')
    for m in data['materials']:
        if m['texture']:assert (folder/m['texture']).is_file() and (ROOT/'src/Art'/m['texture']).read_bytes()==(folder/m['texture']).read_bytes()
    if key=='maneki_neko':
        assert triangles<25000,'Keep a game mesh rather than the rejected dense remesh'
        for part in data['parts']:
            name=data['materials'][part['material']]['name']
            if any(s in name for s in ['Fine charcoal','Soft coral','Gold seal']):
                assert sum(n['z'] for n in part['normals'])/len(part['normals'])<-.25,'Face paint must face forward in Unity, not be backface culled'
    if key in ['bomb','cannon','gold_ingot']:assert max(m['metallic'] for m in data['materials'])>.6
    if key=='graphics_card':assert len([p for p in data['parts'] if p['group']=='rgb_gpu' and data['materials'][p['material']]['emission']>0])==2
    if key=='robot_vacuum':assert any(p['group']=='scanner_emitter' and data['materials'][p['material']]['emission']>0 for p in data['parts'])
    for ext in ['glb','obj']:
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        if ext=='glb':bpy.ops.import_scene.gltf(filepath=str(folder/(key+'.glb')))
        else:
            for line in (folder/(key+'.mtl')).read_text().splitlines():
                if line.startswith('map_'):assert (folder/line.split(' ',1)[1]).is_file(),line
            bpy.ops.wm.obj_import(filepath=str(folder/(key+'.obj')),forward_axis='NEGATIVE_Z',up_axis='Y')
        bpy.context.view_layer.update();parts=[o for o in bpy.context.scene.objects if o.type=='MESH']
        tri=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts)
        assert tri==triangles,(key,ext,tri,triangles)
        assert all(o.data.uv_layers and len(o.data.materials)>0 for o in parts)
        if key=='maneki_neko':
            gltf=glb_json(folder/(key+'.glb'));assert gltf.get('animations')
            assert data['beckonPivot']==({'x':.145,'y':.286,'z':.05} if review else {'x':.180,'y':.318,'z':.005})
            head_components=0
            for ob in parts:
                if not any('ivory porcelain' in m.name.lower().replace('_',' ') for m in ob.data.materials):continue
                bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6);seen=set()
                for vertex in bm.verts:
                    if vertex in seen:continue
                    pending=[vertex];seen.add(vertex);top=-1
                    while pending:
                        v=pending.pop();top=max(top,(ob.matrix_world@v.co).z)
                        for edge in v.link_edges:
                            other=edge.other_vert(v)
                            if other not in seen:seen.add(other);pending.append(other)
                    if top>(.52 if review else .60):head_components+=1
                bm.free()
            assert head_components==1,(ext,head_components,'Ears must be connected to the head surface, not separate joined objects')
            for frame in [1,17,33,49,65]:
                bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
                pts=[o.matrix_world@v.co for o in parts for v in o.data.vertices]
                assert max(v.z for v in pts)<.76,'Paw must remain beside the head, never float above it'
                assert max(v.z for v in pts)-min(v.z for v in pts)<.76
    rows.append({'asset':key,'triangles':triangles,'runtime_normals_winding_uv_textures':'PASS','glb_obj_reimport':'PASS','animation':'PASS' if key=='maneki_neko' else 'not applicable'})
(OUT/'validation.json').write_text(json.dumps(rows,indent=2));print('V11_VALIDATED',rows,flush=True)
