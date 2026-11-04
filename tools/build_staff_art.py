"""Blender 5.2: --background --factory-startup --python tools/build_staff_art.py -- --root PATH.

Original geometry for the Wizard Staff (no game assets), modelled after the user's carved wand photo:
a pale, slightly knotted shaft; a pyrography-burnt carved section with raised leaves; an amber collar
bead; a carved leaf cup; and a bulbous leaf-wrapped handle holding a clear crystal, ending in a point.

Exports src/Art/staff.mesh.json (tip = origin, pointing +Z in Unity, same format as
tools/build_item_art.py), the baked wood atlas src/Art/staff_atlas.png, the inventory render
src/Art/staff_inventory.png, plus an editable scene and preview in art/staff/.
Axes: Blender X/right Y/forward Z/up -> Unity X/right Y/up Z/forward (reverse winding).
"""
import argparse
import json
import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Euler, Matrix, Vector

args = argparse.ArgumentParser(); args.add_argument('--root', type=Path, required=True)
ROOT = args.parse_args(sys.argv[sys.argv.index('--') + 1:]).root.resolve()
OUT = ROOT / 'src' / 'Art'
ART = ROOT / 'art' / 'staff'; ART.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'; scene.cycles.samples = 48
scene.view_settings.view_transform = 'AgX'
scene.render.image_settings.file_format = 'PNG'
rng = random.Random(7)
parts = []
MATS = []


def lin(c): return tuple(x ** 2.2 for x in c)


def material(name, color, kind):
    """Procedural wood: 'shaft' pale grained wood, 'burnt' pyrography-darkened varnished carving,
    'leaf' lighter raised carving with burnt outlines, 'flat' plain (crystal)."""
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; N = nt.nodes; L = nt.links
    for n in list(N): N.remove(n)
    out = N.new('ShaderNodeOutputMaterial'); bsdf = N.new('ShaderNodeBsdfPrincipled'); emit = N.new('ShaderNodeEmission')
    bsdf.inputs['Roughness'].default_value = .55
    tc = N.new('ShaderNodeTexCoord')

    def setv(sock, v):
        if hasattr(v, 'is_output'): L.new(v, sock)
        elif isinstance(v, (int, float)): sock.default_value = v
        else: sock.default_value = (*v[:3], 1)

    def mix(fac, a, b, blend='MIX'):
        x = N.new('ShaderNodeMixRGB'); x.blend_type = blend
        setv(x.inputs[0], fac); setv(x.inputs[1], a); setv(x.inputs[2], b); return x.outputs[0]

    def ramp(src, a, b, ca, cb):
        r = N.new('ShaderNodeValToRGB'); e = r.color_ramp.elements
        e[0].position = a; e[0].color = (*ca, 1); e[1].position = b; e[1].color = (*cb, 1)
        L.new(src, r.inputs['Fac']); return r.outputs['Color']

    def noise(scale, detail=6.0, coords=None):
        n = N.new('ShaderNodeTexNoise'); n.inputs['Scale'].default_value = scale; n.inputs['Detail'].default_value = detail
        L.new(coords or tc.outputs['Object'], n.inputs['Vector']); return n.outputs['Fac']

    def bw(src):
        c = N.new('ShaderNodeRGBToBW'); L.new(src, c.inputs[0]); return c.outputs[0]

    col = mix(0.0, lin(color), lin(color))
    if kind == 'gem':
        # Clear stone: bright milky centre facing up, cooler darker rim, a small white highlight.
        geo = N.new('ShaderNodeNewGeometry'); sep = N.new('ShaderNodeSeparateXYZ'); L.new(geo.outputs['Normal'], sep.inputs[0])
        col = ramp(sep.outputs['Z'], .2, .95, lin((.36, .46, .60)), lin((.90, .95, 1.0)))
        hl = N.new('ShaderNodeVectorMath'); hl.operation = 'DOT_PRODUCT'; hl.inputs[1].default_value = (-.45, .35, .82)
        L.new(geo.outputs['Normal'], hl.inputs[0])
        col = mix(bw(ramp(hl.outputs['Value'], .93, .975, (0, 0, 0), (1, 1, 1))), col, (1, 1, 1))
    elif kind != 'flat':
        # Grain runs along the shaft (Blender Y): stretch coordinates across X/Z only.
        mp = N.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (90, 4, 90)
        L.new(tc.outputs['Object'], mp.inputs['Vector'])
        wave = N.new('ShaderNodeTexWave'); wave.wave_type = 'RINGS'; wave.rings_direction = 'Y'
        wave.inputs['Scale'].default_value = .6; wave.inputs['Distortion'].default_value = 5
        wave.inputs['Detail'].default_value = 3; L.new(mp.outputs['Vector'], wave.inputs['Vector'])
        grain_dark = (.78, .70, .62) if kind == 'shaft' else (.62, .55, .48)
        col = mix(1.0, col, ramp(wave.outputs['Fac'], .25, .85, grain_dark, (1, 1, 1)), 'MULTIPLY')
        col = mix(1.0, col, ramp(noise(40, 4), .3, .7, (.88, .88, .88), (1, 1, 1)), 'MULTIPLY')
        bev = N.new('ShaderNodeBevel'); bev.inputs['Radius'].default_value = .0025; bev.samples = 8
        geo = N.new('ShaderNodeNewGeometry')
        dot = N.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'
        L.new(bev.outputs['Normal'], dot.inputs[0]); L.new(geo.outputs['Normal'], dot.inputs[1])
        if kind == 'burnt':
            # Pyrography: blotchy scorch with darker burnt crevices; worn highlights on carved edges.
            col = mix(1.0, col, ramp(noise(55, 6), .35, .68, lin((.32, .20, .10)), (1, 1, 1)), 'MULTIPLY')
            col = mix(bw(ramp(dot.outputs['Value'], .80, .97, (1, 1, 1), (0, 0, 0))), col, lin((.86, .66, .36)))
        elif kind == 'leaf':
            # Raised leaves: light carved faces with burnt outlines.
            col = mix(bw(ramp(dot.outputs['Value'], .55, .93, (1, 1, 1), (0, 0, 0))), col, lin((.22, .12, .05)))
        else:
            col = mix(1.0, col, ramp(noise(9, 5), .55, .72, (1, 1, 1), lin((.62, .50, .36))), 'MULTIPLY')
        ao = N.new('ShaderNodeAmbientOcclusion'); ao.samples = 16; ao.inputs['Distance'].default_value = .012
        col = mix(1.0, col, ramp(ao.outputs['AO'], .3, 1.0, lin((.25, .14, .06)), (1, 1, 1)), 'MULTIPLY')
    L.new(col, bsdf.inputs['Base Color']); L.new(col, emit.inputs['Color']); L.new(bsdf.outputs[0], out.inputs['Surface'])
    m['ihm_emit'] = emit.name; m['ihm_bsdf'] = bsdf.name; m['ihm_out'] = out.name
    N.new('ShaderNodeTexImage').name = 'IHM Bake Target'
    MATS.append(m)
    return m


