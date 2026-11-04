"""Read-only reference audit; reports parameters, never exports proprietary assets."""
import UnityPy,json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
base=Path('C:/Program Files (x86)/Steam/steamapps/common/REPO/REPO_Data')
env=UnityPy.load(*(str(base/n) for n in ['sharedassets0.assets','resources.assets']))
files={f.name:f for f in env.assets}
def resolve(reader,p):
    f=reader.assets_file
    if p['m_FileID']:f=files[Path(f.externals[p['m_FileID']-1].path.replace('\\','/')).name]
    return f.objects[p['m_PathID']]
def walk(go,result):
    d=go.read_typetree()
    for c in d['m_Component']:
        ob=resolve(go,c['component'])
        if ob.type.name not in ['Transform','Light','MeshRenderer']:continue
        t=ob.read_typetree()
        if ob.type.name=='Transform':
            for child in t['m_Children']:
                tr=resolve(ob,child);walk(resolve(tr,tr.read_typetree()['m_GameObject']),result)
        elif ob.type.name=='Light':result.append({'node':d['m_Name'],'type':'Light','settings':t})
        elif ob.type.name=='MeshRenderer':
            for p in t['m_Materials']:
                if not p['m_PathID']:continue
                mat=resolve(ob,p);m=mat.read_typetree()
                result.append({'node':d['m_Name'],'type':'Material','name':m['m_Name'],'properties':m['m_SavedProperties']})
out=[]
for ob in env.objects:
    if ob.type.name!='GameObject' or b'valuable wizard time glass' not in ob.get_raw_data().lower():continue
    name=ob.read_typetree()['m_Name']
    row={'name':name,'components':[]};walk(ob,row['components']);out.append(row)
(root/'work/shape-polish-15/native-hourglass.json').write_text(json.dumps(out,indent=2))
for r in out:
    print(r['name'])
    for c in r['components']:
        if c['type']=='Light':print(c['node'],{k:v for k,v in c['settings'].items() if k in ['m_Color','m_Intensity','m_Range','m_Type']})
        else:print(c['node'],c['name'],c['properties'].get('m_Floats'),c['properties'].get('m_Colors'))
