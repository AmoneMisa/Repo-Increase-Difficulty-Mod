"""Render the shipped courier pets exactly as exported: src/Art/courier_<key>.mesh.json + its atlas and normal map.

Run:  blender --background --factory-startup --python tools/render_courier.py -- --root . [--only cat,owl] [--out art/courier]

Writes <out>/<key>.png (front three-quarter view on the left, back view on the right, camera framed automatically)
and <out>/courier_lineup.png (all of them on one sheet). build_courier.py calls render() after a rebuild.
"""
import argparse, json, math, sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

SHOWN = ('eyes_happy', 'eyes_wide', 'eyes_sleepy', 'brows_sad', 'brows_focus')  # hidden: the neutral face is rendered


def _load(art, key):
    """One Blender object per exported part, with the exported normals and UVs."""
    model = json.loads((art / f'courier_{key}.mesh.json').read_text(encoding='utf8'))
    objects = []
    images = {}
    for part in model['parts']:
        if part['name'].split('|')[0] in SHOWN: continue
        verts = [(v['x'], v['z'], v['y']) for v in part['vertices']]          # Unity (x, y, z) -> Blender (x, z, y)
        tris = part['triangles']
        faces = [(tris[i + 2], tris[i + 1], tris[i]) for i in range(0, len(tris), 3)]
        mesh = bpy.data.meshes.new(part['name']); mesh.from_pydata(verts, [], faces); mesh.update()
        uv = mesh.uv_layers.new(name='uv')
        for loop in mesh.loops:
            t = part['uv'][loop.vertex_index]; uv.data[loop.index].uv = (t['x'], t['y'])
        for poly in mesh.polygons: poly.use_smooth = True
        mesh.normals_split_custom_set_from_vertices([(n['x'], n['z'], n['y']) for n in part['normals']])
        obj = bpy.data.objects.new(part['name'], mesh); bpy.context.collection.objects.link(obj)
        c = part['color']
        m = bpy.data.materials.new(part['name']); m.use_nodes = True; nt = m.node_tree
        b = nt.nodes['Principled BSDF']
        eye = part['name'].startswith('eyes_')
        b.inputs['Roughness'].default_value = .35 if eye else .9
        b.inputs['Base Color'].default_value = (c['r'], c['g'], c['b'], 1)
        texture = part.get('texture') or ''
        if texture and (art / texture).exists():
            if texture not in images: images[texture] = bpy.data.images.load(str(art / texture))
            tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = images[texture]
            mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'
            mix.inputs[0].default_value = 1.0; mix.inputs[7].default_value = (c['r'], c['g'], c['b'], 1)
            nt.links.new(tex.outputs['Color'], mix.inputs[6]); nt.links.new(mix.outputs[2], b.inputs['Base Color'])
            normal = art / texture.replace('_atlas.png', '_normal.png')
            if texture.endswith('_atlas.png') and normal.exists():
                if normal.name not in images:
                    images[normal.name] = bpy.data.images.load(str(normal)); images[normal.name].colorspace_settings.name = 'Non-Color'
                nm = nt.nodes.new('ShaderNodeTexImage'); nm.image = images[normal.name]
                node = nt.nodes.new('ShaderNodeNormalMap'); node.inputs['Strength'].default_value = .55
                nt.links.new(nm.outputs['Color'], node.inputs['Color']); nt.links.new(node.outputs['Normal'], b.inputs['Normal'])
        mesh.materials.append(m)
        objects.append(obj)
    return objects


def _wipe():
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.lights, bpy.data.cameras):
        for item in list(block):
            if item.users == 0: block.remove(item)


