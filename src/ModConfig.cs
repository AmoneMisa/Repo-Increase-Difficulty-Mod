using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;

namespace IncreaseHardMod;

/// <summary>
/// All tunable knobs for the mod. BepInEx reads/writes these to
/// BepInEx/config/com.kubai.increasehardmod.cfg, so players can edit values
/// without recompiling. We bind each entry once at startup.
/// </summary>
public static class ModConfig
{
    // When does the difficulty ramp begin?
    // levelsCompleted is how many levels the run has finished so far.
    // With RampStartLevel = 4, the first 4 levels are vanilla and scaling
    // kicks in from the 5th level onward (i.e. once levelsCompleted >= 4).
    public static ConfigEntry<int> RampStartLevel;

    // How many levels of depth make up ONE ramp step. 1 = stats grow every level
    // (the classic behaviour); 2 = stats only step up every 2nd level past the
    // start, etc. Lets you stretch the ramp out over a longer run.
    public static ConfigEntry<int> RampLevelsPerStep;

    // Enemy max HP. +PerLevel each level of depth, clamped to Max.
    public static ConfigEntry<float> EnemyHealthPerLevel;
    public static ConfigEntry<float> EnemyHealthMax;

    // Per-enemy HP cap overrides, keyed by the enemy's internal name.
    // Format: "Name:multiplier, Name:multiplier". Names are matched
    // case-insensitively against EnemyParent.enemyName. Any enemy not listed
    // uses the global EnemyHealthMax cap above.
    public static ConfigEntry<string> EnemyHealthMaxPerEnemy;

    // If true, logs each enemy's real internal name the first time it spawns,
    // so you can copy the exact strings into EnemyHealthMaxPerEnemy.
    public static ConfigEntry<bool> LogEnemyNames;

    // Damage players take FROM ENEMIES (not falls/objects). Same ramp shape.
    public static ConfigEntry<float> EnemyDamagePerLevel;
    public static ConfigEntry<float> EnemyDamageMax;

    // Enemy movement speed (and acceleration). Same ramp shape.
    public static ConfigEntry<float> EnemySpeedPerLevel;
    public static ConfigEntry<float> EnemySpeedMax;

    // Enemy respawn wait time. This RAMP REDUCES the time (enemies come back
    // faster). PerLevel is how much faster per level; Min is the floor so they
    // never respawn instantly. e.g. 0.10 = -10% wait per level, floored at 0.3.
    public static ConfigEntry<float> EnemyRespawnFasterPerLevel;
    public static ConfigEntry<float> EnemyRespawnMinFactor;

    // Extraction "quota" (haul goal) required each level.
    public static ConfigEntry<float> QuotaPerLevel;
    public static ConfigEntry<float> QuotaMax;

    // Hard ceiling: quota can never exceed this fraction of the TOTAL loot value
    // present on the level, so a level is always beatable. 0.9 = at most 90%.
    public static ConfigEntry<float> QuotaMaxLootFraction;

    // --- Surprise / special-level system ---
    // Master switch, chance a level becomes special, and how many modifiers it
    // gets. The per-modifier toggles let players disable specific surprises.
    public static ConfigEntry<bool> SurprisesEnabled;
    public static ConfigEntry<int> SpecialLevelChancePercent;
    public static ConfigEntry<int> ModifiersPerLevelMin;
    public static ConfigEntry<int> ModifiersPerLevelMax;
    public static ConfigEntry<int> SurprisesStartLevel;

    // One enable toggle per modifier. Disabled ones are never rolled.
    public static readonly System.Collections.Generic.Dictionary<Modifiers.ModifierType, ConfigEntry<bool>>
        ModifierEnabled = new();

    // Per-modifier effect strengths.
    public static ConfigEntry<float> FragileLootFactor;
    public static ConfigEntry<float> ProtectedLootFactor;
    public static ConfigEntry<float> HeavyLootFactor;
    public static ConfigEntry<float> LightLootFactor;
    public static ConfigEntry<float> LightMonsterMaxMass;
    public static ConfigEntry<float> HeavyMonsterMinMass;
    public static ConfigEntry<float> BiggerSoulDropsFactor;
    public static ConfigEntry<float> MonsterPitDamageFactor;
    public static ConfigEntry<int> ExtraGachaCubesPerSpawn;
    public static ConfigEntry<float> DangerousLootDamageFactor;
    public static ConfigEntry<float> SoulsExplodeHarderFactor;
    public static ConfigEntry<int> SpreadUpgradesCount;
    public static ConfigEntry<string> SpreadUpgradesAllowed;
    public static ConfigEntry<bool> LogSpreadUpgradeNames;
    public static ConfigEntry<bool> AutoReviveAtCar;
    public static ConfigEntry<float> LevelTimerSeconds;
    public static ConfigEntry<float> LevelTimerKillBonus;
    public static ConfigEntry<float> LevelTimerDeathPenalty;
    public static ConfigEntry<float> LevelTimerQuotaBonus;

    // --- Late Join ---
    // When enabled, the host keeps the Photon room (and Steam lobby) reachable
    // during a run/shop/truck instead of sealing it at run start, and re-syncs
    // mod state (modifiers, timer, truck upgrades) whenever a new player connects.
    public static ConfigEntry<bool> LateJoinEnabled;

    // --- Ping ---
    // Press PingKey to mark whatever you're looking at (valuable/enemy/item/location).
    // The marker is broadcast to all players and shown as a world-anchored label.
    public static ConfigEntry<bool> PingEnabled;
    public static ConfigEntry<UnityEngine.KeyCode> PingKey;
    public static ConfigEntry<float> PingMaxDistance;
    public static ConfigEntry<float> PingDurationSeconds;
    // Max live Location pings per player (gated behind the "Ping Anywhere" upgrade).
    public static ConfigEntry<int> MaxLocationPings;
    // Global cap on how many ping markers can be on screen at once (anti-flood).
    public static ConfigEntry<int> MaxActivePings;
    // Shop prices for the two ping-gate upgrades.
    public static ConfigEntry<int> PingAnywhereBasePrice;
    public static ConfigEntry<int> PingInfoBasePrice;
    // "Pin": a one-time permanent unlock (like Ping Anywhere) letting you place Location pins.
    public static ConfigEntry<int> PinBasePrice;

