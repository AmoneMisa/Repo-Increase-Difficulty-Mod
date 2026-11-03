import bpy
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];folder=ROOT/'art/valuables/cannon-water-timer-07/water_timer'
bpy.ops.wm.open_mainfile(filepath=str(folder/'water_timer.blend'),load_ui=False)
scene=bpy.context.scene;scene.render.resolution_x=420;scene.render.resolution_y=560;scene.cycles.samples=20
for frame in range(1,17):
    path=folder/'motion_frames'/f'{frame:04d}.png'
    if path.exists():continue
    scene.frame_set(frame);scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
