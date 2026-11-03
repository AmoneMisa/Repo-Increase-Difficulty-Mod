# Wheeled cannon and animated liquid timer — valuables pack 07

Two original valuable-model assets interpreted from the user's references:

- **Cannon:** green painted historic display cannon, thick metal wheel tires, twelve spokes per wheel, hubs and fasteners, axle, tapered carriage trails and spade, reinforcing collars and a shallow recessed muzzle. It is a static collectible model with exterior visual geometry, without firing behavior.
- **Water watch / liquid timer:** clear concave-waisted acrylic body, cobalt and magenta liquid reservoirs, settled beads, and six falling teardrops. The timer has a six-second seamless transform-animation loop. Droplets are animated mesh objects rather than particles or a fluid simulation; reservoir volumes remain fixed and motion does not respond to gravity when the model is tilted.

## Files and animation

Each numbered asset folder contains `.glb`, `.obj`/`.mtl`, editable `.blend`, and front/rear renders. The GLB contains embedded materials/textures. Keep the shared `textures/` folder for the cannon's OBJ/MTL. The glass/colored-liquid material uses glTF transmission and IOR; OBJ is a basic fallback and requires material reassignment for matching refraction.

**Use water_timer.glb or water_timer.blend for animation.** The GLB has one combined `Liquid_Timer_Flow` clip, starting at time zero and ending at 6 seconds. Set loop/repeat playback in the target importer/engine. The Blend has sampled keyframes 1–145 at 24 fps. The OBJ is a static frame-20 pose and cannot contain motion. `water_timer.animation.json` additionally provides per-node position/scale tracks with axes converted to Unity X/Y/Z. `water_timer_motion.gif` is a six-second moving preview rendered from the same source animation.

One unit is one meter. Blender uses Z up; GLB/OBJ use Y up. Models have ground-level roots. The cannon is approximately 1.544 m wide, 2.124 m long and 1.301 m tall; the timer is 0.104 m wide, 0.038 m deep and 0.260 m tall. The cannon uses 5,048 triangles; the timer uses 1,736. The Blender files include Preview_ camera/floor/lights, excluded from mesh exports. Textures are packed in the sources. Static cannon pieces sharing materials are combined; animated timer parts remain separate.

## Validation

GLB and OBJ files were reimported into Blender and checked for counts, UVs, materials, portable texture links, finite coordinates, dimensions, ground height, closed component topology after seam welding, and nondegenerate triangles. Saved source Blend files reopened successfully.

The exported GLB binary was checked for exactly one clip, six animated nodes, twelve changing channels, a six-second duration and matching first/last values. The reimported GLB also showed actual droplet movement and a matching loop endpoint. Source keyframes were checked at eight time samples for movement, seam equality and containment inside the clear casing. GIF validation checked 144 frames, 16 distinct visible phases, an infinite loop and exactly 6,000 ms duration. Both render views were visually reviewed. Details are in manifest.json.

## Game integration status

Both are categorized as **valuable model assets**, not shop consumables. Loot spawning, value/durability, colliders and game-prefab registration are not implemented. Native game shader conversion and animation playback still require in-game verification. No game fonts/assets are extracted or distributed. This archive does not modify Spritz or the prior model packs.

Rebuild from the repository root with Blender's `--background --factory-startup --python tools/build_cannon_water_timer07.py`, followed by the same Blender invocation for `tools/validate_cannon_water_timer07.py`, then `python tools/package_cannon_water_timer07.py`. The reusable helpers are `tools/valuable_meshkit.py` and `tools/merge_gltf_loop.py`. Intermediate motion-frame PNGs are generated locally but omitted from the ZIP; the GIF is included.
