using System.Collections.Generic;
using HarmonyLib;

namespace IncreaseHardMod.Skills;

/// <summary>
/// Shapes the shop's upgrade shelf for the skill tree (host only; the game fills shelves on the host):
///  - vanilla Map Player Count, Tumble Wings and Death Head Battery are taken out (the tree owns them now);
///  - one Skill Point is moved to the end of the shuffled list, which the shelves fill from first, so every shop
///    with an upgrade slot stocks at least one. A second copy, if any, stays wherever the shuffle put it.
/// This runs right after the game builds and shuffles the candidate lists and before any shelf is filled
/// (the old Map Player Count filter ran after ShopInitialize, when the shelves were already stocked).
/// </summary>
[HarmonyPatch(typeof(ShopManager), "GetAllItemsFromStatsManager")]
internal static class SkillShopPatch
{
    [HarmonyPostfix]
    private static void Postfix(ShopManager __instance)
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        var pool = __instance.potentialItemUpgrades;
        if (pool == null) return;

        int removed = pool.RemoveAll(IsTreeOwnedVanilla);
        int point = pool.FindLastIndex(IsSkillPoint);
        if (point >= 0 && point != pool.Count - 1)
        {
            var item = pool[point];
            pool.RemoveAt(point);
            pool.Add(item);
        }
        Plugin.Log.LogInfo($"[Skills] Shop: removed {removed} vanilla tree upgrade(s); Skill Point {(point >= 0 ? "stocked first" : "not offered")}.");
    }

    internal static bool IsSkillPoint(Item item) => item != null && item.name == "IHM Upgrade " + SkillStore.PointItemId;

    /// <summary>Vanilla upgrades the tree replaces: never sold, never given as quest rewards.</summary>
    internal static bool IsTreeOwnedVanilla(Item item)
    {
        var prefab = item != null && item.prefab != null ? item.prefab.Prefab : null;
        return prefab != null && (prefab.GetComponentInChildren<ItemUpgradeMapPlayerCount>(true) != null ||
                                  prefab.GetComponentInChildren<ItemUpgradePlayerTumbleWings>(true) != null ||
                                  prefab.GetComponentInChildren<ItemUpgradeDeathHeadBattery>(true) != null);
    }
}
