using HarmonyLib;
using UnityEngine;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// Keeps unusable upgrades out of the shop. The game decides whether an upgrade may be offered by comparing
/// <c>StatsManager.GetItemsUpgradesPurchasedTotal</c> with the item's <c>maxPurchaseAmount</c>, in the shop floor
/// roll (ShopManager), the upgrade stand (and reroll) and our quest rewards alike. Our levels are per player, so
/// the raw team-wide purchase count is the wrong measure: with 4 players it would hide a 5-level upgrade after
/// 5 purchases in total, and it cannot see that everyone present is already maxed. Answer that question here once.
/// </summary>
internal static class UpgradeAvailability
{
    private const string ItemPrefix = "IHM Upgrade ";

    /// <summary>True when nobody present can still gain a level from this upgrade.</summary>
    internal static bool Exhausted(string id, int maxLevel)
    {
        if (TruckUpgrades.IsShared(id)) return TruckUpgrades.TeamLevel(id) >= maxLevel;
        var upgrade = RepoUpgrades.GetUpgrade(id);
        var players = GameDirector.instance != null ? GameDirector.instance.PlayerList : null;
        if (upgrade == null || players == null) return false;
        bool anyone = false;
        foreach (var avatar in players)
        {
            if (avatar == null || string.IsNullOrEmpty(avatar.steamID)) continue;
            anyone = true;
            if (upgrade.GetLevel(avatar) < maxLevel) return false;
        }
        return anyone;
    }

    [HarmonyPatch(typeof(StatsManager), nameof(StatsManager.GetItemsUpgradesPurchasedTotal))]
    private static class PurchaseTotal
    {
        [HarmonyPostfix]
        private static void Postfix(string itemName, ref int __result)
        {
            if (itemName == null || !itemName.StartsWith(ItemPrefix, System.StringComparison.Ordinal)) return;
            var definition = PlayerUpgradeRegistry.FindDefinition(itemName.Substring(ItemPrefix.Length));
            if (definition == null || definition.Consumable) return;
            int max = Mathf.Max(1, definition.MaxLevel);
            // Offered while someone can still use it; hidden (counted as sold out) once nobody can.
            __result = Exhausted(definition.Id, max) ? max : Mathf.Min(__result, max - 1);
        }
    }
}
