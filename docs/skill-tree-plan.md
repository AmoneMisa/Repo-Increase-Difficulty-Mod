# Skill tree — design plan (draft for review)

Status: **released in 0.8.0** (steps 1–6: model, storage and network, shop, perk effects, tree screen, quests). Solo-only personal quest fixes from section 7 are still open.

## 1. What changes

| Today | After |
|---|---|
| ~20 custom upgrades sold one by one in the shop for money | One **Skill Point** item in the shop; perks are bought in the tree with points |
| Map Suite: one 10-level ladder | Split into separate map perks with prerequisites |
| Ping Anywhere + Ping Info | Ping root + five info add-ons (three new) |
| Flashlight: vanilla only | New Flashlight branch (range, width, brightness, colour) |
| Vanilla Tumble Wings, Death Head Battery in shop | Moved into the tree, removed from the shop |
| Vanilla Map Player Count | Removed from the shop for good (the Map root replaces it) |
| Quest rewards: money / items / upgrade items | Plus **skill points** as the main progression reward |

**Stays in the shop, outside the tree:** Death Protection (consumable charge) and the truck upgrades (Heal Power, Charge Efficiency, Loot Cash-In). They are team-wide or spendable, which does not fit a personal tree.

## 2. Balance rules

1. **Every point must feel noticeable.** Long 10–20-level ladders are compressed into ≤5 levels with bigger steps; the maximum effect stays the same (melee is +10% × 5 instead of +5% × 10).
2. **Cost by strength, not by depth:** 1 pt for information and moderate stats, 2 pts per level for strong stats (Armor, Monster Dots, Cosmetic Tracker, Ping root), 3 pts per level for the strongest (HP Regen, Inventory Slot).
3. **Prerequisite = parent has at least 1 level** (not maxed), so builds stay flexible. Chains (Zoom I → Zoom II) need the previous node.
4. **Roots cost 1 point and always do something small**, so there are no dead points.
5. **Pacing target:** the whole tree is 160 points (121 before the Arsenal page). At ~2.5–3 points per level, a player fills one page (≈27 pts) around level 10, about half the tree by level 20–25, and everything only on long runs (40+). Every number is a config value.

## 3. The tree (5 pages, 160 points)

`cost × levels`, then the prerequisite. ★ marks new mechanics; everything else already exists and is only re-wired.

### Page 1 — Recon (Карта и разведка) · 28 pts

**Map (Карта)**
| Node | Cost | Effect | Requires |
|---|---|---|---|
| Map Uplink (Карта: база) | 1×1 | Live player count on the map (replaces vanilla Map Player Count) | — |
| Dead Players | 1×1 | Dead player count | Uplink |
| Active Monsters | 1×1 | Active monster count | Uplink |
| Respawn Timers | 1×1 | Respawning monsters | Active Monsters |
| Monster Dots | 2×1 | Monsters as dots on the map | Respawn Timers |
| Quota Heading | 1×1 | Direction to the quota | Uplink |
| Valuable Tracker | 1×1 | Found/total valuables + haul | Quota Heading |
| Map Pings | 1×1 | Team pings shown on the map (also teammates' pings, so it doesn't need your own Ping) | Uplink |
| Zoom I → Zoom II | 1×1 each | Map zoom-out steps | Uplink / Zoom I |
| Cosmetic Tracker | 2×1 | Cosmetic boxes on the map | Uplink |

**Ping (Пинг)**
| Node | Cost | Effect | Requires |
|---|---|---|---|
| Ping Anywhere | 2×1 | Team pings through walls (existing) | — |
| Target Info | 1×1 | Names, prices, rarity (existing Ping Info) | Ping |
| ★ Enemy HP | 1×1 | Pinged monster shows a health bar | Ping |
| ★ Loot Condition | 1×1 | Pinged valuable shows % of original value left | Ping |
| ★ Weight | 1×1 | Pinged object shows its mass and "needs 2 players" for heavy loot | Ping |
| ★ Threat | 1×1 | Pinged monster shows its tier and how hard it hits | Ping |

**Flashlight (Фонарик)** — all ★
| Node | Cost | Effect | Requires |
|---|---|---|---|
| Flashlight Tuning | 1×1 | +15% beam range | — |
| Beam Width | 1×3 | +10% cone angle per level | Tuning |
| Brightness | 1×3 | +15% intensity per level | Tuning |
| Colour | 1×1 | Pick one of 8 beam colours; teammates see it too | Tuning |

### Page 2 — Combat (Урон и заряды) · 43 pts

