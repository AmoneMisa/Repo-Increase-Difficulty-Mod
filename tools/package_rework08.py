"""Package validated meshes and clearly labelled authored interaction previews."""
from pathlib import Path
import json,hashlib,zipfile
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/valuables/depth-rework-08'
rows=json.loads((OUT/'manifest.json').read_text())
assert len(rows)==12 and all(r.get('blend_reopen')=='PASS' and all(r['checks'][k]['status']=='PASS' for k in ['glb','obj']) for r in rows)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',16)
order=['cannon','water_timer','bomb','centrifuge','spritz','graphics_card','ram_pair','robot_vacuum','bath_gift_set','skeleton','monoblock_pc','crystal_skull']
sheet=Image.new('RGB',(1600,1380),'#171e24');draw=ImageDraw.Draw(sheet)
for i,name in enumerate(order):
    shot=Image.open(OUT/name/(name+'_front.png')).convert('RGB');shot.thumbnail((400,400))
    x=(i%4)*400;y=(i//4)*440;sheet.paste(shot,(x+(400-shot.width)//2,y+(400-shot.height)//2))
    draw.text((x+10,y+407),name.replace('_',' '),font=font,fill='#dce6eb')
draw.text((16,1333),'Authored studio renders | Revision 08 | Final native lighting and gameplay require an in-game test.',font=small,fill='#b9c8d0')
sheet.save(OUT/'collection_preview.jpg',quality=94)
for name in ['water_timer','cannon']:
    paths=sorted((OUT/name/'interaction_frames').glob('*.png'));assert len(paths)==72,(name,len(paths))
    frames=[Image.open(p).convert('RGB') for p in paths]
    # Fixed palette avoids unrelated background colour flicker between frames.
    palette=Image.new('RGB',(480*6,600))
    for j,i in enumerate([0,12,24,36,48,60]):palette.paste(frames[i],(j*480,0))
    palette=palette.quantize(colors=192)
    encoded=[]
    for frame in frames:
        d=ImageDraw.Draw(frame);d.rectangle((0,568,480,600),fill='#171e24');d.text((10,576),'Authored behavior preview - not game footage',font=small,fill='#dce6eb')
        encoded.append(frame.quantize(palette=palette,dither=Image.Dither.NONE))
    dest=OUT/(name+'_interaction.gif')
    encoded[0].save(dest,save_all=True,append_images=encoded[1:],duration=[170,160,170]*24,loop=0,optimize=False,disposal=2)
    with Image.open(dest) as gif:
        assert gif.n_frames==72
        duration=0;hashes=set()
        for i in range(gif.n_frames):gif.seek(i);duration+=gif.info['duration'];hashes.add(hashlib.sha256(gif.convert('RGB').tobytes()).hexdigest())
        # Cannon retraces its path and has symmetric lift poses; those frames legitimately repeat.
        assert duration==12000 and len(hashes)>(60 if name=='water_timer' else 30)
    strip=Image.new('RGB',(480*4,600))
    for j,i in enumerate([0,30,36,60]):strip.paste(frames[i],(j*480,0))
    strip.save(OUT/(name+'_sequence.jpg'),quality=92)
readme='''# Valuable collection: depth and interaction rework 08

Twelve revised, importable models with unique baked shading atlases, defined edge normals, physical trim and contact detail. These are original stylized assets. Native R.E.P.O. appearance has not been certified in-game.

## Files and import

Each model folder contains GLB, OBJ/MTL, editable Blender source, front/rear renders and a runtime mesh descriptor. Keep the MTL and PNG alongside the OBJ. GLB embeds its textures. Coordinates and bounds in manifest are Blender metres; GLB/OBJ use Y-up exports. Runtime JSON uses Unity axes and is loaded by the mod's custom loader.

The water-watch GLB has one seamless 18-second loop, moving droplets and material mixing. Material animation uses KHR_animation_pointer; importers without support may only play transforms. OBJ is a static mesh. Blender source and the runtime component retain colour mixing.

The GIFs and collection sheet are authored studio previews, not game footage. The water-watch GIF demonstrates a flip at five seconds. The cannon GIF shows floor dragging, lifting with stopped wheels, then dragging back.

## Implemented mod behavior

- Cannon and water watch are registered as collectible loot by src/Valuables/InteractiveValuables.cs, using inert native valuable presets. Registration skips if a safe donor cannot be found. Other collection models are delivered as assets; their loot registration is not included in this revision. Spritz retains its existing shop implementation: 5,000 price, single-use native tranquilizer effect.
- Cannon has separate axle pivots and compound collision. Wheels rotate from travelled distance while grabbed and in floor contact. Airborne movement, stationary holding and teleports add no rotation. Motion on moving rigidbody floors is compensated.
- Hold the water watch and press R to flip its case and restart the transfer. Configure WaterWatchFlipKey and WaterWatchFlowSeconds under Valuable Collection. Default transfer is 32 seconds, configurable from 12 to 120 seconds; drops fall over six seconds. Liquid colours blend blue, rose and cyan with emission and a small local light.
- The host validates flip ownership, current grab membership and cooldown, then broadcasts state. Multiplayer spawning, late joining and host migration still require live validation.
- The flip rotates the symmetric case around its centre; fluid fades and reforms in the upright local frame. It is a stylized animation, not tilt-aware fluid simulation. Actual bloom depends on the game's rendering. Opaque parts use a native texture shader; glass/emission use Standard material fallback.

## Validation and remaining checks

All twelve GLB/OBJ files were reimported in Blender 5.2 and checked for triangle/part counts, finite coordinates, dimensions, UVs/materials, nondegenerate faces and manifold geometry after seam welding. Blender sources reopen with correct roots. Runtime descriptors pass index/winding/normal/texture checks. Cannon axle pivots and Spritz muzzle/plunger tracks were checked. Water-watch GLB loop duration, seamless endpoints and transform/material animation channels were checked. See manifest.json.

The mod builds with zero warnings/errors, and 18 deterministic motion/flip checks pass. Existing embedded art/audio checks pass. These checks do not establish real collision feel, RPC behavior or final native visual quality. No live game validation or active-profile deployment was performed. Compare appearance, glow, object pickup, floor rolling, flip controls and multiplayer behavior in-game before release. Existing native HUD text is reused; no extracted proprietary fonts or textures are distributed.
'''
(OUT/'README.md').write_text(readme,encoding='utf-8')
files=sorted(p for p in OUT.rglob('*') if p.is_file() and 'interaction_frames' not in p.parts and p.name!='SHA256SUMS.txt')
(OUT/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.relative_to(OUT).as_posix()+'\n' for p in files))
archive=OUT.parent/'Valuables_Depth_Interaction_Rework_08.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in files+[OUT/'SHA256SUMS.txt']:z.write(p,'depth-rework-08/'+p.relative_to(OUT).as_posix())
with zipfile.ZipFile(archive) as z:assert z.testzip() is None
print('PACKAGE_PASS',len(files)+1,'files',archive.stat().st_size,'bytes')
