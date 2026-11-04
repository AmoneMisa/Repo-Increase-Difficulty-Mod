"""Blender 5.2: --background --factory-startup --python tools/build_flamethrower_art.py -- --root PATH.

Original geometry for the Flamethrower C.A.R.T. (no game assets), modelled after the wheeled
flamethrower-turret concept: a sand-coloured four-wheel base, an articulated twin-plate arm with a
hydraulic ram, and a ribbed gun head with twin forward barrels and two red fuel tanks raked up at
the back. The rear deck is left free: the mod mounts the game's own Hauler storage box there.

Exports three models (same format as tools/build_item_art.py) so the turret can yaw and the head
can pitch at runtime:
  src/Art/flamethrower_base.mesh.json    origin: ground centre of the chassis (static)
  src/Art/flamethrower_turret.mesh.json  origin: turntable top centre (yaws)
  src/Art/flamethrower_head.mesh.json    origin: elbow pivot of the gun head (pitches)
plus an editable scene and a preview render in art/flamethrower/.
Axes: Blender X/right Y/forward Z/up -> Unity X/right Y/up Z/forward (reverse winding).
Printed IHM_* lines give the Unity-space mount points used by src/Gear/FlameCart.cs.
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
scene.render.engine = 'CYCLES'; scene.cycles.samples = 48
scene.view_settings.view_transform = 'AgX'
scene.render.image_settings.file_format = 'PNG'

groups = {'base': [], 'turret': [], 'head': []}
current = 'base'


ALL_MATERIALS = []


def _set(sock, v, L):
    if hasattr(v, 'is_output'): L.new(v, sock)
    elif isinstance(v, (int, float)): sock.default_value = v
    else: sock.default_value = (*v[:3], 1)


def material(name, color, kind='paint'):
    """REPO-style painted/worn surface, built procedurally and baked into a per-model atlas.

    Layers, like the game's own item textures: paint grain, blotchy grime, dark dirt in crevices
    (ambient occlusion), chipped lighter edges (bevel-normal mask), sparse scratches and, for
    kind='hazard', yellow/black warning stripes. `color` is sRGB.
    """
    m = bpy.data.materials.new(name); m.diffuse_color = (1, 1, 1, 1); m.use_nodes = True
    nt = m.node_tree; N = nt.nodes; L = nt.links
    for n in list(N): N.remove(n)
    out = N.new('ShaderNodeOutputMaterial'); bsdf = N.new('ShaderNodeBsdfPrincipled'); emit = N.new('ShaderNodeEmission')
    bsdf.inputs['Metallic'].default_value = 0.0; bsdf.inputs['Roughness'].default_value = .85
    tc = N.new('ShaderNodeTexCoord')

    def lin(c): return tuple(x ** 2.2 for x in c)

    def noise(scale, detail=6.0, rough_=.55):
        n = N.new('ShaderNodeTexNoise'); n.inputs['Scale'].default_value = scale
        n.inputs['Detail'].default_value = detail; n.inputs['Roughness'].default_value = rough_
        L.new(tc.outputs['Object'], n.inputs['Vector']); return n.outputs['Fac']

    def ramp(src, a, b, ca, cb, constant=False):
        r = N.new('ShaderNodeValToRGB'); e = r.color_ramp.elements
        if constant: r.color_ramp.interpolation = 'CONSTANT'
        e[0].position = a; e[0].color = (*ca, 1); e[1].position = b; e[1].color = (*cb, 1)
        L.new(src, r.inputs['Fac']); return r.outputs['Color']

    def mix(fac, a, b, blend='MIX'):
        x = N.new('ShaderNodeMixRGB'); x.blend_type = blend
        _set(x.inputs[0], fac, L); _set(x.inputs[1], a, L); _set(x.inputs[2], b, L); return x.outputs[0]

    def bw(src):
        c = N.new('ShaderNodeRGBToBW'); L.new(src, c.inputs[0]); return c.outputs[0]

    base = lin(color)
    if kind == 'hazard':
        w = N.new('ShaderNodeTexWave'); w.bands_direction = 'DIAGONAL'; w.inputs['Scale'].default_value = 7
        w.inputs['Distortion'].default_value = 0; L.new(tc.outputs['Object'], w.inputs['Vector'])
        col = ramp(w.outputs['Fac'], .5, .5001, lin((.07, .065, .06)), lin((.93, .70, .12)), constant=True)
    else:
        col = mix(0.0, base, base)
    # Paint grain and broad blotchy grime.
    col = mix(1.0, col, ramp(noise(55, 3), .3, .7, (.86, .86, .86), (1, 1, 1)), 'MULTIPLY')
    grime = lin((.40, .35, .28)) if kind != 'dark' else lin((.62, .62, .62))
    if kind != 'flat':
        col = mix(1.0, col, ramp(noise(3.5, 8, .6), .40, .72, (1, 1, 1), grime), 'MULTIPLY')
        # Smaller oily stains.
        col = mix(1.0, col, ramp(noise(11, 6, .65), .56, .64, (1, 1, 1), lin((.62, .56, .47))), 'MULTIPLY')
        # Edge wear: chipped paint shows lighter worn metal on rounded edges.
        bev = N.new('ShaderNodeBevel'); bev.inputs['Radius'].default_value = .012; bev.samples = 8
        geo = N.new('ShaderNodeNewGeometry')
        dot = N.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'
        L.new(bev.outputs['Normal'], dot.inputs[0]); L.new(geo.outputs['Normal'], dot.inputs[1])
        edge = ramp(dot.outputs['Value'], .80, .975, (1, 1, 1), (0, 0, 0))
        chips = ramp(noise(16, 5, .7), .38, .62, (0, 0, 0), (1, 1, 1))
        edge_mask = mix(1.0, edge, chips, 'MULTIPLY')
        # Grime band along every edge first (the game's textures darken panel borders), chips on top.
        col = mix(1.0, col, ramp(dot.outputs['Value'], .86, .995, lin((.55, .50, .43)), (1, 1, 1)), 'MULTIPLY')
        worn = {'paint': lin((.60, .58, .54)), 'hazard': lin((.60, .58, .54)),
                'metal': tuple(min(1.0, x * 1.9) for x in base), 'dark': lin((.24, .24, .23))}[kind]
        col = mix(bw(edge_mask), col, worn)
        # Sparse fine scratches.
        vor = N.new('ShaderNodeTexVoronoi'); vor.feature = 'DISTANCE_TO_EDGE'; vor.inputs['Scale'].default_value = 7
        L.new(tc.outputs['Object'], vor.inputs['Vector'])
        scratch = ramp(vor.outputs['Distance'], 0, .012, (1, 1, 1), (0, 0, 0))
        scratch = mix(1.0, scratch, ramp(noise(2.5, 2), .50, .64, (0, 0, 0), (.75, .75, .75)), 'MULTIPLY')
        col = mix(bw(scratch), col, worn)
    # Dirt collects in crevices and contact areas.
    ao = N.new('ShaderNodeAmbientOcclusion'); ao.samples = 16; ao.inputs['Distance'].default_value = .10
    col = mix(1.0, col, ramp(ao.outputs['AO'], .35, 1.0, lin((.20, .17, .13)), (1, 1, 1)), 'MULTIPLY')
    L.new(col, bsdf.inputs['Base Color']); L.new(col, emit.inputs['Color'])
    L.new(bsdf.outputs[0], out.inputs['Surface'])
    m['ihm_emit'] = emit.name; m['ihm_bsdf'] = bsdf.name; m['ihm_out'] = out.name
    N.new('ShaderNodeTexImage').name = 'IHM Bake Target'
    ALL_MATERIALS.append(m)
    return m


sand = material('Sand enamel', (.66, .57, .42))
sand_dark = material('Sand enamel shadow', (.50, .44, .33))
red = material('Fuel tank red', (.55, .14, .11))
steel = material('Burnt steel', (.30, .30, .29), kind='metal')
dark = material('Graphite rubber', (.075, .078, .078), kind='dark')
black = material('Bore void', (.012, .012, .012), kind='flat')
brass = material('Brass fittings', (.62, .48, .22), kind='metal')
bone = material('Pilot light lens', (.95, .78, .38), kind='flat')
hazard = material('Hazard stripes', (1, 1, 1), kind='hazard')
cable_red = material('Cable red', (.48, .10, .08), kind='dark')


def finish(o, mat):
    o.data.materials.append(mat); groups[current].append(o); return o


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


def orient(o, a, b):
    o.rotation_euler = (b - a).to_track_quat('Z', 'Y').to_euler()
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    return o


def bar(name, a, b, r, mat, verts=8):
    a, b = Vector(a), Vector(b)
    return orient(cylinder(name, (a + b) / 2, r, (b - a).length, mat, verts=verts, bevel=False), a, b)


def tube(name, a, b, r, mat, verts=16):
    a, b = Vector(a), Vector(b)
    return orient(cylinder(name, (a + b) / 2, r, (b - a).length, mat, verts=verts), a, b)


def link(name, a, b, width, thick, x, mat):
    """Flat plate from a to b (in the YZ plane at the given X), with rounded ends."""
    a, b = Vector((x, *a)), Vector((x, *b))
    d = b - a
    bpy.ops.mesh.primitive_cube_add(size=1, location=(a + b) / 2); o = bpy.context.object; o.name = name
    o.dimensions = (thick, width, d.length)
    o.rotation_euler = (math.atan2(-d.y, d.z), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    mod = o.modifiers.new('Rounded edges', 'BEVEL'); mod.width = .008; mod.segments = 3
    bpy.ops.object.modifier_apply(modifier=mod.name)
    finish(o, mat)
    for p in (a, b):
        cylinder(name + ' boss', p, width * .55, thick * 1.05, mat, 'X', 18)
    return o


# =========================== BASE (static, origin = ground centre) ==========================
current = 'base'
DECK = .22                     # deck top height
TURRET = Vector((0, .30, DECK))  # turntable centre (front deck)
BOX = Vector((0, -.30, DECK))    # native storage box base (rear deck)

box('Deck plate', (0, -.02, DECK - .04), (.70, 1.22, .08), sand, .012)
box('Under frame', (0, -.02, DECK - .12), (.56, 1.08, .08), sand_dark, .01)
box('Front bumper', (0, .60, DECK - .08), (.62, .05, .10), hazard, .01)
box('Rear bumper', (0, -.64, DECK - .08), (.62, .05, .10), hazard, .01)
for x in (-.36, .36):
    box('Side skirt', (x, -.02, DECK - .07), (.03, 1.12, .07), sand_dark, .006)
    for y in (-.50, -.25, 0, .25, .50):
        cylinder('Skirt rivet', (x * 1.045, y, DECK - .07), .011, .012, steel, 'X', 8)
for y in (-.56, .52):
    for x in (-.29, -.15, 0, .15, .29):
        cylinder('Deck bolt', (x, y, DECK + .002), .012, .012, steel, 'Z', 8)
# Wheels: dark tyre, sand rim, steel hub cap; short axle stubs into the frame.
for x in (-.36, .36):
    for y in (-.44, .40):
        c = Vector((x * 1.08, y, .085))
        cylinder('Wheel tyre', c, .085, .07, dark, 'X', 24)
        cylinder('Wheel rim', c + Vector((math.copysign(.01, x), 0, 0)), .055, .062, sand, 'X', 20)
        cylinder('Wheel hub', c + Vector((math.copysign(.034, x), 0, 0)), .022, .012, steel, 'X', 12)
        bar('Axle stub', (x * .75, y, .085), (x * 1.0, y, .085), .014, steel)
# Front control box (the reference's switch box), with vents, lamps and a cable run to the turret.
box('Control box', (.24, .47, DECK + .07), (.17, .15, .14), sand, .012)
for i in range(4):
    box('Control vent', (.24 + (i - 1.5) * .032, .548, DECK + .09), (.018, .006, .07), dark, .002)
sphere('Lamp green', (.19, .55, DECK + .03), .012, material('Lamp green', (.25, .9, .35), kind='flat'))
sphere('Lamp red', (.29, .55, DECK + .03), .012, red)
bar('Control cable', (.18, .44, DECK + .13), (.08, .32, DECK + .05), .010, dark)
bar('Control cable', (.30, .40, DECK + .13), (.10, .26, DECK + .04), .010, cable_red)
# Turntable ring bolted to the deck, and a low guard rail on both sides of the rear (storage) deck.
cylinder('Turntable ring', TURRET + Vector((0, 0, .012)), .17, .024, steel, 'Z', 28)
for side in (-.34, .34):
    for y in (-.60, -.02):
        bar('Rail post', (side, y, DECK), (side, y, DECK + .16), .011, steel)
    bar('Rail', (side, -.60, DECK + .16), (side, -.02, DECK + .16), .011, steel)
# Tow eye at the back, pull handle at the front.
cylinder('Tow eye', (0, -.68, DECK - .08), .03, .02, steel, 'Y', 14)
bar('Front grab bar', (-.2, .63, DECK - .02), (.2, .63, DECK - .02), .013, dark)

# ========================= TURRET (yaws, origin = turntable top) ==========================
current = 'turret'
T = TURRET + Vector((0, 0, .024))
PIVOT_LOCAL = Vector((0, .02, .54))    # head pivot relative to the turntable
ELBOW_LOCAL = Vector((0, -.09, .30))
cylinder('Turntable', T + Vector((0, 0, .02)), .15, .04, dark, 'Z', 28)
for i in range(8):
    a = i * math.pi / 4
    cylinder('Turntable bolt', T + Vector((math.cos(a) * .125, math.sin(a) * .125, .045)), .011, .012, steel, 'Z', 8)
box('Swivel housing', T + Vector((0, 0, .09)), (.22, .20, .10), sand, .015)
box('Swivel housing cap', T + Vector((0, -.02, .15)), (.18, .14, .03), sand_dark, .006)
for x in (-.095, .095):
    # Lower link leans back to the elbow, upper link forward to the head pivot (twin plates).
    link('Lower arm', (T.y + .02, T.z + .12), (T.y + ELBOW_LOCAL.y, T.z + ELBOW_LOCAL.z), .085, .024, x, sand)
    link('Upper arm', (T.y + ELBOW_LOCAL.y, T.z + ELBOW_LOCAL.z), (T.y + PIVOT_LOCAL.y, T.z + PIVOT_LOCAL.z), .075, .024, x, sand_dark)
cylinder('Elbow pin', T + ELBOW_LOCAL, .022, .24, steel, 'X', 14)
cylinder('Base pin', T + Vector((0, .02, .12)), .022, .24, steel, 'X', 14)
for x in (-.125, .125):
    cylinder('Pin nut', T + ELBOW_LOCAL + Vector((x, 0, 0)), .016, .012, brass, 'X', 6)
# Hydraulic ram between the plates, from the housing front up to the head pivot.
ram_a, ram_b = T + Vector((0, .09, .14)), T + PIVOT_LOCAL + Vector((0, .05, -.05))
ram_m = ram_a.lerp(ram_b, .55)
tube('Ram cylinder', ram_a, ram_m, .03, steel)
tube('Ram rod', ram_m, ram_b, .015, bone)
cylinder('Ram foot', ram_a, .026, .07, dark, 'X', 12)
# Hose bundle climbing the back of the arm.
bar('Arm hose', T + Vector((-.05, -.08, .14)), T + ELBOW_LOCAL + Vector((-.05, -.03, 0)), .011, dark)
bar('Arm hose', T + ELBOW_LOCAL + Vector((-.05, -.03, 0)), T + PIVOT_LOCAL + Vector((-.05, -.08, .02)), .011, dark)
bar('Arm hose', T + Vector((.05, -.08, .14)), T + ELBOW_LOCAL + Vector((.05, -.03, 0)), .011, cable_red)
bar('Arm hose', T + ELBOW_LOCAL + Vector((.05, -.03, 0)), T + PIVOT_LOCAL + Vector((.05, -.08, .02)), .011, cable_red)

# ========================== HEAD (pitches, origin = head pivot) ============================
current = 'head'
P = T + PIVOT_LOCAL
cylinder('Trunnion', P, .035, .30, steel, 'X', 16)
for x in (-.15, .15):
    cylinder('Trunnion cap', P + Vector((x, 0, 0)), .04, .02, brass, 'X', 14)
box('Receiver', P + Vector((0, .05, .07)), (.24, .44, .15), sand, .02)
box('Receiver belly', P + Vector((0, .02, -.02)), (.18, .34, .05), sand_dark, .01)
for x in (-.128, .128):
    box('Side plate', P + Vector((x, .06, .07)), (.018, .36, .11), sand_dark, .005)
    for y in (-.06, .06, .18):
        cylinder('Side bolt', P + Vector((x * 1.08, y, .07)), .011, .01, steel, 'X', 8)
for i in range(8):
    box('Cooling fin', P + Vector((0, -.10 + i * .04, .165)), (.20, .012, .05), dark, .003)
box('Top rail', P + Vector((0, .05, .195)), (.06, .34, .02), steel, .004)
box('Rear block', P + Vector((0, -.19, .07)), (.20, .06, .13), sand_dark, .012)
box('Barrel block', P + Vector((0, .30, .05)), (.20, .08, .11), steel, .012)
NOZZLE = P + Vector((0, .70, .05))
for x in (-.052, .052):
    tube('Barrel', P + Vector((x, .30, .05)), P + Vector((x, .68, .05)), .028, steel)
    tube('Barrel shroud', P + Vector((x, .33, .05)), P + Vector((x, .46, .05)), .042, dark)
    for y in (.36, .40, .44):
        cylinder('Shroud ring', P + Vector((x, y, .05)), .046, .008, steel, 'Y', 16)
    cylinder('Muzzle collar', P + Vector((x, .66, .05)), .040, .04, sand, 'Y', 18)
    cylinder('Nozzle bore', P + Vector((x, .682, .05)), .022, .006, black, 'Y', 14, bevel=False)
box('Barrel clamp', P + Vector((0, .56, .05)), (.17, .03, .05), sand_dark, .004)
box('Under rail', P + Vector((0, .30, -.005)), (.05, .44, .022), dark, .003)
bar('Pilot jet', P + Vector((0, .48, -.01)), P + Vector((0, .66, -.01)), .010, brass)
sphere('Pilot light', P + Vector((0, .67, -.01)), .015, bone)
# Fuel tanks raked up behind the receiver, strapped to a saddle, hoses forward into the body.
rake = math.radians(38)
along = Vector((0, -math.cos(rake), math.sin(rake)))
for x in (-.12, .12):
    c = P + Vector((x, -.20, .19))
    lo, hi = c - along * .14, c + along * .14
    tube('Fuel tank', lo, hi, .07, red, 24)
    for end in (lo, hi):
        dome = sphere('Fuel tank dome', end, .07, red, (1, 1, 1))
    cylinder('Tank valve', hi + along * .075, .022, .04, brass, 'Z', 12)
    for t in (-.07, .07):
        s = c + along * t
        tube('Tank strap', s - along * .011, s + along * .011, .075, dark, 24)
    bar('Feed hose', lo + Vector((0, .02, -.03)), P + Vector((x * .8, .02, .02)), .012, dark)
box('Tank saddle', P + Vector((0, -.17, .13)), (.30, .10, .04), sand_dark, .006, rot=(-rake, 0, 0))

C = Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))


def vec(v): return {'x': round(v[0], 6), 'y': round(v[1], 6), 'z': round(v[2], 6)}


def bake(key, parts, size):
    """Lays every part of one model into a shared UV atlas and bakes the wear shaders into it."""
    img = bpy.data.images.new(key + '_atlas', size, size, alpha=False)
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:
        o.select_set(True)
        layer = o.data.uv_layers.get('Atlas') or o.data.uv_layers.new(name='Atlas')
        o.data.uv_layers.active = layer
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=.003, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=.004, rotate=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    # Only the atlas layout may exist, so bake, export and preview all read the same UVs.
    for o in parts:
        for layer in [l for l in o.data.uv_layers if l.name != 'Atlas']: o.data.uv_layers.remove(layer)
        o.data.uv_layers['Atlas'].active = True; o.data.uv_layers['Atlas'].active_render = True
    for m in ALL_MATERIALS:
        N = m.node_tree.nodes; target = N['IHM Bake Target']; target.image = img; N.active = target
        m.node_tree.links.new(N[m['ihm_emit']].outputs[0], N[m['ihm_out']].inputs['Surface'])
    scene.cycles.samples = 24
    scene.render.bake.margin = 6; scene.render.bake.use_clear = True
    bpy.ops.object.bake(type='EMIT')
    for m in ALL_MATERIALS:
        N = m.node_tree.nodes
        m.node_tree.links.new(N[m['ihm_bsdf']].outputs[0], N[m['ihm_out']].inputs['Surface'])
    img.filepath_raw = str(OUT / f'{key}_atlas.png'); img.file_format = 'PNG'; img.save()
    # Preview what actually ships: the baked atlas on a matte material, nearest filtering like in-game.
    shown = bpy.data.materials.new(key + ' baked'); shown.use_nodes = True
    bsdf = shown.node_tree.nodes.get('Principled BSDF'); bsdf.inputs['Roughness'].default_value = .85
    tex = shown.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = img; tex.interpolation = 'Closest'
    shown.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    return shown


def export(key, parts, origin, shown):
    data = {'version': 1, 'fps': 30, 'frames': 1, 'parts': []}
    for o in parts:
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(45), keep_sharp_edges=False)
        mesh = o.data; mesh.calc_loop_triangles(); vertices = []; normals = []; uv = []; indices = []
        for tri in mesh.loop_triangles:
            for li in reversed(tri.loops):
                loop = mesh.loops[li]; vertices.append(vec(C @ mesh.vertices[loop.vertex_index].co))
                normals.append(vec(C @ mesh.corner_normals[li].vector))
                p = mesh.uv_layers.active.data[li].uv
                uv.append({'x': float(p[0]), 'y': float(p[1])}); indices.append(len(indices))
        p, q, s = o.matrix_world.decompose(); q = (C @ q.to_matrix() @ C).to_quaternion()
        rest = {'position': vec(C @ (p - origin)), 'rotation': {'x': q.x, 'y': q.y, 'z': q.z, 'w': q.w}, 'scale': vec(C @ s)}
        # The colour is all in the baked atlas, so the runtime tint is white.
        data['parts'].append({'name': o.name, 'vertices': vertices, 'normals': normals, 'uv': uv,
                              'triangles': indices, 'color': {'r': 1, 'g': 1, 'b': 1, 'a': 1},
                              'texture': f'{key}_atlas.png', 'rest': rest, 'samples': []})
        o.data.materials.clear(); o.data.materials.append(shown)
    (OUT / f'{key}.mesh.json').write_text(json.dumps(data, separators=(',', ':')), encoding='utf8')


baked = {key: bake(f'flamethrower_{key}', groups[key], size)
         for key, size in (('base', 1024), ('turret', 512), ('head', 1024))}
export('flamethrower_base', groups['base'], Vector((0, 0, 0)), baked['base'])
export('flamethrower_turret', groups['turret'], T, baked['turret'])
export('flamethrower_head', groups['head'], P, baked['head'])

# Preview: stand-in for the native storage box (not exported), lights and an ortho camera.
bpy.ops.mesh.primitive_cube_add(size=1, location=BOX + Vector((0, 0, .29))); ghost = bpy.context.object
ghost.dimensions = (.6, .6, .58); ghost.data.materials.append(material('Native storage stand-in', (.25, .27, .3), kind='dark'))
bpy.ops.mesh.primitive_plane_add(size=6, location=(0, 0, 0)); floor = bpy.context.object
floor.data.materials.append(material('Backdrop', (.05, .06, .065), kind='flat'))
for name, loc, power, size in (('Key', (2.0, 2.2, 2.6), 380, 2.5), ('Fill', (-2.4, .5, 1.6), 160, 2), ('Rim', (0, -2.6, 2.0), 260, 1.5)):
    bpy.ops.object.light_add(type='AREA', location=loc); light = bpy.context.object; light.name = name
    light.data.energy = power; light.data.size = size
    light.rotation_euler = (Vector((0, 0, .5)) - light.location).to_track_quat('-Z', 'Y').to_euler()
bpy.ops.object.camera_add(location=(-2.2, 2.0, 1.45)); cam = bpy.context.object; scene.camera = cam
cam.rotation_euler = (Vector((0, .05, .52)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.0
scene.render.resolution_x = scene.render.resolution_y = 900
scene.render.filepath = str(ART / 'flamethrower_preview.png'); bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ART / 'flamethrower.blend'))


def u(v): w = C @ v; return f'{w.x:.4f} {w.y:.4f} {w.z:.4f}'


print('IHM_TURRET', u(T))
print('IHM_PIVOT_IN_TURRET', u(P - T))
print('IHM_NOZZLE_IN_HEAD', u(NOZZLE - P))
print('IHM_BOX', u(BOX))
print('IHM_FLAME_ART_OK', sum(len(g) for g in groups.values()), 'parts')