    // --- Item Unstuck (valuable safety net) ---
    // Host-authoritative safeguards for valuables: relocate ones trapped in geometry
    // and keep them invulnerable until first grabbed.
    public static ConfigEntry<bool> ItemUnstuckEnabled;
    public static ConfigEntry<bool> ValuableRelocateStuck;
    public static ConfigEntry<float> ValuableStuckSeconds;
    public static ConfigEntry<bool> ValuableInvulnerableUntilGrab;

    // --- Dead Map Access ---
    // Let a dead/spectating player still open their map tool (the map view is re-anchored to
    // the spectate camera so it stays visible). Client-side; each player controls their own.
    public static ConfigEntry<bool> DeadMapAccessEnabled;

    // --- Spectator Minimap ---
    // While dead/spectating on a level, draw the game's own map render in a screen corner so you
    // can keep watching the level layout without holding the map tool open. Client-side.
    public static ConfigEntry<bool> SpectatorMinimapEnabled;
    public static ConfigEntry<UnityEngine.KeyCode> SpectatorMinimapKey;
    public static ConfigEntry<float> SpectatorMinimapScale;

    // --- Map Vote ---
    // While in the truck lobby, players vote on which map to run next (a small set of random
    // candidates). Whatever is leading when the truck departs is used instead of the game's
    // random pick. HOST-SIDE application (the host overrides the pick and syncs it normally).
    public static ConfigEntry<bool> MapVoteEnabled;
    public static ConfigEntry<int> MapVoteChoices;
    public static ConfigEntry<float> MapVoteSeconds;

    // --- Photon reachability HUD ---
    // Small main-menu overlay pinging Photon region name servers so you can see which
    // regions are reachable / their latency before hosting or joining.
    public static ConfigEntry<bool> PhotonHudEnabled;
    public static ConfigEntry<string> PhotonHudRegions;

    // --- Auto Backup Saves ---
    // REPO deletes the run save when you wipe / fail a level (DataDirector.SaveDeleteCheck).
    // When enabled, we copy the save folder to a timestamped backup first, and can
    // optionally skip the deletion entirely so the save stays directly loadable.
    public static ConfigEntry<bool> AutoBackupEnabled;
    public static ConfigEntry<bool> AutoBackupKeepSave;
    public static ConfigEntry<int> AutoBackupMaxPerSave;

    // --- Truck upgrades ---
    // Team-wide truck upgrades sold in the normal shop like any other upgrade (once anyone owns a
    // level, the whole squad gets the effect). Effects apply themselves; there is no separate menu.
    public static ConfigEntry<bool> TruckUpgradesEnabled;
    // Heal power: scales all incoming healing (truck final-heal, medkits, drones).
    public static ConfigEntry<float> TruckHealPercentPerLevel;
    public static ConfigEntry<int> TruckHealMaxLevel;
    public static ConfigEntry<int> TruckHealBaseCost;
    // Charge efficiency: slows the truck charging station's energy-pool drain.
    public static ConfigEntry<float> TruckChargePercentPerLevel;
    public static ConfigEntry<int> TruckChargeMaxLevel;
    public static ConfigEntry<int> TruckChargeBaseCost;
    // Post-quota selling: once bought, leftover valuables are auto-cashed AFTER
    // every extraction quota is closed, for TruckSellFraction of their value.
    public static ConfigEntry<int> TruckSellCost;
    public static ConfigEntry<float> TruckSellFraction;

    // --- Player Upgrades (custom per-player upgrades sold in the DEFAULT shop) ---
    // Registered via REPOLib as runtime clones of vanilla upgrade items, so they roll
    // in the normal shop upgrade booth and are tracked/saved/synced per player.
    public static ConfigEntry<bool> PlayerUpgradesEnabled;
    // Stamina: each level reduces sprint energy drain by this fraction.
    public static ConfigEntry<float> StaminaPercentPerLevel;
    public static ConfigEntry<int> StaminaMaxLevel;
    public static ConfigEntry<int> StaminaBasePrice;
    // HP Regen: very slow passive heal, only up to a fraction of max HP, then stops.
    public static ConfigEntry<float> RegenPercentPerLevel;
    public static ConfigEntry<float> RegenMaxHpFraction;
    public static ConfigEntry<int> RegenMaxLevel;
    public static ConfigEntry<int> RegenBasePrice;
    // Death Protection: consumable charge; survive a lethal hit at 1 HP, re-bought each use.
    public static ConfigEntry<int> DeathProtectMaxCharges;
    public static ConfigEntry<int> DeathProtectBasePrice;
    // Battery Life: separate upgrades per gun type (Bullet/Laser/Tranq) and for drones.
    // Each level adds effective battery life (refunds a fraction of drain), so the item
    // lasts longer. "+100%" = twice the usage.
    public static ConfigEntry<float> GunBatteryPercentPerLevel;
    public static ConfigEntry<int> GunBatteryMaxLevel;
    public static ConfigEntry<int> GunBatteryBasePrice;
    public static ConfigEntry<float> DroneBatteryPercentPerLevel;
    public static ConfigEntry<int> DroneBatteryMaxLevel;
    public static ConfigEntry<int> DroneBatteryBasePrice;
    // Autoscan: passively discovers nearby valuables; each level widens the scan radius.
    public static ConfigEntry<float> AutoscanRangePerLevel;
    public static ConfigEntry<int> AutoscanMaxLevel;
    public static ConfigEntry<float> AutoscanInterval;
    public static ConfigEntry<int> AutoscanBasePrice;
    // Map Suite: single 10-level upgrade, each level unlocking a map feature (counts,
    // quota path, zoom, cosmetic-box tracker, monster dots). Level->feature ladder is fixed.
    public static ConfigEntry<int> MapBasePrice;
    public static ConfigEntry<float> MapZoomFovLevel7;
    public static ConfigEntry<float> MapZoomFovLevel8;
    // Live zoom hotkeys while the map is open at L7+ (adjust the FOV on top of the level default).
    public static ConfigEntry<UnityEngine.KeyCode> MapZoomInKey;
    public static ConfigEntry<UnityEngine.KeyCode> MapZoomOutKey;
    public static ConfigEntry<float> MapZoomStep;
    // When true, the vanilla "Map Player Count" upgrade is removed from the shop (Map Suite replaces it).
    public static ConfigEntry<bool> MapSuiteReplacesVanilla;

