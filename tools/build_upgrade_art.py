"""Blender --background --factory-startup --python tools/build_upgrade_art.py -- --root PATH.

Render the checked-in upgrade atlases on an authored cuboid matching the vanilla pack's
dimensions and UV islands. No game assets, Unity editor, or asset bundles are needed.
The runtime uses the native pack; the editable scene includes an inventory wrapper.
"""
import argparse
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, required=True)
parser.add_argument('--atlas', help='Render only this atlas and save its own preview/scene.')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
root = args.root.resolve()
textures = root / 'src' / 'Textures'
art = root / 'art'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 32
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.65, .65, .65, 1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .65

# Blender Z up, front towards -Y. UVs match the vanilla pack, with a small bleed margin.
w, h, d = .176342, .267431, .070177
vertices = [(-w/2, d/2, 0), (w/2, d/2, 0), (w/2, -d/2, 0), (-w/2, -d/2, 0),
            (-w/2, d/2, h), (w/2, d/2, h), (w/2, -d/2, h), (-w/2, -d/2, h)]
faces = [(1, 0, 4, 5), (2, 1, 5, 6), (0, 3, 7, 4),
         (1, 2, 3, 0), (3, 2, 6, 7), (7, 6, 5, 4)]
uv_rects = [(.002441, .00293, .357994, .542142),
            (.855042, .00293, .996538, .542142),
            (.357994, .00293, .49949, .542142),
            (.640985, .548046, .996538, .689541),
            (.49949, .00293, .855042, .542142),
            (.279529, .548046, .635082, .689541)]
mesh = bpy.data.meshes.new('Upgrade pack - authored UV wrapper')
mesh.from_pydata(vertices, [], faces)
mesh.update()
uv = mesh.uv_layers.new(name='Vanilla pack UV')
for poly, (u0, v0, u1, v1) in zip(mesh.polygons, uv_rects):
    coordinates = [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
    if poly.index == 3:  # Bottom flap follows the native atlas's rotated island.
        coordinates = [(u1, v0), (u1, v1), (u0, v1), (u0, v0)]
    for loop, coordinate in zip(poly.loop_indices, coordinates):
        uv.data[loop].uv = coordinate

def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()

camera_data = bpy.data.cameras.new('Pack icon camera')
camera = bpy.data.objects.new('Pack icon camera', camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera_data.type = 'ORTHO'
camera_data.ortho_scale = .355
camera.location = (.34, -.9, .40)
aim(camera, (0, 0, h / 2))
light_data = bpy.data.lights.new('Softbox', 'AREA')
light = bpy.data.objects.new('Softbox', light_data)
scene.collection.objects.link(light)
light.location = (-.4, -.6, .8)
light_data.energy = 16
light_data.shape = 'DISK'
light_data.size = 1.2
aim(light, (0, 0, h / 2))

packs = []
for atlas in sorted(textures.glob(args.atlas or 'Upgrade_*.png')):
    material = bpy.data.materials.new(atlas.stem)
    material.use_nodes = True
    shader = material.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Roughness'].default_value = .85
    shader.inputs['Specular IOR Level'].default_value = .15
    image = bpy.data.images.load(str(atlas))
    # Match vanilla's 512 px budget rather than shipping oversized generation outputs.
    if tuple(image.size) != (512, 512):
        image.scale(512, 512)
        image.filepath_raw = str(atlas)
        image.file_format = 'PNG'
        image.save()
    image.pack()
    node = material.node_tree.nodes.new('ShaderNodeTexImage')
    node.image = image
    node.extension = 'EXTEND'
    material.node_tree.links.new(node.outputs['Color'], shader.inputs['Base Color'])
    model = bpy.data.objects.new(atlas.stem, mesh.copy())
    scene.collection.objects.link(model)
    model.data.materials.append(material)
    packs.append(model)
    for pack in packs:
        pack.hide_render = pack != model
    scene.render.resolution_x = scene.render.resolution_y = 256
    scene.render.resolution_percentage = 100
    scene.render.filepath = str(textures / ('Icon_' + atlas.name))
    bpy.ops.render.render(write_still=True)

if args.atlas:
    if not packs:
        raise RuntimeError('No matching atlas: ' + args.atlas)
    scene.render.resolution_x = scene.render.resolution_y = 768
    scene.render.filepath = str(art / (Path(args.atlas).stem + '_preview.png'))
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(art / (Path(args.atlas).stem + '.blend')))
    sys.exit(0)

# Review scene: all wrappers, including the reserved inventory pack, use the same scale.
rows = math.ceil(len(packs) / 4)
for index, model in enumerate(packs):
    model.hide_render = False
    col, row = index % 4, index // 4
    model.location = ((col - 1.5) * .29, 0, (rows - 1 - row) * .37)
    text_data = bpy.data.curves.new('Label', 'FONT')
    text_data.body = model.name.removeprefix('Upgrade_').replace('_', ' ').replace('-', ' ')
    text_data.size = .019
    text_data.align_x = 'CENTER'
    text = bpy.data.objects.new(text_data.body, text_data)
    scene.collection.objects.link(text)
    text.location = (model.location.x, -.06, model.location.z - .044)
    text.rotation_euler = (math.pi / 2, 0, 0)
center_z = (rows - 1) * .37 / 2 + h / 2
camera.location = (.65, -3, center_z + .6)
aim(camera, (0, 0, center_z))
camera.data.ortho_scale = max(1.35, (rows * .37 + .12) * 1400 / 1200)
scene.render.resolution_x = 1400
scene.render.resolution_y = 1200
scene.render.film_transparent = False
scene.render.filepath = str(art / 'upgrade_lineup.png')
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(art / 'upgrade_wrappers.blend'))
