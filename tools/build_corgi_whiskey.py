"""Blender 5.2: --background --factory-startup --python tools/build_corgi_whiskey.py -- --root PATH [--preview]

Original models for two collection valuables (no game or third-party assets):
  * plush_corgi    - soft plush toy corgi: one metaball surface with the cream markings painted on as
                     panels (so they never look glued on), felt ears, eyes and nose seated on that surface.
  * whiskey_bottle - big square-shouldered whiskey bottle with an invented "OLD TRUCK No. 9" label.
    The liquid inside is generated and animated at runtime (WhiskeyLiquid.cs), so only the glass, cap and
    labels are exported here; the interior box the liquid fills is written to the model as 'liquid' info.

Writes src/Art/v11_<key>.mesh.json (ValuableVisuals format) and, with --preview, a Cycles preview to
art/valuables/corgi-whiskey-16/<key>.png. Axes: Blender X/right Y/forward Z/up -> Unity X/right Y/up Z/forward.
Sizes follow the game's scale: the bottle matches Swiftbroom's big potion bottles (about 1.4 m tall).
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Quaternion, Vector

parser = argparse.ArgumentParser(); parser.add_argument('--root', type=Path, required=True); parser.add_argument('--preview', action='store_true')
ARGS = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
ROOT = ARGS.root.resolve()
SRC = ROOT / 'src' / 'Art'
ART = ROOT / 'art' / 'valuables' / 'corgi-whiskey-16'; ART.mkdir(parents=True, exist_ok=True)

# Interior volume the runtime liquid fills (Unity local space of the visual, before layout offset).
BOTTLE_HALF = .27
LIQUID = {'halfWidth': .245, 'bottom': .03, 'top': .86, 'fill': .8, 'power': 9}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 64
    scene.view_settings.view_transform = 'Standard'  # true material colours, as the game shows them


def material(name, color, rough=.6, metal=0., trans=0., emit=0., image=None):
    m = bpy.data.materials.new(name); m.use_nodes = True
    p = m.node_tree.nodes['Principled BSDF']
    p.inputs['Base Color'].default_value = (*color, 1); p.inputs['Roughness'].default_value = rough
    p.inputs['Metallic'].default_value = metal; p.inputs['Transmission Weight'].default_value = trans
    p.inputs['IOR'].default_value = 1.5
    p.inputs['Emission Color'].default_value = (*color, 1); p.inputs['Emission Strength'].default_value = emit
    if image:
        tex = m.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(str(SRC / image)); tex.interpolation = 'Linear'
        m.node_tree.links.new(tex.outputs['Color'], p.inputs['Base Color'])
        m['texture'] = image
    return m


def assign(ob, mat, group='static'):
    ob.data.materials.clear(); ob.data.materials.append(mat); ob['group'] = group; return ob


def from_data(name, verts, faces, mat, uvs=None, smooth=True, group='static'):
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update()
    if uvs:
        layer = me.uv_layers.new(name='UVMap')
        for poly in me.polygons:
            for li in poly.loop_indices:
                layer.data[li].uv = uvs[me.loops[li].vertex_index]
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    for poly in me.polygons: poly.use_smooth = smooth
    return assign(ob, mat, group)


def sphere(name, loc, scale, mat, rot=(0, 0, 0), segs=20, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=rings, location=loc, rotation=rot)
    ob = bpy.context.object; ob.name = name; ob.scale = scale; bpy.ops.object.shade_smooth()
    return assign(ob, mat)


def apply_transforms(ob):
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)


def split_by_material(ob, normal_at, names):
    """Separate a multi-material mesh into one object per material, keeping seam normals continuous.
    `names` maps each material to the part name."""
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='MATERIAL'); bpy.ops.object.mode_set(mode='OBJECT')
    out = []
    for piece in list(bpy.context.selected_objects):
        pm = piece.data
        if not pm.polygons: continue
        used = pm.materials[pm.polygons[0].material_index]
        pm.materials.clear(); pm.materials.append(used)
        for poly in pm.polygons: poly.material_index = 0; poly.use_smooth = True
        pm.normals_split_custom_set_from_vertices([normal_at.get(tuple(round(c, 5) for c in v.co), v.normal) for v in pm.vertices])
        piece.name = names[used.name]; piece['group'] = 'static'
        out.append(piece)
    return out


def plush_ear(name, base, radius, height, depth, mat, tilt, lean=-6, taper=.82, root=.4, inner=None, thread=None):
    """Pillow-like plush ear: the upper half of a sphere tapered into a soft rounded triangle, at full width
    where it meets the head. The lower half becomes a wide root reaching `root` x height down into the skull,
    so the ear stays sewn on at any outward tilt. Placed at `base`, tilted outward (`tilt`, Y) and leaned
    (`lean` degrees, X). With `inner`, a pink panel is cut into the ear's own front face (never a separate
    floating piece) and stitched round with `thread`. Returns the list of parts."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=32)
    ob = bpy.context.object; ob.name = name
    for v in ob.data.vertices:
        t = max(0., v.co.z)                                      # 0 at the head seam, 1 at the tip
        z = t * height if v.co.z >= 0 else v.co.z * height * root
        w = (1 - taper * t) if v.co.z >= 0 else 1 + .45 * v.co.z  # root narrows as it sinks in
        v.co = Vector((v.co.x * radius * w, v.co.y * depth * (1 - .55 * t), z))
    parts = []
    normal_at = {}
    if inner is not None:
        me = ob.data; me.materials.clear(); me.materials.append(mat); me.materials.append(inner)
        bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.normal_update()

        def field(p, n):
            t = min(1., max(0., p.z / height))
            return min((.66 * radius * (1 - taper * t) - abs(p.x)) * 3, n.y - .2, p.z - .015 - .3 * abs(p.x), .8 * height - p.z)
        cut_panels(bm, field)
        bm.normal_update(); bm.to_mesh(me); bm.free(); me.update()
        normal_at = {tuple(round(c, 5) for c in v.co): v.normal.copy() for v in me.vertices}
        if thread is not None: parts.append(stitches(me, thread, pitch=.011, dash=.0055, width=.0011))
    frame = Matrix.Rotation(tilt, 3, 'Y') @ Matrix.Rotation(math.radians(lean), 3, 'X')
    world = Matrix.Translation(Vector(base)) @ frame.to_4x4()
    for o in parts: o.matrix_world = world
    ob.matrix_world = world
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.shade_smooth()
    if inner is None:
        return parts + [assign(ob, mat)]
    return parts + split_by_material(ob, normal_at, {mat.name: name, inner.name: name + ' inner'})