    // --- Level Info panel ---
    // Right-side overlay logging each level change (number + name + rolled surprises),
    // with a hotkey to hide/show it.
    public static ConfigEntry<bool> LevelInfoEnabled;
    public static ConfigEntry<UnityEngine.KeyCode> LevelInfoToggleKey;

    // --- Server Info panel ---
    // Passive lobby/menu overlay showing current Photon region, ping, room code and
    // player count. Reads PhotonNetwork state only — never opens sockets or callbacks.
    public static ConfigEntry<bool> ServerInfoEnabled;

    // --- Shopping List panel ---
    // In-shop overlay listing everything currently in the cart (ShopManager.shoppingList),
    // aggregated by name with quantity + summed price, colour-coded by item type, plus a
    // total-vs-currency footer. Reads ShopManager state only.
    public static ConfigEntry<bool> ShoppingListEnabled;

    // --- More Players ---
    // Raise the room's player cap above REPO's default of 6 (host-side; the host's
    // value defines the room). The game already supports up to 20 internally.
    public static ConfigEntry<bool> MorePlayersEnabled;
    public static ConfigEntry<int> MorePlayersMax;

    // --- Shop ---
    // Scale shop item prices up with player count (big lobbies earn more, so keep it
    // hard) and optionally let the same item show up multiple times in one shop.
    public static ConfigEntry<bool> ShopPriceScaleEnabled;
    public static ConfigEntry<int> ShopPriceBaselinePlayers;
    public static ConfigEntry<float> ShopPricePerExtraPlayer;
    public static ConfigEntry<int> ShopDuplicatesExtra;

    // --- Diagnostics ---
    // Temporary kill-switches to isolate whether the mod interferes with the game's
    // own Photon connection (regions spinner / stuck loading). Both default off.
    public static ConfigEntry<bool> DiagnosticDisablePatches;
    public static ConfigEntry<bool> DiagnosticDisableRuntimeObjects;

    // REPOConfig (nickklmao) reads this exact string tag on a ConfigEntry to exclude it
    // from the in-game menu. Used for free-form strings it can't render as a slider/list.
    private const string HideFromRepoConfig = "HideFromREPOConfig";

    private static ConfigEntry<int> BindInt(ConfigFile cfg, string section, string key,
        int def, int min, int max, string desc) =>
        cfg.Bind(section, key, def, new ConfigDescription(desc, new AcceptableValueRange<int>(min, max)));

    private static ConfigEntry<float> BindFloat(ConfigFile cfg, string section, string key,
        float def, float min, float max, string desc) =>
        cfg.Bind(section, key, def, new ConfigDescription(desc, new AcceptableValueRange<float>(min, max)));

    // Short, player-facing explanation of what each surprise modifier actually does.
    private static string ModifierDescription(Modifiers.ModifierType m) => m switch
    {
        Modifiers.ModifierType.FragileLoot        => "Loot breaks and loses value more easily.",
        Modifiers.ModifierType.HeavyLoot          => "Loot weighs more, making it harder to haul.",
        Modifiers.ModifierType.LightLoot          => "Loot weighs less, making it easier to haul.",
        Modifiers.ModifierType.ProtectedLoot      => "Loot takes less damage before breaking.",
        Modifiers.ModifierType.DangerousLoot      => "Hazardous valuables deal more damage to players.",
        Modifiers.ModifierType.ExtraSmallCart     => "An extra small cart spawns on the level.",
        Modifiers.ModifierType.ExtraBigCart       => "An extra big cart spawns on the level.",
        Modifiers.ModifierType.ExtraSouls         => "Monsters drop more souls.",
        Modifiers.ModifierType.SpreadUpgrades     => "Upgrade pickups are scattered around; usable only back in the truck.",
        Modifiers.ModifierType.NoMap              => "No map until you find a map upgrade pickup.",
        Modifiers.ModifierType.ReducedPlayerHp    => "Everyone starts the level wounded — current HP is cut to a rolled percentage (max HP is untouched, so you can heal back up).",
        Modifiers.ModifierType.AutoRevive         => "Each player gets one automatic revive this level.",
        Modifiers.ModifierType.MoneyLossOnDeath   => "Dying loses a percentage of your collected money.",
        Modifiers.ModifierType.LevelTimer         => "Timed level: kills add time, deaths remove it, quota adds it; at 0 the layout rerolls.",
        Modifiers.ModifierType.ChangeVoices       => "Player voices get swapped or randomized.",
        Modifiers.ModifierType.MoreGachaCubes     => "More gacha/cube valuables spawn.",
        Modifiers.ModifierType.SoulsExplodeHarder => "Exploding souls deal more damage to players.",
        Modifiers.ModifierType.BiggerSoulDrops    => "Each soul drop is worth more money (value per drop, not count).",
        Modifiers.ModifierType.MonstersPitTanky   => "Monsters take less damage from death pits.",
        Modifiers.ModifierType.LightMonsters      => "All monsters weigh less, so they're easy to shove around.",
        Modifiers.ModifierType.HeavyMonsters      => "All monsters weigh more, so they're hard to shove around.",
        _                                         => $"The '{m}' surprise modifier.",
    };

