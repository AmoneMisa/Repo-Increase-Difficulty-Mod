using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Patches;

/// <summary>
/// Every bought item gets a place in the truck.
///
/// The truck has a fixed number of item slots (ItemVolume) per size class, and the native fill only puts an item
/// into a free slot of exactly its own class. Once a class runs out (many grenades, drones, orbs, decoys all want
/// "small") the rest of the purchases simply don't spawn that level.
///
/// Host-only replacement of PunManager.TruckPopulateItemVolumes, in three passes:
///   1. exact class, as native;
///   2. a free slot of a bigger class (a grenade fits in a medium slot);
///   3. a short stack above an already used slot of a fitting class, so it settles on top of that item.
/// Everything else (resetting inventory spots, destroying the volumes afterwards) is unchanged.
/// </summary>
[HarmonyPatch(typeof(PunManager), "TruckPopulateItemVolumes", new[] { typeof(List<PunManager.ItemSpawn>) })]
internal static class TruckItemSlotsPatch
{
    private static readonly Dictionary<SemiFunc.itemVolume, SemiFunc.itemVolume[]> Bigger = new()
    {
        [SemiFunc.itemVolume.small] = new[] { SemiFunc.itemVolume.medium, SemiFunc.itemVolume.healthPack, SemiFunc.itemVolume.upgrade, SemiFunc.itemVolume.large, SemiFunc.itemVolume.large_wide, SemiFunc.itemVolume.large_high, SemiFunc.itemVolume.large_plus },
        [SemiFunc.itemVolume.healthPack] = new[] { SemiFunc.itemVolume.small, SemiFunc.itemVolume.medium, SemiFunc.itemVolume.large },
        [SemiFunc.itemVolume.upgrade] = new[] { SemiFunc.itemVolume.small, SemiFunc.itemVolume.medium, SemiFunc.itemVolume.large },
        [SemiFunc.itemVolume.medium] = new[] { SemiFunc.itemVolume.large, SemiFunc.itemVolume.large_wide, SemiFunc.itemVolume.large_high, SemiFunc.itemVolume.large_plus },
        [SemiFunc.itemVolume.large] = new[] { SemiFunc.itemVolume.large_plus, SemiFunc.itemVolume.large_wide, SemiFunc.itemVolume.large_high },
    };

    /// <summary>Whether an item of class <paramref name="item"/> may sit in a free slot of class <paramref name="slot"/>.</summary>
    internal static bool Fits(SemiFunc.itemVolume item, SemiFunc.itemVolume slot) =>
        item == slot || (Bigger.TryGetValue(item, out var list) && System.Array.IndexOf(list, slot) >= 0);

    // Stacking height per layer and how many extra layers a slot may take; big items and vehicles never stack.
    private static float StackStep(SemiFunc.itemVolume v) => v switch
    {
        SemiFunc.itemVolume.small or SemiFunc.itemVolume.healthPack or SemiFunc.itemVolume.upgrade => .35f,
        SemiFunc.itemVolume.medium => .5f,
        _ => 0f,
    };
    private const int MaxLayers = 3;
    // Energy crystals are never moved or stacked: they belong in the charging station's own crystal slots, and
    // extras beyond those must stay unspawned (the station counts bought charge, not loose crystals on a shelf).

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static bool Prefix(PunManager __instance, List<PunManager.ItemSpawn> _spawns)
    {
        try
        {
            var manager = ItemManager.instance;
            if (manager == null || __instance.itemManager == null) return true;
            manager.spawnedItems.Clear();
            if (SemiFunc.IsNotMasterClient()) return false;
            StatsManager.instance.playerInventorySpot1Taken.Clear();
            StatsManager.instance.playerInventorySpot2Taken.Clear();
            StatsManager.instance.playerInventorySpot3Taken.Clear();

            var free = new List<ItemVolume>();
            foreach (var v in __instance.itemManager.itemVolumes) if (v != null) free.Add(v);
            var items = new List<Item>(__instance.itemManager.purchasedItems);
            var used = new List<ItemVolume>();
            var layers = new Dictionary<ItemVolume, int>();
            int exact = 0, bigger = 0, stacked = 0;

            // 1. exact class (native)
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var slot = free.Find(v => v.itemVolume == item.itemVolume);
                if (slot == null) continue;
                Spawn(__instance, item, slot, 0f, _spawns);
                free.Remove(slot); used.Add(slot);
                items.RemoveAt(i--); exact++;
            }
            // 2. a free bigger slot
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!Bigger.TryGetValue(item.itemVolume, out var fits)) continue;
                ItemVolume slot = null;
                foreach (var size in fits) { slot = free.Find(v => v.itemVolume == size); if (slot != null) break; }
                if (slot == null) continue;
                Spawn(__instance, item, slot, 0f, _spawns);
                free.Remove(slot); used.Add(slot);
                items.RemoveAt(i--); bigger++;
            }
            // 3. stacked above a used slot of its own or a bigger class
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float step = StackStep(item.itemVolume);
                if (step <= 0f) continue;
                ItemVolume best = null;
                int bestLayer = int.MaxValue;
                foreach (var slot in used)
                {
                    if (slot == null) continue;
                    bool fits = slot.itemVolume == item.itemVolume ||
                                (Bigger.TryGetValue(item.itemVolume, out var list) && System.Array.IndexOf(list, slot.itemVolume) >= 0);
                    if (!fits) continue;
                    layers.TryGetValue(slot, out int layer);
                    if (layer < MaxLayers && layer < bestLayer) { best = slot; bestLayer = layer; }
                }
                if (best == null) continue;
                layers[best] = bestLayer + 1;
                Spawn(__instance, item, best, step * (bestLayer + 1), _spawns);
                items.RemoveAt(i--); stacked++;
            }

            if (bigger + stacked > 0 || items.Count > 0)
                Plugin.Log.LogInfo($"[Truck] Placed {exact + bigger + stacked} bought item(s): {exact} in their own slots, {bigger} in bigger slots, {stacked} stacked" +
                                   (items.Count > 0 ? $"; no room for {items.Count}: {string.Join(", ", items.ConvertAll(x => x.itemName))}." : "."));

            foreach (var v in __instance.itemManager.itemVolumes)
                if (v != null) Object.Destroy(v.gameObject);
            __instance.itemManager.itemVolumes.Clear();
            return false;
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"[Truck] Item placement fell back to native: {e.Message}");
            return true;
        }
    }

    // Native SpawnItem plus a vertical offset for stacked items.
    private static void Spawn(PunManager pun, Item item, ItemVolume volume, float up, List<PunManager.ItemSpawn> spawns)
    {
        var helper = ShopManager.instance.itemRotateHelper.transform;
        helper.parent = volume.transform;
        helper.localRotation = item.spawnRotationOffset;
        var rotation = helper.rotation;
        helper.parent = ShopManager.instance.transform;
        var spawn = new PunManager.ItemSpawn
        {
            prefab = item.prefab,
            position = volume.transform.position + Vector3.up * up,
            rotation = rotation,
        };
        if (spawns != null) spawns.Add(spawn);
        else pun.ItemSpawnApply(spawn);
    }
}
