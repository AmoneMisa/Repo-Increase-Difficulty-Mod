import bpy, sys
from pathlib import Path
root=Path(sys.argv[sys.argv.index('--')+1]).resolve()
bpy.ops.wm.open_mainfile(filepath=str(root/'art/ihm_items.blend'))
scene=bpy.context.scene; scene.render.resolution_x=800; scene.render.resolution_y=500
scene.cycles.samples=12
out=root/'bin/art-preview'; out.mkdir(parents=True,exist_ok=True)
for frame in (1,3,5,7,9,12,15,18,21,24,27,30):
    scene.frame_set(frame); scene.render.filepath=str(out/f'{frame:02d}.png'); bpy.ops.render.render(write_still=True)
