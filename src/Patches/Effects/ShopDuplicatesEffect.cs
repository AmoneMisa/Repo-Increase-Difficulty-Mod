using System.Collections.Generic;
using HarmonyLib;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Allow the same item (e.g. a gun) to appear more than once in a single shop.
///
/// ShopManager.GetAllItemsFromStatsManager() builds the "potential items" pool, adding
/// each item only up to its per-item maxAmountInShop (usually once). SpawnShopItem then
/// removes an entry from the pool for every item it places, so an item can appear at most
/// as many times as it exists in the pool. By postfixing the pool build to insert extra
/// copies of each regular item, the same item can be rolled into multiple shop slots
/// (bounded by itemSpawnTargetAmount and the number of shop shelves, so the shop doesn't
/// grow — it just may repeat items). This runs master-only, same as the method it patches.
///
/// We only duplicate the regular-item pool (potentialItems), which is where guns/tools
/// live; upgrades and health packs are intentionally left alone.
/// </summary>
[HarmonyPatch(typeof(ShopManager), nameof(ShopManager.GetAllItemsFromStatsManager))]
internal static class ShopDuplicatesEffect
{
    [HarmonyPostfix]
    private static void Postfix(ShopManager __instance)
    {
        int extra = ModConfig.ShopDuplicatesExtra.Value;
        if (extra <= 0)
            return;

        var pool = __instance.potentialItems;
        if (pool == null || pool.Count == 0)
            return;

        var originals = new List<Item>(pool);
        for (int copy = 0; copy < extra; copy++)
            pool.AddRange(originals);

        Shuffle(pool);
    }

    private static readonly System.Random Rng = new();

    private static void Shuffle(List<Item> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
