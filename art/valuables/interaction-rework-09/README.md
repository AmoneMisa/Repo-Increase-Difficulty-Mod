# Custom valuable collection — revision 09

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
