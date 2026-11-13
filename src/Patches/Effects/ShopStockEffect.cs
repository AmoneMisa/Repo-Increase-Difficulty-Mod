using System.Collections.Generic;
using HarmonyLib;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// More different items and upgrades in the shop, with room for them (host; the host fills the shop).
///
/// The native PunManager.ShopPopulateItemVolumes stocks the shop scene's amounts (now 8 items, 4-5 crystals, 10 upgrades and 6 health packs), and
/// puts an item only on a free shelf slot (ItemVolume) of exactly its own size class; once a class runs out, the rest
/// of that kind never appears. This replacement keeps the native secret-shelf pass and order, stocks
/// <c>ExtraItems</c> more items and, with the Upgrade Shelf team upgrade, more upgrades on its own shelves, and in a second pass lets anything still
/// unplaced use a free bigger slot (a grenade on a medium slot), with the same size rule as the truck
/// (<see cref="TruckItemSlotsPatch.Fits"/>). No duplicates: every placed item comes out of the game's own pool, which
/// holds each item only as often as the game allows. Items are never stacked here, so price tags don't overlap.
/// </summary>
[HarmonyPatch(typeof(PunManager), nameof(PunManager.ShopPopulateItemVolumes), new[] { typeof(List<PunManager.ItemSpawn>) })]
internal static class ShopStockEffect
{
    private static int _baseItems = -1, _baseUpgrades = -1;