def seat(name, body, origin, direction, size, mat, sink=.4, segs=20, rings=12):
    """A felt dome stuck onto the body surface: ray cast from `origin`, flattened along the surface normal."""
    hit, loc, normal, _ = body.ray_cast(Vector(origin), Vector(direction).normalized())
    assert hit, name
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=rings)
    ob = bpy.context.object; ob.name = name; ob.scale = size; apply_transforms(ob)
    rot = normal.to_track_quat('Y', 'Z').to_matrix()
    ob.matrix_world = Matrix.Translation(loc - normal * size[1] * sink) @ rot.to_4x4()
    bpy.ops.object.shade_smooth()
    return assign(ob, mat)


# ------------------------------------------------------------------ corgi

def cream_field(p, n):
    """Pembroke markings as a smooth signed field (> 0 = cream) over the plush surface, Blender coords.
    Each marking is an intersection (min) of soft limits; the markings combine as a union (max)."""
    x, y, z = p; ax = abs(x)
    blaze = .032 + max(0., z - .47) * .55 + max(0., .385 - z) * 1.2
    return max(
        min(y - .02, .15 - z, .065 - ax),                                    # chest between the front paws
        min(.15 - ax, y - .07 - max(0., ax - .12) * 3., .372 - 1.6 * ax * ax - z, z - .255 + .25 * max(0., y - .12)),  # muzzle, cheeks and chin
        min(blaze - ax, y + .03 - max(0., .4 - z) * 3., z - .3),                                    # blaze over the forehead
        1.12 - math.sqrt(((x - .065) / .072) ** 2 + ((y + .3) / .075) ** 2 + ((z - .26) / .088) ** 2),  # fluffy tail
    )


def cut_panels(bm, field):
    """Split the triangle mesh exactly along field = 0 so panel seams are smooth curves, then mark faces."""
    value = {v: field(v.co, v.normal) for v in bm.verts}
    seam = set()
    for e in list(bm.edges):
        a, b = e.verts; fa, fb = value[a], value[b]
        if fa * fb >= 0: continue
        t = fa / (fa - fb); pos = a.co.lerp(b.co, t)
        _, v = bmesh.utils.edge_split(e, a, t); v.co = pos; value[v] = 0.; seam.add(v)
    for f in list(bm.faces):
        cut = [v for v in f.verts if v in seam]
        if len(cut) == 2 and not any(e for e in cut[0].link_edges if e.other_vert(cut[0]) is cut[1]):
            bmesh.ops.connect_verts(bm, verts=cut)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    for f in bm.faces:
        f.material_index = 1 if sum(value.get(v, 0.) for v in f.verts) > 0 else 0


