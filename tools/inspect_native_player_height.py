"""Read-only native player dimensions, without exporting game meshes."""
import UnityPy,json,numpy as np,itertools
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
base=Path('C:/Program Files (x86)/Steam/steamapps/common/REPO/REPO_Data')
env=UnityPy.load(str(base/'resources.assets'))
files={f.name:f for f in env.assets}
def resolve(reader,p):
    f=reader.assets_file
    if p['m_FileID']:f=files[Path(f.externals[p['m_FileID']-1].path.replace('\\','/')).name]
    return f.objects[p['m_PathID']]
def vector(v):return np.array([v[k] for k in 'xyz'])
def matrix(t):
    x,y,z,w=[t['m_LocalRotation'][c] for c in 'xyzw'];m=np.eye(4)
    m[:3,:3]=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])@np.diag(vector(t['m_LocalScale']))
    m[:3,3]=vector(t['m_LocalPosition']);return m
def aabb(m,c,e):
    pts=np.array([(m@np.append(c+e*np.array(sign),1))[:3] for sign in itertools.product([-1,1],repeat=3)])
    return {'min':pts.min(0).tolist(),'max':pts.max(0).tolist(),'height':float(pts[:,1].max()-pts[:,1].min())}
def walk(go,parent,path,rows,root=False):
    g=go.read_typetree();comps=[resolve(go,p['component']) for p in g['m_Component']]
    tr=next((c for c in comps if c.type.name=='Transform'),None)
    if tr is None:return
    t=tr.read_typetree();m=parent@matrix(t)
    if root:m[:3,3]=0
    path=path+'/'+g['m_Name']
    for ob in comps:
        if ob.type.name not in ['CapsuleCollider','BoxCollider','SkinnedMeshRenderer','MeshFilter']:continue
        d=ob.read_typetree();row={'path':path,'type':ob.type.name,'active':bool(g['m_IsActive'])}
        if ob.type.name=='CapsuleCollider':
            e=np.ones(3)*d['m_Radius'];e[d['m_Direction']]=d['m_Height']/2
            row.update(aabb(m,vector(d['m_Center']),e));row['local_height']=d['m_Height']
        elif ob.type.name=='BoxCollider':row.update(aabb(m,vector(d['m_Center']),vector(d['m_Size'])/2))
        elif ob.type.name=='SkinnedMeshRenderer':
            b=d['m_AABB'];row.update(aabb(m,vector(b['m_Center']),vector(b['m_Extent'])))
        else:
            p=d['m_Mesh']
            if not p['m_PathID']:continue
            try:b=resolve(ob,p).read_typetree()['m_LocalAABB']
            except (KeyError,ValueError):continue
            row.update(aabb(m,vector(b['m_Center']),vector(b['m_Extent'])))
        rows.append(row)
    for child in t['m_Children']:
        c=resolve(tr,child);walk(resolve(c,c.read_typetree()['m_GameObject']),m,path,rows)
result=[]
for ob in env.objects:
    if ob.type.name!='GameObject' or b'Player' not in ob.get_raw_data():continue
    n=ob.read_typetree()['m_Name']
    if n not in ['Player Avatar','Player Avatar Controller']:continue
    rows=[];walk(ob,np.eye(4),'',rows,True);result.append({'name':n,'id':ob.path_id,'geometry':rows})
(ROOT/'work/shape-polish-15/native-player-dimensions.json').write_text(json.dumps(result,indent=2))
for r in result:
    print(r['name'],r['id'])
    for row in r['geometry']:print(row)
