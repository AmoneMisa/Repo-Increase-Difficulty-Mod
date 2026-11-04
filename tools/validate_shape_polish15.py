"""Validate shipped art against pre-polish motion/attachment contracts and reimport files."""
import bpy,json,math,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/shape-polish-15';BASE=ROOT/'work/shape-polish-15'
rows=[]

def vec(p):return Vector(tuple(p[c] for c in 'xyz'))
def check_mesh(data,key):
    count=0
    for p in data['parts']:
        vs=p['vertices'];ns=p['normals'];uv=p['uv'];ts=p['triangles'];count+=len(ts)//3
        assert len(vs)==len(ns)==len(uv) and len(ts)%3==0
        assert all(math.isfinite(c) for v in vs+ns+uv for c in v.values())
        for i in range(0,len(ts),3):
            ids=ts[i:i+3];assert all(0<=j<len(vs) for j in ids)
            a,b,c=[vec(vs[j]) for j in ids];normal=(b-a).cross(c-a)
            assert normal.length>1e-13,(key,p['name'],'degenerate')
            for j in ids:
                n=vec(ns[j]);assert abs(n.length-1)<.001,(key,p['name'],'normal length')
                assert normal.dot(n)>-1e-13,(key,p['name'],'inverted normal')
    assert count<75000,(key,'triangle budget',count)
    return count

def roundtrip(folder,key,triangles,moving=False):
    imports={}
    for ext in ['glb','obj']:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if ext=='glb':bpy.ops.import_scene.gltf(filepath=str(folder/(key+'.glb')))
        else:
            for line in (folder/(key+'.mtl')).read_text().splitlines():
                if line.startswith('map_'):assert (folder/line.split()[-1]).is_file(),(key,line)
            bpy.ops.wm.obj_import(filepath=str(folder/(key+'.obj')),forward_axis='NEGATIVE_Z',up_axis='Y')
        parts=[o for o in bpy.context.scene.objects if o.type=='MESH']
        actual=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts)
        assert actual==triangles,(key,ext,actual,triangles)
        assert all(o.data.uv_layers and len(o.data.materials) for o in parts)
        for o in parts:
            for mat in o.data.materials:
                assert mat
                for n in mat.node_tree.nodes:
                    if n.type=='TEX_IMAGE' and n.image:assert n.image.size[0]>0,(key,ext,'missing texture')
        if ext=='glb' and moving:
            assert any(o.animation_data and o.animation_data.action for o in bpy.context.scene.objects),(key,'lost animation')
        imports[ext]='PASS'
    return imports

for key in ['poison','ice','sticky','decoy']:
    path=ROOT/'src/Art'/(key+'.mesh.json');data=json.loads(path.read_text());before=json.loads((BASE/path.name).read_text())
    count=check_mesh(data,key)
    old={p['name']:p for p in before['parts'] if p['samples']}
    new={p['name']:p for p in data['parts'] if p['samples']}
    assert old.keys()==new.keys(),(key,'moving part identities')
    for name,p in old.items():
        assert p['rest']==new[name]['rest'] and p['samples']==new[name]['samples'],(key,name,'animation/muzzle contract changed')
    rows.append({'asset':key,'triangles':count,'animation_tracks_unchanged':len(old),'imports':roundtrip(OUT/key,key,count,True)})

for folder in sorted((OUT/'valuables').iterdir()):
    if not folder.is_dir():continue
    key=folder.name;path=next(folder.glob('*.mesh.json'))
    data=json.loads(path.read_text());before=json.loads((BASE/path.name).read_text())
    assert path.read_bytes()==(ROOT/'src/Art'/path.name).read_bytes(),(key,'runtime differs from checked export')
    assert data['materials']==before['materials'],(key,'material identity/settings')
    assert len(data['parts'])==len(before['parts'])
    count=check_mesh(data,key)
    for p,old in zip(data['parts'],before['parts']):
        for field in ['name','group','phase','material','rest','samples']:
            assert p.get(field)==old.get(field),(key,p['name'],field)
        if key=='water_timer' and p['group'] in ('drop','top_pool','bottom_pool'):assert p==old
    for mat in data['materials']:
        if mat['texture']:assert (folder/mat['texture']).read_bytes()==(ROOT/'src/Art'/mat['texture']).read_bytes()
    # Bounds measured from actual runtime meshes, after the original origin conversion.
    def limits(model):
        ps=[vec(v)+vec(p['rest']['position']) for p in model['parts'] for v in p['vertices']]
        return [min(v[k] for v in ps) for k in range(3)]+[max(v[k] for v in ps) for k in range(3)]
    a,b=limits(data),limits(before);error=max(abs(x-y) for x,y in zip(a,b))
    assert error<.009,(key,'bounds or origin moved',error)
    rows.append({'asset':key,'triangles':count,'bounds_change_m':error,'part_bindings_and_rest_poses':'unchanged','imports':roundtrip(folder,key,count,key=='water_timer')})

(OUT/'validation.json').write_text(json.dumps({'status':'PASS','assets':rows,'scope':'Geometry, file imports, unchanged weapon animation and valuable attachment contracts. Not an in-game appearance check.'},indent=2))
print('SHAPE_POLISH_PASS',json.dumps(rows),flush=True)
