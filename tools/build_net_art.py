"""Blender 5.2: --background --factory-startup --python tools/build_net_art.py -- --root PATH.

Original geometry for the Net Launcher (no game assets): a knotted rope-cage funnel at the muzzle, a dark
collar, an octagonal dark-metal body with worn hazard-yellow side panels (dark inset with a white net icon
and two glowing yellow bars), a rope spool at the back between end plates, a grip and a red trigger.

Exports src/Art/net.mesh.json (muzzle = origin, pointing +Z in Unity; same format as build_staff_art.py),
the baked atlas src/Art/net_atlas.png, the inventory render src/Art/net_inventory.png, and an editable
scene + preview in art/net/. Axes: Blender X/right Y/forward Z/up -> Unity X/right Y/up Z/forward.
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
ART = ROOT / 'art' / 'net'; ART.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'; scene.cycles.samples = 48
scene.view_settings.view_transform = 'AgX'
scene.render.image_settings.file_format = 'PNG'
parts = []
MATS = []
S = .85  # overall scale: about the length of the vanilla handgun's grab shape


def lin(c): return tuple(x ** 2.2 for x in c)


def material(name, color, kind):
    """'metal' dark worn metal, 'paint' worn hazard paint, 'rope' twisted cream rope, 'flat' plain/glow."""
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; N = nt.nodes; L = nt.links
    for n in list(N): N.remove(n)
    out = N.new('ShaderNodeOutputMaterial'); bsdf = N.new('ShaderNodeBsdfPrincipled'); emit = N.new('ShaderNodeEmission')
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

    def noise(scale, detail=6.0):
        n = N.new('ShaderNodeTexNoise'); n.inputs['Scale'].default_value = scale; n.inputs['Detail'].default_value = detail
        L.new(tc.outputs['Object'], n.inputs['Vector']); return n.outputs['Fac']

    def bw(src):
        c = N.new('ShaderNodeRGBToBW'); L.new(src, c.inputs[0]); return c.outputs[0]

    col = mix(0.0, lin(color), lin(color))
    if kind in ('metal', 'paint'):
        bev = N.new('ShaderNodeBevel'); bev.inputs['Radius'].default_value = .0022; bev.samples = 8
        geo = N.new('ShaderNodeNewGeometry')
        dot = N.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'
        L.new(bev.outputs['Normal'], dot.inputs[0]); L.new(geo.outputs['Normal'], dot.inputs[1])
        edge = bw(ramp(dot.outputs['Value'], .86, .98, (1, 1, 1), (0, 0, 0)))  # 1 on worn edges
        col = mix(1.0, col, ramp(noise(60, 5), .35, .7, (.78, .76, .72), (1, 1, 1)), 'MULTIPLY')  # grime
        if kind == 'paint':
            chips = bw(ramp(noise(45, 8), .58, .66, (0, 0, 0), (1, 1, 1)))
            col = mix(chips, col, lin((.16, .15, .15)))                       # chipped to dark metal
            col = mix(edge, col, lin((.42, .40, .36)))                        # bare metal on edges
        else:
            col = mix(edge, col, lin((.48, .47, .45)))                        # polished worn edges
        ao = N.new('ShaderNodeAmbientOcclusion'); ao.samples = 16; ao.inputs['Distance'].default_value = .01
        col = mix(1.0, col, ramp(ao.outputs['AO'], .3, 1.0, lin((.2, .18, .15)), (1, 1, 1)), 'MULTIPLY')
    elif kind == 'rope':
        # Twisted strands: diagonal bands along the rope, plus fibre noise.
        wave = N.new('ShaderNodeTexWave'); wave.wave_type = 'BANDS'; wave.bands_direction = 'DIAGONAL'
        wave.inputs['Scale'].default_value = 140; wave.inputs['Distortion'].default_value = 1.5
        L.new(tc.outputs['Object'], wave.inputs['Vector'])
        col = mix(1.0, col, ramp(wave.outputs['Fac'], .3, .9, (.62, .55, .44), (1, 1, 1)), 'MULTIPLY')
        col = mix(1.0, col, ramp(noise(180, 4), .35, .65, (.85, .82, .78), (1, 1, 1)), 'MULTIPLY')
    L.new(col, bsdf.inputs['Base Color']); L.new(col, emit.inputs['Color']); L.new(bsdf.outputs[0], out.inputs['Surface'])
    m['ihm_emit'] = emit.name; m['ihm_out'] = out.name
    N.new('ShaderNodeTexImage').name = 'IHM Bake Target'
    MATS.append(m)
    return m


metal = material('Dark metal', (.13, .13, .14), 'metal')
paint = material('Hazard yellow', (.95, .70, .10), 'paint')
rope = material('Rope', (.90, .82, .64), 'rope')
black = material('Inset', (.035, .035, .04), 'flat')
white = material('Icon', (.93, .93, .90), 'flat')
glow = material('Glow strip', (1.0, .86, .28), 'flat')
red = material('Trigger', (.78, .07, .05), 'flat')


def finish(o, mat):
    o.data.materials.append(mat); parts.append(o); return o


def apply(o):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)


def cyl(name, y0, y1, r0, r1, mat, verts=24, open_ends=False, roll=0.0):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r0, depth=abs(y1 - y0),
                                    location=(0, (y0 + y1) / 2, 0), end_fill_type='NOTHING' if open_ends else 'NGON')
    o = bpy.context.object; o.name = name; o.rotation_euler = (-math.pi / 2, roll, 0); apply(o)
    return finish(o, mat)


def box(name, loc, size, mat, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc); o = bpy.context.object; o.name = name
    o.scale = size; o.rotation_euler = rot; apply(o); return finish(o, mat)


def ring(name, y, radius, minor, mat, major_segments=28):
    bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=minor, major_segments=major_segments,
                                     minor_segments=8, location=(0, y, 0), rotation=(math.pi / 2, 0, 0))
    o = bpy.context.object; o.name = name; apply(o); return finish(o, mat)


def ball(name, loc, r, mat, segs=10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=max(5, segs // 2), radius=r, location=loc)
    o = bpy.context.object; o.name = name; return finish(o, mat)


def strand(name, a, b, r, mat, verts=8):
    a, b = Vector(a), Vector(b); d = b - a
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=d.length, location=(a + b) / 2)
    o = bpy.context.object; o.name = name
    o.rotation_euler = d.to_track_quat('Z', 'Y').to_euler(); apply(o); return finish(o, mat)


# --- Rope-cage net mouth (muzzle at the origin, launcher extends toward -Y) ---------------------
mouth = [(0.0, .062), (-.028, .052), (-.056, .043), (-.084, .034)]
cyl('Funnel', -.004, -.086, .058, .032, metal, 28, open_ends=True)
for y, r in mouth:
    ring('Net ring', y, r, .0042 if y == 0 else .0034, rope)
ribs = 10
for k in range(ribs):
    a = k / ribs * math.tau
    p0 = (math.cos(a) * mouth[0][1], mouth[0][0], math.sin(a) * mouth[0][1])
    p1 = (math.cos(a) * mouth[-1][1], mouth[-1][0], math.sin(a) * mouth[-1][1])
    strand('Net rib', p0, p1, .0031, rope)
    for y, r in mouth:
        ball('Net knot', (math.cos(a) * r, y, math.sin(a) * r), .0052, rope, 8)

# --- Collar and octagonal body ------------------------------------------------------------------
cyl('Collar', -.084, -.106, .036, .038, metal, 24)
ring('Collar ring', -.095, .038, .003, metal)
cyl('Body', -.104, -.286, .049, .049, metal, 16, roll=math.pi / 16)
for side in (-1, 1):
    x = side * .0465
    box('Side panel', (x, -.205, 0), (.007, .132, .058), paint)
    box('Icon inset', (x + side * .004, -.228, 0), (.003, .062, .046), black)
    # White net-racket icon: hoop, grid and handle.
    bpy.ops.mesh.primitive_torus_add(major_radius=.0125, minor_radius=.0011, major_segments=22, minor_segments=4,
                                     location=(x + side * .0058, -.235, .006), rotation=(0, math.pi / 2, 0))
    hoop = bpy.context.object; hoop.name = 'Icon hoop'; apply(hoop); finish(hoop, white)
    for d in (-.006, 0, .006):
        box('Icon line', (x + side * .0058, -.235 + d, .006), (.0012, .0011, .022), white)
        box('Icon line', (x + side * .0058, -.235, .006 + d), (.0012, .022, .0011), white)
    box('Icon handle', (x + side * .0058, -.218, -.011), (.0012, .0016, .02), white, (math.radians(-40), 0, 0))
    for z in (.011, -.004):
        box('Glow bar', (x + side * .004, -.152, z), (.004, .03, .0075), glow)
box('Top rail', (0, -.195, .049), (.03, .15, .008), paint)

# --- Rope spool at the back ---------------------------------------------------------------------
cyl('Spool core', -.286, -.352, .034, .034, metal, 20)
for i in range(9):
    ring('Spool rope', -.292 - i * .0068, .039, .0046, rope, 26)
cyl('Front plate', -.282, -.29, .051, .051, metal, 24)
cyl('Rear plate', -.348, -.358, .051, .051, paint, 24)
ball('Rear cap', (0, -.36, 0), .016, metal, 12).scale = (1, .45, 1)

# --- Grip and trigger ---------------------------------------------------------------------------
box('Grip', (0, -.245, -.083), (.032, .046, .105), metal, (math.radians(14), 0, 0))
box('Grip panel', (0, -.247, -.083), (.034, .028, .07), paint, (math.radians(14), 0, 0))
box('Trigger', (0, -.205, -.062), (.01, .012, .026), red, (math.radians(-10), 0, 0))
box('Trigger guard', (0, -.198, -.081), (.014, .05, .006), metal)

# Round every hard edge: bevel widths per part (metres, before scaling). Nothing stays boxy.
SOFT = {'Grip': (.014, 5), 'Grip panel': (.011, 5), 'Side panel': (.006, 4), 'Top rail': (.0038, 4), 'Body': (.006, 3),
        'Front plate': (.005, 4), 'Rear plate': (.005, 4), 'Collar': (.004, 3), 'Trigger': (.0035, 3), 'Trigger guard': (.0028, 3),
        'Icon inset': (.0016, 2), 'Glow bar': (.0018, 2), 'Spool core': (.003, 2)}
for o in parts:
    key = o.name.split('.')[0]
    if key not in SOFT: continue
    width, segs = SOFT[key]
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    mod = o.modifiers.new('Soft', 'BEVEL'); mod.width = width; mod.segments = segs
    mod.limit_method = 'ANGLE'; mod.angle_limit = math.radians(30); mod.harden_normals = False
    bpy.ops.object.modifier_apply(modifier=mod.name)

# Scale the whole launcher about the muzzle.
for o in parts:
    o.location = o.location * S; o.scale = o.scale * S
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

# --- Bake one atlas ---------------------------------------------------------------------------------
img = bpy.data.images.new('net_atlas', 1024, 1024, alpha=False)
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
img.filepath_raw = str(OUT / 'net_atlas.png'); img.file_format = 'PNG'; img.save()
shown = bpy.data.materials.new('Net baked'); shown.use_nodes = True
bsdf = shown.node_tree.nodes.get('Principled BSDF'); bsdf.inputs['Roughness'].default_value = .55
tex = shown.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = img; tex.interpolation = 'Closest'
shown.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])

# --- Export (origin = muzzle) -------------------------------------------------------------------
C = Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))


def vec(v): return {'x': round(v[0], 6), 'y': round(v[1], 6), 'z': round(v[2], 6)}


data = {'version': 1, 'fps': 30, 'frames': 1, 'parts': []}
for o in parts:
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40), keep_sharp_edges=False)
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
                          'color': {'r': 1, 'g': 1, 'b': 1, 'a': 1}, 'texture': 'net_atlas.png', 'rest': rest, 'samples': []})
    o.data.materials.clear(); o.data.materials.append(shown)
(OUT / 'net.mesh.json').write_text(json.dumps(data, separators=(',', ':')), encoding='utf8')

# --- Preview and inventory render ----------------------------------------------------------------
centre = Vector((0, -.19 * S, -.02))
for name, loc, power, size in (('Key', (.45, .25, .55), 60, .8), ('Fill', (-.5, -.3, .3), 22, .8), ('Rim', (0, -.8, .45), 40, .5)):
    bpy.ops.object.light_add(type='AREA', location=loc); light = bpy.context.object; light.name = name
    light.data.energy = power; light.data.size = size
    light.rotation_euler = (centre - light.location).to_track_quat('-Z', 'Y').to_euler()
bpy.ops.object.camera_add(location=(.42, .08, .22)); cam = bpy.context.object; scene.camera = cam
cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam.data.type = 'ORTHO'; cam.data.ortho_scale = .42
scene.render.resolution_x = scene.render.resolution_y = 256; scene.render.film_transparent = True
scene.render.filepath = str(OUT / 'net_inventory.png'); bpy.ops.render.render(write_still=True)
bpy.ops.mesh.primitive_plane_add(size=3, location=(0, -.2, -.14)); floor = bpy.context.object
floor.data.materials.append(material('Backdrop', (.05, .05, .055), 'flat'))
scene.render.resolution_x = scene.render.resolution_y = 900; scene.render.film_transparent = False
scene.render.filepath = str(ART / 'net_preview.png'); bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ART / 'net.blend'))
print('IHM_NET_ART_OK', len(parts), 'parts')
