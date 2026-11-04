"""Blender 5.2: --background --factory-startup --python tools/build_flamethrower_art.py -- --root PATH.

Original geometry for the Flamethrower C.A.R.T. turret (no game assets). Exports
src/Art/flamethrower.mesh.json in the same format as tools/build_item_art.py, plus an
editable scene and a preview render in art/flamethrower/.
Axes: Blender X/right Y/forward Z/up -> Unity X/right Y/up Z/forward (reverse winding).
Origin: centre of the mount base. The nozzle tip position is printed as IHM_NOZZLE.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

args = argparse.ArgumentParser(); args.add_argument('--root', type=Path, required=True)
ROOT = args.parse_args(sys.argv[sys.argv.index('--') + 1:]).root.resolve()
OUT = ROOT / 'src' / 'Art'
ART = ROOT / 'art' / 'flamethrower'; ART.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'; scene.cycles.samples = 32
scene.view_settings.view_transform = 'AgX'
scene.render.image_settings.file_format = 'PNG'

wear = bpy.data.images.load(str(OUT / 'workshop_wear.png'))
parts = []


def material(name, color, metal=0.0, rough=.7):
    m = bpy.data.materials.new(name); m.diffuse_color = (*color, 1); m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Metallic'].default_value = metal; p.inputs['Roughness'].default_value = rough
    t = m.node_tree.nodes.new('ShaderNodeTexImage'); t.image = wear; t.interpolation = 'Closest'
    mix = m.node_tree.nodes.new('ShaderNodeMixRGB'); mix.blend_type = 'MULTIPLY'; mix.inputs[0].default_value = 1
    mix.inputs[1].default_value = (*(c ** 2.2 for c in color), 1)
    m.node_tree.links.new(t.outputs['Color'], mix.inputs[2]); m.node_tree.links.new(mix.outputs[0], p.inputs['Base Color'])
    return m


sand = material('Sand enamel', (.74, .63, .43), .1)
sand_dark = material('Sand enamel shadow', (.55, .46, .31), .1)
red = material('Fuel tank red', (.74, .17, .12), .15, .55)
steel = material('Burnt steel', (.30, .30, .28), .6, .5)
dark = material('Graphite rubber', (.055, .062, .060))
black = material('Bore void', (.012, .012, .012))
brass = material('Brass fittings', (.72, .55, .24), .7, .4)
bone = material('Pilot light lens', (.95, .78, .38))


def finish(o, mat):
    o.data.materials.append(mat); parts.append(o); return o


def box(name, loc, size, mat, bevel=.006, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc); o = bpy.context.object; o.name = name
    o.dimensions = size; o.rotation_euler = rot
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel:
        mod = o.modifiers.new('Rounded edges', 'BEVEL'); mod.width = bevel; mod.segments = 3
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(o, mat)


def cylinder(name, loc, r, depth, mat, axis='Z', verts=20, bevel=True):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc)
    o = bpy.context.object; o.name = name
    if axis == 'Y': o.rotation_euler.x = math.pi / 2
    if axis == 'X': o.rotation_euler.y = math.pi / 2
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel:
        mod = o.modifiers.new('Turned rim', 'BEVEL'); mod.width = min(r * .15, depth * .2, .006); mod.segments = 3
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(o, mat)


def sphere(name, loc, r, mat, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=10, radius=r, location=loc)
    o = bpy.context.object; o.name = name; o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(o, mat)


def bar(name, a, b, r, mat):
    a, b = Vector(a), Vector(b)
    o = cylinder(name, (a + b) / 2, r, (b - a).length, mat, verts=8, bevel=False)
    o.rotation_euler = (b - a).to_track_quat('Z', 'Y').to_euler()
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    return o


# --- Mount: turntable, column and yoke --------------------------------------------------
cylinder('Turntable plate', (0, 0, .025), .19, .05, dark, verts=28)
for i in range(8):
    a = i * math.pi / 4
    cylinder('Turntable bolt', (math.cos(a) * .16, math.sin(a) * .16, .055), .013, .016, steel, verts=8)
cylinder('Swivel column', (0, 0, .13), .08, .17, steel, verts=20)
box('Yoke cross plate', (0, 0, .225), (.40, .16, .045), sand_dark, .008)
for x in (-.17, .17):
    box('Yoke cheek', (x, 0, .33), (.045, .19, .23), sand, .01)
    cylinder('Trunnion cap', (x * 1.08, 0, .40), .045, .03, steel, 'X', 16)

# --- Gun body ---------------------------------------------------------------------------
box('Receiver', (0, .02, .41), (.25, .46, .19), sand, .025)
box('Receiver top rail', (0, .02, .515), (.12, .40, .03), sand_dark, .006)
box('Rear ignition box', (0, -.24, .40), (.20, .10, .15), sand_dark, .015)
for y in (.14, .19, .24):
    box('Cooling fin', (0, y, .41), (.29, .018, .23), dark, .004)
box('Heat shield', (0, .38, .42), (.21, .14, .16), steel, .012)
for x in (-.055, .055):
    cylinder('Barrel', (x, .55, .42), .032, .30, steel, 'Y', 16)
    cylinder('Barrel ring', (x, .47, .42), .040, .025, dark, 'Y', 16)
    cylinder('Nozzle flare', (x, .70, .42), .046, .05, dark, 'Y', 18)
    cylinder('Nozzle bore', (x, .726, .42), .026, .006, black, 'Y', 14, bevel=False)
box('Nozzle bridge', (0, .64, .42), (.11, .03, .035), steel, .004)
cylinder('Pilot jet', (0, .68, .475), .012, .07, brass, 'Y', 10)
sphere('Pilot light', (0, .72, .475), .016, bone)
box('Rear grip', (0, -.33, .43), (.05, .08, .12), dark, .012)

# --- Fuel tanks, straps and hoses ---------------------------------------------------------
for x in (-.215, .215):
    cylinder('Fuel tank', (x, -.08, .44), .082, .34, red, 'Y', 24)
    sphere('Fuel tank dome', (x, .09, .44), .082, red, (1, .45, 1))
    sphere('Fuel tank dome', (x, -.25, .44), .082, red, (1, .45, 1))
    cylinder('Tank valve', (x, -.30, .44), .03, .05, brass, 'Y', 12)
    for y in (-.17, .01):
        cylinder('Tank strap', (x, y, .44), .087, .022, dark, 'Y', 24)
    bar('Feed hose', (x, .13, .44), (x * .45, .20, .36), .014, dark)
    box('Tank bracket', (x * .78, -.08, .40), (.06, .22, .04), sand_dark, .006)
bar('Return hose', (-.215, -.30, .44), (0, -.30, .36), .012, dark)
bar('Return hose', (.215, -.30, .44), (0, -.30, .36), .012, dark)

nozzle = Vector((0, .73, .42))
C = Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))


def vec(v): return {'x': round(v[0], 6), 'y': round(v[1], 6), 'z': round(v[2], 6)}


def pose(o):
    p, q, s = o.matrix_local.decompose(); q = (C @ q.to_matrix() @ C).to_quaternion()
    return {'position': vec(C @ p), 'rotation': {'x': q.x, 'y': q.y, 'z': q.z, 'w': q.w}, 'scale': vec(C @ s)}


data = {'version': 1, 'fps': 30, 'frames': 1, 'parts': []}
for o in parts:
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(45), keep_sharp_edges=False)
    if not o.data.uv_layers:
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(island_margin=.02); bpy.ops.object.mode_set(mode='OBJECT')
    mesh = o.data; mesh.calc_loop_triangles(); vertices = []; normals = []; uv = []; indices = []
    for tri in mesh.loop_triangles:
        for li in reversed(tri.loops):
            loop = mesh.loops[li]; vertices.append(vec(C @ mesh.vertices[loop.vertex_index].co))
            normals.append(vec(C @ mesh.corner_normals[li].vector))
            p = mesh.uv_layers.active.data[li].uv
            uv.append({'x': float(p[0]), 'y': float(p[1])}); indices.append(len(indices))
    col = mesh.materials[0].diffuse_color
    data['parts'].append({'name': o.name, 'vertices': vertices, 'normals': normals, 'uv': uv,
                          'triangles': indices, 'color': {'r': col[0], 'g': col[1], 'b': col[2], 'a': 1},
                          'texture': 'workshop_wear.png', 'rest': pose(o), 'samples': []})
(OUT / 'flamethrower.mesh.json').write_text(json.dumps(data, separators=(',', ':')), encoding='utf8')

# Preview and editable scene.
bpy.ops.mesh.primitive_plane_add(size=4, location=(0, 0, 0)); floor = bpy.context.object
floor.data.materials.append(material('Backdrop', (.05, .06, .065)))
for name, loc, power, size in (('Key', (1.5, -1.8, 2.4), 260, 2.5), ('Fill', (-2, 0, 1.4), 120, 2), ('Rim', (0, 2.2, 1.8), 200, 1.5)):
    bpy.ops.object.light_add(type='AREA', location=loc); light = bpy.context.object; light.name = name
    light.data.energy = power; light.data.size = size
    light.rotation_euler = (Vector((0, 0, .35)) - light.location).to_track_quat('-Z', 'Y').to_euler()
bpy.ops.object.camera_add(location=(1.55, 1.35, 1.0)); cam = bpy.context.object; scene.camera = cam
cam.rotation_euler = (Vector((0, .12, .33)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 1.35
scene.render.resolution_x = scene.render.resolution_y = 900
scene.render.filepath = str(ART / 'flamethrower_preview.png'); bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ART / 'flamethrower.blend'))
unity_nozzle = C @ nozzle
print('IHM_NOZZLE', round(unity_nozzle.x, 4), round(unity_nozzle.y, 4), round(unity_nozzle.z, 4))
print('IHM_FLAME_ART_OK', len(parts), 'parts')