pale = material('Pale shaft wood', (.86, .74, .55), 'shaft')
knot = material('Knot', (.45, .30, .16), 'shaft')
burnt = material('Burnt carving', (.52, .32, .13), 'burnt')
amber = material('Varnished amber', (.74, .52, .22), 'burnt')
leaf = material('Carved leaf', (.84, .64, .32), 'leaf')
crystal = material('Crystal', (.86, .93, 1.0), 'gem')


def finish(o, mat):
    o.data.materials.append(mat); parts.append(o); return o


def taper(name, y0, y1, r0, r1, mat, verts=14):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r0, depth=abs(y1 - y0), location=(0, (y0 + y1) / 2, 0))
    o = bpy.context.object; o.name = name; o.rotation_euler.x = -math.pi / 2
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish(o, mat)


def ellipsoid(name, loc, r, scale, mat, rot=None, segs=14):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=max(6, segs // 2), radius=r, location=loc)
    o = bpy.context.object; o.name = name; o.scale = scale
    if rot is not None: o.rotation_euler = rot
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish(o, mat)


def leaf_mesh(name, length, width, height, bend):
    """Pointed carved leaf: lens outline, raised centre vein, bent to hug a round surface of radius `bend`."""
    import bmesh
    bm = bmesh.new(); steps = 9; rows = []
    for i in range(steps + 1):
        t = i / steps; y = (t - .5) * length
        w = width * .5 * math.sin(math.pi * t) ** .75
        row = []
        for x, z in ((-w, 0.0), (-w * .45, height * .7), (0.0, height), (w * .45, height * .7), (w, 0.0)):
            z -= (x * x) / (2 * bend)  # wrap around the surface
            row.append(bm.verts.new((x, y, z)))
        rows.append(row)
    for a, b in zip(rows, rows[1:]):
        for j in range(4):
            bm.faces.new((a[j], a[j + 1], b[j + 1], b[j]))
    bottom = [r[0] for r in rows] + [r[-1] for r in reversed(rows)]
    bm.faces.new(bottom[::-1])
    mesh = bpy.data.meshes.new(name); bm.to_mesh(mesh); bm.free()
    o = bpy.data.objects.new(name, mesh); scene.collection.objects.link(o)
    return o


def leaf_on(name, y, radius, angle, size=1.0, tilt=0.0, mat=leaf):
    """A raised carved leaf lying on a round surface (local Z = outward normal), slightly sunk in."""
    o = leaf_mesh(name, .016 * size, .0075 * size, .0022, radius)
    o.location = ((radius - .0004) * math.sin(angle), y, (radius - .0004) * math.cos(angle))
    o.rotation_euler = (Matrix.Rotation(angle, 4, 'Y') @ Matrix.Rotation(tilt, 4, 'Z')).to_euler()
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish(o, mat)


# --- Pale shaft (tip at the origin, wand extends toward -Y) ---------------------------------
ellipsoid('Rounded tip', (0, -.003, 0), .0056, (1, 1, 1), pale, segs=12)
taper('Shaft', -.003, -.30, .0056, .0105, pale, 14)
for y, a in ((-.11, .4), (-.19, 2.6), (-.25, 4.4)):
    r = .0056 + (.0105 - .0056) * (-y / .30)
    ellipsoid('Knot', (r * math.sin(a) * .92, y, r * math.cos(a) * .92), .0028, (1, 1.6, .7), knot,
              (Matrix.Rotation(a, 4, 'Y')).to_euler(), 8)

# --- Burnt carved section with raised leaves -----------------------------------------------
taper('Carved section', -.30, -.43, .0112, .0150, burnt, 16)
for i in range(38):
    y = -.306 - i * .0032
    r = .0112 + (.0150 - .0112) * ((-.30 - y) / .13)
    leaf_on('Carved leaf', y, r, i * 2.39996 + rng.uniform(-.25, .25), rng.uniform(.75, 1.1), rng.uniform(-.7, .7))
for i in range(5):  # small carved flowers between the leaves
    y = -.32 - i * .025; a = i * 1.9 + 1
    r = .0112 + (.0150 - .0112) * ((-.30 - y) / .13) + .0016
    ellipsoid('Carved flower', (r * math.sin(a), y, r * math.cos(a)), .0032, (1, 1, .45), leaf,
              Matrix.Rotation(a, 4, 'Y').to_euler(), 10)

# --- Collar bead, neck, leaf cup -----------------------------------------------------------
taper('Collar upper', -.43, -.452, .0145, .0235, amber, 18)
taper('Collar lower', -.452, -.476, .0235, .0118, amber, 18)
taper('Neck', -.476, -.490, .0118, .0128, burnt, 14)
ellipsoid('Leaf cup', (0, -.506, .003), .021, (1, 1.25, .72), amber, segs=18)
ellipsoid('Leaf cup hollow', (0, -.504, .0125), .016, (.72, 1.0, .25), burnt, segs=16)
for side in (-1, 1):
    ellipsoid('Cup leaf', (side * .016, -.512, .002), 1, (.0075, .016, .0026), leaf,
              Euler((0, side * 1.25, side * .35)), 12)

# --- Bulb handle with the crystal ----------------------------------------------------------
ellipsoid('Handle bulb', (0, -.566, 0), .0255, (1, 1.55, 1), burnt, segs=22)
def bulb_r(y): return .0255 * math.sqrt(max(.05, 1 - ((y + .566) / .0395) ** 2))


for i in range(22):
    a = i * 2.39996 + .5; y = -.551 - (i % 6) * .0058
    if math.cos(a) > .5: continue  # keep the crystal on top clear
    leaf_on('Handle leaf', y, bulb_r(y) - .0008, a, rng.uniform(.8, 1.0), rng.uniform(-.7, .7))
CRYSTAL = Vector((0, -.567, .0215))
bpy.ops.mesh.primitive_torus_add(major_radius=.0150, minor_radius=.0030, major_segments=22, minor_segments=8, location=CRYSTAL - Vector((0, 0, .004)))
finish(bpy.context.object, burnt).name = 'Crystal bezel'
ellipsoid('Crystal', CRYSTAL, .0138, (1, 1, .85), crystal, segs=20)
for dy, a, tilt in ((.019, 0, math.pi / 2), (-.019, 0, math.pi / 2), (0, .66, 0), (0, -.66, 0)):
    y = CRYSTAL.y + dy
    leaf_on('Crystal leaf', y, bulb_r(y) - .0008, a, 1.0, tilt)
taper('Pointed end', -.598, -.645, .019, .0035, burnt, 16)

# --- Bake one atlas for the whole wand --------------------------------------------------------
img = bpy.data.images.new('staff_atlas', 1024, 1024, alpha=False)
bpy.ops.object.select_all(action='DESELECT')
for o in parts:
    o.select_set(True)
    layer = o.data.uv_layers.get('Atlas') or o.data.uv_layers.new(name='Atlas'); o.data.uv_layers.active = layer
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=.003, scale_to_bounds=False)
bpy.ops.uv.pack_islands(margin=.004, rotate=True)
bpy.ops.object.mode_set(mode='OBJECT')
for o in parts:
    for layer in [l for l in o.data.uv_layers if l.name != 'Atlas']: o.data.uv_layers.remove(layer)
    o.data.uv_layers['Atlas'].active = True; o.data.uv_layers['Atlas'].active_render = True
