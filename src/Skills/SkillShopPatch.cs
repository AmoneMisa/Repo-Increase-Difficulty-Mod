using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Skills;

/// <summary>
/// Shapes the shop for the skill tree (host only; the game fills shelves on the host):
///  - vanilla Map Player Count, Tumble Wings and Death Head Battery are taken out of every candidate list the
///    shop fills from (upgrades, items, consumables, health packs and the secret-shop lists), because the tree
///    owns them now;
///  - one Skill Point is moved to the end of the shuffled upgrade list, which the shelves fill from first, so every
///    shop with an upgrade slot stocks at least one. A second copy, if any, stays wherever the shuffle put it.
/// This runs right after the game builds and shuffles the candidate lists and before any shelf is filled.
/// As a last guard, <see cref="BlockSpawn"/> refuses to spawn one of those upgrades in the shop by any other route.
/// </summary>
[HarmonyPatch(typeof(ShopManager), nameof(ShopManager.GetAllItemsFromStatsManager))]
internal static class SkillShopPatch
{
    [HarmonyPostfix]
    private static void Postfix(ShopManager __instance)
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        int removed = Strip(__instance.potentialItemUpgrades) + Strip(__instance.potentialItems) +
                      Strip(__instance.potentialItemConsumables) + Strip(__instance.potentialItemHealthPacks);
        if (__instance.potentialSecretItems != null)
            foreach (var list in __instance.potentialSecretItems.Values) removed += Strip(list);

        var pool = __instance.potentialItemUpgrades;
        int point = pool != null ? pool.FindLastIndex(IsSkillPoint) : -1;
        if (point >= 0 && point != pool.Count - 1)
        {
            var item = pool[point];
            pool.RemoveAt(point);
            pool.Add(item);
        }
        Plugin.Log.LogInfo($"[Skills] Shop: removed {removed} vanilla tree upgrade entr(ies); Skill Point {(point >= 0 ? "stocked first" : "not offered")}.");
    }

    private static int Strip(List<Item> list) => list != null ? list.RemoveAll(IsTreeOwnedVanilla) : 0;

    internal static bool IsSkillPoint(Item item) => item != null && item.name == "IHM Upgrade " + SkillStore.PointItemId;

    /// <summary>Vanilla upgrades the tree replaces: never sold, never given as quest rewards.</summary>
    internal static bool IsTreeOwnedVanilla(Item item) => item != null && item.prefab != null && IsTreeOwnedVanilla(item.prefab.Prefab);

    internal static bool IsTreeOwnedVanilla(GameObject prefab) =>
        prefab != null && (prefab.GetComponentInChildren<ItemUpgradeMapPlayerCount>(true) != null ||
                           prefab.GetComponentInChildren<ItemUpgradePlayerTumbleWings>(true) != null ||
                           prefab.GetComponentInChildren<ItemUpgradeDeathHeadBattery>(true) != null);

    // The shop's upgrade reroll stand picks replacements straight from every item in the game (not from the lists
    // above), so a reroll brought the tree-owned upgrades back. Pick again until it lands on another upgrade;
    // if nothing else is left, the stand keeps that slot's current upgrade (the game's own handling of null).
    [HarmonyPatch(typeof(UpgradeStand), nameof(UpgradeStand.GetWeightedUpgradeExcluding))]
    private static class RerollStand
    {
        private static bool _repicking;

        [HarmonyPostfix]
        private static void Postfix(UpgradeStand __instance, Item excludeItem, Dictionary<string, int> displayedCounts,
            Dictionary<string, int> selectedDuringReroll, bool secondAttempt, ref Item __result)
        {
            if (_repicking || !ModConfig.PlayerUpgradesEnabled.Value || !IsTreeOwnedVanilla(__result)) return;
            var rejected = __result;
            _repicking = true;
            try
            {
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    var next = __instance.GetWeightedUpgradeExcluding(excludeItem, displayedCounts, selectedDuringReroll, secondAttempt);
                    if (!IsTreeOwnedVanilla(next)) { __result = next; return; }
                }
                __result = null;
            }
            finally
            {
                _repicking = false;
                Plugin.Log.LogInfo($"[Skills] Reroll stand: replaced {rejected.name} with {(__result != null ? __result.name : "nothing (slot kept)")}.");
            }
        }
    }

    // Every shop shelf item is spawned here (directly, or from the list the level generator collects).
    [HarmonyPatch(typeof(PunManager), nameof(PunManager.ItemSpawnApply))]
    private static class BlockSpawn
    {
        [HarmonyPrefix]
        private static bool Prefix(PunManager.ItemSpawn _spawn)
        {
            if (!ModConfig.PlayerUpgradesEnabled.Value || !SemiFunc.RunIsShop()) return true;
            GameObject prefab = null;
            try { prefab = _spawn.prefab != null ? _spawn.prefab.Prefab : null; } catch { }
            if (!IsTreeOwnedVanilla(prefab)) return true;
            Plugin.Log.LogInfo($"[Skills] Shop: blocked {prefab.name} from a shelf (the skill tree owns it).");
            return false;
        }
    }
}