def tube(name, points, radius, mat):
    """Thin felt cord (mouth line) through world points."""
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'; cu.bevel_depth = radius; cu.bevel_resolution = 3
    sp = cu.splines.new('POLY'); sp.points.add(len(points) - 1)
    for p, q in zip(sp.points, points): p.co = (*q, 1)
    ob = bpy.data.objects.new(name, cu); bpy.context.collection.objects.link(ob)
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.convert(target='MESH'); ob = bpy.context.object
    bpy.ops.object.shade_smooth()
    return assign(ob, mat)


def stitches(me, mat, pitch=.017, dash=.0095, width=.0016):
    """Running stitch along every fur/cream panel seam: small raised thread dashes, one mesh."""
    bm = bmesh.new(); bm.from_mesh(me); bm.normal_update()
    seam = [e for e in bm.edges if len(e.link_faces) == 2 and e.link_faces[0].material_index != e.link_faces[1].material_index]
    links = {}
    for e in seam:
        for v in e.verts: links.setdefault(v, []).append(e)
    used = set(); verts = []; faces = []
    for e0 in seam:
        if e0 in used: continue
        # Walk the chain both ways from e0 into an ordered list of points.
        chain = [e0.verts[0], e0.verts[1]]; used.add(e0)
        for end in (1, 0):
            while True:
                tip = chain[-1] if end else chain[0]
                nxt = next((e for e in links.get(tip, []) if e not in used), None)
                if nxt is None: break
                used.add(nxt); w = nxt.other_vert(tip)
                if end: chain.append(w)
                else: chain.insert(0, w)
        pts = [(v.co.copy(), v.normal.copy()) for v in chain]
        travelled, nextAt = 0., pitch * .5
        for (a, na), (b, nb) in zip(pts, pts[1:]):
            seg = (b - a).length
            while nextAt + dash <= travelled + seg and seg > 1e-6:
                t0 = (nextAt - travelled) / seg; t1 = min(1., (nextAt + dash - travelled) / seg)
                p0, p1 = a.lerp(b, t0), a.lerp(b, t1); n = na.lerp(nb, .5).normalized()
                tdir = (p1 - p0).normalized(); side = n.cross(tdir).normalized() * width; up = n * width * .8
                base = len(verts)
                for p in (p0 + n * .0006, p1 + n * .0006):
                    verts.extend([p - side, p + side, p + side + up, p - side + up])
                faces.extend([(base, base + 1, base + 2, base + 3)[::-1], (base + 4, base + 5, base + 6, base + 7),
                              (base, base + 4, base + 5, base + 1), (base + 1, base + 5, base + 6, base + 2),
                              (base + 2, base + 6, base + 7, base + 3), (base + 3, base + 7, base + 4, base)])
                nextAt += pitch
            travelled += seg
    bm.free()
    return from_data('Corgi stitches', verts, faces, mat, smooth=False)