for m in MATS:
    N = m.node_tree.nodes; t = N['IHM Bake Target']; t.image = img; N.active = t
    m.node_tree.links.new(N[m['ihm_emit']].outputs[0], N[m['ihm_out']].inputs['Surface'])
scene.cycles.samples = 24; scene.render.bake.margin = 6
bpy.ops.object.bake(type='EMIT')
img.filepath_raw = str(OUT / 'staff_atlas.png'); img.file_format = 'PNG'; img.save()
shown = bpy.data.materials.new('Staff baked'); shown.use_nodes = True
bsdf = shown.node_tree.nodes.get('Principled BSDF'); bsdf.inputs['Roughness'].default_value = .6
tex = shown.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = img; tex.interpolation = 'Closest'
shown.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])

# --- Export (origin = tip) -----------------------------------------------------------------
C = Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))


def vec(v): return {'x': round(v[0], 6), 'y': round(v[1], 6), 'z': round(v[2], 6)}


data = {'version': 1, 'fps': 30, 'frames': 1, 'parts': []}
for o in parts:
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(50), keep_sharp_edges=False)
    mesh = o.data; mesh.calc_loop_triangles(); vertices = []; normals = []; uv = []; indices = []
    for tri in mesh.loop_triangles:
        for li in reversed(tri.loops):
            loop = mesh.loops[li]; vertices.append(vec(C @ mesh.vertices[loop.vertex_index].co))
            normals.append(vec(C @ mesh.corner_normals[li].vector))
            p = mesh.uv_layers.active.data[li].uv
            uv.append({'x': float(p[0]), 'y': float(p[1])}); indices.append(len(indices))
    p, q, s = o.matrix_world.decompose(); q = (C @ q.to_matrix() @ C).to_quaternion()
    rest = {'position': vec(C @ p), 'rotation': {'x': q.x, 'y': q.y, 'z': q.z, 'w': q.w}, 'scale': vec(C @ s)}
    data['parts'].append({'name': o.name, 'vertices': vertices, 'normals': normals, 'uv': uv, 'triangles': indices,
                          'color': {'r': 1, 'g': 1, 'b': 1, 'a': 1}, 'texture': 'staff_atlas.png', 'rest': rest, 'samples': []})
    o.data.materials.clear(); o.data.materials.append(shown)
