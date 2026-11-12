# IncreaseDifficultyMod

**English** · [Русский](#-русский)

---

## 🇬🇧 English

A difficulty & gameplay mod for **R.E.P.O.** (BepInEx / HarmonyLib / REPOLib). It ramps the challenge
as a run goes deeper, sprinkles in random per-level "surprise" modifiers, adds custom shop upgrades,
a team ping system, a round-results board and a set of quality-of-life features for lobbies.

- **GUID:** `com.kubai.increasehardmod`
- **Version:** `0.10.8`

Mid-level joining also catches up live battery charge, upgraded capacity and native battery indicator colours. New-monster selection now recommends a score of 100.

Version 0.10.2 adds unique ritual reward valuables: a quantum computer, five matching crystal coffins, a living portrait and an eternal sun. Each keeps the previous $250,000 base reward; Swiftbroom is unchanged ([details](release-notes/v0.10.2.md)).

Version 0.10.1 adds two monsters — the Mischief Fairy (Tier I, carries light valuables out of carts and extraction points) and Hansel and Gretel (Tier II, seize and throw a player) — and the Witch's recorded laugh after a successful spell, with a 12-second repeat limit per Witch ([details](release-notes/v0.10.1.md)).

Version 0.10.0 adds five monsters (Golden Pig, Witch, Basilisk, Gorgon, Naga Queen), eight perks against them and their HUD statuses, plus Spread Upgrades, Ice/Sticky Pistol, shop info and Link Drone fixes ([details](release-notes/v0.10.0.md)).

Version 0.7.12 adds the plush corgi, three Rare wing cosmetics, an Armor HUD row, cleaner pings and a set of shop, HUD and multiplayer fixes ([details](release-notes/v0.7.12.md)).

Version 0.7.4 fixes Ice Pistol freezing and Link Drone pulling ([details](release-notes/v0.7.4.md)).

Version 0.7.3 includes the Steam/Photon late-join, Unity 6 freeze, poison-bolt player damage
and native HUD visibility fixes. Right-side order: quota, extraction count, Map Suite data,
modifier names, with descriptions in Tab. [Fix details](release-notes/v0.7.3.md).

Version 0.7.2 corrects custom mesh centres and full-footprint spawn checks, reduces bomb
mass to 9 kg, adds orientation-driven liquid flow and independent centrifuge tubes,
and fixes shop/retry HUD state and mid-run joining. The updated build adds local item
lighting, dark cast-metal bomb/cannon finishes, a 3 kg gold ingot and a beckoning porcelain
Maneki-neko (13 valuables total). Current balance: cannon 8 kg, doubled-size neko 6 kg,
wheeled skeleton 3 kg including its articulated parts. [Fix details](release-notes/v0.7.2.md).

Version 0.7.0 adds Spritz and eleven map-restricted valuables, stronger mechanical props,
destruction-triggered heavy-bomb explosions and one-time centrifuge sample-kit value loss.
See [release notes](release-notes/v0.7.0.md).

### Contents

- [Requirements](#requirements)
- [Installation and updating](#installation-and-updating)
- [Features](#features)
- [Default keys](#default-keys)
- [Configuration](#configuration)
- [Building](#building)
- [Notes](#notes)

### Requirements

- [BepInEx 5](https://github.com/BepInEx/BepInEx) (5.4.21)
- [REPOLib](https://thunderstore.io/c/repo/p/Zehs/REPOLib/) — required for the custom shop upgrades.
  A client without REPOLib will not see this mod's custom upgrades in the shop.
- Optional: [REPOConfig](https://thunderstore.io/c/repo/p/REPOModding/REPOConfig/) (needs
  [MenuLib](https://thunderstore.io/c/repo/p/REPOModding/MenuLib/)) for editing settings in-game.
  Both now live under the **REPOModding** author (the old `nickklmao` packages are deprecated).
- Everyone in the lobby should run the mod to avoid desync (several features are host-authoritative).

### Installation and updating

**Thunderstore Mod Manager / r2modman:** install IncreaseDifficultyMod and its required dependencies, then launch the game with **Start modded**. REPOConfig and MenuLib are optional for changing settings in-game. For a local ZIP, use the manager's **Import local mod** option; install BepInExPack and REPOLib separately if the import does not resolve dependencies.

**Manual installation:** install BepInEx 5 and REPOLib, close the game, and copy the archive's `plugins/IncreaseDifficultyMod` directory into `BepInEx/plugins/` in the game or your active mod-manager profile. The final plugin path is `BepInEx/plugins/IncreaseDifficultyMod/IncreaseDifficultyMod.dll`.

**Updating from Increase Hard Mod:** replace the previous installation. Remove the old `IncreaseHardMod.dll` from the active plugins folder before launching; keep only one active copy of this mod. If the old mod was installed as a separate manager package, uninstall that package and install IncreaseDifficultyMod. Keep `BepInEx/config/com.kubai.increasehardmod.cfg`: the plugin GUID and configuration filename intentionally stay the same, so existing settings and saved upgrade IDs continue to work.

All players in a lobby must use the same mod version and restart the game after updating. Models, textures, icons and audio are embedded in the DLL; no separate asset download is needed. The release ZIP contains the mod only, without personal configs, saves or game assemblies.

### Features

#### Difficulty ramp

Difficulty scales with `RunManager.levelsCompleted`, so every client derives the same value from the
game's synced level count (host-authoritative, no extra networking). Configurable start level,
levels-per-step, per-step growth and caps for:

- **Enemy health** — with per-enemy cap overrides (Elsa and Loom are excluded by default).
- **Enemy damage** and **enemy speed**.
- **Enemy respawn time** (faster respawns deeper in the run, with a minimum factor).
- **Quota** growth, capped both by a multiplier and by a fraction of the available loot.

#### Surprise modifiers

Each eligible level can roll one or more random modifiers, broadcast by the host over Photon so the
whole lobby shares the same set. Every modifier can be toggled individually; the chance of a special
level is configurable.

| Group | Modifier | Effect |
|---|---|---|
| Loot | Fragile Loot | Loot breaks and loses value more easily |
| Loot | Heavy Loot | Loot weighs more |
| Loot | Light Loot | Loot weighs less |
| Loot | Protected Loot | Loot takes less damage |
| Loot | Dangerous Loot | Hazardous valuables hurt players more |
| Level | Extra Small / Big Cart | An extra cart spawns |
| Level | Extra Souls | Monsters drop more souls |
| Level | Bigger Soul Drops | Each soul drop is worth more |
| Level | Spread Upgrades | Up to 6 pickups across the map; submit them with a successful quota to receive them in the next truck |
| Level | More Gacha Cubes | More gacha/cube valuables spawn |
| Player | No Map | No map until you find a map upgrade pickup |
| Player | Reduced Player HP | Everyone starts wounded (rolled %) |
| Player | Auto Revive | One automatic revive per player (optionally at the truck, fully healed) |
| Player | Money Loss on Death | Lose a % of collected money when you die |
| Player | Level Timer | Timed level: kills add time, deaths remove it, quota adds it; at 0 the layout rerolls |
| Player | Change Voices | Player voices get swapped/randomized |
| Monsters | Souls Explode Harder | Exploding souls deal more damage |
| Monsters | Monsters Pit-Tanky | Monsters take less death-pit damage |
| Monsters | Light / Heavy Monsters | Light monsters yield to one player's grab without Strength upgrades; heavy monsters are harder to move. Frozen/mounted restrictions and native hold limits remain. |

#### More surprise modifiers: tiers, conflicts, depth-weighted pool

On top of the 21 original surprises there are now 25 **stat modifiers** (46 in total). Each one is
data-driven (a tier, a category and up to three multipliers) and is rolled, synced, shown in the HUD, blocked
in the Black Market and switched on/off in the *Surprise Modifiers* config section exactly like the originals.

- **Tiers:** Very good, Good, Mixed, Bad, Very bad. The pool is weighted by tier, and the weighting tilts with depth:
  deeper runs roll more bad modifiers and fewer generous ones.
- **Conflicts:** two modifiers never turn the same lever on one level (no *Brutal* and *Docile* Monsters together), the
  original opposites stay exclusive (Heavy / Light Loot, Fragile / Protected Loot, Light / Heavy Monsters), and no
  second punishment is piled onto the same lever (no *Heavy Load* on a *Timed Level*, no extra damage taken with *Reduced Player HP*, no cheaper loot with *Fragile Loot*).
- **They compose** with the depth ramp, the economy events, the truck's Heal Power and the shield items - the multipliers
  multiply; they are applied by the same hooks.

| Category | Modifiers |
| --- | --- |
| Loot | *Gilded Loot* (+25% value), *Rich Pickings* (+50% value, +25% quota), *Cheap Trinkets* (-20% value), *Light Load* (-20% quota), *Heavy Load* (+25% quota) |
| Monsters | *Frail*, *Docile*, *Sluggish* Monsters, *Slow Respawns*, *Thin Patrols*; *Tough*, *Brutal*, *Hasty* Monsters, *Relentless Respawns*, *Infestation*; *Apex Hunters* (tougher, harder, faster), *Swarm* (+60% monsters, -15% health) |
| Players | *Padded Suits* (-20% damage taken), *Stimulants* (+40% healing), *Glass Cannons* (+30% damage taken, monsters -30% health), *Thin Skin* (+30% damage taken), *Poor Medicine* (-40% healing), *Fragile Everything* |
| Environment | *Sprawling Facility* (+25% size, +15% loot value), *Compact Facility* (-20% size, -15% monsters), *Extra Dock* (+1 extraction), *Cramped Dock* (-1 extraction) |

#### Level scaling and economy events

Scaling switches live in `Modules`, with tuning in *Level Scaling*. Economic surprises use the
shared `Modules/Surprises` switch and tuning in *Surprises*. Everything is host-side; clients get
the result through the game's normal sync.

| Module | What it does (defaults) |
| --- | --- |
| **Map value** | Valuables gain value with depth: x1.02 per level after level 2, capped at x3. |
| **Enemy count** | More monsters per level after level 8: +3% per level, capped at x2 (the extras come from the hardest tier already unlocked). |
| **Level size** | Bigger maps after level 8: +3% modules and grid per level, capped at x1.5. More modules also means more extraction points through the game's own thresholds. |
| **Extractions** | One extra extraction point every 10 levels, at most 5 per level. |
| **Moon phases** | Recommended: one phase every 5 levels. The optional interval override defaults to 5; three new cumulative phases follow the four native phases. |
| **Economic surprises** | Part of `Modules/Surprises`. From level 3 on, a level gets a random economic surprise with 50% chance: a multiplier on loot value, quota and/or extraction pay. 20 events from *Gold Rush* (+35% loot value) and *Windfall* (+30% pay) to *Tax Audit* (-25% pay, +10% quota) and *Crushing Quota* (+40%). Bad events get likelier with depth. Tune `Surprises/EconomyEventChancePercent` and `EconomyStartLevel`; 0% disables this category. Old economy settings migrate automatically. Shop price events remain under `Modules/ShopEconomy`. |

#### Shop economy and the Black Market

| Module | What it does |
| --- | --- |
| **Shop economy** | On entering a shop the host may roll a price event (50% by default): 19 of them from *Black Friday* (everything 45% off) and *Medic Sale* to *Inflation*, *Luxury Tax* and *Hyper Inflation* (up to +90%). Each event moves four price channels separately - items, upgrades, health packs, power crystals - and this mod's drones, gear, personal upgrades and truck upgrades follow the same channels automatically. It stacks with the player-count price scaling. Bad events get likelier with depth. The active event is shown in the HUD. |
| **Black Market** | In the shop press `B`: a keyboard-driven screen (Left/Right tab, Up/Down choose, Enter buys) lists every surprise modifier, level economy event and shop economy. Pay to block one for the rest of the run (it is saved with the run and reset by a new run). Base price 35 / 50 / 65 (modifier / level economy / shop economy), x2 for the nastiest things down to x0.5 for perks, plus 7% per level completed. Guests can buy too; the host checks the price and the balance. |

Level economy events also avoid clashing with the surprise modifiers already on the level (for example no
pay cut together with *Money Loss on Death*, no heavy quota on a *Timed Level*).

#### Skill tree and shop upgrades (via REPOLib)

**Skill tree (0.8.0).** The personal upgrades are perks in a skill tree: press **K** during a run, or use
**SKILL TREE** in the Esc menu. Click a perk to learn a level; each perk needs the one before it. Points are spent
only in the shop or truck (the host checks every purchase), and **Refund All** returns them for $10K.

- **Points:** the shop's **Skill Point** upgrade (at least one per shop, rising price) and some quests.
- **Pages:** Recon (map, ping, flashlight), Combat (melee / grenade / tumble damage, gun shots, drones,
  Overcharge), Survival (stamina, Second Wind, Armor, HP regeneration), Utility (inventory slots, Autoscan, vanilla
  Death Head Battery and Tumble Wings), Arsenal (damage per weapon, longer status effects) and Contracts
  (quest-only perks from special contract quests: Cart Handling, Gentle Hands, Calm Presence, Soft Steps).
- **Trade-offs (0.8.5):** every page except Contracts has three TRADE-OFFS rows. Each trade-off is better at one
  thing and worse at another; you hold one per row per page. A row opens after enough points in that page's perks
  (rows II and III also need the row below), costs 1/2/3 points, and **Reset Trade-offs** ($5K) refunds only them.
  The full tree is 240 points.
- **Old saves:** bought upgrades are converted into matching perks the first time a save loads.
- The full design, costs and balance are in [docs/skill-tree-plan.md](docs/skill-tree-plan.md). Every number is in
  the `Skill Tree` and `Player Upgrades` config sections.

**Still sold in the shop:** the Skill Point and Death Protection (1 charge: survive one lethal hit with 1 HP; re-buy
after it saves you, ~50,000).

**Truck upgrades** - bought once, they apply to the whole team:

| Upgrade | Levels | What it does |
| --- | --- | --- |
| **Heal Power** | 10 | The truck's heal is multiplied by 1.05 per level, compounding (35 HP: 36.75 at level 1, 38.59 at level 2, about 57 at level 10); heal packs, drones and regeneration are not affected. The Taxman's line shows the real amount. Starts at 10,000 |
| **Charge Efficiency** | 10 | The charging station multiplies remaining energy drain by 0.95 per level, compounding like Slower Stamina Use: 95% / 90.25% / 85.7% at levels 1-3, 59.9% at level 10; minimum 5%. Starts at 10,000. Stacks with the power-crystal economy: fewer crystals burned per recharge |
| **Loot Cash-In** | 1 | Once every quota is closed, leftover valuables are sold automatically for 75% of their value. One-time unlock, 50,000 |

#### Utility drones (shop)

Four extra drones. They are built on the game's own drones, so aiming, the beam, attaching,
the on/off switch, the opening animation and the battery work exactly like the originals. Every
player must run the mod, with the same module toggles.

| Drone | Price (value tier) | What it does |
| --- | --- | --- |
| **Ghost Drone** | 11,000-15,000 | Attach it to a valuable: players walk straight through that valuable and it is left out of extraction counting and suction while the drone is on. Weight, price and durability are unchanged. Anyone still inside it when the drone goes off stays pass-through until they step out. |
| **Silencer Drone** | 7,000-9,000 | Attach it to a valuable, item, cart, player or monster: the noise it makes (collisions, sounds, voice chat/TTS, and the radius monsters investigate it from) drops to 8% by default. It does not make the target deaf. |
| **Link Drone Set (A + B)** | 19,000-24,000 | One purchase gives both halves. Attach A to one object and B to another within 6 m: a physical cable ties them together (the cable is straight, colour-coded; faint amber while waiting, red under strain). Grab one end and the other is slowly reeled in, 0.75 m/s down to 1.25 m. It lets go if over-stretched for too long; switch a half off and on to relink. |
| **Repair Drone** | 15,000-18,000 base (7,500-9,000 after the custom-item discount) | Attach it to a damaged valuable to restore its lost value, up to its original value, at up to 500 per second. The battery does not drain on its own, so a full battery always repairs 25,000 - more than the 20,000 power crystal it takes to recharge. A valuable that was fully destroyed cannot be repaired. |

#### Extra gear (shop)

Orbs, guns and a grenade, again built on vanilla items (so models, handling and battery UI feel native)
with this mod's own models, animations and sounds.

| Item | Price (value tier) | What it does |
| --- | --- | --- |
| **Defence Orb** | 19,000-24,000 | Protects **loot**. While active, value that valuables would lose from breaking inside its field is paid from its battery instead (about 3,500 at full charge). Slowly self-discharges (default 20% per minute). |
| **Defence Drone** | 11,000-15,000 | Protects a **player**: attach it to a teammate (or yourself) and it absorbs up to 350 damage in total - the battery is the pool, so hits drain it. It also empties on its own in 45 seconds. When the pool runs out mid-hit, the remainder reaches the player. |
| **Protection Orb** | 11,000-15,000 | Players inside the field take 25% less damage, but the orb burns through its charge about three times faster than a normal orb. |
| **Poison Crossbow** | 19,000-24,000 | 7 damage per bolt and a poison that deals 125 damage over 15 seconds. Every bolt refreshes the poison to 15 s, it never stacks. A poisoned monster stays actively hostile and hunts players while the poison lasts. A third fewer shots than a normal pistol. |
| **Decoy Grenade** | 1,000-1,300 | Arm it and throw it: for 5 seconds it lures nearby monsters to itself, then disappears. |
| **Pulse Ice Pistol** | 11,000-15,000 | Freezes monsters and players for 4.5 seconds with a visible ice coating. Player freeze works in the shop too: pauses movement/jumping without shot damage; repeated hits refresh the timer. The frozen player sees a countdown. Frozen monsters cannot be picked up, take only 60% of firearm damage, and take 35% more from melee weapons. |
| **Sticky Pistol** | 9,000-10,000 | Pulls a monster or object toward you, as long as you could lift it freely with one strength level less than you have. |
| **Flamethrower C.A.R.T.** | $75K-$120K in the shop | A wheeled flamethrower turret with a loot box on the back. Grab it like any item; while holding it press **E** (shown in the item prompt) to toggle the flame: every monster within 7 m in the direction you look takes 10 damage per second (walls block it, players are never hurt). A full battery holds 700 damage per target (70 s of flame) and recharges at the charging station. Drop loot onto the rear box to store it (the Hauler's storage system, up to $25K); the box is extracted at an extraction point. |
| **Wizard Staff** | Same price as the vanilla revolver | A carved wand that throws chain lightning. Fire it like a gun: the bolt strikes the first monster within 5 m of where you aim (a small aim assist helps) then jumps to the nearest monster not yet struck within 5 m, up to 3 monsters in total. The 75 damage is shared between them: 75 to a lone monster, 38 + 37 for two, 25 each for three. Walls block every jump and players are never hurt. One battery bar per bolt, 8 bolts per charge; recharges at the charging station and counts as a bullet gun for upgrades and quests. |

Each drone, orb and gun has its own on/off switch in the `Modules` section of the config.

### Courier pet

A courier is a small walking helper with a delivery backpack that follows you all run. **Unlock one from the gacha**:
open the courier menu (`G`) and pull with a gacha coin. Every pull unlocks one of sixteen pets: penguin, chicken, cat, owl,
beaver, sheep, cow, dog, fairy, little devil, little angel, dragon, little mermaid, skeleton cat, stone elemental
and phoenix (common ones about 11% each, the rarest - dragon, elemental and phoenix - about 2%). A duplicate gives half a coin back. Everyone starts with one free
coin; more are **found rarely on levels** (a glowing, chiming gold token, about one level in five, first come first
served). Recolour its body, second colour and cap/backpack (12 swatches) in the lobby, the shop or next to the truck.
Couriers idle, run, fly, get petted and change mood (happy, focused, surprised, sad, sleepy).

- **Pet it** like the game's Elsa: aim your grab at any courier and grab. It wriggles free with Elsa's pink hearts, a happy face and one of three sounds of its own animal; keep holding to keep petting.
- **Courier menu** (`C`, arrows to move, `Enter` to pick): charge the item you're holding or aiming at in the truck and
  bring it back; leave held equipment in the truck or fetch it; put the item in your hands into its backpack and take items out
  (1 slot, up to 3); drop everything and follow; gacha; appearance.
- **Monsters go for couriers.** A monster nearby may notice it and come to look, and one right next to it knocks it
  back and hurts it. A knocked-out courier drops what it carries and returns to the truck after 45 s.
- **Courier perk page** (skill tree page 7, 47 points): *Head Search* (2 points) shows the route to the nearest reachable head, and *Head Rescue* (5 points) brings it back from up to three rooms away. Other perks include *Ally Delivery*, *Cart Delivery*, *Carry Weight*,
  *Bigger Backpack* (up to 3 slots), *Courier Speed*, *Dash*, *Flight*, *Courier Health* (up to double),
  *Tough Courier* (up to 40% less damage), *Low Profile* (monsters go for it less), *Quick Recovery* and
  *Lucky Finds* (a rare random upgrade on a level).
- The host simulates every courier; items it carries or stashes can't be grabbed by anyone else.

### Team ping system

Press the ping key to mark what you aim at. Items use a thin 2-pixel contour of their actual
silhouette and a downward arrow above the target. The interior stays transparent. Monsters are
framed by a red targeting reticle that locks on and rotates. Location markers raise a blue beacon in
the world (rotating rune circle on the floor, light pillar, hovering winged emblem and halo) and
keep an on-screen arrow so they can still be found through walls. Projection follows the game's displayed render texture,
including low-resolution rendering and letterboxing. Photon shares the target ViewID and world
position; the sender draws once locally and ignores its own network echo.

Gated behind ping upgrades:

- **Ping Info** — optional information addon for Ping Anywhere (value, rarity, exact names). Does not enable pinging.
- **Ping Anywhere** — unlocks all team pings (players, monsters, objects and surfaces, visible through walls),
  capped per player.

A global anti-flood cap limits how many pings can be on screen at once.

#### Level results (MVP / OOPS CHAMP)

When a level ends, a results board shows per-player loot hauled, loot broken, deaths and revives,
plus an **MVP** (best score) and an **OOPS CHAMP** (most value broken). The host records the stats
and broadcasts them, so everyone running the mod sees identical numbers; hauled loot is credited
only on a successful extraction.

#### Lobby & quality-of-life

- **Map Vote** — in the truck lobby players vote on the next map among a few random candidates
  (NumPad 1–6 or on-screen buttons). The leader when the truck departs is used; no votes = vanilla
  random pick.
- **Late Join** — the host keeps the room joinable during a run/shop/truck and re-syncs mod state
  to anyone who connects mid-game.
- **More Players** — raise the room's max player count above the default 6 (host-side).
- **Shop price scaling** — optionally scale shop prices with player count (host-side).
- **Shop duplicates** and **Shopping List** HUD — a cart overview in the shop with per-item
  quantities, summed prices and a running total vs. team currency.
- **Item Unstuck** — relocates embedded valuables to a collision-checked safe spot. A sustained
  failed grab also rescues valuables wedged in window frames or partially embedded in shelves
  (6 seconds by default). Releases grab anchors and briefly protects the valuable during recovery.
  Also keeps valuables invulnerable until first grabbed (host-side anti-softlock).
- **Challenges (0.8.5)** — a run level may have a glowing challenge shrine (shown on the map). Press **E**
  (your interact key) next to it to start a timed challenge: **Wisp Escort** (stay near a wisp until it reaches its home),
  **Target Practice** (shoot every floating target; guns use no battery meanwhile), **Hunt** (kill the marked
  strongest monster, nobody may die), **Cull** (kill several monsters, nobody may die) or **Smash** (break every
  crate by hitting, shooting, tumbling into or throwing things at it). Since 0.8.6 also **Monster Waves** (three
  waves of monsters come at the shrine one after another, nobody may die), **Vigil** (stand in the shrine's ring for
  40 s while its light draws monsters), **Spark Hunt** (collect 8 sparks spread over the level) and **Offering** (bring
  loot worth 15% of the level's loot into the shrine's ring), **Mushroom Path** (mushrooms light up in a sequence;
  jump on them in the same order, three rounds) and **Capture the Flag** (type the answer to a sum in chat, carry the
  flag to its base, then hold it for 2 minutes against monsters with nobody dying). Winning gives a skill point for everyone,
  an upgrade or a cosmetic box. Host-side; the chance per level is in the `Challenges` config section.
- **Loot Safety Net** — while an extraction point is open, the host spawns more loot when the loot
  the team can still collect (in the point, carried, in boxes, found or not) is not enough to fill it.
  It never spawns while enough loot is left.
  New loot appears away from players, is marked on the map and everyone sees a notice. Replaces
  the game's own top-up, which also counts loot nobody can reach. Host-side.
- **Auto Backup Saves** — always protects the checkpoint from automatic deletion after a wipe
  or early exit when the game would delete it. Optional backup snapshots are kept indefinitely.
- **Voice Recovery** — local recovery of failed microphone capture and voice streams after
  audio-device changes; manual Unity audio reset with Left Ctrl + F9.
- **Spectator Map** — one module for dead/spectating players. Opens a large map following the
  current view. Use the normal map key or hold Tab; arrows pan, map zoom keys change scale,
  and Home restores follow. Toggle `Modules/SpectatorMap`; key and size are in `Spectator Map`.

#### HUD & diagnostics

- **Level Info** panel — logs each level change (run number + name, plus rolled surprises) using
  the game's native TMP font.
- **Server Info** overlay — Photon region, ping, room code and player count (read-only, no sockets).
- **Level timer** on the native HUD for timed levels.
- **Photon HUD** — opt-in diagnostic that probes Photon region name servers (leave off for normal play).
- **Diagnostics** — switches to disable all patches or all runtime objects for troubleshooting.

### Default keys

| Action | Key | Config section |
|---|---|---|
| Place ping | `Mouse2` (middle mouse) | Ping |
| Dismiss / recall results board | `F7` / `Esc` to close | Round Results |
| Hide / show map-vote HUD | `F6` | Map Vote |
| Hide / show Level Info | `F8` | Level Info |
| Reset local audio and voice | `Left Ctrl + F9` | Voice Recovery |
| Spectator map (hold) | `Tab` | Spectator Map |
| Map zoom in / out (Map Zoom perks) | `-` / `=` | Player Upgrades |
| Open / close the skill tree | `K` (pages `1`–`7`, `Q` / `E`) | Skill Tree |
| Courier menu / pet a courier | `C` / grab it | Courier |
| Shop item info | `I` while aiming at a shop item | Gear |
| Dodge Roll perk | `Left Alt` + `A`/`D` | Monsters |
| Map vote | `NumPad 1–6` or click | Map Vote |
| Extra inventory slots | `4`–`7` | Inventory Slots |

### Save protection and backups

When the game would automatically delete a failed run, its checkpoint is preserved on the host.
Return to the main menu and select the existing save to continue from the last saved checkpoint;
no game restart or manual file copying is needed. The explicit delete-save menu button still works.

`Modules / AutoBackup` controls additional snapshots in `saves_IncreaseHardMod_backups` next to
the game's `saves` folder. Snapshots are never automatically pruned or overwritten. Turning
snapshots off, a missing save folder or a copy failure does not disable checkpoint protection.
Legacy `KeepSaveLoadable` and `MaxBackupsPerSave` settings are ignored.

### Voice and audio recovery

`Modules / VoiceRecovery` (default true) checks local microphone capture every two seconds
while focused and connected to a voice room. After ten seconds of failed capture it requests
a recorder restart, with a 30-second cooldown and at most three attempts per local recorder.
Silence and mute are not failures; mute, voice detection and push-to-talk remain under game control.

After an audio-device change, voice recording and playback restart after a short delay,
at most once per 15 seconds. **Left Ctrl + F9** requests a local Unity audio reset followed by
a voice-stream restart, including with automatic recovery disabled. It briefly interrupts sound;
it does not leave the game. `Voice Recovery / RecoveryKey=None` disables the shortcut.
These recovery controls can help with audio problems under Proton; a live Proton test has not
been performed and some silent driver failures require manual recovery.

`Voice Recovery / NetworkDiagnostics=true` logs the Photon room region, RTT, RTT variance and
cumulative reliable resends once per minute. It does not change transport, region, send rates
or timeouts, and does not guarantee lower latency.

### Save protection and backups

Failed runs and early exits never automatically delete the run's save. Return to the main menu
and select the existing save to continue from its last saved checkpoint without restarting the
game or copying files. The menu's explicit delete-save button still works.

`Modules / AutoBackup` controls additional snapshots in `saves_IncreaseHardMod_backups`
next to the game's `saves` folder. Snapshots are never automatically pruned or overwritten.
Disabling snapshots or a backup error does not disable save protection. The old
`KeepSaveLoadable` and `MaxBackupsPerSave` settings are ignored.

### Configuration

### Voice and audio recovery

`Voice Recovery / Enabled` (default true) checks local microphone capture every two
seconds while focused and connected to a voice room. After ten seconds of failed
capture it requests a recorder restart, with a 30-second cooldown and at most three
attempts per local recorder. Silence is not a failure. Mute, voice detection and
push-to-talk settings remain under the game's control.

After an audio device change, voice recording and playback restart after a short
delay (at most once per 15 seconds). For lost game audio, **Left Ctrl + F9** requests
a local Unity audio reset followed by a voice-stream restart. This briefly interrupts
sound; it does not reconnect or leave the game. `RecoveryKey=None` disables the shortcut.
The shortcut also works with automatic recovery disabled.

`Voice Recovery / NetworkDiagnostics=true` logs the actual Photon room region, RTT,
RTT variance and cumulative reliable resends once per minute. It does not change the
server region, transport, send rate or connection timeouts. These diagnostics cannot
repair an ISP route or guarantee a lower ping. Automatic recovery cannot detect every
silent audio driver failure; use the manual shortcut and retain logs for those cases.


All settings live in `BepInEx/config/com.kubai.increasehardmod.cfg` and are editable in-game via
REPOConfig. **Every module on/off switch is gathered in the first section, `Modules`** (surprises, upgrades,
each drone and gear item, each difficulty scaling stat, lobby tools, HUDs...), so you don't have to scroll
through tuning values to find them; values from the old sections are carried over the first time. Other sections: General, Enemies, Quota, Surprises, Surprise Modifiers, Modifier Tuning,
Player Upgrades, Truck Upgrades, Ping, Shop, Map Vote, Late Join, Item Unstuck, Loot Safety Net, More Players,
Spectator Map, Round Results, Auto Backup Saves, and HUD toggles (Level Info,
Server Info, Shopping List, Photon HUD), plus Voice Recovery and Diagnostics.

### Building

Requires the .NET SDK and a local REPO install. The project references game assemblies from the
Steam install and publicizes `Assembly-CSharp` at build time.

```bash
dotnet build
```

Override the game location if it isn't in the default Steam path:

```bash
dotnet build -p:GameDir="D:\Games\REPO"
```

Release output: `bin/Release/IncreaseDifficultyMod.dll` (use `dotnet build -c Release`). To build and validate the uploadable Thunderstore ZIP, run `powershell -ExecutionPolicy Bypass -File tools/package-thunderstore.ps1`. Building does not install the mod automatically. Explicit local deployment is available with `-p:DeployToGame=true -p:PluginsDir="YOUR_PROFILE/BepInEx/plugins/IncreaseDifficultyMod"`; this disables a legacy DLL in that same directory.

### Notes

- `_decompiled/` (game code) is git-ignored and regenerated locally; it is proprietary and not
  distributed.
- Menu-safe by design: Photon/socket probes are gated to `InRoom` so nothing touches networking on
  the main menu.

---

## 🇷🇺 Русский

Мод сложности и геймплея для **R.E.P.O.** (BepInEx / HarmonyLib / REPOLib). Чем глубже забег, тем
сложнее игра; на уровнях случайно выпадают «сюрпризы»-модификаторы; плюс кастомные улучшения в
магазине, командная система пингов, таблица итогов уровня и набор удобств для лобби.

- **GUID:** `com.kubai.increasehardmod`
- **Версия:** `0.10.8`

При входе посреди уровня также досылаются фактический заряд, увеличенная ёмкость и восстанавливаются штатные цвета индикатора батареи. Рекомендуемый балл выбора новых монстров — 100.

Версия 0.10.2 добавляет уникальные награды ритуалов: квантовый компьютер, пять вариантов хрустального гроба, живую картину и вечное солнце. Базовая стоимость каждой награды — $250 000; Swiftbroom без изменений ([подробности](release-notes/v0.10.2.md)).

Версия 0.10.1 добавляет ведьме записанный смех после успешного заклинания, не чаще раза в 12 секунд у одной ведьмы ([подробности](release-notes/v0.10.1.md)).

Версия 0.10.0 добавляет пять монстров (Золотая свинка, Ведьма, Василиск, Горгона, Королева Наг), восемь перков против них и HUD-статусы, а также исправления Spread Upgrades, ледяного и притягивающего пистолетов, подсказок магазина и Linked Drone ([подробности](release-notes/v0.10.0.md)).

В 0.7.12 добавлены плюшевый корги, три редких крыла, строка брони на HUD, более чистые пинги и исправления магазина, HUD и мультиплеера ([подробности](release-notes/v0.7.12.md)).

В 0.7.4 исправлены заморозка Ледяного пистолета и подтягивание Link Drone ([подробности](release-notes/v0.7.4.md)).

В 0.7.3 собраны исправления Late Join, заморозки Unity 6, урона арбалета по игрокам
и видимости HUD. Порядок справа: квота, количество квот, данные Map Suite,
названия модификаторов; описания доступны в Tab. [Исправления и ограничения проверки](release-notes/v0.7.3.md).

В 0.7.2 исправлены центры моделей и проверка полного места для спавна, масса бомбы снижена
до 9 кг. Добавлены физическая анимация жидкости часов, отдельное выпадение пробирок,
исправления HUD и позднего входа. В обновлённой сборке — подсветка предметов, тёмный металл
бомбы и пушки, золотой слиток и фарфоровый неко с движущейся лапой (всего 13 ценностей).
Текущий баланс: пушка 8 кг, увеличенный вдвое неко 6 кг, скелет на колёсах 3 кг вместе с костями.
[Описание исправлений](release-notes/v0.7.2.md).

В 0.7.0 добавлены Spritz и 11 ценностей с ограничениями по картам, усиленная прочность
механических предметов, взрыв тяжёлой бомбы при уничтожении и одноразовое списание
стоимости потерянного комплекта пробирок. [Описание изменений](release-notes/v0.7.0.md).

### Содержание

- [Требования](#требования)
- [Установка и обновление](#установка-и-обновление)
- [Возможности](#возможности)
- [Клавиши по умолчанию](#клавиши-по-умолчанию)
- [Настройка](#настройка)
- [Сборка](#сборка)
- [Примечания](#примечания)

### Требования

- [BepInEx 5](https://github.com/BepInEx/BepInEx) (5.4.21)
- [REPOLib](https://thunderstore.io/c/repo/p/Zehs/REPOLib/) — нужен для кастомных улучшений в
  магазине. Клиент без REPOLib не увидит эти улучшения.
- Мод желательно ставить всем в лобби, чтобы избежать рассинхрона (часть функций работает на стороне хоста).

### Установка и обновление

**Thunderstore Mod Manager / r2modman:** установите IncreaseDifficultyMod с обязательными зависимостями и запускайте игру через **Start modded**. Для настройки в игре дополнительно установите REPOConfig и MenuLib. Для локального ZIP используйте **Import local mod**; если импорт не установил зависимости, отдельно установите BepInExPack и REPOLib.

**Ручная установка:** установите BepInEx 5 и REPOLib, закройте игру и скопируйте папку `plugins/IncreaseDifficultyMod` из архива в `BepInEx/plugins/` игры или активного профиля менеджера. Итоговый путь плагина: `BepInEx/plugins/IncreaseDifficultyMod/IncreaseDifficultyMod.dll`.

**Обновление с Increase Hard Mod:** замените прежнюю установку. Перед запуском уберите старый `IncreaseHardMod.dll` из активной папки плагинов; должна остаться одна активная копия мода. Если прежняя версия установлена отдельным пакетом в менеджере, удалите этот пакет и установите IncreaseDifficultyMod. Сохраните файл `BepInEx/config/com.kubai.increasehardmod.cfg`: GUID плагина и название конфига оставлены прежними, поэтому настройки и идентификаторы сохранённых улучшений сохраняют совместимость.

Все участники лобби должны использовать одну версию мода и перезапустить игру после обновления. Модели, текстуры, иконки и звуки встроены в DLL; отдельно скачивать ресурсы не нужно. В релизном ZIP только мод: без личных конфигов, сохранений и сборок игры.

### Возможности

#### Рост сложности

Сложность зависит от `RunManager.levelsCompleted`, поэтому у всех клиентов значение одинаковое —
берётся из синхронизируемого счётчика уровней игры (решает хост, лишней сети нет). Настраиваются
стартовый уровень, число уровней на шаг, прирост за шаг и потолки для:

- **Здоровья врагов** — с индивидуальными потолками для каждого врага (Elsa и Loom по умолчанию исключены).
- **Урона** и **скорости** врагов.
- **Времени респауна врагов** (чем глубже, тем быстрее, с минимальным множителем).
- **Квоты** — рост ограничен и множителем, и долей доступного лута.

#### Модификаторы-«сюрпризы»

На подходящем уровне может выпасть один или несколько случайных модификаторов; хост рассылает их
через Photon, так что у всего лобби набор одинаковый. Каждый модификатор можно отключить отдельно,
шанс особого уровня настраивается.

| Группа | Модификатор | Эффект |
|---|---|---|
| Лут | Хрупкий лут | Лут легче ломается и теряет стоимость |
| Лут | Тяжёлый лут | Лут весит больше |
| Лут | Лёгкий лут | Лут весит меньше |
| Лут | Защищённый лут | Лут получает меньше урона |
| Лут | Опасный лут | Опасные ценности сильнее ранят игроков |
| Уровень | Доп. малая / большая тележка | Появляется лишняя тележка |
| Уровень | Больше душ | С монстров падает больше душ |
| Уровень | Дорогие души | Каждая душа стоит больше |
| Уровень | Разбросанные улучшения | Улучшения разбросаны по уровню, применяются только в грузовике |
| Уровень | Больше гача-кубов | Появляется больше гача/кубов |
| Игрок | Нет карты | Карты нет, пока не найдёшь улучшение карты |
| Игрок | Сниженное HP | Все начинают раненными (случайный %) |
| Игрок | Авто-воскрешение | Одно авто-воскрешение на игрока (по желанию у грузовика, с полным HP) |
| Игрок | Потеря денег при смерти | При смерти теряется % собранных денег |
| Игрок | Таймер уровня | Уровень на время: убийства добавляют время, смерти отнимают, квота добавляет; на 0 раскладка перегенерируется |
| Игрок | Смена голосов | Голоса игроков меняются местами / случайно |
| Монстры | Души взрываются сильнее | Взрывающиеся души наносят больше урона |
| Монстры | Монстры устойчивы к ямам | Монстры получают меньше урона от смертельных ям |
| Монстры | Лёгкие / тяжёлые монстры | Все монстры весят меньше (легко толкать) или больше (тяжело толкать) |

#### Больше модификаторов-«сюрпризов»: тиры, конфликты, пул по глубине

К 21 исходному «сюрпризу» добавлено 25 **статовых модификаторов** (всего 46). Каждый описан данными (тир, категория и до трёх
множителей) и бросается, синхронизируется, показывается в HUD, блокируется на Чёрном рынке и включается/выключается в
секции *Surprise Modifiers* конфига так же, как исходные.

- **Тиры:** очень хорошо, хорошо, смешанный, плохо, очень плохо. Пул взвешен по тирам, и вес смещается с глубиной:
  чем глубже забег, тем больше плохих модификаторов и меньше щедрых.
- **Конфликты:** два модификатора никогда не тянут один и тот же рычаг на одном уровне (нет *Brutal* вместе с *Docile* Monsters),
  исходные противоположности остаются взаимоисключающими (тяжёлый/лёгкий лут, хрупкий/защищённый лут, лёгкие/тяжёлые монстры),
  и нет второго наказания на том же рычаге (нет *Heavy Load* на *Timed Level*, нет лишнего урона по игрокам вместе с *Reduced Player HP*, нет подешевевшего лута вместе с *Fragile Loot*).
- **Складываются** с ростом сложности по глубине, событиями экономики, лечением грузовика (Heal Power) и щитами: множители
  перемножаются, применяются в тех же местах.

| Категория | Модификаторы |
| --- | --- |
| Лут | *Gilded Loot* (+25% стоимости), *Rich Pickings* (+50% стоимости, +25% квота), *Cheap Trinkets* (-20% стоимости), *Light Load* (-20% квота), *Heavy Load* (+25% квота) |
| Монстры | *Frail*, *Docile*, *Sluggish* Monsters, *Slow Respawns*, *Thin Patrols*; *Tough*, *Brutal*, *Hasty* Monsters, *Relentless Respawns*, *Infestation*; *Apex Hunters* (крепче, сильнее, быстрее), *Swarm* (+60% монстров, -15% здоровья) |
| Игроки | *Padded Suits* (-20% получаемого урона), *Stimulants* (+40% лечения), *Glass Cannons* (+30% получаемого урона, у монстров -30% здоровья), *Thin Skin* (+30% получаемого урона), *Poor Medicine* (-40% лечения), *Fragile Everything* |
| Окружение | *Sprawling Facility* (+25% размер, +15% стоимости лута), *Compact Facility* (-20% размер, -15% монстров), *Extra Dock* (+1 экстракция), *Cramped Dock* (-1 экстракция) |

#### Масштаб уровня и события экономики

Переключатели роста уровня находятся в `Modules`, параметры — в *Level Scaling*. Экономические
события входят в общий переключатель `Modules/Surprises`, их параметры находятся в *Surprises*.
Всё считает хост, клиенты получают результат обычной синхронизацией игры.

| Модуль | Что делает (по умолчанию) |
| --- | --- |
| **Ценность карты** | Ценности дорожают с глубиной: x1,02 за уровень после 2-го, максимум x3. |
| **Число врагов** | Больше монстров после 8-го уровня: +3% за уровень, максимум x2 (добавка берётся из самого сильного уже открытого тира). |
| **Размер уровня** | Карты крупнее после 8-го уровня: +3% модулей и сетки за уровень, максимум x1,5. Больше модулей — больше точек экстракции по пороговым значениям игры. |
| **Экстракции** | Одна дополнительная точка экстракции раз в 10 уровней, не больше 5 на уровень. |
| **Фазы луны** | Рекомендуется одна фаза каждые 5 уровней. Необязательная настройка интервала по умолчанию равна 5; после четырёх базовых фаз идут три новые накопительные. |
| **Экономические сюрпризы** | Входят в общий `Modules/Surprises`. Начиная с 3-го уровня с шансом 50% меняют стоимость лута, квоту и/или оплату за экстракцию. 20 событий, от *Gold Rush* (+35% к стоимости лута) и *Windfall* (+30% к оплате) до *Tax Audit* (-25% к оплате, +10% к квоте) и *Crushing Quota* (+40% к квоте). Плохие события с глубиной выпадают чаще. Параметры — `Surprises/EconomyEventChancePercent` и `EconomyStartLevel`; шанс 0% отключает эту категорию. Прежние значения переносятся автоматически. События цен магазина остаются в `Modules/ShopEconomy`. |

#### Экономика магазина и Чёрный рынок

| Модуль | Что делает |
| --- | --- |
| **Экономика магазина** | При входе в магазин хост может бросить событие цен (по умолчанию 50%): 19 штук, от *Black Friday* (всё на 45% дешевле) и *Medic Sale* до *Inflation*, *Luxury Tax* и *Hyper Inflation* (до +90%). Каждое событие отдельно двигает четыре канала цен - предметы, улучшения, аптечки, кристаллы энергии, - и дроны, снаряжение, личные и грузовые улучшения этого мода идут по тем же каналам автоматически. Складывается с масштабированием цен по числу игроков. С глубиной плохие события выпадают чаще. Активное событие видно в HUD. |
| **Чёрный рынок** | В магазине нажми `B`: экран с управлением клавиатурой (Влево/Вправо - вкладка, Вверх/Вниз - выбор, Enter - купить) показывает все модификаторы-«сюрпризы», события экономики уровня и экономики магазина. Заплати, чтобы заблокировать нужное до конца забега (сохраняется с забегом, новый забег сбрасывает). Базовая цена 35 / 50 / 65 (модификатор / экономика уровня / экономика магазина), от x2 за самое неприятное до x0,5 за бонусы, плюс 7% за каждый пройденный уровень. Покупать могут и гости; хост проверяет цену и баланс. |

События экономики уровня также избегают конфликтов с уже выпавшими модификаторами (например, нет
урезанной оплаты вместе с *Money Loss on Death* и тяжёлой квоты на *Timed Level*).

#### Дерево навыков и улучшения в магазине (через REPOLib)

**Дерево навыков (0.8.0).** Личные улучшения стали перками дерева навыков: нажми **K** во время забега или
кнопку **SKILL TREE** в меню Esc. Нажми на перк, чтобы изучить уровень; каждому перку нужен предыдущий. Очки
тратятся только в магазине или грузовике (каждую покупку проверяет хост), а **Refund All** возвращает их за $10K.

- **Очки:** улучшение **Skill Point** в магазине (хотя бы одно в каждом магазине, цена растёт) и некоторые задания.
- **Страницы:** Разведка (карта, пинг, фонарик), Бой (урон в ближнем бою, гранатами и кувырком, выстрелы, дроны,
  Overcharge), Выживание (выносливость, Second Wind, броня, регенерация HP), Снаряжение (слоты инвентаря, Autoscan,
  ванильные Death Head Battery и Tumble Wings), Арсенал (урон каждого оружия, более долгие статусы) и Контракты
  (перки только из особых заданий-контрактов: Cart Handling, Gentle Hands, Calm Presence, Soft Steps).
- **Компромиссы (0.8.5):** на каждой странице, кроме Контрактов, есть три ряда TRADE-OFFS. Каждый компромисс
  сильнее в одном и слабее в другом; на странице держится по одному в каждом ряду. Ряд открывается после
  достаточного числа очков в перках страницы (рядам II и III нужен ещё ряд ниже), стоит 1/2/3 очка, а
  **Reset Trade-offs** ($5K) возвращает очки только за них. Всё дерево стоит 240 очков.
- **Старые сохранения:** купленные улучшения переводятся в подходящие перки при первой загрузке сохранения.
- Полный дизайн, стоимость и баланс — в [docs/skill-tree-plan.md](docs/skill-tree-plan.md). Все числа настраиваются
  в разделах конфига `Skill Tree` и `Player Upgrades`.

**По-прежнему продаются в магазине:** Skill Point и Death Protection (1 заряд: переживаешь один смертельный удар
с 1 HP; после срабатывания купи снова, ~50 000).

**Улучшения грузовика** - покупаются один раз и действуют на всю команду:

| Улучшение | Уровни | Что делает |
| --- | --- | --- |
| **Heal Power** | 10 | Лечение в грузовике умножается на 1,05 за уровень с накоплением (35 HP: 36,75 на уровне 1, 38,59 на уровне 2, около 57 на уровне 10); аптечки, дроны и регенерация не затронуты. Таксман показывает реальное значение. От 10 000 |
| **Charge Efficiency** | 10 | Остаточный расход энергии умножается на 0,95 за уровень с накоплением, как у Slower Stamina Use: 95% / 90,25% / 85,7% на уровнях 1-3, 59,9% на уровне 10; минимум 5%. От 10 000. Работает вместе с экономикой кристаллов: на зарядку уходит меньше кристаллов |
| **Loot Cash-In** | 1 | Когда все квоты закрыты, оставшиеся ценности продаются автоматически за 75% стоимости. Разовая разблокировка, 50 000 |

#### Утилитарные дроны (магазин)

Четыре дополнительных дрона. Они сделаны на основе игровых дронов, поэтому наведение, луч,
прикрепление, выключатель, анимация раскрытия и батарея работают так же, как у оригиналов. Мод
должен стоять у всех игроков, и переключатели модулей должны совпадать.

| Дрон | Цена (категория) | Что делает |
| --- | --- | --- |
| **Ghost Drone** | 11 000-15 000 | Прикрепи к ценности: игроки проходят сквозь неё, а при экстракции она не считается и не засасывается, пока дрон включён. Вес, цена и прочность не меняются. Кто остался внутри при выключении дрона, проходит сквозь, пока не выйдет. |
| **Silencer Drone** | 7 000-9 000 | Прикрепи к ценности, предмету, тележке, игроку или монстру: шум от него (столкновения, звуки, голос и TTS, радиус, с которого монстры идут на шум) падает до 8% по умолчанию. Цель не глохнет. |
| **Link Drone Set (A + B)** | 19 000-24 000 | Одна покупка даёт обе половины. Прикрепи A к одному объекту, B к другому в радиусе 6 м: физический трос стягивает их (трос прямой, цветной; бледно-жёлтый в ожидании, красный при натяжении). Если держать один конец, второй медленно подтягивается, 0,75 м/с до 1,25 м. При долгом перенапряжении трос отпускает; выключи и включи половину, чтобы связать снова. |
| **Repair Drone** | 15 000-18 000 база (7 500-9 000 после скидки на наши предметы) | Прикрепи к повреждённой ценности: возвращает потерянную стоимость до оригинальной, до 500 в секунду. Батарея не разряжается сама по себе, поэтому полной батареи всегда хватает на 25 000 - больше, чем стоит кристалл в 20 000 для перезарядки. Полностью разрушенную ценность починить нельзя. |

#### Дополнительное снаряжение (магазин)

Орбы, оружие и граната, тоже на основе ванильных предметов (поэтому модели, управление и индикатор
батареи ощущаются родными), но со своими моделями, анимациями и звуками.

| Предмет | Цена (категория) | Что делает |
| --- | --- | --- |
| **Defence Orb** | 19 000-24 000 | Защищает **лут**. Пока активен, стоимость, которую ценности потеряли бы при поломке внутри поля, оплачивается из его батареи (около 3 500 на полном заряде). Медленно разряжается сам (по умолчанию 20% в минуту). |
| **Defence Drone** | 11 000-15 000 | Защищает **игрока**: прикрепи к товарищу (или себе), он поглощает до 350 урона суммарно - пул это батарея, удары её расходуют. Сам тоже разряжается за 45 секунд. Если пул кончился посреди удара, остаток доходит до игрока. |
| **Protection Orb** | 11 000-15 000 | Игроки в поле получают на 25% меньше урона, но орб тратит заряд примерно втрое быстрее обычного. |
| **Poison Crossbow** | 19 000-24 000 | 7 урона за болт и яд, наносящий 125 урона за 15 секунд. Каждый болт обновляет яд до 15 с, не накапливается. Отравленный монстр остаётся активно враждебным и охотится на игроков, пока яд действует. На треть меньше выстрелов, чем у обычного пистолета. |
| **Decoy Grenade** | 1 000-1 300 | Активируй и брось: 5 секунд приманивает ближайших монстров к себе, затем исчезает. |
| **Pulse Ice Pistol** | 11 000-15 000 | Замораживает монстров и игроков на 4,5 секунды с видимым ледяным покрытием. На игроках работает и в магазине: останавливает движение и прыжки без урона от выстрела. Повторное попадание обновляет таймер; замороженный игрок видит обратный отсчёт. Замороженного монстра нельзя поднять, он получает 60% урона от огнестрела и на 35% больше от ближнего боя. |
| **Sticky Pistol** | 9 000-10 000 | Притягивает монстра или предмет к тебе, если ты смог бы свободно поднять его с силой на один уровень ниже твоей. |
| **Flamethrower C.A.R.T.** | $75K-$120K в магазине | Огнемётная турель на колёсах с ящиком для добычи сзади. Хватай как любой предмет; держа его, нажми **E** (видно в подсказке предмета), чтобы включить/выключить пламя: каждый монстр в 7 м по направлению взгляда получает 10 урона в секунду (стены блокируют, игроков не задевает). Полной батареи хватает на 700 урона по цели (70 с огня), заряжается на зарядной станции. Добычу кладут в задний ящик (система хранения Hauler, до $25K); ящик выносится в точке эвакуации. |
| **Wizard Staff** | Как ванильный револьвер | Резной жезл, бьющий цепной молнией. Стреляет как оружие: молния поражает первого монстра в 5 м по направлению прицела (небольшая помощь в прицеливании) и перескакивает на ближайшего ещё не поражённого монстра в 5 м, всего до 3 монстров. 75 урона делятся между ними: 75 одному монстру, 38 + 37 двоим, по 25 троим. Стены прерывают цепь, игроков не задевает. Одно деление батареи на разряд, 8 разрядов на заряд; заряжается на станции, для улучшений и квестов считается огнестрельным оружием. |

У каждого дрона, орба и оружия свой переключатель в секции `Modules` конфига.

#### Командные пинги

Нажми клавишу пинга, чтобы отметить цель. Предмет получает тонкий контур 2 пикселя по
реальному силуэту и стрелку сверху; внутри остаётся прозрачным. Метка на полу — кольцо и стрелка.
Координаты учитывают фактическое положение игровой render texture, пониженное разрешение и
чёрные полосы. Photon передаёт другим игрокам ViewID цели и мировую позицию. У отправителя метка
рисуется один раз локально; собственное сетевое эхо игнорируется.

Зависят от улучшений:

- **Ping Info** — дополнение к Ping Anywhere: стоимость, редкость и точные названия; не открывает пинги.
- **Ping Anywhere** — открывает все пинги команды: игроков, монстров, предметов и поверхностей, с лимитом на игрока.

Глобальный анти-флуд ограничивает число пингов на экране одновременно.

#### Итоги уровня (MVP / OOPS CHAMP)

По завершении уровня показывается таблица: у каждого игрока — вынесенный лут, разбитый лут, смерти
и воскрешения, а также **MVP** (лучший результат) и **OOPS CHAMP** (больше всего испорченной
стоимости). Статистику ведёт и рассылает хост, поэтому у всех с модом цифры одинаковые; вынесенный
лут засчитывается только при успешной эвакуации.

#### Лобби и удобства

- **Голосование за карту** — в лобби грузовика игроки выбирают следующую карту из нескольких
  случайных (NumPad 1–6 или кнопки на экране). Побеждает лидер на момент отъезда; нет голосов —
  обычный случайный выбор игры.
- **Поздний вход** — хост оставляет комнату открытой во время забега/магазина/грузовика и
  синхронизирует состояние мода для зашедших посреди игры.
- **Больше игроков** — поднимает лимит комнаты выше стандартных 6 (на стороне хоста).
- **Масштаб цен в магазине** — цены могут расти с числом игроков (на стороне хоста).
- **Дубликаты в магазине** и HUD **Shopping List** — обзор корзины в магазине: количество,
  суммарная цена по каждой позиции и итог относительно общих денег команды.
- **Item Unstuck** — возвращает застрявшие ценности в проверенное свободное место. Если предмет
  зажат в оконной раме или частично вошёл в полку, удерживайте захват и тяните его: после длительной
  неудачной попытки (по умолчанию 6 секунд) мод освободит его. Захваты отпускаются, а на время переноса
  включается краткая защита от повреждений. Также сохраняется неуязвимость до первого захвата.
  Работает на стороне хоста.
- **Испытания (0.8.5)** — на уровне может появиться светящееся святилище испытаний (видно на карте). Нажмите **E**
  (клавишу взаимодействия) рядом с ним, чтобы начать испытание на время: **Проводи огонька** (держитесь рядом с огоньком, пока он
  не доберётся домой), **Стрельба по мишеням** (подстрелите все мишени; оружие при этом не тратит заряд),
  **Охота** (убейте отмеченного сильнейшего монстра, никто не должен умереть), **Зачистка** (убейте нескольких
  монстров, никто не должен умереть) или **Разгром** (разбейте все ящики ударом, выстрелом, кувырком или броском).
  С 0.8.6 также **Волны монстров** (три волны монстров идут к святилищу одна за другой, никто не должен умереть),
  **Бдение** (простойте в кругу святилища 40 с, пока его свет притягивает монстров), **Сбор искр** (соберите 8 искр
  по уровню), **Подношение** (принесите в круг святилища добычу на 15% стоимости всей добычи уровня), **Грибная тропа**
  (грибы загораются по очереди; прыгайте по ним в том же порядке, три раунда) и **Захват флага** (напишите в чат ответ
  на пример, отнесите флаг на базу и удержите его 2 минуты против монстров, чтобы никто не умер).
  Победа даёт очко навыков всем, улучшение или коробку косметики. Работает на стороне хоста; шанс на уровень —
  в разделе конфига `Challenges`.
- **Loot Safety Net** — пока точка сдачи открыта, хост добавляет ценности, если тех, до которых
  команда ещё может собрать (в точке, в руках, в коробках, найденных или нет), не хватает на её квоту.
  Пока ценностей хватает, ничего не добавляется.
  Новые ценности появляются вдали от игроков, отмечаются на карте, и все видят сообщение. Заменяет
  встроенную добавку игры, которая учитывает и недоступные ценности. Работает на стороне хоста.
- **Auto Backup Saves** — всегда защищает чекпоинт, когда игра собирается автоматически удалить
  сохранение после вайпа или раннего выхода. Дополнительные снимки хранятся без ограничения.
- **Voice Recovery** — локальное восстановление захвата микрофона и голосовых потоков после
  смены аудиоустройства; ручной сброс звука Unity через Left Ctrl + F9.
- **Spectator Map — карта для мёртвых** — единый модуль: крупная карта следует за камерой
  наблюдения. Обычная клавиша карты или удержание Tab открывает её; стрелки сдвигают область,
  клавиши масштаба меняют приближение, Home возвращает слежение. Переключатель —
  `Modules/SpectatorMap`, клавиша и размер — в секции `Spectator Map`.

#### HUD и диагностика

- Панель **Level Info** — журнал смены уровней (номер забега + название, выпавшие сюрпризы) родным шрифтом TMP.
- Оверлей **Server Info** — регион Photon, пинг, код комнаты и число игроков (только чтение, без сокетов).
- **Таймер уровня** в родном HUD для уровней на время.
- **Photon HUD** — опциональная диагностика, пробует серверы регионов Photon (в обычной игре держи выключенным).
- **Diagnostics** — переключатели, отключающие все патчи или все рантайм-объекты для поиска проблем.

### Курьер-питомец

Курьер — маленький помощник с рюкзаком, который весь забег ходит за вами. **Получите его из гачи**: откройте меню
курьера (`G`) и крутите за монету. Каждая прокрутка открывает одно из пяти животных: пингвин и курочка (по 25%), котик
и бобёр (по 20%), сова (редкая, 10%). Повтор возвращает половину монеты. У каждого есть одна бесплатная монета; новые
**изредка попадаются на уровнях** (светящийся звенящий золотой жетон, примерно на каждом пятом уровне, достаётся
первому). Цвета тела, дополнительный и кепки с рюкзаком (12 цветов) меняются в лобби, магазине или у машины. Курьеры
стоят, бегают, летают, радуются поглаживанию и меняют настроение (радость, сосредоточенность, удивление, грусть, сон).

- **Погладить** как Эльзу: наведите захват на любого курьера и схватите. Он выскользнет с розовыми сердечками, как у Эльзы, довольной мордочкой и одним из трёх звуков своего животного; держите захват, чтобы гладить дальше.
- **Меню курьера** (`C`, стрелки, `Enter`): зарядить в машине предмет из рук или под прицелом и вернуть; отнести снаряжение из рук в машину или принести его обратно;
  убрать предмет из рук в рюкзак курьера и достать обратно (1 слот, до 3); бросить всё и идти за вами; гача; внешний вид.
- **Монстры нападают на курьеров.** Монстр рядом может заметить курьера и подойти, а вплотную — отбросить и ранить.
  Выбитый курьер роняет груз и через 45 с возвращается к машине.
- **Страница перков «Курьер»** (7-я страница дерева, 47 очков): *Поиск головы* (2 очка) показывает путь к ближайшей доступной голове, а *Доставка головы* (5 очков) позволяет принести её с расстояния до трёх комнат. Также доступны *Доставка союзнику*, *Доставка тележки*,
  *Грузоподъёмность*, *Большой рюкзак* (до 3 слотов), *Скорость курьера*, *Рывок*, *Полёт*, *Здоровье курьера*
  (до двух раз больше), *Крепкий курьер* (до 40% меньше урона), *Незаметность* (монстры реже нападают),
  *Быстрое восстановление* и *Удачные находки* (редкое случайное улучшение на уровне).
- Всех курьеров считает хост; предметы у курьера в руках или в рюкзаке никто другой схватить не может.

### Клавиши по умолчанию

| Действие | Клавиша | Секция конфига |
|---|---|---|
| Поставить пинг | `Mouse2` (средняя кнопка мыши) | Ping |
| Скрыть / вернуть таблицу итогов | `F7` / `Esc` для закрытия | Round Results |
| Скрыть / показать голосование | `F6` | Map Vote |
| Скрыть / показать Level Info | `F8` | Level Info |
| Сбросить локальный звук и голос | `Left Ctrl + F9` | Voice Recovery |
| Карта наблюдателя (удержание) | `Tab` | Spectator Map |
| Зум карты + / − (перки Map Zoom) | `-` / `=` | Player Upgrades |
| Открыть / закрыть дерево навыков | `K` (страницы `1`–`7`, `Q` / `E`) | Skill Tree |
| Меню курьера / погладить курьера | `C` / захват | Courier |
| Голосование за карту | `NumPad 1–6` или мышь | Map Vote |
| Дополнительные слоты | `4`–`7` | Inventory Slots |

### Защита сохранений и бэкапы

Когда игра собирается автоматически удалить проваленный забег, у хоста сохраняется чекпоинт.
Вернитесь в главное меню и загрузите существующее сохранение, чтобы продолжить с последнего
сохранённого чекпоинта без перезапуска игры и копирования файлов. Ручное удаление через меню работает.

`Modules / AutoBackup` управляет дополнительными снимками в `saves_IncreaseHardMod_backups`
рядом с папкой `saves` игры. Снимки не перезаписываются и не удаляются автоматически. Выключение
снимков, отсутствие папки сохранения или ошибка копирования не отключают защиту чекпоинта.
Прежние `KeepSaveLoadable` и `MaxBackupsPerSave` игнорируются.

### Восстановление голоса и звука

`Modules / VoiceRecovery` (по умолчанию включено) проверяет захват локального микрофона раз в
две секунды, пока игра в фокусе и подключена к голосовой комнате. После десяти секунд сбоя
запрашивается перезапуск записи: пауза 30 секунд, до трёх попыток на экземпляр записи.
Тишина и выключенный микрофон не считаются сбоем; mute, активация голосом и push-to-talk
остаются под управлением игры.

После смены аудиоустройства запись и воспроизведение голоса перезапускаются с небольшой
задержкой, не чаще раза в 15 секунд. **Left Ctrl + F9** запрашивает локальный сброс звука Unity
и перезапуск голоса, в том числе при выключенном автоматическом восстановлении. Звук ненадолго
прерывается, выхода из игры нет. `Voice Recovery / RecoveryKey=None` отключает сочетание.
Это может помочь с проблемами звука под Proton; живой тест на Proton пока не проведён.
Некоторые сбои драйвера без явной ошибки требуют ручного восстановления.

`Voice Recovery / NetworkDiagnostics=true` включает вывод региона комнаты Photon, RTT,
разброса RTT и накопленных повторных отправок раз в минуту. Транспорт, регион, частоты отправки
и тайм-ауты не меняются; снижение пинга не гарантируется.

### Настройка

Все настройки хранятся в `BepInEx/config/com.kubai.increasehardmod.cfg` и редактируются прямо в
игре через REPOConfig. **Все переключатели модулей собраны в первой секции `Modules`** (сюрпризы, улучшения, каждый дрон и предмет, каждая характеристика роста сложности, лобби-инструменты, HUD...), чтобы не листать значения настройки; значения из прежних секций переносятся при первом запуске. Остальные секции: General, Enemies, Quota, Surprises, Surprise Modifiers, Modifier
Tuning, Player Upgrades, Truck Upgrades, Ping, Shop, Map Vote, Late Join, Item Unstuck, Loot Safety Net, More
Players, Spectator Map, Round Results, Auto Backup Saves, переключатели HUD
(Level Info, Server Info, Shopping List, Photon HUD), Voice Recovery и Diagnostics.

### Сборка

Нужны .NET SDK и локальная установка REPO. Проект ссылается на сборки игры из Steam и делает
`Assembly-CSharp` публичной при сборке.

```bash
dotnet build
```

Если игра лежит не в стандартной папке Steam, укажи путь:

```bash
dotnet build -p:GameDir="D:\Games\REPO"
```

Release-файл: `bin/Release/IncreaseDifficultyMod.dll` (команда `dotnet build -c Release`). Для сборки и проверки ZIP для Thunderstore запустите `powershell -ExecutionPolicy Bypass -File tools/package-thunderstore.ps1`. Обычная сборка не устанавливает мод автоматически. Для явной установки укажите `-p:DeployToGame=true -p:PluginsDir="ПРОФИЛЬ/BepInEx/plugins/IncreaseDifficultyMod"`; старая DLL в этой же папке будет отключена.

### Примечания

- `_decompiled/` (код игры) исключён через git-ignore и создаётся локально; он проприетарный и не распространяется.
- Безопасно для меню: проверки Photon/сокетов выполняются только при `InRoom`, поэтому в главном меню сеть не затрагивается.


### Round results loot summary / Сводка лута в итогах

The results board includes the initial value of generated level loot, the count and current value
left uncollected, the unfound subset of that remainder, and income from souls. Souls are excluded
from the starting loot baseline. Soul income is the proportional share of actual extraction or
Loot Cash-In payouts, including currency rounding and economy taxes/bonuses; extracted soul value
is shown alongside it. The host records and synchronizes the summary. Results remain available
in the shop/truck with F7 until the next run level starts.

В итогах показаны начальная стоимость лута уровня, количество и текущая стоимость несобранного,
ненайденная часть этого остатка и доход от душ. Души не входят в начальную стоимость уровня.
Доход от душ — их доля в фактической выплате при сдаче или автопродаже Loot Cash-In, с учётом
округления и событий экономики. Рядом указана стоимость сданных душ. Сводку считает хост и
передаёт остальным. До следующего уровня итоги можно открыть клавишей F7 в магазине/грузовике.

### UI presentation / Оформление интерфейса

Results and Black Market use full-screen overlays with the game's native Teko Light styling. Results
hide automatically on entering the shop; F7 recalls the saved table and Esc closes it. Large
lobbies use PgUp/PgDn pages. Server information sits at the top left. The shopping list has no
backdrop and shows quantity, item name, K-unit price and total. Map voting is compact; F6 hides
its HUD without cancelling the vote or NumPad voting. The toggle is configurable under
Map Vote / ToggleHudKey. Unstuck messages have no backdrop and sit above the inventory.
Shop-event titles use a subdued colour; holding/opening the map (Tab by default) shows the
explanation of its price changes. Physical upgrade packs use authored atlases on native geometry and
UVs; custom inventory icons remain, and the Tab list resolves readable names without IHM IDs.
Sticky Pistol uses a short cable cast and an attached tether while pulling, without the native
bullet/tracer or muzzle flash, with softer cable/latch sounds and no damage.

Итоги и Black Market занимают весь экран и используют родное начертание Teko Light. Итоги
автоматически скрываются при входе в магазин; F7 возвращает сохранённую таблицу, Esc закрывает.
Для больших лобби есть страницы PgUp/PgDn. Статус сервера расположен слева вверху. Список
корзины без подложки показывает количество, название, цену в K и итог. Голосование компактное:
F6 скрывает HUD без отмены голосования и выбора на NumPad. Клавиша настраивается в Map Vote /
ToggleHudKey. Сообщения Unstuck без подложки расположены над инвентарём. Название события
магазина приглушено; при открытой/удерживаемой карте (по умолчанию Tab) видны изменения цен.
Упаковки улучшений используют авторские атласы на ванильной геометрии и UV; иконки инвентаря — отдельные прозрачные изображения,
а в Tab отображаются читаемые названия без IHM. Sticky Pistol выпускает трос и показывает
связь с притягиваемым предметом, без обычной пули, трассера и вспышки, с мягким звуком и без урона.


### 0.6.0 inventory and shared truck upgrades / инвентарь и общие улучшения машины

**Inventory Slot** is sold in the normal shop. Each use adds one real inventory slot, from three
base slots up to **seven total** (four upgrade levels). Slots include native item icons and battery
indicators. Extra keys default to **4–7**, configurable under `Inventory Slots`; the module is under
`Modules / InventorySlots`, and its base price under `Player Upgrades / InventorySlotBasePrice`.
The authored Inventory wrapper and separate transparent icon are included in the DLL.
Saved contents of extra slots are restored across truck/level transitions.
Main-row number keys remain reserved for inventory: vote using **NumPad 1–6 or click**. If an
inventory action shares a binding with voting, the inventory action takes priority.

Capped custom upgrades cannot be consumed at maximum level. Ping Info and Ping Anywhere cap
at one; Map Suite at ten; Inventory Slot at four levels. Other upgrades use their declared/configured
limits. Death Protection can be purchased/used again after its charge is spent.
Ping Info alone does **not** enable any ping: Ping Anywhere is required for players, objects and surfaces.

Truck **Heal Power**, **Charge Efficiency** and **Loot Cash-In** are team upgrades. A purchase
increases the shared team level once, reaches late joiners, and remains after the buyer leaves.
They use host-synchronized saved levels. This does not make personal inventory slots team upgrades.

**Inventory Slot** продаётся в обычном магазине и даёт **+1 слот** за использование: от трёх
базовых до **семи всего** (четыре уровня). Это штатные ячейки с иконками предметов и индикаторами
батареи. Клавиши дополнительных слотов по умолчанию — **4–7**, настраиваются в `Inventory Slots`;
переключатель модуля — `Modules / InventorySlots`, базовая цена —
`Player Upgrades / InventorySlotBasePrice`. Готовая обёртка Inventory и отдельная прозрачная иконка
включены в DLL. Содержимое дополнительных слотов сохраняется при переходах между машиной и уровнем.
Цифры основного ряда остаются инвентарю; голосование — **NumPad 1–6 или мышь**. При совпадении
назначений приоритет получает действие инвентаря.

На максимальном уровне предмет улучшения не съедается. Ping Info и Ping Anywhere имеют
кап 1, Map Suite — 10, Inventory Slot — 4 уровня. Для остальных действуют их объявленные/настроенные
лимиты. Death Protection можно использовать повторно после расходования заряда.
Ping Info сам по себе **не** открывает никакие пинги: нужен Ping Anywhere (прежняя покупка Pin переносится автоматически).

**Heal Power**, **Charge Efficiency** и **Loot Cash-In** машины действуют на всю команду. Покупка
однократно повышает общий уровень, распространяется на поздно подключившихся и сохраняется после
выхода покупателя. Уровни сохраняет и синхронизирует хост. Улучшение личных слотов остаётся персональным.


### 0.6.1 HUD, pings and item fixes / исправления HUD, пингов и предметов

The server status is compact and avoids the native health/energy indicators. Voting sits below
the actual money display. Modifier names follow the quota counter, with full economy explanations
in Tab. The atlas loader matches complete filenames so it cannot use an `Icon_Upgrade_…` render as
a printed `Upgrade_…` texture. Active custom drones and shield orbs refresh native self-hover
physics on the host; inactive/stowed devices regain normal physics as overrides expire. Extra
inventory slots use independent animation anchors, so native HUD animation keeps slots 1–7 in a row.

Статус сервера стал компактным и не перекрывает здоровье/энергию; голосование располагается под
деньгами. Названия модификаторов следуют за счётчиком целей, объяснение экономики доступно в Tab.
Загрузчик точно сопоставляет имена: `Icon_Upgrade_…` больше не попадает на упаковку вместо атласа
`Upgrade_…`. Включённые кастомные дроны и защитные сферы обновляют штатное зависание на хосте;
после выключения/убирания в инвентарь временные переопределения физики истекают. У новых ячеек
собственные координаты анимации, поэтому штатный HUD сохраняет слоты 1–7 в одной строке.


### Monster balance in 0.6.1 / баланс монстров

The host config controls the lobby's enemy health, attack damage, movement speed, respawn wait,
per-enemy health caps and Heavy/Light mass tuning. Guests receive it on join and after setting changes.
Changing settings within a level does not reset the level timer or auto-revive budget.

Difficulty steps are `max(0, (levelsCompleted - RampStartLevel) / RampLevelsPerStep)` with integer division.
Health, damage and speed depth factors are `min(1 + perStep * steps, cap)`; health uses its per-enemy cap
when present. Respawn wait uses `max(1 - reductionPerStep * steps, minimumFactor)`.
The matching level surprise multiplies the depth factor **after** the depth cap/floor.
Disabling an enemy scaling module disables its depth ramp; level surprises are controlled separately by
Surprises and their individual switches. Enemy count has its own start/per-level/cap settings under
Level Scaling and is decided during generation, so count changes apply on the next generated level.
Late health changes preserve remaining-health percentage and do not revive dead enemies.

Пример: усиление HP ×2 и Frail Monsters ×0,75 дают итог ×1,5. Tough Monsters ×1,4 поверх
капа глубины ×3 дают ×4,2: кап ограничивает рост от глубины, а не отдельный сюрприз.
Так же сочетаются урон, скорость и время до респавна. Модули ScaleEnemyHealth / Damage / Speed /
Respawn отключают соответствующий рост от глубины; сюрпризы имеют независимые выключатели.
Настройки хоста передаются гостям, включая индивидуальные капы HP и массу Light/Heavy Monsters.
Изменение количества врагов применяется при следующей генерации уровня. При изменении максимального
HP сохраняется доля оставшегося здоровья; мёртвые монстры остаются мёртвыми.


### 0.6.2 ping and HUD fixes / пинги и HUD

- Voting stays in a stable upper-screen band, independent of animated currency bounds.
- Photon diagnostics use a separate top-right area and native HUD typography without a backdrop.
- Pings use the game's interaction layers, ignore local-player/irrelevant trigger volumes and resolve the physical owner of a hit collider. Small visible items get a narrow, occlusion-tested aiming tolerance.
- Location markers use a larger soft cyan ring and arrow. Item silhouettes retain a transparent centre with a soft halo around the 2-pixel contour.
- Different ping cues identify locations, valuables, upgrades, items, enemies, carts, vehicles and cosmetic boxes. Sounds follow the game's mixer; **Ping / SoundVolume** controls volume (0 mutes). **Ping / MarkerScale** controls location-marker size.
- Repeating your ping on the same target/location refreshes the marker instead of stacking contours. Ping Anywhere or Pin is still required for floor/wall marks.

Голосование закреплено в верхней части экрана. Диагностика Photon находится справа сверху,
не перекрывает статус сервера и использует игровой шрифт без подложки.
Исправлено распознавание предметов и улучшений по коллайдерам; небольшие видимые цели имеют
узкий допуск прицеливания с проверкой препятствий. Метка пола стала крупнее, мягкого голубого цвета;
кольцо, стрелка и обводка предмета получили мягкое свечение без заливки середины.
Звук зависит от типа цели. Громкость — **Ping / SoundVolume** (0 выключает), размер метки пола —
**Ping / MarkerScale**. Повторный пинг той же цели обновляет метку вместо наложения контуров.
Для пола и стен нужен Ping Anywhere (прежняя покупка Pin переносится автоматически).

Modifier names stay beneath the quota; descriptions are shown separately below the level heading only while Tab is open. Valuable ping prices contain one dollar sign.

Названия модификаторов находятся под квотой; описание выводится отдельно под уровнем только при открытом Tab. В цене пинга лута один знак доллара.

### 0.6.3 inventory, Tab and stamina / инвентарь, Tab и выносливость

Extra slots initialize correctly when copied from a hidden HUD and after a level transition. Modifier descriptions follow the native level number in Tab. Sprint drain uses `base × (1 − p)^level`; at the default 5% and level 5, 77.38% of the base drain remains (22.62% reduction). Supported saved levels are bounded to the configured cap, at most 20.

Дополнительные слоты корректно инициализируются при копировании скрытого HUD и после перехода уровня. Описание модификатора следует за штатным числом уровня в Tab. Расход спринта: `база × (1 − p)^уровень`; при стандартных 5% и уровне 5 остаётся 77,38% базового расхода (снижение на 22,62%). Уровень из сохранения ограничен настроенным пределом, максимум 20.

### 0.6.3 ping merge, Autoscan and battery upgrades / пинги, Autoscan и батареи

- **Ping Anywhere** now includes the former **Pin** upgrade. Only one surface-marker upgrade is sold. Saved Pin purchases migrate per player without granting an extra level; the legacy Tab entry is cleared. **Ping Info** only adds value, rarity and exact names. Markers still expire after **Ping / DurationSeconds** and obey the existing caps.
- **Autoscan** uses the complete native valuable registry, including crowded rooms and loot with unusual colliders. Native discoverable items are scanned too, with native team discovery. It waits for generation/discovery/map readiness, skips dead players and shops, and keeps the configured interval and radius per level.
- Bullet, laser and tranquilizer battery upgrades have distinct wrapper illustrations and matching transparent icons. Battery drain is corrected for the first shot and whole-bar weapons, with no second watchdog refund. Deployed drones use their activator's upgrade, even if somebody else grabbed them.

- **Pin** объединён с **Ping Anywhere**: в продаже одно улучшение меток поверхностей. Купленный Pin переносится по игрокам без лишнего уровня; дублирующая запись в Tab очищается. **Ping Info** добавляет сведения. Время метки — **Ping / DurationSeconds**, лимиты сохранены.
- **Autoscan** сканирует полный игровой список ценностей и предметы со штатным обнаружением, включая переполненные комнаты и необычные коллайдеры. Радиус и интервал настраиваются как раньше; сканирование ждёт загрузки уровня и не работает в магазине или после смерти.
- Для Bullet, Laser и Tranq подключены разные упаковки и прозрачные иконки. Исправлены первый выстрел, дробный расход целых делений батареи и повторный возврат расхода. Развёрнутый дрон использует улучшение активировавшего его игрока.

### 0.6.3 code audit / аудит кода

Death Protection respects shield reduction and native hit rejection. Regeneration keeps fractional healing and its configured cap. Sprint reduction scales the whole native sprint cost, including speed upgrades, and carries sub-float deductions. Live charging stations refresh team bonuses. Battery purchases stop at their effective cap. Network events validate types, finite values and sender authority; Black Market rejects invalid modifier IDs. Version, saved upgrade IDs and configuration filename remain 0.6.3-compatible.

Исправлены расход зарядов Death Protection, дробная регенерация и её кап, полный расход спринта с доплатой за скорость и накоплением дробей, обновление работающей зарядной станции, эффективные капы батарей, проверка сетевых сообщений и некорректных покупок Black Market. Версия — 0.6.3, ID сохранений и имя конфига сохранены.

HUD labels inherit game font settings, but independent overlay canvases currently do not reproduce the native blur/chromatic processing consistently, and their helper clears the glow colour. Diagnostic fallbacks still use Unity GUI fonts. This remains an open visual issue.

### 0.6.3 master reconciliation / объединение master

Retains the quest-system work already on remote master. Quest network events are separate from the ice pistol and Sticky Pistol; host state/buffs validate sender and payload. The merged build passes 93 controlled network checks and resolves 131 current-game Harmony targets. Quest progression/rewards still require in-game verification.

Сохранена система квестов из remote master. Сетевые события квестов отделены от ледяного пистолета и Sticky Pistol; состояние и бонусы проверяют хоста и формат сообщения. После объединения проходят 93 сетевые проверки и разрешаются 131 точки патчей текущей игры. Прохождение квестов и награды ещё требуют проверки в игре.


### 0.6.4 HUD, quests and balance corrections / исправления HUD, квестов и баланса

- **J** toggles quests, closed by default; Tab no longer opens quests automatically. Change `Quests / PeekKey` in config to rebind it. PgUp/PgDn page crowded lists. Keys are ignored while typing.
- Right-hand HUD order: quota amount, quota count, Map Suite statistics, modifier names. On Tab, map statistics and modifier explanations appear together in the bounded sidebar, with separate pagination when needed. No text backplates.
- Ping Anywhere is the base unlock for all target categories, including teammates. Ping Info only adds names, prices and rarity. Previous Pin ownership still migrates to Ping Anywhere.
- Link Drone Set purchases deliver two separately deployable parts, A and B, with a shared saved set identity. Disabling or losing one part never pairs the other with a different kit. Pair labels identify each set; old owned pairs migrate once.
- Quest dollar rewards are converted once into the native wallet's K units (rounded up to whole K, matching extraction). Economic payout multipliers apply only to extraction, not quest rewards. Existing inflated saved balances are **not** guessed or automatically rewritten.
- Shared Heal Power uses `(1 + p)^level` (default p = 0.05, config `HealBonusPerLevel`) and only the truck's heal; shared Charge Efficiency uses `max(0.05, (1-p)^level)`. Defaults: healing 1.05x compounding per level (1.63x at level 10), charging drain 95% / 90.25% / 85.7% (config `ChargeReductionPerLevel`, replacing `ChargePercentPerLevel`). Existing upgrade IDs and owned levels are retained; formulas also apply to saved levels.

**RU:** J открывает/закрывает квесты; по умолчанию они скрыты и не открываются через Tab. Клавиша меняется в `Quests / PeekKey`, страницы — PgUp/PgDn. Порядок справа: квота, количество квот, данные Map Suite, названия модификаторов. В Tab статистика карты и описания модификаторов видны одновременно в ограниченной колонке без подложки; при необходимости у каждого блока есть страницы. Ping Anywhere открывает все пинги, Ping Info только добавляет сведения. Набор Link Drone даёт два отдельных предмета A/B с постоянной сохранённой парой. Денежные награды квестов переводятся в тысячи один раз, экономические множители применяются к выдаче денег за эвакуацию. Уже завышенный баланс сохранения автоматически не меняется. Улучшения грузовика действуют на команду с убывающей эффективностью по формулам выше; изученные уровни сохраняются.

Replace the previous DLL with the 0.6.4 package and restart every client. Controlled fixtures do not certify live multiplayer delivery or the game's final blur/chromatic post-processing.
Замените предыдущую DLL новой 0.6.4 и перезапустите игру у всех участников. Проверки на стенде не заменяют проверку сетевой доставки предметов и итогового размытия HUD в самой игре.
