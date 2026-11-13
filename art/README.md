# Blender item visuals

Created against `e6e0bb9` (master), using Blender 5.2.1. All shipped geometry and textures are original; no extracted game assets are included.

## Contents

- `ihm_items.blend`: editable low-poly models, materials, packed textures, camera, and keyframes 1–30 at 30 fps.
- `item_lineup.png`: Blender render. The seven tiles are texture swatches, not drone models.
- `item_motion.gif`: preview of the Blender keyframes (looped for review).
- `item_sounds.wav`: review reel in order: crossbow, ice pistol, sticky pistol, ice impact, decoy chirp.
- `../src/Art`: embedded PNGs and mesh/animation exports.
- `../src/Visuals/ItemArt.cs`: runtime loading, native material reuse, and animation playback.
- `../src/Audio`: 14 original mono PCM cues generated with `tools/build_item_audio.py`.
- `../src/Visuals/EnemyStatusVisual.cs`: animated poison mist and a blue cracked-ice material using each enemy's native shader.

| Item | Appearance / movement |
| --- | --- |
| Ghost Drone | Pale blue coating, ghost emission icon |
| Silencer Drone | Graphite coating, muted speaker icon |
| Link A / B | Orange / cyan coating; linked rings with one / two identification ticks |
| Repair Drone | Green coating and repair cross |
| Defence Drone / Orb | Blue coating and shield |
| Protection Orb | Amber coating and shield with cross |
| Poison Crossbow | Green receiver, bow limbs, poison cartridge, bolt launch and return, cable movement |
| Pulse Ice Pistol | Cyan block, radial cooling fins, sliding upper mechanism and emitter recoil |
| Sticky Pistol | Pink pressure drum, feed tube, wide nozzle and piston recoil |
| Decoy Grenade | Orange speaker beacon and antenna deployment on activation |

## 0.10.15 gear

`tools/build_gear15_art.py` (same exporter and conventions) builds the Smoke / Cryo / Glue grenades, Antidote, Adrenaline Shot, Repair Kit (key `repairkit`; `repair` belongs to the Repair Drone), the Boomerang Sickle (`sickle`, `sickle_broken`) and the Armored C.A.R.T. plating (`armored`), plus inventory renders and `art/gear15/gear15.blend`. `tools/build_gear15_audio.py` writes their cues. Both leave the older assets untouched.

The glue effect uses the original `IHM/Glue` shader (`tools/IceShaderBundle/Assets/Shaders/IHMGlue.shader`), built into `src/Art/ihm_glue.bundle` with `Unity.exe -batchmode -quit -projectPath tools/IceShaderBundle -executeMethod BuildGlueBundle.Build`; `RenderGluePreview.Render` writes `art/gear15/glue_shader.png`. `Assets/Editor/GlueMesh.cs` is a copy of `src/Visuals/GlueMesh.cs` (keep them in sync).

## Rebuild

