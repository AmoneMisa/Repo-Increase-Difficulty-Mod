# Collection game integration — 0.7.0 — 2026-11-02

Installed into the existing Default Thunderstore profile. Previous DLL backed up at
`work/deployment-backups/before-collection-integration/IncreaseDifficultyMod.dll`.
Installed version: **0.7.0** (Release).
Latest replacement SHA-256: `DC9DCF81095737F9C3CB32CF2942D4D4D95BD90C7F7039D233868763D687519B`.
This replacement completed before the user's subsequent instruction to deliver archives only.
Future builds must not be installed automatically. An already running session keeps its
previous loaded DLL until restart. The previous mapped DLL was preserved under a disabled filename.
The immediate previous build is also backed up in `work/deployment-backups/before-0.7.0/`.

Spritz is a shop item: fixed $5,000, one close-range native tranquilizer use, consumed on
hit or miss, with injector-fitted collision and plunger animation. Shop selection is random.
Gear is enabled in the installed profile. Valuable Collection defaults to enabled.

The user's Sunrise and McJannel names map to the installed game's Sunstone Valley Resort
(`Level - Desert`) and McJannek Station (`Level - Arctic`). Swiftbroom Academy is
`Level - Wizard`, Headman Manor is `Level - Manor`, and Museum of Human Art is
`Level - Museum`. These names were read from the installed Level assets.

| Valuable | Base value | Root mass | Fragility | Allowed locations |
|---|---:|---:|---|---|
| Crystal skull | $2,500–4,000 | 0.25 kg | Very fragile | Sunstone, Swiftbroom, McJannek |
| Heavy bomb | $28,000–35,000 | 50 kg | Reinforced | McJannek |
| Winx bath set | $2,000–3,000 | 0.8 kg | Normal | Sunstone, Museum, Manor |
| Cannon / gaubiza | $15,000 | 55 kg | Reinforced | Manor, Sunstone, Museum |
| Liquid watch | $2,200–3,200 | 0.65 kg | Fragile | McJannek, Swiftbroom, Manor, Museum |
| Centrifuge | $7,000–9,500 | 10 kg | Sturdy | McJannek |
| Wheeled teaching skeleton | $12,000–15,000 | 10 kg + joint bodies | Sturdy | McJannek, Sunstone, Museum |
| Robot vacuum | $6,000–8,000 | 5 kg | Sturdy | Manor, Museum |
| Graphics card | $4,500–5,500 | 0.75 kg | Very fragile | Museum, McJannek |
| RAM pair | $4,500–5,500 | 0.15 kg | Very fragile | Museum, McJannek |
| Monoblock PC | $6,000–8,000 | 3 kg | Normal | McJannek, Museum, Manor, Sunstone |

Location pools are dedicated to each native map, avoiding generic-pool spillover to
other maps, shops, arenas or custom levels. Items are eligible for random selection;
they are not guaranteed on every generated level. Damage reduces their value normally.
Very fragile presets use native fragility 100 / durability 5; fragile uses 85 / 20.
Normal uses 35 / 65, sturdy uses 22 / 85, and reinforced uses 10 / 95.
Lower fragility reduces impact sensitivity; higher durability reduces value lost on a break.

The centrifuge's open case collision permits selecting a tube. Inversion for 0.22 s,
damage to the case/sample, or pulling a selected tube outward by 3.5 cm for 0.12 s
releases the kit. Kit loss subtracts $2,000–3,000 once using native BreakRPC and the
native value HUD. Ordinary case damage is separate. Loss is clamped to remaining value.
The host waits for initial price assignment, then replicates committed/pending/paid
state with the sample poses. Disable/enable preserves missing tubes and the payment
flag; a new host can finish a pending debit without repeating an applied one.

The bomb listens to the native impact detector's destruction event. It uses the native
explosive grenade preset and damage path (size 1.2, player damage 75, enemy damage 160,
force multiplier 4). The event runs through the native validated destruction RPC and
has a per-instance duplicate guard. Selling/extraction and scene unload use normal
object removal and do not invoke this explosion handler.

## Clone and wheeled-prop fixes

Native donor meshes are replaced by an empty mesh and their renderers disabled before
registration. This serialized geometry removal survives Instantiate and renderer
re-enabling; the previous forceRenderingOff-only approach did not survive cloning.
The donor components/transforms remain valid for native scripts. Custom-part bindings
and wheel/flip pivots are rebuilt from each spawn's actual hierarchy. Missing visual
parts disable the affected behavior with one diagnostic instead of repeated watch errors.

Cannon, vacuum and wheeled skeleton spawn upright, clear the floor, shed inherited
frozen constraints and use low-friction floor contact. A host-side planar assist uses
the current holders' native grab displacement, allowing one player to pull on wheels.
It adds no lift and is disabled in the air or without a grab. Wheel motion is derived
from movement, including remote-client transform movement. The bomb mass is now 50 kg.

Real Unity 6000.2.9f1 fixture: 62 clone/binding/physics checks pass, including repeated
copies, forced renderer re-enabling, template destruction, upright floor placement,
and moving 55/15/5 kg bodies with one-holder assistance. These isolated tests use stub
metadata and do not certify native multiplayer collisions or in-game appearance.

Build: zero errors/warnings. Collection: 136 checks covering the exact location matrix,
prices, durability tiers, excluded map types, bounded kit cost, deferred payment,
duplicate callbacks and authority transfer. Motion rules: 22 checks.
Embedded art/audio validation passes. Installation checksum verified. The game was
closed during deployment; startup registration and gameplay have not been play-tested.

Launch through the Default profile and generate a new level to use the new loot lists.
All multiplayer participants need the same build and matching collection settings.
Live checks still needed: shop Spritz purchase/use, actual spawning by map, pickup and
joint stability, native glass/glow appearance, fragility and bomb chain explosions on
host/client. Existing game HUD rendering is reused; no new overlay is added.
