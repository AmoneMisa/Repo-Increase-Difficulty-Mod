using System.Linq;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// With MoreStandsForShops installed, the shop always offers at least one Strength (grab strength) upgrade.
/// That mod rebuilds the shop's upgrade pool from its own per-item counts, so Strength can be missing from
/// a shop full of extra stands. After both the game and that mod have built the pool, make sure Strength is
/// in it (when the team may still buy it) and move it to the end: the game's SpawnShopItem takes upgrades
/// from the end of the list, so the first upgrade stand gets it. Host only, since only the host stocks the shop.
/// </summary>
[HarmonyPatch(typeof(ShopManager), nameof(ShopManager.GetAllItemsFromStatsManager))]
internal static class GuaranteedStrengthUpgrade
{
    private const string MoreStandsGuid = "DarkSpider90.MoreStandsForShops";
    private const string StrengthItem = "Item Upgrade Player Grab Strength";

    internal static bool MoreStandsInstalled =>
        Chainloader.PluginInfos.ContainsKey(MoreStandsGuid) ||
        Chainloader.PluginInfos.Values.Any(p => p?.Metadata?.GUID != null && p.Metadata.GUID.IndexOf("MoreStandsForShops", System.StringComparison.OrdinalIgnoreCase) >= 0);

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter(MoreStandsGuid)]
    private static void Postfix(ShopManager __instance)
    {
        if (!ModConfig.GuaranteedStrengthEnabled.Value || __instance == null || !SemiFunc.IsMasterClientOrSingleplayer() ||
            !MoreStandsInstalled || StatsManager.instance == null) return;
        try
        {
            if (!StatsManager.instance.itemDictionary.TryGetValue(StrengthItem, out var strength) || strength == null) return;
            if (strength.maxPurchase && StatsManager.instance.GetItemsUpgradesPurchasedTotal(strength.name) >= strength.maxPurchaseAmount) return;
            var pool = __instance.potentialItemUpgrades;
            if (pool == null) __instance.potentialItemUpgrades = pool = new System.Collections.Generic.List<Item>();
            int index = pool.FindIndex(i => i != null && i.name == StrengthItem);
            bool added = index < 0;
            if (added) pool.Add(strength);
            else { var found = pool[index]; pool.RemoveAt(index); pool.Add(found); }
            Plugin.Log.LogInfo($"[Shop] Strength upgrade guaranteed with MoreStandsForShops ({(added ? "added to" : "moved up in")} an upgrade pool of {pool.Count}).");
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"[Shop] Strength guarantee failed harmlessly: {e.Message}");
        }
    }
}
