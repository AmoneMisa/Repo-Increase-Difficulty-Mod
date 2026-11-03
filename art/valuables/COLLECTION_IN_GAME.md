# Collection integration — 0.7.2 — 2026-11-03

Archive delivery only. **0.7.2 has not been installed automatically.**
The old 0.7.0 deployment predates the user's archive-only instruction; it does not
validate this package. All participants must manually update, restart and generate
a new level. Embedded meshes/textures/audio need no separate download.

Spritz: shop item, $5,000, one close-range tranquilizer use, consumed on hit or miss.
Shop and valuable selection are random, not guaranteed each level.

Sunrise/McJannel correspond to Sunstone Valley Resort (`Level - Desert`) and McJannek
Station (`Level - Arctic`). Swiftbroom is `Level - Wizard`, Headman Manor is
`Level - Manor`, Museum is `Level - Museum`. Dedicated pools restrict loot by map.

| Valuable | Base value | Root mass | Durability | Locations |
|---|---:|---:|---|---|
| Crystal skull | $2,500–4,000 | 0.25 kg | Very fragile | Sunstone, Swiftbroom, McJannek |
| Heavy bomb | $28,000–35,000 | 30 kg | Reinforced | McJannek |
| Winx bath set | $2,000–3,000 | 0.8 kg | Normal | Sunstone, Museum, Manor |
| Cannon | $15,000 | 55 kg | Reinforced | Manor, Sunstone, Museum |
| Liquid watch | $2,200–3,200 | 0.65 kg | Fragile | McJannek, Swiftbroom, Manor, Museum |
| Centrifuge | $7,000–9,500 | 4.5 kg | Sturdy | McJannek |
| Wheeled skeleton | $12,000–15,000 | 10 kg + joint bodies | Sturdy | McJannek, Sunstone, Museum |
| Robot vacuum | $6,000–8,000 | 3.5 kg | Sturdy | Manor, Museum |
| Graphics card | $4,500–5,500 | 0.75 kg | Very fragile | Museum, McJannek |
| RAM pair | $4,500–5,500 | 0.15 kg | Very fragile | Museum, McJannek |
| Monoblock PC | $6,000–8,000 | 3 kg | Normal | McJannek, Museum, Manor, Sunstone |
| Gold ingot | $3,500–4,800 | 3 kg | Reinforced | Sunstone/Sunstore, McJannek |
| Porcelain Maneki-neko | $12,000–16,000 | 6.5 kg | Fragile | Museum, Manor |

0.7.2 sizes relative to 0.7.1: bomb −20%, cannon −15%, centrifuge/vacuum +10%.
Each model uses its own bounds, mass centre and grab midpoint: X centred, Y above
the floor, Z forward from the native spawn origin. Volume selection uses native
category dimensions. A full mesh-footprint overlap check runs before custom spawning;
obstructed slots receive an ordinary same-map/category valuable, or are skipped if
there is no native fallback. Donor geometry remains empty. Bomb collision uses
separate convex pieces rather than one full enclosing box.

Wheel assistance is host-authoritative, floor-only and requires a grab. It adds no
lift; wheel visuals track travel. Liquid watch flow runs continuously, reverses on
physical inversion and pauses sideways; coloured drops, reservoir levels, slosh and
light need no activation key.

Centrifuge samples have rounded capsule contact, glass/cap/liquid grouped per tube,
and share the case body while seated. Pulling a tube by 3.5 cm for 0.12 s or damaging
it releases that sample. Inversion beyond 0.22 s releases successive samples. Case
damage can release the entire kit. Each released tube gets its own body; extracted
pieces have no separate loot value. Shake/impact audio uses embedded liquid/glass cues.
First kit loss commits a $2,000–3,000 penalty once through native BreakRPC, clamped
to remaining value. Ordinary damage is extra. Pending/applied state and the partial
missing-sample mask replicate with tube poses.

Bomb destruction invokes native explosive-grenade damage (size1.2, player damage75,
enemy damage160, force4). A per-instance guard prevents duplicate detonation.
Normal sale/extraction and scene unloading do not trigger the handler.

Lighting revision: RAM and GPU have continuous luminous guides and small RGB spill
lights. Vacuum projects rotating rings and a sweep downward onto the floor; PC has
a weak forward screen light. All twelve centrifuge liquids emit; two small fill lights
remain attached to individual samples after release. Water watch retains its coloured
emissive liquids and soft local light. Metal finishes replace the bomb/cannon green.
The porcelain cat is about0.64 m tall, with ears integrated into one head surface,
conformal face paint, an external rounded collar and inward-folded lower paws.
Its continuous raised forepaw and collision follow a local
shoulder pivot through a2.67 s fore/aft cycle, synchronized by Photon time. Its entire
paw sweep is included in the spawn reservation. The small tapered ingot is about15 cm long.

Validation: Release builds without errors/warnings;167 collection/liquid/late-spawn
policy checks,22 motion checks,39 HUD checks and97 network-handler checks pass.
These use dependency stand-ins and are not a live multiplayer run. The actual Unity
6000.2.9f1 fixture now passes199 clone/physics/lighting checks, including the projected
vacuum cookie, PC light, detached sample light and local paw motion. Native network
components are stand-ins. GLB/OBJ reimports, normals/winding, UVs, texture references
and animation bounds pass Blender validation for all six lighting-revision meshes.
The centrifuge passed its separate reimport test; all13 models passed the arithmetic
centre/floor/forward spawn-bound check.

Live checks remain: map placement, pickup/rolling and sample stability, late joining,
replicated value loss/explosions, native glass/glow and HUD appearance against
`art/references/hud-text-style.png`. No claim of live verification.
