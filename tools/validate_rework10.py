"""Reimport delivered models and check runtime meshes, textures and tube grouping."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/valuables/physics-rework-10';rows=[]
for key in ['bomb','centrifuge']:
 folder=OUT/key;data=json.loads((folder/('v10_'+key+'.mesh.json')).read_text());expected=0
 for p in data['parts']:
  assert len(p['vertices'])==len(p['normals'])==len(p['uv'])
  assert len(p['triangles'])%3==0;expected+=len(p['triangles'])//3
  for v in p['vertices']+p['normals']:assert all(math.isfinite(v[c]) for c in ['x','y','z'])
  for i in range(0,len(p['triangles']),3):
   ids=p['triangles'][i:i+3];assert all(0<=j<len(p['vertices']) for j in ids)
   v=[Vector(tuple(p['vertices'][j][c] for c in ['x','y','z'])) for j in ids]
   n=Vector(tuple(p['normals'][ids[0]][c] for c in ['x','y','z']))
   assert (v[1]-v[0]).cross(v[2]-v[0]).dot(n)>0,(key,p['name'],'winding or degenerate triangle')
 for m in data['materials']:
  if m['texture']:assert (folder/m['texture']).is_file() and (ROOT/'src/Art'/m['texture']).is_file()
 assert (ROOT/'src/Art'/('v10_'+key+'.mesh.json')).read_bytes()==(folder/('v10_'+key+'.mesh.json')).read_bytes()
 counts={}
 for ext in ['glb','obj']:
  bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
  if ext=='glb':bpy.ops.import_scene.gltf(filepath=str(folder/(key+'.glb')))
  else:
   for line in (folder/(key+'.mtl')).read_text().splitlines():
    if line.startswith('map_'):assert (folder/line.split(' ',1)[1]).is_file(),line
   bpy.ops.wm.obj_import(filepath=str(folder/(key+'.obj')),forward_axis='NEGATIVE_Z',up_axis='Y')
  parts=[o for o in bpy.context.scene.objects if o.type=='MESH'];tri=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts)
  assert tri==expected,(key,ext,tri,expected)
  assert all(o.data.uv_layers and len(o.data.materials)>0 for o in parts)
  counts[ext]={'parts':len(parts),'triangles':tri,'status':'PASS'}
 if key=='centrifuge':
  groups={p['group'] for p in data['parts'] if p['group'].startswith('tube_')};assert len(groups)==12
  assert all(len([p for p in data['parts'] if p['group']==g])==4 for g in groups)
 if key=='bomb':assert len(data['parts'])>10
 rows.append({'key':key,'runtime_winding_normals_textures':'PASS','reimport':counts})
(OUT/'validation.json').write_text(json.dumps(rows,indent=2));print('REWORK10_VALIDATED',rows,flush=True)
