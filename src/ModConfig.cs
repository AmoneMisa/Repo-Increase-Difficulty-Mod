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

    public static void Init(ConfigFile cfg)
    {
        RampStartLevel = cfg.Bind(
            "General", "RampStartLevel", 4,
            "Levels completed before difficulty scaling starts. 4 = first 4 levels are vanilla, scaling begins on the 5th.");

        EnemyHealthPerLevel = cfg.Bind(
            "Enemies", "HealthPerLevel", 0.15f,
            "Extra enemy max HP per level past the ramp start. 0.15 = +15% per level (cumulative, additive).");
        EnemyHealthMax = cfg.Bind(
            "Enemies", "HealthMaxMultiplier", 3.0f,
            "Global upper cap on the enemy HP multiplier. 3.0 = at most 3x vanilla HP.");
        EnemyHealthMaxPerEnemy = cfg.Bind(
            "Enemies", "HealthMaxPerEnemy", "Elsa:2, Loom:1.5, Cleanup Crew:1.5, Tick:1.5",
            "Per-enemy HP cap overrides. Format: 'Name:multiplier, Name:multiplier'. Names match EnemyParent.enemyName (case-insensitive). Unlisted enemies use HealthMaxMultiplier. Enable LogEnemyNames to see exact names.");
        LogEnemyNames = cfg.Bind(
            "Enemies", "LogEnemyNames", true,
            "Log each enemy's internal name the first time it spawns, so you can tune HealthMaxPerEnemy.");

        EnemyDamagePerLevel = cfg.Bind(
            "Enemies", "DamagePerLevel", 0.10f,
            "Extra enemy attack damage per level past the ramp start. 0.10 = +10% per level.");
        EnemyDamageMax = cfg.Bind(
            "Enemies", "DamageMaxMultiplier", 3.0f,
            "Upper cap on the enemy damage multiplier.");

        EnemySpeedPerLevel = cfg.Bind(
            "Enemies", "SpeedPerLevel", 0.06f,
            "Extra enemy movement speed per level past the ramp start. 0.06 = +6% per level.");
        EnemySpeedMax = cfg.Bind(
            "Enemies", "SpeedMaxMultiplier", 1.75f,
            "Upper cap on the enemy speed multiplier. Keep modest; too high makes enemies uncatchable/janky.");

        EnemyRespawnFasterPerLevel = cfg.Bind(
            "Enemies", "RespawnFasterPerLevel", 0.08f,
            "How much shorter the enemy respawn wait gets per level. 0.08 = -8% wait per level.");
        EnemyRespawnMinFactor = cfg.Bind(
            "Enemies", "RespawnMinFactor", 0.3f,
            "Floor for the respawn-wait multiplier. 0.3 = respawn wait never drops below 30% of vanilla.");

        QuotaPerLevel = cfg.Bind(
            "Quota", "QuotaPerLevel", 0.10f,
            "Extra extraction quota per level past the ramp start. 0.10 = +10% per level.");
        QuotaMax = cfg.Bind(
            "Quota", "QuotaMaxMultiplier", 5.0f,
            "Upper cap on the quota multiplier.");
        QuotaMaxLootFraction = cfg.Bind(
            "Quota", "QuotaMaxLootFraction", 0.9f,
            "Quota can never exceed this fraction of the total loot value on the level (keeps levels beatable). 0.9 = at most 90%.");

        // --- Surprise modifier effect strengths ---
        FragileLootFactor = cfg.Bind(
            "Modifier Tuning", "FragileLootFactor", 2.0f,
            "FragileLoot: multiplies loot fragility. >1 = breaks more easily. 2.0 = twice as fragile.");
        ProtectedLootFactor = cfg.Bind(
            "Modifier Tuning", "ProtectedLootFactor", 0.4f,
            "ProtectedLoot: multiplies loot fragility. <1 = harder to break. 0.4 = 60% less fragile.");
        HeavyLootFactor = cfg.Bind(
            "Modifier Tuning", "HeavyLootFactor", 1.75f,
            "HeavyLoot: multiplies loot mass. >1 = heavier.");
        LightLootFactor = cfg.Bind(
            "Modifier Tuning", "LightLootFactor", 0.5f,
            "LightLoot: multiplies loot mass. <1 = lighter.");
        LightMonsterMaxMass = cfg.Bind(
            "Modifier Tuning", "LightMonsterMaxMass", 3.0f,
            "LightMonsters: cap every monster's mass at this value (lighter = easier to shove around).");
        HeavyMonsterMinMass = cfg.Bind(
            "Modifier Tuning", "HeavyMonsterMinMass", 7.0f,
            "HeavyMonsters: raise every monster's mass to at least this value (heavier = harder to move).");
        BiggerSoulDropsFactor = cfg.Bind(
            "Modifier Tuning", "BiggerSoulDropsFactor", 2.0f,
            "BiggerSoulDrops: multiplies the money value of each enemy soul drop. 2.0 = each drop worth twice as much. Does not change drop count.");
        MonsterPitDamageFactor = cfg.Bind(
            "Modifier Tuning", "MonsterPitDamageFactor", 0.5f,
            "MonstersPitTanky: multiplies death-pit chip damage dealt to monsters (moon 2+). 0.5 = monsters take half pit damage. Instant-kill pits on early moons are unaffected.");
        ExtraGachaCubesPerSpawn = cfg.Bind(
            "Modifier Tuning", "ExtraGachaCubesPerSpawn", 1,
            "MoreGachaCubes: how many additional cosmetic 'gacha' cubes to spawn per cube the game would normally place. Capped by the level's available spawn spots. 1 = roughly double.");
        DangerousLootDamageFactor = cfg.Bind(
            "Modifier Tuning", "DangerousLootDamageFactor", 2.0f,
            "DangerousLoot: multiplies the damage hazardous valuables (cursed dolls, radioactive cups, barrels, saws, etc.) deal to players. 2.0 = double damage.");
        SoulsExplodeHarderFactor = cfg.Bind(
            "Modifier Tuning", "SoulsExplodeHarderFactor", 2.0f,
            "SoulsExplodeHarder: multiplies the player damage of enemy-soul explosions (moon 4+). 2.0 = double explosion damage.");
        SpreadUpgradesCount = cfg.Bind(
            "Modifier Tuning", "SpreadUpgradesCount", 3,
            "SpreadUpgrades: how many upgrade item pickups to scatter across the special level. They can't be used on that level, only once back in the truck.");
        LevelTimerSeconds = cfg.Bind(
            "Modifier Tuning", "LevelTimerSeconds", 300f,
            "LevelTimer: starting countdown in seconds. When it hits 0 the level restarts and rerolls to a fresh layout (progress is not lost, the level is just regenerated).");
        LevelTimerKillBonus = cfg.Bind(
            "Modifier Tuning", "LevelTimerKillBonus", 10f,
            "LevelTimer: seconds added to the clock for each enemy killed.");
        LevelTimerDeathPenalty = cfg.Bind(
            "Modifier Tuning", "LevelTimerDeathPenalty", 30f,
            "LevelTimer: seconds removed from the clock each time a player dies.");
        LevelTimerQuotaBonus = cfg.Bind(
            "Modifier Tuning", "LevelTimerQuotaBonus", 60f,
            "LevelTimer: seconds added when an extraction point's quota is completed.");

        // --- Late Join ---
        LateJoinEnabled = cfg.Bind(
            "Late Join", "Enabled", true,
            "Host keeps the room/lobby joinable during a run/shop/truck (REPO normally seals it at run start) and re-syncs mod state to anyone who connects mid-game.");

        // --- Cart / truck upgrades ---
        CartUpgradesEnabled = cfg.Bind(
            "Cart Upgrades", "Enabled", true,
            "Enable the host-driven cart/truck upgrade shop. Levels persist for the whole run and are synced to clients.");
        CartUpgradeMenuKey = cfg.Bind(
            "Cart Upgrades", "MenuKey", UnityEngine.KeyCode.U,
            "Key the HOST presses (while in the truck/shop) to open/close the cart upgrade menu.");
        CartUpgradeCostGrowth = cfg.Bind(
            "Cart Upgrades", "CostGrowth", 1.6f,
            "Cost multiplier per already-owned level of a given upgrade. 1.6 = each level costs 60% more than the last.");
        CartHealPercentPerLevel = cfg.Bind(
            "Cart Upgrades", "HealPercentPerLevel", 0.25f,
            "Heal Power: extra healing effectiveness per level. 0.25 = +25% healing per level (truck final-heal, medkits, drones).");
        CartHealMaxLevel = cfg.Bind(
            "Cart Upgrades", "HealMaxLevel", 3,
            "Heal Power: maximum purchasable level.");
        CartHealBaseCost = cfg.Bind(
            "Cart Upgrades", "HealBaseCost", 10000,
            "Heal Power: cost of the first level (grows by CostGrowth each level).");
        CartChargePercentPerLevel = cfg.Bind(
            "Cart Upgrades", "ChargePercentPerLevel", 0.20f,
            "Charge Efficiency: reduction of the truck charging station's energy-pool drain per level. 0.20 = -20% drain per level.");
        CartChargeMaxLevel = cfg.Bind(
            "Cart Upgrades", "ChargeMaxLevel", 3,
            "Charge Efficiency: maximum purchasable level.");
        CartChargeBaseCost = cfg.Bind(
            "Cart Upgrades", "ChargeBaseCost", 10000,
            "Charge Efficiency: cost of the first level (grows by CostGrowth each level).");
        CartSellCost = cfg.Bind(
            "Cart Upgrades", "SellUnlockCost", 50000,
            "Loot Cash-In: one-time cost to unlock selling leftover valuables after all quotas are closed.");
        CartSellFraction = cfg.Bind(
            "Cart Upgrades", "SellFraction", 0.75f,
            "Loot Cash-In: fraction of a leftover valuable's value returned as currency when sold. 0.75 = 75%.");

        // --- Surprise / special-level system ---
        SurprisesEnabled = cfg.Bind(
            "Surprises", "Enabled", true,
            "Master switch for the surprise/special-level system.");
        SpecialLevelChancePercent = cfg.Bind(
            "Surprises", "SpecialLevelChancePercent", 40,
            "Percent chance (0-100) that any eligible level becomes a special level with modifiers.");
        ModifiersPerLevelMin = cfg.Bind(
            "Surprises", "ModifiersPerLevelMin", 1,
            "Minimum number of modifiers a special level rolls.");
        ModifiersPerLevelMax = cfg.Bind(
            "Surprises", "ModifiersPerLevelMax", 2,
            "Maximum number of modifiers a special level rolls.");
        SurprisesStartLevel = cfg.Bind(
            "Surprises", "StartLevel", 1,
            "Levels completed before special levels can appear. 1 = from the 2nd level onward.");

        foreach (Modifiers.ModifierType m in System.Enum.GetValues(typeof(Modifiers.ModifierType)))
        {
            if (m == Modifiers.ModifierType.None)
                continue;
            ModifierEnabled[m] = cfg.Bind(
                "Surprise Modifiers", m.ToString(), true,
                $"Allow the '{m}' modifier to be rolled on special levels.");
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