def corgi():
    # Felt colours sampled from the reference: deep orange, warm cream, dusty pink, red bandana.
    fur = material('Corgi fur', (.6, .19, .013), .97, image='v11_corgi_felt_orange.png')
    white = material('Corgi cream', (.8, .64, .41), .98, image='v11_corgi_felt_cream.png')
    thread = material('Corgi stitch thread', (.86, .74, .55), .9)
    pink = material('Corgi felt pink', (.68, .2, .19), .92)
    black = material('Corgi felt black', (.01, .009, .008), .28)
    scarf = material('Corgi bandana', (1, 1, 1), .95, image='v11_corgi_bandana.png')
    band = material('Corgi bandana band', (.3, .016, .012), .95)

    def E(center, half):
        # Element from the visible half extents (metaball surfaces sit at ~0.76 of the influence size).
        radius = max(half) / .76
        return (center, radius, tuple(h / (radius * .76) for h in half))

    # Chibi plush sitting on its belly: an oversized round head over a short loaf body, stubby front
    # paws pointing forward, back feet splayed to the sides with their soles to the front, cream tail.
    elements = [
        E((0, .04, .4), (.2, .17, .175)),                                    # big round head
        E((.1, .12, .33), (.11, .09, .09)), E((-.1, .12, .33), (.11, .09, .09)),  # puffy cheeks
        E((0, .2, .328), (.088, .08, .066)),                                 # rounded snout
        E((0, -.13, .19), (.17, .19, .17)),                                  # body loaf
        E((0, 0, .19), (.145, .11, .15)),                                    # chest
        E((.14, -.12, .14), (.09, .13, .12)), E((-.14, -.12, .14), (.09, .13, .12)),      # haunches
        E((.065, -.285, .26), (.072, .075, .088)),                           # fluffy tail
    ]
    def blob(name, elems):
        mb = bpy.data.metaballs.new(name); mb.resolution = mb.render_resolution = .008; mb.threshold = .6
        ob = bpy.data.objects.new(name, mb); bpy.context.collection.objects.link(ob)
        for co, radius, size in elems:
            e = mb.elements.new(); e.type = 'ELLIPSOID'; e.co = co; e.radius = radius; e.stiffness = 2
            e.size_x, e.size_y, e.size_z = size
        bpy.context.view_layer.update()
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
        bpy.ops.object.convert(target='MESH')
        out = bpy.context.object; out.name = name
        dec = out.modifiers.new('Plush decimate', 'DECIMATE'); dec.ratio = .45
        bpy.ops.object.modifier_apply(modifier=dec.name)
        bpy.ops.object.shade_smooth()
        return out

    body = blob('Corgi fur', elements)
    me = body.data; me.materials.clear(); me.materials.append(fur); me.materials.append(white)

    # Paint the markings: cut the surface along a smooth curve so they read as sewn felt panels.
    bm = bmesh.new(); bm.from_mesh(me); bm.normal_update()
    cut_panels(bm, cream_field)
    bm.normal_update(); bm.to_mesh(me); bm.free(); me.update()
    stitch = stitches(me, thread)
    # Felt texture coordinates (islands packed at an even texel density).
    bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); bpy.context.view_layer.objects.active = body
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.004)
    bpy.ops.object.mode_set(mode='OBJECT')
    # Continuous normals across the panel seams (each panel otherwise re-derives them on its own).
    normal_at = {tuple(round(c, 5) for c in v.co): v.normal.copy() for v in me.vertices}

    def front(x, z):
        hit, loc, normal, _ = body.ray_cast(Vector((x, 1, z)), Vector((0, -1, 0)))
        assert hit, (x, z)
        return loc, normal

    parts = [stitch]
    ear_bases = {}
    # Big felt ears on the top corners of the head, leaning outward, with pink inner panels.
    for side in (1, -1):
        hit, top, _, _ = body.ray_cast(Vector((side * .125, -.015, 1)), Vector((0, 0, -1)))
        base = top - Vector((0, 0, .02))
        tilt = side * math.radians(33)
        ear_group = 'corgi_ear_r' if side > 0 else 'corgi_ear_l'
        for piece in plush_ear('Corgi ear', base, .118, .2, .05, fur, tilt, taper=.68, inner=pink, thread=thread):
            piece['group'] = ear_group; parts.append(piece)
        ear_bases[ear_group] = base.copy()
        parts.append(seat('Corgi eye', body, (side * .09, 1, .405), (0, -1, 0), (.024, .014, .026), black, sink=.3))
    parts.append(seat('Corgi nose', body, (0, 1, .365), (0, -1, 0), (.04, .027, .026), black, sink=.3))

    # Open smile: philtrum and two curved lips, a dark open mouth and the tongue hanging out.
    smile = []
    for k in range(-10, 11):
        x = .075 * k / 10
        z = .312 - .03 * math.sin(math.pi * abs(k) / 10) + .012 * (abs(k) / 10) ** 2
        loc, normal = front(x, z); smile.append(loc + normal * .002)
    parts.append(tube('Corgi smile', smile, .0042, black))
    stem = [front(0, z)[0] + front(0, z)[1] * .002 for z in (.343, .328, .313)]
    parts.append(tube('Corgi philtrum', stem, .0042, black))
    parts.append(seat('Corgi mouth', body, (0, 1, .287), (0, -1, 0), (.036, .008, .016), black, sink=.25))
    loc, normal = front(0, .287)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12)
    tongue = bpy.context.object; tongue.name = 'Corgi tongue'
    for v in tongue.data.vertices:                          # rounded drop, wider at the tip
        t = (1 - v.co.z) / 2
        v.co = Vector((v.co.x * .03 * (.75 + .35 * t), v.co.y * .012, v.co.z * .042))
    bpy.ops.object.shade_smooth()
    tongue.matrix_world = Matrix.Translation(loc + normal * .02 + Vector((0, 0, -.026))) @ Matrix.Rotation(math.radians(18), 4, 'X')
    parts.append(assign(tongue, pink, 'corgi_tongue'))
    pivots = {'corgi_tongue': loc + normal * .012}

    pivots.update(ear_bases)
    # Limbs are their own felt pieces sewn onto the body (separate meshes never melt into each other),
    # each on a pivot so they can paddle when the toy is picked up. Cream soles with pink pads.
    limbs = []
    for side in (1, -1):
        tag = 'r' if side > 0 else 'l'
        # each leg carries a shoulder / hip piece that sinks well into the body, so no gap shows at the join
        limbs.append(('corgi_paw_f' + tag, [E((side * .088, .13, .085), (.06, .1, .072)), E((side * .088, .2, .075), (.065, .06, .074)),
                                            E((side * .085, .02, .12), (.058, .085, .075))],
                      Vector((side * .086, .03, .11)), side * .088, .072, 1.))          # pivot inside the shoulder join
        limbs.append(('corgi_foot_b' + tag, [E((side * .17, .04, .075), (.072, .1, .07)), E((side * .155, -.08, .1), (.065, .085, .075))],
                      Vector((side * .16, -.05, .095)), side * .175, .07, .95))         # pivot inside the hip join
    for group, elems, pivot, x, z, s in limbs:
        limb = blob('Corgi limb', elems)
        limb.data.materials.clear(); limb.data.materials.append(white)
        bpy.ops.object.select_all(action='DESELECT'); limb.select_set(True); bpy.context.view_layer.objects.active = limb
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.004)
        bpy.ops.object.mode_set(mode='OBJECT')
        parts.append(assign(limb, white, group))
        parts.append(seat('Corgi paw pad', limb, (x, 1, z - .008), (0, -1, 0), (.03 * s, .007, .024 * s), pink, sink=.1, segs=14, rings=8))
        for dx, dz in ((-.029, .03), (0, .043), (.029, .03)):
            parts.append(seat('Corgi toe bean', limb, (x + dx * s, 1, z + dz * s), (0, -1, 0), (.012 * s, .006, .013 * s), pink, sink=.1, segs=10, rings=6))
        for piece in parts[-4:]: piece['group'] = group
        pivots[group] = pivot
    global CORGI_PIVOTS
    CORGI_PIVOTS = [{'group': g, 'position': vec(C @ Vector(p))} for g, p in pivots.items()]

    # Red bandana, fitted to the body: a band round the neck (each point found on the real surface, so it never
    # sticks out where the neck is slimmer) and a triangular flap laid onto the chest the same way.
    ring = []
    for k in range(48):
        a = 2 * math.pi * k / 48
        # a tilted band: under the chin at the front, rising to the nape behind the head
        centre = Vector((0, -.03, .245 + .085 * (1 - math.cos(a)) / 2))
        d = Vector((math.sin(a), math.cos(a), 0))
        hit, loc, normal, _ = body.ray_cast(centre + d * .3, -d)
        if hit and (loc - centre).length < .2: ring.append((loc, normal))
    if len(ring) > 8:
        # the neckband: a flat strip of the bandana cloth lying on the neck
        n = len(ring); verts = []; faces = []
        for k, (loc, normal) in enumerate(ring):
            tangent = (ring[(k + 1) % n][0] - ring[k - 1][0]).normalized()
            up = tangent.cross(normal).normalized()
            if up.z < 0: up = -up
            for j, w in enumerate((-.02, .02)):
                verts.append(tuple(loc + normal * .008 + up * w))
        for k in range(n):
            a, b = 2 * k, 2 * ((k + 1) % n)
            faces.append((a, b, b + 1, a + 1)); faces.append((a + 1, b + 1, b, a))   # both sides, so it never vanishes edge-on
        parts.append(from_data('Corgi bandana collar', verts, faces, band, smooth=True))
        # the knot at the nape, with two short tails
        back = min(ring, key=lambda r: r[0].y)
        knot_at = back[0] + back[1] * .012
        parts.append(assign(sphere('Corgi bandana knot', knot_at, (.024, .018, .02), band), band))
        for side in (1, -1):
            tail = [knot_at + Vector((side * (.01 + .03 * t), -.012 - .01 * t, -.01 - .055 * t)) for t in (0, .5, 1)]
            parts.append(tube('Corgi bandana tail', tail, .011, band))
    cols, rows = 16, 12; verts = []; uvs = []; faces = []
    for r in range(rows + 1):
        v = r / rows; z = .11 + (.25 - .11) * v; half = .118 * v + .004   # stays on the front of the chest
        for c in range(cols + 1):
            u = c / cols; x = -half + 2 * half * u
            hit, loc, normal, _ = body.ray_cast(Vector((x, 1, z)), Vector((0, -1, 0)))
            p = loc + normal * .009 if hit and normal.y > .25 else Vector((x, .1, z))
            verts.append(tuple(p)); uvs.append((.5 + (u - .5) * (.03 + .94 * v), .02 + .96 * v))
    for r in range(rows):
        for c in range(cols):
            q = r * (cols + 1) + c; faces.append((q, q + 1, q + cols + 2, q + cols + 1))
    parts.append(from_data('Corgi bandana flap', verts, faces, scarf, uvs, smooth=True))

    parts[:0] = split_by_material(body, normal_at, {fur.name: 'Corgi fur', white.name: 'Corgi cream panels'})
    for o in parts: apply_transforms(o)
    return parts