def _stage():
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 48; scene.cycles.use_denoising = True
    scene.view_settings.view_transform = 'Standard'   # true colours: this is a check of the art, not a beauty shot
    world = scene.world or bpy.data.worlds.new('world'); scene.world = world; world.use_nodes = True
    bg = world.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (.5, .52, .56, 1); bg.inputs['Strength'].default_value = .55
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, 0)); floor = bpy.context.object
    m = bpy.data.materials.new('floor'); m.use_nodes = True
    m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.115, .12, .135, 1)
    m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = .95
    floor.data.materials.append(m)
    for loc, power, size in (((1.4, 2.4, 2.2), 130, 2.2), ((-2.4, 1.2, 1.3), 45, 2.5), ((.3, -2.6, 1.9), 90, 2.0)):
        bpy.ops.object.light_add(type='AREA', location=loc); light = bpy.context.object
        light.data.energy = power; light.data.size = size
        light.rotation_euler = (Vector((0, 0, .28)) - light.location).to_track_quat('-Z', 'Y').to_euler()
    return scene


def render(root, keys, out, width=1400, height=760):
    art = Path(root) / 'src' / 'Art'; out = Path(out); out.mkdir(parents=True, exist_ok=True)
    sheets = []
    for key in keys:
        _wipe(); scene = _stage()
        lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
        for copy, (yaw, dx) in enumerate(((math.radians(-28), -.36), (math.radians(152), .36))):
            pivot = bpy.data.objects.new(f'{key} view{copy}', None); bpy.context.collection.objects.link(pivot)
            for obj in _load(art, key): obj.parent = pivot
            pivot.rotation_euler = (0, 0, yaw); pivot.location = (dx, 0, 0)
            bpy.context.view_layer.update()
            for obj in pivot.children:
                for corner in obj.bound_box:
                    p = obj.matrix_world @ Vector(corner)
                    lo = Vector(map(min, lo, p)); hi = Vector(map(max, hi, p))
        centre = (lo + hi) / 2
        direction = Vector((0, 1, .30)).normalized()
        bpy.ops.object.camera_add(location=centre + direction * 6); cam = bpy.context.object; scene.camera = cam
        cam.rotation_euler = (-direction).to_track_quat('-Z', 'Y').to_euler()
        cam.data.type = 'ORTHO'
        span_x = hi.x - lo.x; span_z = (hi.z - lo.z) + (hi.y - lo.y) * .30
        cam.data.ortho_scale = max(span_x, span_z * width / height) * 1.12
        scene.render.resolution_x, scene.render.resolution_y = width, height
        scene.render.filepath = str(out / f'{key}.png'); bpy.ops.render.render(write_still=True)
        sheets.append(out / f'{key}.png')
        print('COURIER_RENDERED', key, flush=True)
    if len(sheets) > 1:   # contact sheet, three across, each tile at half size
        tiles = []
        for path in sheets:
            img = bpy.data.images.load(str(path)); w, h = img.size
            px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::2, ::2]
            tiles.append(px); bpy.data.images.remove(img)
        cols = 3; rows = math.ceil(len(tiles) / cols); th, tw = tiles[0].shape[:2]
        sheet = np.zeros((rows * th, cols * tw, 4), dtype=np.float32); sheet[..., 3] = 1
        for i, tile in enumerate(tiles):
            r = rows - 1 - i // cols; c = i % cols
            sheet[r * th:(r + 1) * th, c * tw:(c + 1) * tw] = tile
        img = bpy.data.images.new('lineup', cols * tw, rows * th, alpha=False)
        img.pixels.foreach_set(sheet.ravel()); img.filepath_raw = str(out / 'courier_lineup.png'); img.file_format = 'PNG'; img.save()


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--root', type=Path, required=True)
    ap.add_argument('--only', default='')
    ap.add_argument('--out', default='art/courier')
    o = ap.parse_args(sys.argv[sys.argv.index('--') + 1:])
    root = o.root.resolve()
    every = sorted(p.name[len('courier_'):-len('.mesh.json')] for p in (root / 'src' / 'Art').glob('courier_*.mesh.json'))
    wanted = [k for k in every if not o.only or k in o.only.split(',')]
    render(root, wanted, root / o.out)
    print('IHM_COURIER_RENDER_OK', wanted)
