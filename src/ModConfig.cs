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
    public static ConfigEntry<float> LevelTimerSeconds;
    public static ConfigEntry<float> LevelTimerKillBonus;
    public static ConfigEntry<float> LevelTimerDeathPenalty;
    public static ConfigEntry<float> LevelTimerQuotaBonus;

    // --- Late Join ---
    // When enabled, the host keeps the Photon room (and Steam lobby) reachable
    // during a run/shop/truck instead of sealing it at run start, and re-syncs
    // mod state (modifiers, timer, cart upgrades) whenever a new player connects.
    public static ConfigEntry<bool> LateJoinEnabled;

    // --- Ping ---
    // Press PingKey to mark whatever you're looking at (valuable/enemy/item/location).
    // The marker is broadcast to all players and shown as a world-anchored label.
    public static ConfigEntry<bool> PingEnabled;
    public static ConfigEntry<UnityEngine.KeyCode> PingKey;
    public static ConfigEntry<float> PingMaxDistance;
    public static ConfigEntry<float> PingDurationSeconds;

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

    // --- Cart / truck upgrades ---
    // A small host-driven upgrade shop (open with CartUpgradeMenuKey while in the
    // truck/shop). Levels persist for the whole run and are synced to clients.
    public static ConfigEntry<bool> CartUpgradesEnabled;
    public static ConfigEntry<UnityEngine.KeyCode> CartUpgradeMenuKey;
    public static ConfigEntry<float> CartUpgradeCostGrowth;
    // Heal power: scales all incoming healing (truck final-heal, medkits, drones).
    public static ConfigEntry<float> CartHealPercentPerLevel;
    public static ConfigEntry<int> CartHealMaxLevel;
    public static ConfigEntry<int> CartHealBaseCost;
    // Charge efficiency: slows the truck charging station's energy-pool drain.
    public static ConfigEntry<float> CartChargePercentPerLevel;
    public static ConfigEntry<int> CartChargeMaxLevel;
    public static ConfigEntry<int> CartChargeBaseCost;
    // Post-quota selling: once bought, leftover valuables can be cashed in AFTER
    // every extraction quota is closed, for CartSellFraction of their value.
    public static ConfigEntry<int> CartSellCost;
    public static ConfigEntry<float> CartSellFraction;

    // --- Level Info panel ---
    // Right-side overlay logging each level change (number + name + rolled surprises),
    // with a hotkey to hide/show it.
    public static ConfigEntry<bool> LevelInfoEnabled;
    public static ConfigEntry<UnityEngine.KeyCode> LevelInfoToggleKey;
    public static ConfigEntry<int> LevelInfoMaxEntries;

    // --- Server Info panel ---
    // Passive lobby/menu overlay showing current Photon region, ping, room code and
    // player count. Reads PhotonNetwork state only — never opens sockets or callbacks.
    public static ConfigEntry<bool> ServerInfoEnabled;

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
        Modifiers.ModifierType.ReducedPlayerHp    => "Everyone's max HP is cut for the level.",
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
            "DIAGNOSTIC (default off). If true, skip creating the mod's runtime menu/HUD objects (networking, timer, cart menu, ping, Photon HUD). Used only to isolate whether they interfere with Photon connecting. Leave off for normal play.");

        RampStartLevel = BindInt(cfg,
            "General", "RampStartLevel", 4, 0, 30,
            "Levels completed before difficulty scaling starts. 4 = first 4 levels are vanilla, scaling begins on the 5th.");

        EnemyHealthPerLevel = BindFloat(cfg,
            "Enemies", "HealthPerLevel", 0.15f, 0f, 2f,
            "Extra enemy max HP per level past the ramp start. 0.15 = +15% per level (cumulative, additive).");
        EnemyHealthMax = BindFloat(cfg,
            "Enemies", "HealthMaxMultiplier", 3.0f, 1f, 20f,
            "Global upper cap on the enemy HP multiplier. 3.0 = at most 3x vanilla HP.");
        // Free-form string (name:mult list) — REPOConfig can't render this sensibly, so hide it.
        EnemyHealthMaxPerEnemy = cfg.Bind(
            "Enemies", "HealthMaxPerEnemy", "Elsa:2, Loom:1.5, Cleanup Crew:1.5, Tick:1.5",
            new ConfigDescription(
                "Per-enemy HP cap overrides. Format: 'Name:multiplier, Name:multiplier'. Names match EnemyParent.enemyName (case-insensitive). Unlisted enemies use HealthMaxMultiplier. Enable LogEnemyNames to see exact names.",
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
            "Ping", "DurationSeconds", 6f, 1f, 30f,
            "How long a ping marker stays visible.");

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
            "Key to hide/show the level info panel.");
        LevelInfoMaxEntries = BindInt(cfg,
            "Level Info", "MaxEntries", 6, 1, 20,
            "How many recent level changes to keep listed on screen.");

        // --- Server Info panel ---
        ServerInfoEnabled = cfg.Bind(
            "Server Info", "Enabled", true,
            "Show a passive overlay in the lobby/menu with the current Photon region, ping, room code and player count. Reads connection state only — never opens sockets, so it's safe on the main menu.");

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

        // --- Cart / truck upgrades ---
        CartUpgradesEnabled = cfg.Bind(
            "Cart Upgrades", "Enabled", true,
            "Enable the host-driven cart/truck upgrade shop. Levels persist for the whole run and are synced to clients.");
        CartUpgradeMenuKey = cfg.Bind(
            "Cart Upgrades", "MenuKey", UnityEngine.KeyCode.U,
            "Key the HOST presses (while in the truck/shop) to open/close the cart upgrade menu.");
        CartUpgradeCostGrowth = BindFloat(cfg,
            "Cart Upgrades", "CostGrowth", 1.6f, 1f, 5f,
            "Cost multiplier per already-owned level of a given upgrade. 1.6 = each level costs 60% more than the last.");
        CartHealPercentPerLevel = BindFloat(cfg,
            "Cart Upgrades", "HealPercentPerLevel", 0.25f, 0f, 2f,
            "Heal Power: extra healing effectiveness per level. 0.25 = +25% healing per level (truck final-heal, medkits, drones).");
        CartHealMaxLevel = BindInt(cfg,
            "Cart Upgrades", "HealMaxLevel", 3, 0, 20,
            "Heal Power: maximum purchasable level.");
        CartHealBaseCost = BindInt(cfg,
            "Cart Upgrades", "HealBaseCost", 10000, 0, 1000000,
            "Heal Power: cost of the first level (grows by CostGrowth each level).");
        CartChargePercentPerLevel = BindFloat(cfg,
            "Cart Upgrades", "ChargePercentPerLevel", 0.20f, 0f, 1f,
            "Charge Efficiency: reduction of the truck charging station's energy-pool drain per level. 0.20 = -20% drain per level.");
        CartChargeMaxLevel = BindInt(cfg,
            "Cart Upgrades", "ChargeMaxLevel", 3, 0, 20,
            "Charge Efficiency: maximum purchasable level.");
        CartChargeBaseCost = BindInt(cfg,
            "Cart Upgrades", "ChargeBaseCost", 10000, 0, 1000000,
            "Charge Efficiency: cost of the first level (grows by CostGrowth each level).");
        CartSellCost = BindInt(cfg,
            "Cart Upgrades", "SellUnlockCost", 50000, 0, 1000000,
            "Loot Cash-In: one-time cost to unlock selling leftover valuables after all quotas are closed.");
        CartSellFraction = BindFloat(cfg,
            "Cart Upgrades", "SellFraction", 0.75f, 0f, 1f,
            "Loot Cash-In: fraction of a leftover valuable's value returned as currency when sold. 0.75 = 75%.");

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
