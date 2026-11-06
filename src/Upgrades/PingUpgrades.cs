using System.Collections.Generic;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>One base ping unlock, plus its optional information addon.</summary>
internal static class PingUpgrades
{
    public const string PingAnywhereId = "IHM_PingAnywhere";
    public const string PingInfoId = "IHM_PingInfo";
    internal const string LegacyPinId = "IHM_Pin";

    public static void Register()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value || !ModConfig.PingEnabled.Value) return;
        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = PingAnywhereId, DisplayName = "Ping Anywhere",
            MaxLevel = 1,
        });
        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = PingInfoId, DisplayName = "Ping Info",
            MaxLevel = 1,
        });
    }

    // Registration/load and ownership checks cover clients that receive saved stats later.
    // Merge by player ID, never add levels: both purchases are the same one-level unlock.
    internal static void MigrateLegacyPins()
    {
        var stats = StatsManager.instance;
        if (stats == null) return;
        var all = stats.dictionaryOfDictionaries;
        if (!all.TryGetValue("playerUpgrade" + LegacyPinId, out var legacy) || legacy.Count == 0) return;
        if (!all.TryGetValue("playerUpgrade" + PingAnywhereId, out var merged))
            all["playerUpgrade" + PingAnywhereId] = merged = new Dictionary<string, int>();
        foreach (var entry in legacy)
            if (entry.Value > 0) merged[entry.Key] = 1;
        legacy.Clear(); // Retain the native sync dictionary, with no duplicate Tab entry.
        var upgrade = RepoUpgrades.GetUpgrade(PingAnywhereId);
        if (upgrade != null) upgrade.PlayerDictionary = merged;
    }

    // Skill tree: the Ping branch (root + info add-ons).
    public static bool OwnsPingAnywhere() => Skills.SkillEffects.LocalHas(Skills.SkillCatalog.Ping);
    public static bool OwnsPingInfo() => Skills.SkillEffects.LocalHas(Skills.SkillCatalog.TargetInfo);
    public static bool OwnsEnemyHp() => Skills.SkillEffects.LocalHas(Skills.SkillCatalog.EnemyHp);
    public static bool OwnsLootCondition() => Skills.SkillEffects.LocalHas(Skills.SkillCatalog.LootCondition);
    public static bool OwnsWeight() => Skills.SkillEffects.LocalHas(Skills.SkillCatalog.Weight);
    public static bool OwnsThreat() => Skills.SkillEffects.LocalHas(Skills.SkillCatalog.Threat);
}
