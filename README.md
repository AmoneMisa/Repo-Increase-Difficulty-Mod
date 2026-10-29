# Increase Hard Mod

A difficulty & gameplay mod for **R.E.P.O.** (BepInEx / HarmonyLib / REPOLib). It ramps the
challenge as a run goes deeper, sprinkles in random per-level "surprise" modifiers, and adds a set of
custom shop upgrades — including a team ping system that uses the game's own on-screen highlight.

- **GUID:** `com.kubai.increasehardmod`
- **Version:** `0.2.0`

## Requirements

- [BepInEx 5](https://github.com/BepInEx/BepInEx) (5.4.21)
- [REPOLib](https://thunderstore.io/c/repo/p/Zehs/REPOLib/) — required for the custom shop upgrades.
  A client without REPOLib will not see this mod's custom upgrades in the shop.

## Features

### Difficulty ramp

Difficulty scales with `RunManager.levelsCompleted`, so every client derives the same value from the
game's synced level count (host-authoritative with no extra networking). Configurable start level,
levels-per-step, per-step growth and caps. Affects enemy health, damage, speed, respawns, and quota.

### Surprise modifiers

Each eligible level can roll one or more random modifiers, broadcast by the host over Photon so the
whole lobby shares the same set. Examples:

- **Loot twists** — Fragile / Heavy / Light / Protected / Dangerous loot.
- **Level content** — extra carts, extra souls, scattered upgrades, more gacha cubes.
- **Player conditions** — no map, reduced starting HP, one-shot auto-revive, money loss on death,
  timed levels, swapped voices.
- **Monster tweaks** — lighter/heavier monsters, tankier vs. pit damage, harder soul explosions.

### Custom shop upgrades (via REPOLib)

Stamina, HP Regeneration, Death Protection, Battery Life, Autoscan, Map Suite, Truck upgrades, and the
ping upgrades below.

### Level results (MVP / OOPS CHAMP)

When a level ends, a results board shows per-player loot hauled, loot broken, deaths and revives,
plus an **MVP** (best score) and an **OOPS CHAMP** (most value broken). The host records the stats
and broadcasts them, so everyone running the mod sees identical numbers; hauled loot is credited only
on a successful extraction. `F7` dismisses / recalls it (configurable under *Round Results*).

### Team ping system

Press the ping key (default **Mouse2**) to mark whatever you're aiming at. Pings use the game's own
`ValuableDiscoverGraphic` highlight (the animated corner-bracket square from valuable discovery),
tinted per ping type, so they match the in-game style, track their target, dim off-screen and fade out
on their own. Pings are broadcast over Photon carrying the pinged object's PhotonView ViewID, so every
client highlights the same object; Location pings anchor to a small proxy at the point.

Gated behind ping upgrades:

- **Ping Info** — enriches ping labels (value, rarity, name).
- **Ping Anywhere** / **Pin** — unlocks Location pings (anywhere you aim), capped per player.

A global anti-flood cap limits how many pings can be on screen at once.

## Configuration

All settings live in `BepInEx/config/com.kubai.increasehardmod.cfg` and are editable in-game via
REPOConfig. Sections include: General, Enemies, Quota, Surprises, Modifier Tuning, Player Upgrades,
Truck Upgrades, Ping, Shop, Map Vote, Late Join, Item Unstuck, More Players, Dead Map Access,
Spectator Minimap, and various HUD toggles
(Level Info, Server Info, Shopping List, Photon HUD). A Diagnostics section can disable all patches or
runtime objects for troubleshooting.

## Building

Requires the .NET SDK and a local REPO install. The project references game assemblies from the Steam
install and publicizes `Assembly-CSharp` at build time.

```bash
dotnet build
```

Override the game location if it isn't in the default Steam path:

```bash
dotnet build -p:GameDir="D:\Games\REPO"
```

The build deploys the compiled plugin into your BepInEx plugins folder for the active profile.

## Notes

- `_decompiled/` (game code) is git-ignored and regenerated locally; it is proprietary and not
  distributed.
- Menu-safe by design: Photon/socket probes are gated to `InRoom` so nothing touches networking on the
  main menu.
