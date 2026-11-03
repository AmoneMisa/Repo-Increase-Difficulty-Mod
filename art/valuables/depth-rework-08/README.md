# Valuable collection: depth and interaction rework 08

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