| Node | Cost | Effect | Requires |
|---|---|---|---|
| Brawler (Урон: база) | 1×1 | +5% melee, grenade and tumble damage | — |
| Grenade Damage | 1×5 | +10% per level | Brawler |
| Melee Damage | 1×5 | +10% per level | Brawler |
| Tumble Damage | 1×5 | +10% damage, −10% self-damage per level | Brawler |
| Gunsmith (Заряды: база) | 1×1 | +1 shot per charge on every gun, scaled to the gun | — |
| Bullet / Laser / Tranq Extra Shots | 1×5 each | Existing per-gun scaling (Handgun +1 per level) | Gunsmith |
| Technician (Техника) | 1×1 | +10% drone battery | — |
| Drone Battery | 1×5 | +20% per level (was 20 × 5%) | Technician |
| Overcharge | 1×5 | Existing | Technician |

### Page 3 — Survival (Выживание) · 27 pts

| Node | Cost | Effect | Requires |
|---|---|---|---|
| Survivor (Выживание: база) | 1×1 | +5% max stamina | — |
| Stamina Efficiency | 1×5 | Existing "Slower Stamina Use" | Survivor |
| ★ Second Wind | 1×2 | Stamina recovers 15% faster per level | Stamina Efficiency |
| Armor | 2×5 | 4% less damage per level, 20% cap (was 10 × 2%) | Survivor |
| HP Regeneration | 3×3 | Existing | Survivor |

The mockup's split stamina/armor chains (+10%/+20%, ballistic/energy resistance) are left out on purpose. The game has no damage types, so ballistic vs energy resistance would be fake, and splitting one stat into many 1/1 nodes adds clicks without adding choices.

### Page 4 — Utility (Снаряжение) · 23 pts

| Node | Cost | Effect | Requires |
|---|---|---|---|
| Inventory Slot | 3×4 | +1 slot per level (existing, 7 max) | — |
| Autoscan | 1×5 | Existing | — |
| Death Head Battery (vanilla) | 1×3 | Vanilla upgrade, applied through the game's own upgrade call | — |
| Tumble Wings (vanilla) | 1×3 | Vanilla upgrade, same | — |

### Page 5 — Arsenal (Арсенал) · 39 pts

Added on request: damage per weapon, each one separate, and longer status effects.

| Node | Cost | Effect | Requires |
|---|---|---|---|
| ★ Marksman (Стрелок) | 1×1 | Small damage bonus on every gun | — |
| ★ Handgun Damage | 1×5 | More damage per level | Marksman |
| ★ Revolver Damage | 1×3 | ″ | Marksman |
| ★ Shotgun Damage | 1×4 | ″ | Marksman |
| ★ Cart Cannon Damage | 1×3 | ″, for whoever operates the cart | Marksman |
| ★ Cart Laser Damage | 1×3 | ″, for whoever operates the cart | Marksman |
| ★ Wizard Staff Damage | 1×4 | ″ | Marksman |
| ★ Poison Crossbow Damage | 1×3 | Bolt hit damage (the poison itself is Fast Poison) | Marksman |
| ★ Status Lab (Статусы) | 1×1 | Root of the status branch | — |
| ★ Longer Tranquilizer | 1×3 | Each level moves the duration a third of the way to 8 s | Status Lab |
| ★ Longer Net | 1×3 | Net 5 s → 6 → 7 → 8 s | Status Lab |
| ★ Longer Freeze | 1×3 | Ice 4.5 s → 5.7 → 6.8 → 8 s | Status Lab |
| ★ Fast Poison | 1×3 | Poison 15 s → 12.7 → 10.3 → 8 s with the **same total damage**, so it hits harder per second | Status Lab |

Status perks affect statuses your own weapons put on monsters. No status ever goes past 8 s, and poison is never shorter than 8 s. Ice Pistol, Net Launcher and Sticky deal no damage, so they get no damage perk; their value is in the status branch. The per-level damage percentages are set in step 4 and are config values.

## 4. Getting points

**Shop.** A new "Skill Point" upgrade item: the player who uses it gets +1 point. Its price starts at about $6K, the average of today's per-level prices, and rises with every point the team has bought this run, the same way vanilla upgrade prices rise. It appears in every shop.

**Quests.** A new reward type gives skill points:
- Team quest: +1 point to **every** player, so a squad isn't split over one reward.
- Personal quest: +1 to that player.
- Rank SS and higher: +2.
- Funny quests: +1 at most, and only one funny quest per level.

Upgrade-item rewards keep only vanilla upgrades (custom perks are no longer items).

**Estimate per level:** 2–3 quests at ~60% success give ~1.5–2 points, plus ~1 bought point, so about 2.5–3 per level.

## 5. Spending, sync, saves

