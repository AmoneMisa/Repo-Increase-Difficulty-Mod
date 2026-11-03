"""Short actual-mesh animation preview, rendered from the delivered Blender scene."""
import bpy
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];folder=ROOT/'art/valuables/lighting-collection-11/maneki_neko'
bpy.ops.wm.open_mainfile(filepath=str(folder/'maneki_neko.blend'),load_ui=False)
scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.color_type='MATERIAL';scene.display.shading.light='STUDIO';scene.display.shading.show_shadows=True
scene.render.resolution_x=480;scene.render.resolution_y=480;scene.render.resolution_percentage=100
scene.frame_start=1;scene.frame_end=64;scene.render.fps=24
scene.render.image_settings.file_format='PNG'
frames=ROOT/'work/neko11-motion-frames';frames.mkdir(exist_ok=True)
# The authoring file ends on the rear preview; restore the reference front camera.
from mathutils import Vector
parts=[o for o in scene.objects if o.type=='MESH' and not o.name.startswith('Preview_')]
scene.frame_set(17);bpy.context.view_layer.update();pts=[o.matrix_world@v.co for o in parts for v in o.data.vertices]
lo=Vector(tuple(min(p[i] for p in pts) for i in range(3)));hi=Vector(tuple(max(p[i] for p in pts) for i in range(3)));center=(lo+hi)/2;size=max(hi-lo)
scene.camera.location=center+Vector((1.4,-3,1.1))*max(size,.4);scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler()
for i in range(24):
    scene.frame_set(round(1+i*64/24));scene.render.filepath=str(frames/('%03d.png'%i));bpy.ops.render.render(write_still=True)
print('NEKO_PREVIEW_FRAMES_COMPLETE (assemble with tools/package_lighting11.py)',flush=True)