# ------------------------------------------------------------------ whiskey bottle

def superellipse(hw, p, n=48):
    pts = []
    for j in range(n):
        a = 2 * math.pi * j / n
        c, s = math.cos(a), math.sin(a)
        pts.append((hw * math.copysign(abs(c) ** (2 / p), c), hw * math.copysign(abs(s) ** (2 / p), s)))
    return pts


def loft(name, rings, mat, n=48, close_bottom=True):
    verts = []; faces = []
    for z, hw, p in rings:
        verts.extend((x, y, z) for x, y in superellipse(hw, p, n))
    for k in range(len(rings) - 1):
        for j in range(n):
            a = k * n + j; b = k * n + (j + 1) % n
            faces.append((a, b, b + n, a + n))
    if close_bottom:
        c = len(verts); verts.append((0, 0, rings[0][0]))
        for j in range(n): faces.append((c, (j + 1) % n, j))
    ob = from_data(name, verts, faces, mat, smooth=True)
    return ob


def face_y(x, hw, p):
    """Front face of the rounded-square body (Blender -Y) at horizontal position x."""
    u = min(.999, abs(x) / hw)
    return -hw * (1 - u ** p) ** (1 / p)


def whiskey():
    glass = material('Whiskey glass', (.80, .78, .70), .04, trans=.95)
    cap = material('Whiskey cap', (.018, .016, .015), .5)
    label = material('Whiskey label', (1, 1, 1), .7, image='v11_whiskey_label.png')
    neck = material('Whiskey neck band', (1, 1, 1), .7, image='v11_whiskey_neck.png')
    hw = BOTTLE_HALF
    # Classic square whiskey bottle: crisp bevelled corners, high square shoulder, short thick neck.
    P = 9
    rings = [(0, .236, P), (.01, .262, P), (.03, hw, P), (.86, hw, P), (.895, .262, 7), (.935, .228, 5),
             (.975, .17, 3.2), (1.005, .118, 2.2), (1.03, .100, 2), (1.19, .096, 2), (1.205, .104, 2), (1.215, .099, 2)]
    parts = [loft('Whiskey glass', rings, glass)]
    # Shoulder sleeve: black neck label wrapping the shoulder and the neck, just outside the glass.
    sleeve = [r for r in rings if .89 <= r[0] <= 1.17] + [(1.17, .096, 2)]
    n = 48; verts = []; uvs = []; faces = []
    for k, (z, w, pp) in enumerate(sleeve):
        pts = superellipse(w * 1.012 + .0015, pp, n)
        for j in range(n + 1):
            x, y = pts[j % n]; verts.append((x, y, z)); uvs.append((j / n, (z - sleeve[0][0]) / (sleeve[-1][0] - sleeve[0][0])))
    for k in range(len(sleeve) - 1):
        for j in range(n):
            q = k * (n + 1) + j; faces.append((q, q + 1, q + n + 2, q + n + 1))
    parts.append(from_data('Whiskey shoulder sleeve', verts, faces, neck, uvs, smooth=True))
    # Ribbed screw cap.
    n = 40; verts = []; faces = []
    for k, z in enumerate((1.17, 1.18, 1.395, 1.41)):
        for j in range(n):
            a = 2 * math.pi * j / n; r = (.108 if k in (1, 2) else .104) + (.0035 if j % 2 and k in (1, 2) else 0)
            verts.append((r * math.cos(a), r * math.sin(a), z))
    for k in range(3):
        for j in range(n):
            a = k * n + j; b = k * n + (j + 1) % n; faces.append((a, b, b + n, a + n))
    top = len(verts); verts.append((0, 0, 1.416))
    for j in range(n): faces.append((top, 3 * n + j, 3 * n + (j + 1) % n))
    bottom = len(verts); verts.append((0, 0, 1.17))
    for j in range(n): faces.append((bottom, (j + 1) % n, j))
    parts.append(from_data('Whiskey cap', verts, faces, cap, smooth=False))
    # Front label conforming to the flat-ish face, plus a matching back label.
    for side, (z0, z1, half, arch) in ((-1, (.15, .70, .238, .06)), (1, (.30, .58, .16, 0))):
        cols, rows = 24, 6; verts = []; uvs = []; faces = []
        for r in range(rows + 1):
            for c in range(cols + 1):
                x = -half + 2 * half * c / cols; u = c / cols
                z = z0 + (z1 + arch * math.sin(math.pi * u) - z0) * r / rows
                y = (face_y(x, hw, P) - .0025) * (1 if side < 0 else -1)
                verts.append((x * (1 if side < 0 else -1), y, z)); uvs.append((c / cols, r / rows))
        for r in range(rows):
            for c in range(cols):
                a = r * (cols + 1) + c; faces.append((a, a + 1, a + cols + 2, a + cols + 1))
        parts.append(from_data('Whiskey label' + (' back' if side > 0 else ''), verts, faces, label, uvs, smooth=True))
    return parts