Pass an **absolute** root to Blender; Blender can change its working directory when saving files.

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --python tools/build_item_art.py -- --root (Get-Location).Path
python tools/validate_item_art.py
dotnet build -p:NuGetAudit=false -p:PluginsDir="$PWD\bin\preview"
```

`build_item_art.py` regenerates the scene and exported assets from procedural source. Hand edits in the blend must be ported to the builder before regeneration. The exporter converts Blender X/Y/Z to Unity X/Z/Y, reverses triangle winding, and samples local transforms at 30 fps. Each gun is authored relative to its muzzle. Decoy is authored around its body center. There is no FBX importer or Unity asset bundle dependency.

## Integration

The reference-driven silhouette pass replaces the box receivers with tapered elliptical shells, swept ochre recurve bow limbs with spring inserts, a narrower rail, rounded grips and a barrel-shaped decoy. The crossbow cable now changes rotation and length about fixed limb endpoints when released and redrawn. Muzzle origins and runtime export format are unchanged.

The second art pass uses desaturated olive, blue-grey and dusty rose, smaller bevels, irregular receiver corners, repair straps and a shared pixel wear atlas across weapon surfaces. `python tools/build_item_audio.py` regenerates sound assets. Rerender motion frames with Blender's `--python tools/render_motion.py -- ABSOLUTE_ROOT`; the PNG sequence goes to `bin/art-preview`.

Drone/orb coatings use the native UVs; function icons use `droneIcon` / `orbIcon` emission sockets. Shell/core animations, battery widgets, targeting, and effects remain native. Weapon body renderers are hidden with `forceRenderingOff`, without removing transforms referenced by game scripts. The new visuals align to the native gun muzzle; all existing colliders and grab points remain intact. If a resource or expected hierarchy is missing, loading logs a warning and retains the donor model.

Gun motion starts from `ShootBulletRPC` on each client, including misses, independently of host-only hit effects. Each shot plays a one-second cosmetic cycle with a reset on repeated fire; it does not change the native firing cadence or reload logic. Grenade animation observes `isActive` on each client and holds the deployed pose. These are rigid-part animations, not player-hand animations.

## Verification and remaining game checks

Weapon audio replaces the native shot/global-shot pair, avoiding double shots. Hit cues, drone/orb activation/shutdown and the decoy beacon use the game's `Sound` class, so mixer settings, attenuation, occlusion and the Silencer patch apply. The decoy chirps every 0.8 seconds; its native ticking is muted. The cues are synthesized mechanical/electronic effects, not recordings from the game.

The host sends confirmed poison/freeze target IDs via Photon event 187; clients only accept the current master. Remaining duration is corrected for transit time. Snapshots every 0.8 seconds cover late joins and delayed enemy spawns without caching expired events. Repeated hits refresh a single effect. Poison emits up to 36 fading particles per enemy, with a short tail when poison expires. Frost clones only that enemy's materials, retains native shaders/skinning, applies a blue crack texture and a subtle emission pulse, and restores original slots on expiry/death/disable. Both effects can coexist. Gameplay damage and freeze logic remain host-owned.

Additional game checks: poison/freeze hits and misses; refresh before expiry; both statuses at once; death and pooled enemy respawn; joining during an active status; sound master volume and Silencer attenuation; grenade locator chirps on both peers. Verify the particle shader fallback and frost appearance for different enemy types. Compile/export checks do not replace these play tests.

Verified: Blender renders, geometry/UV/index validation, handedness and triangle winding, sampled track lengths, finite normalized transforms, return-to-idle poses, PNG coverage, PCM format/duration/peak/RMS/edge samples, and .NET build. Game prefab hierarchy was inspected read-only against the installed game. The installed shader inventory includes `Particles/Standard Unlit`, the primary poison-cloud shader.

Not yet play-tested. In a disposable profile, check each item in the shop and inventory; grab/equip/drop it; fire hits, misses, and repeated shots; activate the decoy; open/close drones and orbs. Repeat as host and client. Check shader appearance, muzzle alignment, outlines, and collider fit: colliders intentionally retain the donor shape, so crossbow limbs and the wider decoy casing are cosmetic. Check unloaded batteries and scene changes. No compiled plugin was deployed into the active game profile.


Weapon material compatibility: the installed stun grenade uses `Fresnel Opaque` with
`_BaseTexture` / `_BaseColor`. `ItemArt` recognizes this native albedo property alongside
`_BaseMap` and `_MainTex`, inspects all material slots (including disabled prefab renderers),
and borrows a native gun body material only when the item's own body has no compatible
preset. Custom atlas UVs use explicit texture scale/offset and Fresnel UV controls; donor
normal/emission maps are cleared on private copies. Original grenade materials and scripts
remain available for native ticking, pin and activation behavior. Verified by read-only
inspection of the installed grenade's shader properties and a .NET build; live decoy
appearance and activation still require an in-game check after replacing the plugin DLL.

## Valuable collection revision 08

The latest collection is [revision 09](valuables/interaction-rework-09/README.md),
with a revised skull, continuous RAM diffusers, Winx packaging, loose centrifuge
samples, floor-guided vacuum motion, articulated skeleton physics and original
material/motion audio. All eleven valuables now have runtime registration; Spritz
retains its separate shop implementation. Export/data checks and compilation pass.
Live native appearance and multiplayer physics still require game validation.

See [the collection notes](valuables/depth-rework-08/README.md) and
[preview sheet](valuables/depth-rework-08/collection_preview.jpg).
The pack includes twelve GLB/OBJ/Blender models with revised edge shading, unique baked
atlases and physical detail. Cannon and water watch additionally have loot registration
and interactive components in `src/Valuables`; the other collection valuables remain
exported assets. Spritz retains its separate shop implementation.

Hold the water watch and press R to flip it; the key and 32-second flow time are
configurable under `Valuable Collection`. Cannon wheels respond to floor dragging
while grabbed and stop in the air. The GIFs are authored previews, not live game footage.
Mesh reimport checks, 18 deterministic motion tests and compilation pass; native
appearance, collisions, spawning and multiplayer still require in-game validation.
