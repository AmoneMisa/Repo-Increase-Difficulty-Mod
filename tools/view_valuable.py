"""Blender: --background --factory-startup --python tools/view_valuable.py -- MODEL.mesh.json OUT_PREFIX

Render a valuable's exported mesh JSON (ValuableVisuals format) from the front, side and three-quarter view, each part
at its rest position, coloured by its material colour and with its pivots marked (red balls). For checking exports.
"""
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:]
model = json.loads(Path(args[0]).read_text())
out = args[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'CYCLES'
if scene.render.engine == 'CYCLES': scene.cycles.samples = 24


def B(v): return Vector((v['x'], v['z'], v['y']))


mats = []
art = Path(args[0]).resolve().parent
for m in model['materials']:
    mat = bpy.data.materials.new(m['name']); mat.use_nodes = True
    bsdf = mat.node_tree.nodes['Principled BSDF']
    c = m['color']; bsdf.inputs['Base Color'].default_value = (c['r'], c['g'], c['b'], 1)
    if m.get('texture') and (art / m['texture']).exists():          # show the real texture, as the game does
        tex = mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(str(art / m['texture']))
        mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    mats.append(mat)
lo = Vector((1e9,) * 3); hi = -lo
for part in model['parts']:
    rest = B(part['rest']['position'])
    verts = [B(v) + rest for v in part['vertices']]
    tris = part['triangles']
    faces = [(tris[i], tris[i + 2], tris[i + 1]) for i in range(0, len(tris), 3)]
    me = bpy.data.meshes.new(part['name']); me.from_pydata([tuple(v) for v in verts], [], faces); me.update()
    if part.get('uv'):
        layer = me.uv_layers.new(name='UVMap')
        for poly in me.polygons:
            for li in poly.loop_indices:
                u = part['uv'][me.loops[li].vertex_index]; layer.data[li].uv = (u['x'], u['y'])
    ob = bpy.data.objects.new(part['name'], me); bpy.context.collection.objects.link(ob)
    me.materials.append(mats[part['material']])
    for v in verts:
        lo = Vector(tuple(min(lo[i], v[i]) for i in range(3))); hi = Vector(tuple(max(hi[i], v[i]) for i in range(3)))
red = bpy.data.materials.new('pivot'); red.use_nodes = True; red.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (1, 0, 0, 1)
for p in model.get('pivots') or []:
    bpy.ops.mesh.primitive_uv_sphere_add(radius=(hi - lo).length * .012, location=B(p['position'])); bpy.context.object.data.materials.append(red)
world = bpy.data.worlds.new('w'); scene.world = world; world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (.5, .52, .56, 1)
bpy.ops.object.light_add(type='SUN', location=(2, -3, 4)); bpy.context.object.data.energy = 3
bpy.ops.object.camera_add(); cam = bpy.context.object; scene.camera = cam
scene.render.resolution_x = scene.render.resolution_y = 600
c = (lo + hi) / 2; r = (hi - lo).length
for name, d in (('front', Vector((0, 1, .15))), ('side', Vector((1, 0, .15))), ('three', Vector((.8, .8, .5)))):
    cam.location = c + d.normalized() * r * 1.9
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = f'{out}_{name}.png'; bpy.ops.render.render(write_still=True)
print('VIEW_DONE')
