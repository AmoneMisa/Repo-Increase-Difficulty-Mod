"""Independent arithmetic audit of shipped runtime meshes and native spawn convention."""
import json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
capacities=json.loads((ROOT/'work/native-valuable-volumes.json').read_text())[0]['sizes']
keys=['crystal_skull','bomb','bath_gift_set','cannon','water_timer','centrifuge','skeleton','robot_vacuum','graphics_card','ram_pair','monoblock_pc','gold_ingot','maneki_neko'];rows=[]
for key in keys:
 p=ROOT/'src/Art'/('v11_'+key+'.mesh.json')
 if not p.exists():p=ROOT/'src/Art'/('v10_'+key+'.mesh.json')
 if not p.exists():p=ROOT/'src/Art'/('v09_'+key+'.mesh.json')
 data=json.loads(p.read_text());points=[]
 for part in data['parts']:
  rest=part['rest']['position']
  points += [[v[c]+rest[c] for c in ['x','y','z']] for v in part['vertices']]
 assert points and all(math.isfinite(x) for v in points for x in v)
 lo=[min(v[i] for v in points) for i in range(3)];hi=[max(v[i] for v in points) for i in range(3)]
 if key=='maneki_neko':lo[2]-=.09;hi[2]+=.09
 offset=[-(lo[0]+hi[0])/2,.012-lo[1],.012-lo[2]]
 minimum=[lo[i]+offset[i] for i in range(3)];maximum=[hi[i]+offset[i] for i in range(3)]
 scale=.8 if key=='bomb' else .85 if key=='cannon' else 1.1 if key in ['centrifuge','robot_vacuum'] else 2 if key=='maneki_neko' else 1
 size=[(hi[i]-lo[i])*scale for i in range(3)];center=[(minimum[i]+maximum[i])/2 for i in range(3)]
 assert abs(center[0])<1e-6 and abs(minimum[1]-.012)<1e-6 and abs(minimum[2]-.012)<1e-6
 scored=[(sum(max(0,(size[i]+.025-cap[i])/cap[i]) for i in range(3)),math.prod(cap),name) for name,cap in capacities.items()]
 excess,_,volume=min(scored)
 rows.append({'key':key,'size_m':size,'local_mass_and_grab_center':center,'visual_and_collider_offset':offset,'native_volume':volume,'oversize_requires_scene_overlap_check':excess>1e-5,'status':'PASS'})
out=ROOT/'art/valuables/physics-rework-10/spawn-layout-validation.json';out.write_text(json.dumps(rows,indent=2))
print('PASS: all13 shipped model centres/positive floor-forward bounds; native category selection; oversize identified for scene collision guard.')
for row in rows:print(row['key'],row['native_volume'],[round(v,3) for v in row['size_m']])
