"""Read-only REPO prefab mass and grab thresholds; exports numeric metadata only."""
import json, struct
from pathlib import Path
import UnityPy
root=Path(__file__).resolve().parents[1]
base=Path('C:/Program Files (x86)/Steam/steamapps/common/REPO/REPO_Data')
env=UnityPy.load(str(base/'resources.assets'),str(base/'globalgamemanagers.assets'))
files={f.name:f for f in env.assets}
def resolve(o,p):
    if not p or not p.get('m_PathID'):return None
    f=o.assets_file
    if p['m_FileID']:f=files[Path(f.externals[p['m_FileID']-1].path.replace('\\','/')).name]
    return f.objects.get(p['m_PathID'])
def walk(o,rows,seen):
    if o is None or (o.assets_file.name,o.path_id) in seen:return
    seen.add((o.assets_file.name,o.path_id))
    if o.type.name not in ('GameObject','Transform','Rigidbody','MonoBehaviour'):return
    d=o.read_typetree(check_read=False); name=d.get('m_Name','')
    if o.type.name=='GameObject':
        for p in d['m_Component']:walk(resolve(o,p['component']),rows,seen)
    elif o.type.name=='Transform':
        for p in d['m_Children']:
            t=resolve(o,p)
            if t:walk(resolve(t,t.read_typetree()['m_GameObject']),rows,seen)
    elif o.type.name=='Rigidbody':rows.append({'component':'Rigidbody','mass':d.get('m_Mass')})
    elif o.type.name=='MonoBehaviour':
        script=resolve(o,d.get('m_Script'))
        cls=script.read_typetree().get('m_ClassName') if script else None
        if cls=='EnemyRigidbody':
            # Installed type trees retain only the MonoBehaviour header. The first
            # serialized fields are verified in EnemyRigidbody.current.txt: enemy,
            # followTarget, gravity (aligned), customGravity, grabForceNeeded.
            raw=o.get_raw_data(); size=struct.unpack_from('<i',raw,28)[0]
            header=32+((size+3)//4)*4
            fileid,pathid=struct.unpack_from('<iq',raw,header+32)
            force=resolve(o,{'m_FileID':fileid,'m_PathID':pathid})
            amount=struct.unpack_from('<f',force.get_raw_data(),len(force.get_raw_data())-4)[0] if force else None
            rows.append({'component':cls,'grabForce':amount})
        elif cls=='EnemyParent':rows.append({'component':cls,'enemyName':d.get('enemyName')})
        elif cls=='NotValuableObject':
            raw=o.get_raw_data(); size=struct.unpack_from('<i',raw,28)[0]
            header=32+((size+3)//4)*4
            fileid,pathid=struct.unpack_from('<iq',raw,header)
            preset=resolve(o,{'m_FileID':fileid,'m_PathID':pathid})
            if preset:
                mass=struct.unpack_from('<f',preset.get_raw_data(),len(preset.get_raw_data())-4)[0]
                rows.append({'component':cls,'presetMass':mass})
out=[]
for o in env.objects:
    if o.type.name!='GameObject' or b'Enemy - ' not in o.get_raw_data():continue
    name=o.read_typetree()['m_Name']
    if not name.startswith('Enemy - '):continue
    rows=[]
    try:walk(o,rows,set())
    except Exception as e:rows.append({'error':str(e)})
    if rows:out.append({'prefab':name,'physics':rows})
path=root/'docs'/'native-enemy-physics.json';path.write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(out,ensure_ascii=False));print(path)
