using System.Collections.Generic;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>One surface-marker unlock, plus an independent information upgrade.</summary>
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
            Description = "Unlocks location pings on any surface, visible through walls. Markers expire after the configured duration; live markers are limited. Includes the former Pin unlock.",
            MaxLevel = 1, BaseShopPrice = ModConfig.PingAnywhereBasePrice.Value,
            IconFile = "Upgrade_Ping.png",
        });
        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = PingInfoId, DisplayName = "Ping Info",
            Description = "Adds valuable prices, cosmetic rarity and exact names to your object pings. Does not unlock location pings.",
            MaxLevel = 1, BaseShopPrice = ModConfig.PingInfoBasePrice.Value,
            IconFile = "Upgrade_Ping_Info.png",
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

    public static bool OwnsPingAnywhere()
    {
        MigrateLegacyPins();
        return OwnsLocal(PingAnywhereId);
    }
    public static bool OwnsPingInfo() => OwnsLocal(PingInfoId);

    private static bool OwnsLocal(string id)
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value) return false;
        var avatar = SemiFunc.PlayerAvatarLocal();
        if (avatar == null || string.IsNullOrEmpty(avatar.steamID)) return false;
        var upgrade = RepoUpgrades.GetUpgrade(id);
        return upgrade != null && upgrade.GetLevel(avatar.steamID) > 0;
    }
}
