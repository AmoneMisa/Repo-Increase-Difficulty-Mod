from pathlib import Path
import json,hashlib,zipfile,shutil
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/valuables/interaction-rework-09'
rows=json.loads((OUT/'manifest.json').read_text());assert len(rows)==12 and all(r.get('blend_reopen')=='PASS' and r['checks']['glb']['status']=='PASS' and r['checks']['obj']['status']=='PASS' for r in rows)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19);small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',15)
order=['crystal_skull','ram_pair','centrifuge','robot_vacuum','skeleton','bath_gift_set','bomb','cannon','graphics_card','monoblock_pc','spritz','water_timer']
sheet=Image.new('RGB',(1600,1380),'#171e24');d=ImageDraw.Draw(sheet)
for i,name in enumerate(order):
    im=Image.open(OUT/name/(name+'_front.png')).convert('RGB');im.thumbnail((400,400));x=i%4*400;y=i//4*440
    sheet.paste(im,(x+(400-im.width)//2,y+(400-im.height)//2));d.text((x+10,y+408),name.replace('_',' '),font=font,fill='#dce6eb')
d.text((15,1333),'Revision 09 | Studio renders and authored motion studies | Native appearance and physics need live game testing',font=small,fill='#b9c8d0');sheet.save(OUT/'collection_preview.jpg',quality=94)
for name in ['centrifuge','robot_vacuum','skeleton']:
    paths=sorted((OUT/name/'motion_frames').glob('*.png'));assert len(paths)==36
    frames=[]
    for p in paths:
        im=Image.open(p).convert('RGB');d=ImageDraw.Draw(im);d.rectangle((0,560,480,600),fill='#171e24');d.text((8,566),'Authored motion study - not live game physics',font=small,fill='#dce6eb');d.text((8,583),name.replace('_',' '),font=small,fill='#b9c8d0');frames.append(im)
    pal=Image.new('RGB',(480*6,600))
    for j,i in enumerate([0,6,12,18,24,30]):pal.paste(frames[i],(480*j,0))
    pal=pal.quantize(colors=192);encoded=[im.quantize(palette=pal,dither=Image.Dither.NONE) for im in frames]
    dest=OUT/(name+'_motion.gif');encoded[0].save(dest,save_all=True,append_images=encoded[1:],duration=[170,160,170]*12,loop=0,optimize=False,disposal=2)
    with Image.open(dest) as gif:
        # The spill study intentionally holds its settled pose; GIF combines adjacent still frames.
        assert gif.n_frames>=(15 if name=='centrifuge' else 30);duration=0
        for j in range(gif.n_frames):gif.seek(j);duration+=gif.info['duration']
        assert duration==6000
    strip=Image.new('RGB',(1920,600))
    for j,i in enumerate([0,10,18,30]):strip.paste(frames[i],(j*480,0))
    strip.save(OUT/(name+'_sequence.jpg'),quality=92)
audio=OUT/'audio';audio.mkdir(exist_ok=True)
for p in (ROOT/'src/Audio').glob('valuable_*.wav'):shutil.copyfile(p,audio/p.name)
code=OUT/'runtime-source';code.mkdir(exist_ok=True)
for p in (ROOT/'src/Valuables').glob('*.cs'):shutil.copyfile(p,code/p.name)
shutil.copyfile(ROOT/'src/Visuals/DonorVisuals.cs',code/'DonorVisuals.cs')
readme='''# Custom valuable collection — revision 09

## Visual changes

Twelve meshes share an original, muted painted palette, baked contact shading, edge wear and restrained metal response. Native R.E.P.O. albedo was inspected as a visual reference; proprietary game textures/fonts are not included. Final native appearance has not been verified in-game.

The crystal skull restores the approved colorless cut-quartz appearance, with slimmer recessed cheek arches, enlarged rounded-square sockets, flush orbital rims and a nearly closed jaw (shape revision 10). RAM has exactly two continuous rainbow diffuser meshes with gradient albedo/emission and a slow runtime glow pulse. The bath set uses new Winx fan-art labels and a printed header based on the supplied packaging reference.

## Runtime behavior implemented in the mod

- Eleven valuables register through REPOLib using dedicated native-map pools: cannon, water watch, centrifuge, vacuum, skeleton, RAM, skull, bath set, graphics card, monoblock and heavy bomb. Spritz is a separate 5,000 shop item with its single-use tranquilizer behavior and injector-fitted collision. Exact user-specified location and price tables are in COLLECTION_IN_GAME.md. The bomb explodes through the native destruction/grenade path when broken.
- Centrifuge has twelve separate, selectable tube assemblies and an open collision cage. Inversion (dot up < -0.35 for 0.22 s), actual case/sample damage, or pulling upward on a selected sample releases the kit. A single random $2,000–3,000 kit-value loss uses native BreakRPC; ordinary case damage is separate. The host waits for initial value assignment and replicates pending/paid state for authority transfer. Disable/enable preserves missing samples and payment state. Detached samples remain unpriced physics parts.
- Cannon, vacuum and wheeled skeleton spawn upright and receive bounded, host-side floor assistance from native grab displacement. One holder can move them on wheels. Floor contact enables appropriate motor/rolling audio and wheel motion; assistance adds no lift and stops while airborne or ungrabbed. Native lifting mass is preserved. Bomb mass is 50 kg.
- Skeleton stand casters roll during floor dragging. Fifteen separate rigidbody groups use limited spring joints for pelvis, torso, skull, shoulders, elbows, wrists, hips, knees and ankles. World/player contacts and movement can deflect them. Self-collision is suppressed for stability. The host simulates and broadcasts part poses at 10 Hz; clients interpolate and retain grab colliders. Pooling and large teleports reset poses.
- Cannon floor rolling and water-watch R-to-flip behavior are retained. Water-watch flow defaults to 32 seconds, with slow blue/rose/cyan mixing, emission and local light. Flip key and flow time remain configurable under Valuable Collection.
- Ten original mono Foley/synth cues cover metal, plastic, glass, bone, paper, rolling, motor, case flip, drip and loose tubes. Native PhysAudio presets route material impacts and break sounds through the game's audio system; extra movement/interaction cues are host-authorized. No additional HUD is drawn.

## Asset formats

GLB files contain meshes, materials and textures. Keep OBJ, MTL and PNG together when importing. Blender sources are editable and pack their image dependencies. JSON descriptors and PNGs are embedded by the current C# project. Runtime source snapshots and WAVs are included for review, not as a standalone installable plugin.

Rigidbodies/joints and reactive motion are created by the C# runtime; static GLB/OBJ import alone does not provide those game behaviors. Water-watch GLB has a seamless 18-second animated loop; colour animation uses KHR_animation_pointer, which some importers ignore. The GIFs are authored motion studies, not recordings of the game or evidence of tested Unity physics.

## Validation

All twelve GLB and OBJ exports were reimported in Blender 5.2. Checks cover triangle/part counts, finite coordinates, UVs/materials, bounds, manifold topology after seam welding, nondegenerate faces, portable material paths, correct runtime normals/winding and matching embedded resources. Sources reopen. Separate checks cover two continuous light bars, twelve tube groups, fifteen articulated skeletal groups and five casters. Skull and skeleton are capped below 10,000 and 22,000 triangles respectively. Water-watch clip timing, seamless endpoints and moving material channels pass. See manifest.json.

Twenty-two deterministic motion/flip/spill rules and 136 collection contract checks pass, including durability ordering, one-time sample-kit debits and pending/paid authority transfer. Ten sound cues pass format, duration, level, clipping and edge checks. The mod builds with zero warnings/errors. The integration build was installed in the Default Thunderstore profile and its checksum verified; see COLLECTION_IN_GAME.md. No live game verification was performed.

The clone hotfix also passes 62 isolated Unity 6 clone/binding/rolling checks. Donor geometry is removed in serialized mesh state, and bindings are rebuilt per spawn to prevent the donor vase and stale animation references. A new live game test is still required for the hotfix.

Remaining live checks: native lighting/style and glass/emission; loot spawning, grab points and collider fit; skeleton stability during contact/drop/drag; inverted tube release and pooling; vacuum floor handling and sound levels; host/client pose and audio agreement, late joining and host migration. These are not certified by the export or pure-math checks. No live game verification has been performed.
'''
(OUT/'README.md').write_text(readme,encoding='utf-8')
shutil.copyfile(OUT.parent/'COLLECTION_IN_GAME.md',OUT/'COLLECTION_IN_GAME.md')
files=sorted(p for p in OUT.rglob('*') if p.is_file() and 'motion_frames' not in p.parts and p.name!='SHA256SUMS.txt' and p.suffix!='.blend1')
(OUT/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.relative_to(OUT).as_posix()+'\n' for p in files))
archive=OUT.parent/'Valuables_Interaction_Rework_09.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in files+[OUT/'SHA256SUMS.txt']:z.write(p,'interaction-rework-09/'+p.relative_to(OUT).as_posix())
with zipfile.ZipFile(archive) as z:assert z.testzip() is None
print('PACKAGE09_PASS',len(files)+1,'files',archive.stat().st_size,'bytes')