    [HarmonyPrefix]
    private static bool Prefix(PunManager __instance, List<PunManager.ItemSpawn> _spawns)
    {
        var shop = ShopManager.instance;
        if (shop == null || SemiFunc.IsNotMasterClient()) return true;
        try
        {
            if (_baseItems < 0) { _baseItems = shop.itemSpawnTargetAmount; _baseUpgrades = shop.itemUpgradesAmount; }
            shop.itemSpawnTargetAmount = _baseItems + ModConfig.ShopExtraItems.Value;
            shop.itemUpgradesAmount = _baseUpgrades + Upgrades.ShopUpgrades.ExtraUpgradeSlots; // the shop scene's own count (10 in the current game), plus Upgrade Shelf
            Upgrades.UpgradeShelfRack.HostBegin();
            // Upgrade Shelf: its extra upgrades get slots on a rack of their own.
            int shelf = Upgrades.ShopUpgrades.ExtraUpgradeSlots;
            if (shelf > 0) shop.itemVolumes.AddRange(Upgrades.UpgradeShelfRack.HostBuild(shop, shelf));
            // The mod's consumables never take a health pack's slot: they come out of every pool here and go on a
            // stand of their own (a copy of the health-pack shelf by a free wall) after the normal stocking.
            var consumables = TakeConsumables(shop);
            var stand = consumables.Count > 0 ? Upgrades.UpgradeShelfRack.HostBuildConsumableStand(shop, consumables.Count) : new List<ItemVolume>();
            // Crystal Stock: more power crystals at once (the game rolls 4-5), up to what the truck lacks.
            int crystals = Upgrades.ShopUpgrades.CrystalStock;
            if (crystals > 0)
            {
                shop.itemConsumablesAmount = UnityEngine.Mathf.Max(shop.itemConsumablesAmount, crystals);
                Upgrades.ShopUpgrades.TopUpCrystals(shop.potentialItemConsumables, shop.itemConsumablesAmount);
            }

            // Secret shelves: exactly as native (one in three gets an item; the volumes are then removed).
            int secretCount = 0;
            foreach (var pair in shop.secretItemVolumes)
            {
                foreach (var volume in pair.Value)
                    if (volume != null && shop.potentialSecretItems.ContainsKey(pair.Key) && UnityEngine.Random.Range(0, 3) == 0)
                        __instance.SpawnShopItem(volume, shop.potentialSecretItems[pair.Key], ref secretCount, true, _spawns);
                foreach (var volume in pair.Value)
                    if (volume != null) UnityEngine.Object.Destroy(volume.gameObject);
                pair.Value.Clear();
            }

            var pools = new[] { shop.potentialItems, shop.potentialItemConsumables, shop.potentialItemUpgrades, shop.potentialItemHealthPacks };
            var targets = new[] { shop.itemSpawnTargetAmount, shop.itemConsumablesAmount, shop.itemUpgradesAmount, shop.itemHealthPacksAmount };
            var counts = new int[4];
            var free = new List<ItemVolume>();

            // Pass 1, native order and rule: each slot takes an item of exactly its class, items first.
            foreach (var volume in shop.itemVolumes)
            {
                if (volume == null) continue;
                bool placed = false;
                for (int k = 0; k < 4 && !placed; k++)
                    if (counts[k] < targets[k]) placed = __instance.SpawnShopItem(volume, pools[k], ref counts[k], false, _spawns);
                if (!placed) free.Add(volume);
            }

            // Pass 2: whatever is still wanted goes on a free slot of a bigger class.
            int bigger = 0;
            for (int k = 0; k < 4; k++)
                for (int i = pools[k].Count - 1; i >= 0 && counts[k] < targets[k]; i--)
                {
                    var item = pools[k][i];
                    if (item == null) continue;
                    var slot = free.Find(v => TruckItemSlotsPatch.Fits(item.itemVolume, v.itemVolume));
                    if (slot == null) continue;
                    if (!PlaceOn(__instance, slot, item, _spawns)) continue;
                    pools[k].RemoveAt(i);
                    counts[k]++;
                    free.Remove(slot);
                    bigger++;
                }

            int onStand = 0;
            for (int i = 0; i < stand.Count && i < consumables.Count; i++)
                if (PlaceOn(__instance, stand[i], consumables[i], _spawns)) onStand++;
            foreach (var volume in stand) if (volume != null) UnityEngine.Object.Destroy(volume.gameObject);
            // No wall for the stand (a cramped shop): the rest still turn up on a free slot of a fitting size.
            for (int i = onStand; i < consumables.Count; i++)
            {
                var slot = free.Find(v => TruckItemSlotsPatch.Fits(consumables[i].itemVolume, v.itemVolume));
                if (slot == null || !PlaceOn(__instance, slot, consumables[i], _spawns)) continue;
                free.Remove(slot);
                onStand++;
            }
            if (consumables.Count > 0) Plugin.Log.LogInfo($"[Shop] Consumables: {onStand}/{consumables.Count} placed ({stand.Count} stand slot(s)).");

            Plugin.Log.LogInfo($"[Shop] Stocked {counts[0]}/{targets[0]} items, {counts[1]}/{targets[1]} consumables, " +
                               $"{counts[2]}/{targets[2]} upgrades, {counts[3]}/{targets[3]} health packs on {shop.itemVolumes.Count} slot(s) " +
                               $"({bigger} on a bigger slot, {free.Count} slot(s) left empty).");

            foreach (var volume in shop.itemVolumes)
                if (volume != null) UnityEngine.Object.Destroy(volume.gameObject);
            shop.itemVolumes.Clear();
            return false;
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"[Shop] Stocking fell back to native: {e.Message}");
            return true;
        }
    }

    /// <summary>The mod's consumables, taken out of every native pool (each as often as the shop allows it), shuffled.</summary>
    private static List<Item> TakeConsumables(ShopManager shop)
    {
        var taken = new List<Item>();
        foreach (var pool in new[] { shop.potentialItems, shop.potentialItemConsumables, shop.potentialItemUpgrades, shop.potentialItemHealthPacks })
            for (int i = pool.Count - 1; i >= 0; i--)
            {
                var prefab = pool[i] != null && pool[i].prefab != null ? pool[i].prefab.Prefab : null;
                if (prefab == null || prefab.GetComponent<Gear.ModConsumable>() == null) continue;
                taken.Add(pool[i]);
                pool.RemoveAt(i);
            }
        for (int i = taken.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (taken[i], taken[j]) = (taken[j], taken[i]);
        }
        return taken;
    }

    // Native SpawnShopItem matches classes exactly; this places a chosen item on any slot, same rotation handling.
    private static bool PlaceOn(PunManager pun, ItemVolume volume, Item item, List<PunManager.ItemSpawn> spawns)
    {
        var helper = ShopManager.instance.itemRotateHelper.transform;
        helper.parent = volume.transform;
        helper.localRotation = item.spawnRotationOffset;
        var rotation = helper.rotation;
        helper.parent = ShopManager.instance.transform;
        var spawn = new PunManager.ItemSpawn { prefab = item.prefab, position = volume.transform.position, rotation = rotation };
        if (spawns != null) spawns.Add(spawn);
        else pun.ItemSpawnApply(spawn);
        return true;
    }
}
