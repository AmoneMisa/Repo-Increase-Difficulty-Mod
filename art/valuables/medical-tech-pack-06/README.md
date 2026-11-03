# Medical and tech valuables — pack 06

Eight real original mesh assets: the reference bomb with a new coarse worn-paint texture pass; an open laboratory centrifuge with tubes; the three-ring metal Spritz syringe; an anatomical teaching skeleton on a rolling stand; a triple-fan GPU; a pair of RGB RAM sticks; a lidar robot vacuum; and a three-bottle pink bath gift set with original fairy illustrations.

The texture treatment is R.E.P.O.-inspired: desaturated painted colors, point-filtered 128 px wear tiles, scratches, paint chips, and subdued specular highlights. It is an art interpretation, not a verified in-game style match. The bath labels are original artwork, with no copied character or brand images.

## Mesh files

Each asset folder contains GLB, OBJ/MTL, editable Blender source with packed images, and actual front/rear mesh renders. GLBs contain embedded textures. For OBJ, keep the shared `textures/` folder in place; MTL image paths are relative. All components were unwrapped; UVs are per object, not a baked shared atlas. The Blender sources include `Preview_` staging excluded from mesh exports.

| Asset | Triangles | Mesh parts | Blender X x Y x Z (m) |
|---|---:|---:|---|
| Worn Bomb | 4336 | 4 | 2.867 x 1.680 x 1.680 |
| Lab Centrifuge | 3604 | 11 | 0.470 x 0.608 x 0.636 |
| Spritz | 1152 | 17 | 0.147 x 0.526 x 0.062 |
| Teaching Skeleton | 14678 | 4 | 0.564 x 0.526 x 1.691 |
| Graphics Card | 2876 | 6 | 0.354 x 0.168 x 0.081 |
| Ram Pair | 1128 | 9 | 0.144 x 0.058 x 0.053 |
| Robot Vacuum | 1708 | 3 | 0.350 x 0.351 x 0.138 |
| Bath Gift Set | 1988 | 9 | 0.277 x 0.104 x 0.279 |

One unit equals one meter. Blender Z up; both exports use Y up. Asset roots are at ground level. Static parts sharing materials are combined into a small number of renderers; the centrifuge lid and Spritz moving parts remain distinct. These are assemblies of closed intersecting components, not single fused solids. The skeleton is a static display model, without a rig or ragdoll. The robot vacuum and GPU fans have no gameplay movement. The skeleton uses the earlier approved original skull mesh.

## Spritz implementation in the mod

`src/Gear/Spritz.cs` registers Spritz through the same REPOLib/RunStartStats path as existing gear, gated by the existing GearEnabled setting. It clones a native tranquilizer donor with a separate bullet prefab, keeps the donor's stun duration and enemy eligibility, sets range to 1.8 m, suppresses lethal/player/prop damage and the gun muzzle flash, and adds the new syringe model and original mechanical hiss. The runtime needle tip is aligned to the native muzzle, verified from the exported coordinates and winding. The 12-frame plunger press is stored in the runtime mesh resource; the GLB and Blender model are static poses.

Price is fixed at $5,000, including during this mod's shop-economy and player-count price changes. The item is single-use: one accepted activation consumes the dose even on a miss; it cannot be used in the shop. A local spent flag is set on each peer before native firing, blocking rapid or replayed activation and battery refills from producing another shot. The host removes the item from persistent purchased-item state and destroys the network object after the short use animation. A migration during that delay can be completed by the next host. Existing native item text is used; no separate HUD, system font, or extracted game font is introduced.

`spritz-runtime/` contains a copy of the embedded mesh, icon, textures and sound for review. The project embeds the originals under src/Art and src/Audio. These resource files alone are not an installable mod. The new implementation was built into bin/preview, not deployed into the active game profile.

## Integration status

The six other newly requested items are valuable **model assets**. Their loot registration, values, durability, physical colliders, and spawn balancing have not been added. The bomb remains a visual valuable model without explosion behavior. Existing approved PC/skull deliverables are not replaced by this archive.

The .NET project build succeeded with the preview output target. Existing art/audio checks passed for 16 original item sound cues. All sixteen GLB/OBJ exports were reimported into fresh Blender scenes and checked for matching triangle/part counts, dimensions, finite coordinates, ground height, UVs, materials, portable texture references, and closed manifold components after welding exported seams. All source Blend files reopened. GLB binary lengths/embedded texture buffers passed. Spritz runtime winding, normals, texture coverage, sampled animation length and exact needle/muzzle alignment passed. Detailed results are in manifest.json. Both render views for each asset were visually reviewed; PNGs are renders of the exported source geometry.

**Still requires live testing:** Spritz shop availability and displayed $5,000 price, equip/grab/collider fit from the native donor, stun on supported monsters, missed-use consumption, save/truck persistence, second-use denial, and host/client/host-migration behavior. Check the native item name, description and price against art/references/hud-text-style.png at comparable resolution: no in-game font/glow comparison has been performed. Compile and mesh checks do not establish gameplay readiness or the final native shader appearance.

## Rebuild

From the repository root:

1. `python tools/build_valuable_textures.py`
2. Run Blender with `--background --factory-startup --python tools/build_medical_tech_pack06.py`.
3. Run Blender with `--background --factory-startup --python tools/validate_medical_tech_pack06.py`.
4. `python tools/build_item_audio.py` and `python tools/validate_item_art.py`.
5. `dotnet build --no-restore -p:NuGetAudit=false -p:PluginsDir=bin/preview`.
6. `python tools/package_medical_tech_pack06.py`.

Single-asset geometry rebuild: append `-- spritz` (or another asset ID) to the Blender builder call, then rerun validation. Source references are the original model files at art/valuables/heavy-bomb-reference-05 and art/references/valuable-skull-approved.blend. No proprietary game models or fonts are packaged.
