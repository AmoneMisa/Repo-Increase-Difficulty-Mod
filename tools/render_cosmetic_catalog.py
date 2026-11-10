"""Render the exported models themselves for fit, silhouette and tint review; never exports the preview body."""
import argparse,json,sys
from pathlib import Path
import bpy
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
import cosmetic_kit as K
import face_parts as F

p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--base',type=Path);p.add_argument('--sets',nargs='*')
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);a.root=a.root.resolve();K.configure(a.root,a.base)
sets=a.sets or ['musketeer','bombbird','talkingdog','rabbid','jackal','panda','penguin','kitty','chipmunk']
slots={'hood':'Hat','suit':'Body Top','belt':'Body Bottom','glove_r':'Arm Right','glove_l':'Arm Left','boot_r':'Leg Right','boot_l':'Leg Left',
       'face':'Face Top','lips':'Face Bottom','neck':'Head Bottom'}   # the face parts exist only for some sets

def vec(v):return Vector((v['x'],v['z'],v['y']))

for kind in sets:
    K.setup_scene();tint_mats=[]
    for piece,slot in slots.items():
        key=kind+'_'+piece
        if not (K.SRC/f'v11_{key}.mesh.json').exists():continue
        data=json.loads((K.SRC/f'v11_{key}.mesh.json').read_text())
        mats=[]
        for m in data['materials']:
            c=m['color'];mat=K.material(m['name'],(c['r'],c['g'],c['b']),m['roughness'],m['metallic'],image=m['texture'],clean=True)
            mats.append(mat)
            if m.get('tint'):tint_mats.append(mat)
        for part in data['parts']:
            rest=vec(part['rest']['position']);vs=[vec(v)+rest for v in part['vertices']];tri=part['triangles']
            faces=[(tri[i],tri[i+2],tri[i+1]) for i in range(0,len(tri),3)]
            # Do not weld vertices here: exported UV seams and their indices must survive verbatim.
            mesh=bpy.data.meshes.new(part['name']);mesh.from_pydata(vs,[],faces);mesh.update()
            ob=bpy.data.objects.new(part['name'],mesh);bpy.context.collection.objects.link(ob)
            mesh.materials.append(mats[part['material']])
            for poly in mesh.polygons:poly.use_smooth=True
            mesh.normals_split_custom_set_from_vertices([vec(n) for n in part['normals']])
            uv=ob.data.uv_layers.new(name='UV')
            for poly in ob.data.polygons:
                for li in poly.loop_indices:
                    tex=part['uv'][ob.data.loops[li].vertex_index];uv.data[li].uv=(tex['x'],tex['y'])
            ob.matrix_world=K.frame_matrix(K.FRAMES[slot])
    K.stand_in_body()
    # Eye stand-ins at the native eyes' measured place and size. Review only, never exported.
    F.preview_eyes()
    cam=K.lights_and_camera();cam.data.type='ORTHO';cam.data.ortho_scale=2.8
    scene=bpy.context.scene;scene.render.resolution_x=820;scene.render.resolution_y=1000;scene.cycles.samples=32
    folder=a.root/'art'/'cosmetics'/kind;folder.mkdir(parents=True,exist_ok=True)
    target=Vector((0,0,.38))
    for name,eye in [('front',(1.2,3.6,1.1)),('back',(-1.2,-3.6,1.1)),('side',(3.8,-.3,.8))]:
        K.look(cam,target+Vector(eye),target);K.render(folder/f'{kind}_{name}.png')
    if True:
        # Close view of the head round the native eyes.
        cam.data.ortho_scale=.95;head=Vector((0,0,.80))
        for name,eye in [('face',(.5,3.6,.35)),('face_side',(3.2,1.6,.3))]:
            K.look(cam,head+Vector(eye),head);K.render(folder/f'{kind}_{name}.png')
        cam.data.ortho_scale=2.8
    # Exact slot tint operation: replace the base colour while retaining neutral textures and fixed details.
    for m in tint_mats:
        for n in m.node_tree.nodes:
            if n.type=='MIX' and n.data_type=='RGBA':n.inputs['B'].default_value=(.13,.42,.86,1)
        m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.13,.42,.86,1)
    K.look(cam,target+Vector((1.2,3.6,1.1)),target);K.render(folder/f'{kind}_tint.png')
    print('CATALOG_REVIEW',kind,flush=True)
