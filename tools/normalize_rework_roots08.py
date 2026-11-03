"""Repair source root names without modifying exported geometry."""
import bpy,json
from pathlib import Path
OUT=Path(__file__).resolve().parents[1]/'art/valuables/depth-rework-08'
for row in json.loads((OUT/'manifest.json').read_text()):
    name=row['asset'];path=OUT/name/(name+'.blend')
    bpy.ops.wm.open_mainfile(filepath=str(path),load_ui=False)
    roots=[o for o in bpy.context.scene.objects if o.type=='EMPTY' and o.parent is None and any(c.type=='MESH' for c in o.children)]
    assert len(roots)==1,(name,[o.name for o in roots])
    root=roots[0]
    if root.name!=name:
        other=bpy.data.objects.get(name)
        if other:
            assert not other.users_collection
            bpy.data.objects.remove(other,do_unlink=True)
        root.name=name
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(path))
    print('ROOT_OK',name,flush=True)
