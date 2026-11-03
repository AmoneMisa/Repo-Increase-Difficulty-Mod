"""Correct corner normal hemispheres at narrow orbital cuts in source and delivered meshes."""
import bpy,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import rework_valuables08 as prev
OUT=prev.ROOT/'art/valuables/interaction-rework-09'
for name in ['crystal_skull','skeleton']:
    folder=OUT/name;bpy.ops.wm.open_mainfile(filepath=str(folder/(name+'.blend')),load_ui=False)
    parts=[o for o in bpy.context.scene.objects if o.type=='MESH' and not o.name.startswith('Preview_')];fixed=0
    for ob in parts:
        for poly in ob.data.polygons:
            if any(ob.data.corner_normals[li].vector.dot(poly.normal)<=0 for li in poly.loop_indices):poly.use_smooth=False;fixed+=1
        ob.data.update()
    bpy.context.view_layer.update();bpy.ops.object.select_all(action='DESELECT')
    for ob in parts:ob.select_set(True)
    bpy.data.objects[name].select_set(True)
    prev.runtime_export(name,parts,folder)
    path=folder/('v09_'+name+'.mesh.json');(folder/('v08_'+name+'.mesh.json')).replace(path)
    (prev.ROOT/'src/Art'/path.name).write_bytes(path.read_bytes())
    bpy.ops.export_scene.gltf(filepath=str(folder/(name+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_animations=False)
    bpy.ops.wm.obj_export(filepath=str(folder/(name+'.obj')),export_selected_objects=True,forward_axis='NEGATIVE_Z',up_axis='Y',export_materials=True,export_uv=True,export_normals=True,path_mode='RELATIVE')
    p=folder/(name+'.mtl');p.write_text('\n'.join('map_Kd '+Path(l[7:].replace('\\','/')).name if l.startswith('map_Kd ') else l for l in p.read_text().splitlines())+'\n')
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(folder/(name+'.blend')))
    print('CORNER_NORMALS_REPAIRED',name,fixed,flush=True)