(OUT / 'staff.mesh.json').write_text(json.dumps(data, separators=(',', ':')), encoding='utf8')

# --- Preview and inventory render ----------------------------------------------------------
for name, loc, power, size in (('Key', (.5, .3, .8), 60, .8), ('Fill', (-.6, -.2, .4), 25, .8), ('Rim', (0, -.9, .5), 40, .5)):
    bpy.ops.object.light_add(type='AREA', location=loc); light = bpy.context.object; light.name = name
    light.data.energy = power; light.data.size = size
    light.rotation_euler = (Vector((0, -.32, 0)) - light.location).to_track_quat('-Z', 'Y').to_euler()
bpy.ops.object.camera_add(location=(.55, -.05, .42)); cam = bpy.context.object; scene.camera = cam
cam.rotation_euler = (Vector((0, -.33, 0)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam.data.type = 'ORTHO'; cam.data.ortho_scale = .72
scene.render.resolution_x = scene.render.resolution_y = 256; scene.render.film_transparent = True
scene.render.filepath = str(OUT / 'staff_inventory.png'); bpy.ops.render.render(write_still=True)
bpy.ops.mesh.primitive_plane_add(size=3, location=(0, -.3, -.03)); floor = bpy.context.object
floor.data.materials.append(material('Backdrop', (.05, .06, .065), 'flat'))
scene.render.resolution_x = scene.render.resolution_y = 900; scene.render.film_transparent = False
scene.render.filepath = str(ART / 'staff_preview.png'); bpy.ops.render.render(write_still=True)
cam.location = CRYSTAL + Vector((.12, .10, .22)); cam.rotation_euler = (Vector((0, -.53, 0)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam.data.ortho_scale = .2; cam.data.clip_start = .001
scene.render.filepath = str(ART / 'staff_handle_closeup.png'); bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ART / 'staff.blend'))
print('IHM_STAFF_ART_OK', len(parts), 'parts; crystal at', tuple(round(x, 4) for x in (C @ CRYSTAL)))