    public static void Init(ConfigFile cfg)
    {
        DiagnosticDisablePatches = cfg.Bind(
            "Diagnostics", "DisablePatches", false,
            "DIAGNOSTIC (default off). If true, skip ALL Harmony gameplay patches. Used only to isolate whether the mod's patches interfere with Photon connecting. Leave off for normal play.");
        DiagnosticDisableRuntimeObjects = cfg.Bind(
            "Diagnostics", "DisableRuntimeObjects", false,
            "DIAGNOSTIC (default off). If true, skip creating the mod's runtime menu/HUD objects (networking, timer, ping, Photon HUD). Used only to isolate whether they interfere with Photon connecting. Leave off for normal play.");

        RampStartLevel = BindInt(cfg,
            "General", "RampStartLevel", 4, 0, 30,
            "Levels completed before difficulty scaling starts. 4 = first 4 levels are vanilla, scaling begins on the 5th.");

        RampLevelsPerStep = BindInt(cfg,
            "General", "RampLevelsPerStep", 1, 1, 10,
            "How many levels of depth make up ONE difficulty step. 1 = stats grow every level; 2 = stats step up only every 2nd level past the start (stretches the ramp).");

        EnemyHealthPerLevel = BindFloat(cfg,
            "Enemies", "HealthPerLevel", 0.15f, 0f, 2f,
            "Extra enemy max HP per level past the ramp start. 0.15 = +15% per level (cumulative, additive).");
        EnemyHealthMax = BindFloat(cfg,
            "Enemies", "HealthMaxMultiplier", 3.0f, 1f, 20f,
            "Global upper cap on the enemy HP multiplier. 3.0 = at most 3x vanilla HP.");
        // Free-form string (name:mult list) — REPOConfig can't render this sensibly, so hide it.
        EnemyHealthMaxPerEnemy = cfg.Bind(
            "Enemies", "HealthMaxPerEnemy", "Elsa:1, Loom:1, Cleanup Crew:1.5, Tick:1.5",
            new ConfigDescription(
                "Per-enemy HP cap overrides. Format: 'Name:multiplier, Name:multiplier'. Names match EnemyParent.enemyName (case-insensitive). A cap of 1 EXCLUDES that enemy from HP scaling (Elsa/Loom are excluded by default). Unlisted enemies use HealthMaxMultiplier. Enable LogEnemyNames to see exact names.",
                null, HideFromRepoConfig));
        LogEnemyNames = cfg.Bind(
            "Enemies", "LogEnemyNames", true,
            "Log each enemy's internal name the first time it spawns, so you can tune HealthMaxPerEnemy.");

        EnemyDamagePerLevel = BindFloat(cfg,
            "Enemies", "DamagePerLevel", 0.10f, 0f, 2f,
            "Extra enemy attack damage per level past the ramp start. 0.10 = +10% per level.");
        EnemyDamageMax = BindFloat(cfg,
            "Enemies", "DamageMaxMultiplier", 3.0f, 1f, 20f,
            "Upper cap on the enemy damage multiplier.");

        EnemySpeedPerLevel = BindFloat(cfg,
            "Enemies", "SpeedPerLevel", 0.06f, 0f, 1f,
            "Extra enemy movement speed per level past the ramp start. 0.06 = +6% per level.");
        EnemySpeedMax = BindFloat(cfg,
            "Enemies", "SpeedMaxMultiplier", 1.75f, 1f, 4f,
            "Upper cap on the enemy speed multiplier. Keep modest; too high makes enemies uncatchable/janky.");

        EnemyRespawnFasterPerLevel = BindFloat(cfg,
            "Enemies", "RespawnFasterPerLevel", 0.08f, 0f, 1f,
            "How much shorter the enemy respawn wait gets per level. 0.08 = -8% wait per level.");
        EnemyRespawnMinFactor = BindFloat(cfg,
            "Enemies", "RespawnMinFactor", 0.3f, 0.05f, 1f,
            "Floor for the respawn-wait multiplier. 0.3 = respawn wait never drops below 30% of vanilla.");

        QuotaPerLevel = BindFloat(cfg,
            "Quota", "QuotaPerLevel", 0.10f, 0f, 2f,
            "Extra extraction quota per level past the ramp start. 0.10 = +10% per level.");
        QuotaMax = BindFloat(cfg,
            "Quota", "QuotaMaxMultiplier", 5.0f, 1f, 20f,
            "Upper cap on the quota multiplier.");
        QuotaMaxLootFraction = BindFloat(cfg,
            "Quota", "QuotaMaxLootFraction", 0.9f, 0.1f, 1f,
            "Quota can never exceed this fraction of the total loot value on the level (keeps levels beatable). 0.9 = at most 90%.");

        // --- Surprise modifier effect strengths ---
        FragileLootFactor = BindFloat(cfg,
            "Modifier Tuning", "FragileLootFactor", 2.0f, 0.1f, 10f,
            "FragileLoot: multiplies loot fragility. >1 = breaks more easily. 2.0 = twice as fragile.");
        ProtectedLootFactor = BindFloat(cfg,
            "Modifier Tuning", "ProtectedLootFactor", 0.4f, 0.1f, 10f,
            "ProtectedLoot: multiplies loot fragility. <1 = harder to break. 0.4 = 60% less fragile.");
        HeavyLootFactor = BindFloat(cfg,
            "Modifier Tuning", "HeavyLootFactor", 1.75f, 0.1f, 10f,
            "HeavyLoot: multiplies loot mass. >1 = heavier.");
        LightLootFactor = BindFloat(cfg,
            "Modifier Tuning", "LightLootFactor", 0.5f, 0.1f, 10f,
            "LightLoot: multiplies loot mass. <1 = lighter.");
        LightMonsterMaxMass = BindFloat(cfg,
            "Modifier Tuning", "LightMonsterMaxMass", 3.0f, 0.1f, 50f,
            "LightMonsters: cap every monster's mass at this value (lighter = easier to shove around).");
        HeavyMonsterMinMass = BindFloat(cfg,
            "Modifier Tuning", "HeavyMonsterMinMass", 7.0f, 0.1f, 50f,
            "HeavyMonsters: raise every monster's mass to at least this value (heavier = harder to move).");
        BiggerSoulDropsFactor = BindFloat(cfg,
            "Modifier Tuning", "BiggerSoulDropsFactor", 2.0f, 0.1f, 10f,
            "BiggerSoulDrops: multiplies the money value of each enemy soul drop. 2.0 = each drop worth twice as much. Does not change drop count.");
        MonsterPitDamageFactor = BindFloat(cfg,
            "Modifier Tuning", "MonsterPitDamageFactor", 0.5f, 0f, 5f,
            "MonstersPitTanky: multiplies death-pit chip damage dealt to monsters (moon 2+). 0.5 = monsters take half pit damage. Instant-kill pits on early moons are unaffected.");
        ExtraGachaCubesPerSpawn = BindInt(cfg,
            "Modifier Tuning", "ExtraGachaCubesPerSpawn", 1, 0, 10,
            "MoreGachaCubes: how many additional cosmetic 'gacha' cubes to spawn per cube the game would normally place. Capped by the level's available spawn spots. 1 = roughly double.");
        DangerousLootDamageFactor = BindFloat(cfg,
            "Modifier Tuning", "DangerousLootDamageFactor", 2.0f, 0.1f, 10f,
            "DangerousLoot: multiplies the damage hazardous valuables (cursed dolls, radioactive cups, barrels, saws, etc.) deal to players. 2.0 = double damage.");
        SoulsExplodeHarderFactor = BindFloat(cfg,
            "Modifier Tuning", "SoulsExplodeHarderFactor", 2.0f, 0.1f, 10f,
            "SoulsExplodeHarder: multiplies the player damage of enemy-soul explosions (moon 4+). 2.0 = double explosion damage.");
        SpreadUpgradesCount = BindInt(cfg,
            "Modifier Tuning", "SpreadUpgradesCount", 3, 0, 10,
            "SpreadUpgrades: how many upgrade item pickups to scatter across the special level. They can't be used on that level, only once back in the truck.");
        SpreadUpgradesAllowed = cfg.Bind(
            "Modifier Tuning", "SpreadUpgradesAllowed", "",
            new ConfigDescription(
                "SpreadUpgrades: comma-separated list of upgrade item names allowed to be scattered (case-insensitive, substring match against the item's internal name). Empty = any upgrade. Enable LogSpreadUpgradeNames to print the exact names to the log.",
                null, HideFromRepoConfig));
        LogSpreadUpgradeNames = cfg.Bind(
            "Modifier Tuning", "LogSpreadUpgradeNames", false,
            "Log the internal names of all upgrade items the first time a SpreadUpgrades level generates, so you can fill in SpreadUpgradesAllowed.");
        AutoReviveAtCar = cfg.Bind(
            "Modifier Tuning", "AutoReviveAtCar", true,
            "AutoRevive: bring the auto-revived player back at the level's spawn point (the truck/car drop-off) and fully heal them, instead of reviving them on the spot where they died. Still only one auto-revive per player per level.");
        LevelTimerSeconds = BindFloat(cfg,
            "Modifier Tuning", "LevelTimerSeconds", 300f, 30f, 1200f,
            "LevelTimer: starting countdown in seconds. When it hits 0 the level restarts and rerolls to a fresh layout (progress is not lost, the level is just regenerated).");
        LevelTimerKillBonus = BindFloat(cfg,
            "Modifier Tuning", "LevelTimerKillBonus", 10f, 0f, 120f,
            "LevelTimer: seconds added to the clock for each enemy killed.");
        LevelTimerDeathPenalty = BindFloat(cfg,
            "Modifier Tuning", "LevelTimerDeathPenalty", 30f, 0f, 300f,
            "LevelTimer: seconds removed from the clock each time a player dies.");
        LevelTimerQuotaBonus = BindFloat(cfg,
            "Modifier Tuning", "LevelTimerQuotaBonus", 60f, 0f, 300f,
            "LevelTimer: seconds added when an extraction point's quota is completed.");

        // --- Late Join ---
        LateJoinEnabled = cfg.Bind(
            "Late Join", "Enabled", true,
            "Host keeps the room/lobby joinable during a run/shop/truck (REPO normally seals it at run start) and re-syncs mod state to anyone who connects mid-game.");

        // --- Ping ---
        PingEnabled = cfg.Bind(
            "Ping", "Enabled", true,
            "Enable the ping system: press the ping key to mark what you're looking at for the whole team.");
        PingKey = cfg.Bind(
            "Ping", "PingKey", UnityEngine.KeyCode.Mouse2,
            "Key/button to place a ping on whatever you're aiming at (default: middle mouse).");
        PingMaxDistance = BindFloat(cfg,
            "Ping", "MaxDistance", 60f, 5f, 200f,
            "Maximum distance the ping raycast reaches. Beyond this a ping is placed in mid-air along your aim.");
        PingDurationSeconds = BindFloat(cfg,
            "Ping", "DurationSeconds", 4f, 1f, 30f,
            "How long a ping marker stays visible (seconds).");
        MaxLocationPings = BindInt(cfg,
            "Ping", "MaxLocationPings", 3, 1, 20,
            "Maximum live Location pings a single player can have at once (needs the 'Ping Anywhere' upgrade). Placing more drops your oldest.");
        MaxActivePings = BindInt(cfg,
            "Ping", "MaxActivePings", 3, 1, 20,
            "Global anti-flood cap: the most ping markers shown on screen at once (across all players). Placing more drops the oldest.");
        PingAnywhereBasePrice = BindInt(cfg,
            "Ping", "PingAnywhereBasePrice", 6000, 0, 1000000,
            "Ping Anywhere upgrade: approximate shop price (unlocks Location pings on any surface / through walls).");
        PingInfoBasePrice = BindInt(cfg,
            "Ping", "PingInfoBasePrice", 6000, 0, 1000000,
            "Ping Info upgrade: approximate shop price (your pings reveal value / rarity / exact names).");
        PinBasePrice = BindInt(cfg,
            "Ping", "PinBasePrice", 30000, 0, 1000000,
            "Pin: approximate shop price for the one-time permanent unlock (buy once, place Location pins forever — like 'Ping Anywhere').");

        // --- Item Unstuck ---
        ItemUnstuckEnabled = cfg.Bind(
            "Item Unstuck", "Enabled", true,
            "Master switch for the valuable safety-net features (relocate stuck loot, invulnerable-until-grab, undamaged-value HUD). The relocate/invuln parts are HOST-SIDE (computed by the host).");
        ValuableRelocateStuck = cfg.Bind(
            "Item Unstuck", "RelocateStuck", true,
            "Detect valuables that have settled embedded inside world geometry and teleport them back to their last safe resting spot (or lift them out). Anti-softlock; host-side.");
        ValuableStuckSeconds = BindFloat(cfg,
            "Item Unstuck", "StuckSeconds", 6f, 2f, 60f,
            "How many seconds a valuable must sit embedded in geometry (and at rest, ungrabbed) before it's relocated.");
        ValuableInvulnerableUntilGrab = cfg.Bind(
            "Item Unstuck", "InvulnerableUntilGrab", true,
            "Valuables take no damage / lose no value until a player grabs them for the first time (uses the game's own indestructible flag). Host-side.");

        // --- Dead Map Access ---
        DeadMapAccessEnabled = cfg.Bind(
            "Dead Map Access", "Enabled", true,
            "Allow dead/spectating players to open their map tool. The map view is re-anchored to the spectate camera so it stays visible. Client-side — each player controls their own map.");

        // --- Spectator Minimap ---
        SpectatorMinimapEnabled = cfg.Bind(
            "Spectator Minimap", "Enabled", true,
            "While dead/spectating on a level, HOLD the minimap key to show the game's own map render in the centre of the screen so you can track the level without the full map tool. Hidden automatically if you open the full map tool. Client-side.");
        SpectatorMinimapKey = cfg.Bind(
            "Spectator Minimap", "HoldKey", UnityEngine.KeyCode.Tab,
            "Hold this key while dead to show the spectator minimap (default: Tab).");
        SpectatorMinimapScale = BindFloat(cfg,
            "Spectator Minimap", "Scale", 0.5f, 0.1f, 0.9f,
            "Minimap size as a fraction of screen height. 0.5 = half the screen tall.");

        // --- Map Vote ---
        MapVoteEnabled = cfg.Bind(
            "Map Vote", "Enabled", true,
            "While in the truck lobby, let players vote on the next map from a few random candidates. Whatever is leading when the truck departs is used instead of the game's random pick. Vote with the number keys (or the on-screen buttons). If nobody votes, the game's normal random selection is used, so this is harmless when ignored. HOST-SIDE: the host applies the winning map and syncs it to everyone normally.");
        MapVoteChoices = BindInt(cfg,
            "Map Vote", "Choices", 3, 2, 6,
            "How many random candidate maps to offer for the vote each lobby.");
        MapVoteSeconds = BindFloat(cfg,
            "Map Vote", "Seconds", 60f, 10f, 300f,
            "How long the vote stays open (host-authoritative). It closes early the moment everyone in the lobby has voted. When it closes, the leading map is locked in and used when the truck departs. If nobody votes, the game's normal random selection is used.");

        // --- Photon reachability HUD ---
        PhotonHudEnabled = cfg.Bind(
            "Photon HUD", "Enabled", false,
            "OPT-IN DIAGNOSTIC (default off). Show a small overlay on the main menu with reachability/latency to Photon region name servers. " +
            "Leave this OFF for normal play: it TCP-probes the same Photon name servers the game uses, and that connection churn can stall REPO's own 'pick best region' step (regions spin forever / stuck loading online). Only enable it briefly when you're actively diagnosing a connection problem.");
        PhotonHudRegions = cfg.Bind(
            "Photon HUD", "Regions", "ns=ns.photonengine.io, exitgames=ns.exitgames.com",
            new ConfigDescription(
                "Comma-separated 'label=host' (or 'label=host:port') pairs to TCP-probe for the HUD. " +
                "Default probes the Photon name servers (port 443) — the endpoint the game contacts when connecting with an empty/best region, which is where 'stuck loading online' usually hangs.",
                null, HideFromRepoConfig));

        // --- Level Info panel ---
        LevelInfoEnabled = cfg.Bind(
            "Level Info", "Enabled", true,
            "Show a panel on the right side of the screen that logs each level change (run number + name, plus any rolled surprise modifiers).");
        LevelInfoToggleKey = cfg.Bind(
            "Level Info", "ToggleKey", UnityEngine.KeyCode.F8,
            "Key to hide/show the level info announcement.");

        // --- Server Info panel ---
        ServerInfoEnabled = cfg.Bind(
            "Server Info", "Enabled", true,
            "Show a passive overlay in the lobby/menu with the current Photon region, ping, room code and player count. Reads connection state only — never opens sockets, so it's safe on the main menu.");

        // --- Shopping List panel ---
        ShoppingListEnabled = cfg.Bind(
            "Shopping List", "Enabled", false,
            "While in the shop, show a panel listing everything in the cart (yours and your friends'): quantity, name and summed price per item, colour-coded by item type, with a running total vs. your team's currency.");

        // --- More Players ---
        MorePlayersEnabled = cfg.Bind(
            "More Players", "Enabled", false,
            "Raise the room's max player count above REPO's default of 6. HOST-SIDE: the host's value defines the room cap; joiners don't need this on. Everyone in the lobby should still run the mod to avoid desync.");
        MorePlayersMax = BindInt(cfg,
            "More Players", "MaxPlayers", 6, 2, 20,
            "Maximum players allowed in the room when More Players is enabled. REPO supports up to 20 internally. Large lobbies are unofficial and may be unstable.");

        // --- Shop ---
        ShopPriceScaleEnabled = cfg.Bind(
            "Shop", "PriceScaleEnabled", false,
            "Scale shop item prices up with the number of players, so bigger lobbies (which earn more) still feel expensive. HOST-SIDE: prices are computed by the host and synced.");
        ShopPriceBaselinePlayers = BindInt(cfg,
            "Shop", "PriceBaselinePlayers", 4, 1, 20,
            "Player count at which prices are unchanged. Each player above this raises prices by PricePerExtraPlayer; at or below it, prices are vanilla.");
        ShopPricePerExtraPlayer = BindFloat(cfg,
            "Shop", "PricePerExtraPlayer", 0.15f, 0f, 2f,
            "Extra shop price per player above the baseline. 0.15 = +15% per extra player (e.g. 8 players with baseline 4 = +60%).");
        ShopDuplicatesExtra = BindInt(cfg,
            "Shop", "DuplicatesExtra", 0, 0, 10,
            "Allow the same item (e.g. a gun) to appear more than once in a single shop by adding this many extra copies of each item to the shop pool. 0 = vanilla (usually one of each).");

        // --- Auto Backup Saves ---
        AutoBackupEnabled = cfg.Bind(
            "Auto Backup Saves", "Enabled", true,
            "Back up the run save before REPO deletes it on a level fail/wipe. Backups go to a 'saves_IncreaseHardMod_backups' folder next to your saves.");
        AutoBackupKeepSave = cfg.Bind(
            "Auto Backup Saves", "KeepSaveLoadable", true,
            "Also skip the deletion entirely so the failed save stays in your normal save list and can be reloaded from its last checkpoint (the shop before the failed level).");
        AutoBackupMaxPerSave = BindInt(cfg,
            "Auto Backup Saves", "MaxBackupsPerSave", 5, 0, 100,
            "How many timestamped backups to keep per save file. Oldest are pruned. 0 = keep all.");

        // --- Truck upgrades ---
        TruckUpgradesEnabled = cfg.Bind(
            "Truck Upgrades", "Enabled", true,
            "Enable the team-wide truck upgrades (Heal Power, Charge Efficiency, Loot Cash-In). Bought in the normal shop; effects apply for the whole team.");
        TruckHealPercentPerLevel = BindFloat(cfg,
            "Truck Upgrades", "HealPercentPerLevel", 0.25f, 0f, 2f,
            "Heal Power: extra healing effectiveness per level. 0.25 = +25% healing per level (truck final-heal, medkits, drones).");
        TruckHealMaxLevel = BindInt(cfg,
            "Truck Upgrades", "HealMaxLevel", 3, 0, 20,
            "Heal Power: maximum purchasable level.");
        TruckHealBaseCost = BindInt(cfg,
            "Truck Upgrades", "HealBaseCost", 10000, 0, 1000000,
            "Heal Power: cost of the first level (grows by CostGrowth each level).");
        TruckChargePercentPerLevel = BindFloat(cfg,
            "Truck Upgrades", "ChargePercentPerLevel", 0.20f, 0f, 1f,
            "Charge Efficiency: reduction of the truck charging station's energy-pool drain per level. 0.20 = -20% drain per level.");
        TruckChargeMaxLevel = BindInt(cfg,
            "Truck Upgrades", "ChargeMaxLevel", 3, 0, 20,
            "Charge Efficiency: maximum purchasable level.");
        TruckChargeBaseCost = BindInt(cfg,
            "Truck Upgrades", "ChargeBaseCost", 10000, 0, 1000000,
            "Charge Efficiency: cost of the first level (grows by CostGrowth each level).");
        TruckSellCost = BindInt(cfg,
            "Truck Upgrades", "SellUnlockCost", 50000, 0, 1000000,
            "Loot Cash-In: one-time cost to unlock selling leftover valuables after all quotas are closed.");
        TruckSellFraction = BindFloat(cfg,
            "Truck Upgrades", "SellFraction", 0.75f, 0f, 1f,
            "Loot Cash-In: fraction of a leftover valuable's value returned as currency when sold. 0.75 = 75%.");

        // --- Player Upgrades (custom, sold in the default shop via REPOLib) ---
        PlayerUpgradesEnabled = cfg.Bind(
            "Player Upgrades", "Enabled", true,
            "Master switch for the mod's custom per-player upgrades that appear in the normal shop. Requires REPOLib. Takes effect on restart.");
        StaminaPercentPerLevel = BindFloat(cfg,
            "Player Upgrades", "StaminaPercentPerLevel", 0.10f, 0.01f, 0.5f,
            "Slower Stamina Use: sprint energy drain reduction per level. 0.10 = -10% drain per level (per player).");
        StaminaMaxLevel = BindInt(cfg,
            "Player Upgrades", "StaminaMaxLevel", 5, 1, 20,
            "Slower Stamina Use: maximum purchasable level.");
        StaminaBasePrice = BindInt(cfg,
            "Player Upgrades", "StaminaBasePrice", 4000, 0, 1000000,
            "Slower Stamina Use: approximate shop price (at one player, before the game's own scaling).");
        RegenPercentPerLevel = BindFloat(cfg,
            "Player Upgrades", "RegenPercentPerLevel", 0.002f, 0.0005f, 0.05f,
            "HP Regen: fraction of MAX HP healed per SECOND, per level. 0.002 = 0.2%/s per level (very slow — ~1 HP every 5s at level 1 for a 100 HP player).");
        RegenMaxHpFraction = BindFloat(cfg,
            "Player Upgrades", "RegenMaxHpFraction", 0.30f, 0.05f, 1f,
            "HP Regen: stops once current HP reaches this fraction of max HP. 0.30 = regen only fills up to 30% of max HP.");
        RegenMaxLevel = BindInt(cfg,
            "Player Upgrades", "RegenMaxLevel", 3, 1, 20,
            "HP Regen: maximum purchasable level.");
        RegenBasePrice = BindInt(cfg,
            "Player Upgrades", "RegenBasePrice", 20000, 0, 1000000,
            "HP Regen: approximate shop price of the first level (very expensive).");
        DeathProtectMaxCharges = BindInt(cfg,
            "Player Upgrades", "DeathProtectMaxCharges", 1, 1, 9,
            "Death Protection: max charges you can stockpile at once. Each charge saves you from one lethal hit (survive at 1 HP) and is consumed on use; re-buy to restore.");
        DeathProtectBasePrice = BindInt(cfg,
            "Player Upgrades", "DeathProtectBasePrice", 50000, 0, 1000000,
            "Death Protection: approximate shop price per charge (very-very expensive).");
        GunBatteryPercentPerLevel = BindFloat(cfg,
            "Player Upgrades", "GunBatteryPercentPerLevel", 0.05f, 0.01f, 0.5f,
            "Gun Battery Life: extra effective battery life per level (shared by the Bullet/Laser/Tranq gun upgrades). 0.05 = +5%/level; capped at +100% total.");
        GunBatteryMaxLevel = BindInt(cfg,
            "Player Upgrades", "GunBatteryMaxLevel", 20, 1, 100,
            "Gun Battery Life: maximum purchasable level (20 x 5% = +100%).");
        GunBatteryBasePrice = BindInt(cfg,
            "Player Upgrades", "GunBatteryBasePrice", 6000, 0, 1000000,
            "Gun Battery Life: approximate shop price of the first level (each gun type is a separate upgrade).");
        DroneBatteryPercentPerLevel = BindFloat(cfg,
            "Player Upgrades", "DroneBatteryPercentPerLevel", 0.05f, 0.01f, 0.5f,
            "Drone Battery Life: extra effective battery life per level. 0.05 = +5%/level; capped at +100% total.");
        DroneBatteryMaxLevel = BindInt(cfg,
            "Player Upgrades", "DroneBatteryMaxLevel", 20, 1, 100,
            "Drone Battery Life: maximum purchasable level (20 x 5% = +100%).");
        DroneBatteryBasePrice = BindInt(cfg,
            "Player Upgrades", "DroneBatteryBasePrice", 6000, 0, 1000000,
            "Drone Battery Life: approximate shop price of the first level.");
        AutoscanRangePerLevel = BindFloat(cfg,
            "Player Upgrades", "AutoscanRangePerLevel", 5f, 1f, 30f,
            "Autoscan: scan radius added per level, in metres. 5 = level 1 discovers valuables within 5m, level 2 within 10m, etc.");
        AutoscanMaxLevel = BindInt(cfg,
            "Player Upgrades", "AutoscanMaxLevel", 5, 1, 20,
            "Autoscan: maximum purchasable level.");
        AutoscanInterval = BindFloat(cfg,
            "Player Upgrades", "AutoscanInterval", 1f, 0.1f, 10f,
            "Autoscan: seconds between scans. Lower = discovers valuables faster but costs more CPU.");
        AutoscanBasePrice = BindInt(cfg,
            "Player Upgrades", "AutoscanBasePrice", 8000, 0, 1000000,
            "Autoscan: approximate shop price of the first level.");
        MapBasePrice = BindInt(cfg,
            "Player Upgrades", "MapBasePrice", 15000, 0, 1000000,
            "Map Suite: approximate shop price of the FIRST level (very expensive). A single upgrade with 10 levels, each unlocking a map feature: L1-5 live counts + quota path, L6 team pings on the map, L7-8 zoom out, L9 cosmetic-box tracker, L10 monster dots.");
        MapZoomFovLevel7 = BindFloat(cfg,
            "Player Upgrades", "MapZoomFovLevel7", 62f, 50f, 120f,
            "Map Suite L7: camera field-of-view used while the map is open (higher = more zoomed out, see more of the level at once). Vanilla map view is 50.");
        MapZoomFovLevel8 = BindFloat(cfg,
            "Player Upgrades", "MapZoomFovLevel8", 74f, 50f, 120f,
            "Map Suite L8: camera field-of-view while the map is open once level 8 is reached (higher = even more zoomed out).");
        MapZoomInKey = cfg.Bind(
            "Player Upgrades", "MapZoomInKey", UnityEngine.KeyCode.Minus,
            "Map Suite L7+: key to zoom the map view IN (lower FOV) while the map is open. Also accepts Keypad Minus and '['.");
        MapZoomOutKey = cfg.Bind(
            "Player Upgrades", "MapZoomOutKey", UnityEngine.KeyCode.Equals,
            "Map Suite L7+: key to zoom the map view OUT (higher FOV) while the map is open. Also accepts Keypad Plus and ']'.");
        MapZoomStep = BindFloat(cfg,
            "Player Upgrades", "MapZoomStep", 6f, 1f, 30f,
            "Map Suite L7+: how much field-of-view each zoom-hotkey press adds/removes on top of the level's default FOV.");
        MapSuiteReplacesVanilla = cfg.Bind(
            "Player Upgrades", "MapSuiteReplacesVanilla", true,
            "Remove the vanilla 'Map Player Count' upgrade from the shop so Map Suite replaces it (Map Suite already includes live player counts).");

        // --- Surprise / special-level system ---
        SurprisesEnabled = cfg.Bind(
            "Surprises", "Enabled", true,
            "Master switch for the surprise/special-level system.");
        SpecialLevelChancePercent = BindInt(cfg,
            "Surprises", "SpecialLevelChancePercent", 40, 0, 100,
            "Percent chance (0-100) that any eligible level becomes a special level with modifiers.");
        ModifiersPerLevelMin = BindInt(cfg,
            "Surprises", "ModifiersPerLevelMin", 1, 0, 10,
            "Minimum number of modifiers a special level rolls.");
        ModifiersPerLevelMax = BindInt(cfg,
            "Surprises", "ModifiersPerLevelMax", 2, 0, 10,
            "Maximum number of modifiers a special level rolls.");
        SurprisesStartLevel = BindInt(cfg,
            "Surprises", "StartLevel", 1, 0, 30,
            "Levels completed before special levels can appear. 1 = from the 2nd level onward.");

        foreach (Modifiers.ModifierType m in System.Enum.GetValues(typeof(Modifiers.ModifierType)))
        {
            if (m == Modifiers.ModifierType.None)
                continue;
            ModifierEnabled[m] = cfg.Bind(
                "Surprise Modifiers", m.ToString(), true,
                $"{ModifierDescription(m)} Toggle whether it can be rolled on special levels.");
        }

        RebuildPerEnemyCaps();
        EnemyHealthMaxPerEnemy.SettingChanged += (_, _) => RebuildPerEnemyCaps();
    }

    // Parsed form of EnemyHealthMaxPerEnemy, rebuilt whenever the string changes.
    private static Dictionary<string, float> _perEnemyHealthMax = new();

    private static void RebuildPerEnemyCaps()
    {
        var map = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var entry in EnemyHealthMaxPerEnemy.Value.Split(','))
        {
            int colon = entry.LastIndexOf(':');
            if (colon <= 0)
                continue;

            string name = entry.Substring(0, colon).Trim();
            string valueText = entry.Substring(colon + 1).Trim();
            if (name.Length == 0)
                continue;

            if (float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out float mult))
                map[name] = mult;
        }
        _perEnemyHealthMax = map;
    }

    /// <summary>
    /// The HP cap multiplier for a given enemy name: its per-enemy override if
    /// one exists, otherwise the global cap.
    /// </summary>
    public static float HealthMaxForEnemy(string enemyName)
    {
        if (enemyName != null && _perEnemyHealthMax.TryGetValue(enemyName, out float mult))
            return mult;
        return EnemyHealthMax.Value;
    }
}