# ------------------------------------------------------------------ export

C = Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0)))


def vec(v): return {'x': round(float(v.x), 6), 'y': round(float(v.y), 6), 'z': round(float(v.z), 6)}


def export(key, parts, extra=None):
    mats = []; index = {}; records = []
    lo = Vector((1e9, 1e9, 1e9)); hi = -lo
    for ob in parts:
        me = ob.data
        if any(len(p.vertices) > 3 for p in me.polygons):
            bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free(); me.update()
        pts = [ob.matrix_world @ v.co for v in me.vertices]
        a = Vector(tuple(min(p[i] for p in pts) for i in range(3))); b = Vector(tuple(max(p[i] for p in pts) for i in range(3)))
        lo = Vector(tuple(min(lo[i], a[i]) for i in range(3))); hi = Vector(tuple(max(hi[i], b[i]) for i in range(3)))
        center = (a + b) / 2
        m = me.materials[0]
        if m.name not in index:
            p = m.node_tree.nodes['Principled BSDF']; base = p.inputs['Base Color'].default_value
            index[m.name] = len(mats)
            mats.append({'name': m.name, 'texture': m.get('texture', ''), 'color': {'r': base[0], 'g': base[1], 'b': base[2], 'a': 1},
                         'transmission': p.inputs['Transmission Weight'].default_value, 'emission': p.inputs['Emission Strength'].default_value,
                         'roughness': p.inputs['Roughness'].default_value, 'metallic': p.inputs['Metallic'].default_value})
        uv_layer = me.uv_layers.active
        verts = []; normals = []; uv = []; tri = []
        for poly in me.polygons:
            start = len(verts)
            for li in poly.loop_indices:
                vi = me.loops[li].vertex_index
                verts.append(vec(C @ (ob.matrix_world @ me.vertices[vi].co - center)))
                n = me.corner_normals[li].vector if poly.use_smooth else poly.normal
                normals.append(vec((C @ n).normalized()))
                u = uv_layer.data[li].uv if uv_layer else (0, 0)
                uv.append({'x': round(float(u[0]), 6), 'y': round(float(u[1]), 6)})
            tri.extend([start, start + 2, start + 1])
        mi = index[m.name]
        color = {'r': 1, 'g': 1, 'b': 1, 'a': 1} if mats[mi]['texture'] else mats[mi]['color']
        records.append({'name': ob.name, 'group': ob.get('group', 'static'), 'phase': 0, 'material': mi, 'vertices': verts, 'normals': normals,
                        'uv': uv, 'triangles': tri, 'color': color, 'texture': mats[mi]['texture'],
                        'rest': {'position': vec(C @ center), 'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'scale': {'x': 1, 'y': 1, 'z': 1}}, 'samples': []})
    data = {'version': 1, 'fps': 24, 'frames': 1, 'materials': mats, 'parts': records}
    if extra: data.update(extra)
    (SRC / ('v11_' + key + '.mesh.json')).write_text(json.dumps(data, separators=(',', ':')))
    size = C @ (hi - lo); mid = C @ ((hi + lo) / 2)
    print(f'EXPORT {key}: parts {len(records)}, triangles {sum(len(r["triangles"]) // 3 for r in records)}, '
          f'size (Unity) {size.x:.3f} x {size.y:.3f} x {size.z:.3f}, center {mid.x:.3f},{mid.y:.3f},{mid.z:.3f}', flush=True)


def preview(key, parts, liquid=False, view=(1.05, -2.3, .75)):
    scene = bpy.context.scene
    if liquid:  # preview stand-in for the runtime liquid
        amber = material('Preview whiskey', (.30, .075, .01), .12, trans=.35)
        h = LIQUID['halfWidth']; z1 = LIQUID['bottom'] + (LIQUID['top'] - LIQUID['bottom']) * LIQUID['fill']
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, (LIQUID['bottom'] + z1) / 2)); ob = bpy.context.object
        ob.scale = (2 * h, 2 * h, z1 - LIQUID['bottom']); assign(ob, amber)
    lo = Vector((1e9, 1e9, 1e9)); hi = -lo
    for ob in parts:
        for v in ob.data.vertices:
            p = ob.matrix_world @ v.co; lo = Vector(tuple(min(lo[i], p[i]) for i in range(3))); hi = Vector(tuple(max(hi[i], p[i]) for i in range(3)))
    mid = (lo + hi) / 2; size = max(hi - lo)
    bpy.ops.mesh.primitive_plane_add(size=size * 12, location=(0, 0, lo.z)); assign(bpy.context.object, material('Floor', (.09, .085, .08), .8))
    world = bpy.data.worlds.new('World'); scene.world = world; world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.05, .055, .06, 1)
    for loc, energy, sz in (((-1.6, -2.2, 2.4), 900, 2.5), ((2.2, -.8, 1.6), 350, 2), ((0, 2.5, 2.0), 500, 2)):
        bpy.ops.object.light_add(type='AREA', location=Vector(loc) * size * 1.4 + mid); light = bpy.context.object
        light.data.energy = energy * size * size; light.data.size = sz * size
        light.rotation_euler = (mid - light.location).to_track_quat('-Z', 'Y').to_euler()
    cam_loc = mid + Vector(view).normalized() * size * 2.5
    bpy.ops.object.camera_add(location=cam_loc); cam = bpy.context.object
    cam.rotation_euler = (mid - cam_loc).to_track_quat('-Z', 'Y').to_euler(); cam.data.lens = 55; scene.camera = cam
    scene.render.resolution_x = 900; scene.render.resolution_y = 900
    scene.render.filepath = str(ART / (key + '.png')); bpy.ops.render.render(write_still=True)
    if key == 'plush_corgi':  # side view: checks the low loaf and leg length
        cam.location = mid + Vector((1, .15, .25)).normalized() * size * 2.5
        cam.rotation_euler = (mid - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = str(ART / (key + '_side.png')); bpy.ops.render.render(write_still=True)
        # Held pose, using the runtime's peak rotations (CorgiPlushMotion). Unity (x,y,z) is Blender (x,z,y):
        # a mirror, so a Unity rotation by +a about axis u is a Blender rotation by -a about C @ u.
        pose = {'corgi_ear_r': [(14, 'Z'), (4, 'X')], 'corgi_ear_l': [(-14, 'Z'), (4, 'X')], 'corgi_tongue': [(17, 'Z'), (-11, 'X')],
                'corgi_paw_fr': [(-28, 'X')], 'corgi_paw_fl': [(-8, 'X')], 'corgi_foot_br': [(12, 'Y'), (-6, 'X')], 'corgi_foot_bl': [(-12, 'Y')]}
        axis = {'X': Vector((1, 0, 0)), 'Y': Vector((0, 1, 0)), 'Z': Vector((0, 0, 1))}
        piv = {q['group']: C @ Vector((q['position']['x'], q['position']['y'], q['position']['z'])) for q in CORGI_PIVOTS}
        for ob in parts:
            g = ob.get('group', 'static')
            if g not in pose: continue
            R = Matrix.Identity(3)
            for ang, ax in pose[g]: R = R @ Matrix.Rotation(math.radians(-ang), 3, C @ axis[ax])
            ob.matrix_world = Matrix.Translation(piv[g]) @ R.to_4x4() @ Matrix.Translation(-piv[g]) @ ob.matrix_world
        cam.location = mid + Vector(view).normalized() * size * 2.5
        cam.rotation_euler = (mid - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = str(ART / (key + '_pose.png')); bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(ART / (key + '.blend')))


for key, build in (('plush_corgi', corgi), ('whiskey_bottle', whiskey)):
    reset()
    parts = build()
    extra = {'liquid': LIQUID} if key == 'whiskey_bottle' else {'pivots': CORGI_PIVOTS}
    if ARGS.preview:
        # Export first (export triangulates in place; previews use the same geometry).
        export(key, parts, extra); preview(key, parts, liquid=key == 'whiskey_bottle', view=(.55, 2.3, .75) if key == 'plush_corgi' else (1.05, -2.3, .75))
    else:
        export(key, parts, extra)
print('CORGI_WHISKEY_COMPLETE', flush=True)