- **Open the tree:** a key (default `K`) and a "Skill Tree" button in the Esc menu.
- **Where you can spend:** viewing works anywhere; spending works only in the shop and lobby. This keeps builds a decision between levels and avoids changing stats mid-fight.
- **Host-authoritative:** the client asks; the host checks points, prerequisite and cap, then sets the level. Every client receives it.
- **Storage:** each node stays a REPOLib player upgrade with **no shop item**. That gives saving and Photon sync for free and keeps existing upgrade ids, so old saves carry over. Earned points are one more hidden upgrade; spent points are computed from node levels.
- **Vanilla nodes** (Tumble Wings, Battery Head) call the game's own upgrade path, so the game saves and syncs them.
- **Migration of old saves:** levels already bought are converted into the matching nodes. Where a ladder was compressed (melee 10 → 5), a level converts to the equivalent effect and any rounding is refunded as unspent points. Map Suite level N unlocks the nodes of its old features.
- **Respec:** in the shop, refund all points for a money fee (open decision 2).

## 6. UI

A full-screen menu with 4 pages, laid out like the mockups: page tabs, branch panels, nodes with icon, name and `[level/max]`, and connectors that light up when a node is owned. Built as a new toolkit piece (`HudTreeView`) on `src/Hud` with native Teko text.

AGENTS.md allows a backplate here because this is a full-screen menu. Hovering a node shows a tooltip with the effect now and at the next level, the cost and the prerequisite. Locked nodes are dimmed; affordable ones pulse.

The mockups' painted background art is out of scope. Panels, borders and icons are procedural or small embedded PNGs, like the current upgrade icons.

## 7. Quests

### Solo fixes
- Solo gets **2 personal quests** instead of 1, and multiplayer-only quests (Revive) are never offered.
- Kill and haul targets for 1 player are re-checked against real solo runs (the `KillFactor` / `MoneyFactor` for 1 player).
- "No deaths" in solo means "don't die", and is offered more often.

### New quests

The tracking source for each one is listed. Rows marked "research" need a hook to be confirmed first.

| Quest | Type | Tracking |
|---|---|---|
| **Hat Trick** — kill 3 monsters within 15 s | Team | enemy death events (existing) |
| **Crown Jewel** — extract the level's most valuable item undamaged | Team | loot ledger knows initial values (existing) |
| **Speedrun** — close the first extraction within N minutes | Team | extraction events (existing) |
| **Lookout** — ping N different monsters | Personal | ping controller; offered only with Ping Anywhere |
| **Night Shift** — keep your flashlight off for 3 minutes in total | Personal | `FlashlightController.LightActive` |
| **Tourist** — visit N different rooms | Personal | room volumes (research) |
| **Long Throw** — throw an object 15 m | Personal | grab release → landing point |
| **Heavy Lifter** — carry a heavy valuable 20 m | Personal | grabbed object mass and distance |
| **Ghost** — finish the level without being chased | Personal | enemy chase target (research) |

### Funny ones (small reward, one per level at most)

| Quest | Tracking |
|---|---|
| **Talk It Out** — speak into voice chat for 30 s ("solo: talk to yourself") | voice-chat talking flag |
| **Bunny Hop** — jump 100 times | jump input / grounded change |
| **Ballerina** — spin around 10 times within 5 s | camera yaw accumulator |
| **Pay Respects** — crouch for 3 s where a monster died | death position + crouch |
| **Monster Hugger** — stay within 2 m of a living monster for 5 s and survive | distance check |
| **Oops** — break a valuable worth $2K+ (yes, on purpose) | valuable destroyed event |
| **Cart Surfer** — ride on top of a cart for 5 s | ground collider is a cart (research) |

## 8. Build order

1. **Model** (engine-free, unit-tested): node definitions, costs, prerequisites, validation, migration math.
2. **Storage and network:** hidden REPOLib upgrades, points upgrade, spend request/validate/apply, migration on load.
3. **Shop:** Skill Point item. Remove custom perk items, Map Player Count, Tumble Wings and Battery Head from the shop and the quest reward pool.
4. **Effects:** re-scale compressed ladders, gate Map features per node, add the new Ping info, Flashlight and Second Wind perks.
5. **Tree UI** (`HudTreeView`) and the Esc button.
6. **Quests:** skill-point rewards, solo fixes, new quests.
7. Tests, release notes (EN/RU), README, in-game validation in solo and with a guest.

Steps 1–3 can ship first with a plain list UI, so the economy is testable early.

## 9. Open decisions

1. **Where to spend points:** shop and lobby only (recommended), or anywhere.
2. **Respec:** a money fee in the shop (recommended), free, or never.
3. **Pacing:** is "one page by level ~10" right, or should points come faster?
4. **Flashlight capstone:** add a "Strobe" node that briefly staggers small monsters? It's fun but touches enemy AI. Not in the plan yet.
5. **Tumble Wings / Battery Head caps:** 3 levels each, or keep vanilla's uncapped stacking?
