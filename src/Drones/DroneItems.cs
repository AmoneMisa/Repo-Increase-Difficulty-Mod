using System.Collections.Generic;
using HarmonyLib;
using REPOLib.Modules;
using UnityEngine;

namespace IncreaseHardMod.Drones;

internal enum DroneKind
{
    Ghost,
    Silencer,
    Link,
    Repair,
    Defence,
}

/// <summary>
/// Registers the mod's utility drones as real shop items.
///
/// There are no custom 3D assets: each drone is a CLONE of a vanilla drone (prefab + Item asset),
/// recoloured, with the vanilla effect scripts stripped and our <see cref="UtilityDrone"/> added.
/// The vanilla <see cref="ItemDrone"/> keeps doing targeting (beam, attach, battery, on/off sync),
/// so everything about aiming/attaching behaves exactly like the game's own drones; we only add
/// the effect that runs on the attached target.
///
/// The Link kit is two items: "Link Drone Set (A + B)" is the only one sold in the shop
/// (<c>maxAmountInShop</c> = 0 on B hides it); buying A also buys B, so both are delivered by the
/// game's normal truck item system.
///
/// Items are created once, on the first StatsManager.RunStartStats (when the vanilla item list is
/// loaded) and queued with REPOLib; REPOLib re-adds them to the item dictionary on later runs.
/// </summary>
internal static class DroneItems
{
    public const string GhostName = "Item Drone Ghost";
    public const string SilencerName = "Item Drone Silencer";
    public const string LinkAName = "Item Drone Link A";
    public const string LinkBName = "Item Drone Link B";
    public const string RepairName = "Item Drone Repair";

    private static bool _built;
    private static GameObject _holder;

    internal static GameObject EnsureHolder()
    {
        if (_holder == null)
        {
            _holder = new GameObject("IHM_DronePrefabs");
            _holder.SetActive(false);
            Object.DontDestroyOnLoad(_holder);
        }
        return _holder;
    }

    [HarmonyPatch(typeof(StatsManager), "RunStartStats")]
    private static class OnRunStartStats
    {
        // Run before REPOLib's own postfix so our items are queued in time to be added this pass.
        [HarmonyPostfix]
        [HarmonyPriority(Priority.High)]
        private static void Postfix()
        {
            BuildOnce();
            Gear.GearItems.BuildOnce();
        }
    }

    private static void BuildOnce()
    {
        if (_built || !ModConfig.DronesEnabled.Value)
            return;
        _built = true;
        try
        {
            var baseItem = FindBaseDrone();
            if (baseItem == null)
            {
                Plugin.Log.LogWarning("[Drones] No vanilla drone found to clone; utility drones disabled.");
                return;
            }

            EnsureHolder();

            if (ModConfig.GhostEnabled.Value)
                Make(baseItem, GhostName, "Ghost Drone", DroneKind.Ghost, 0, 5500f, 7500f, shop: 1,
                new Color(0.75f, 0.85f, 1f),
                "Attach to a valuable to let players walk through it and take it out of extraction counting while the drone is on.",
                targets: (true, false, false, false));
            if (ModConfig.SilencerEnabled.Value)
                Make(baseItem, SilencerName, "Silencer Drone", DroneKind.Silencer, 0, 3500f, 4500f, shop: 1,
                new Color(0.55f, 0.55f, 0.6f),
                "Attach to a valuable, item, cart, player or enemy to make its noise (and the noise enemies investigate) much quieter.",
                targets: (true, true, true, true));
            if (ModConfig.RepairEnabled.Value)
                Make(baseItem, RepairName, "Repair Drone", DroneKind.Repair, 0, 15000f, 18000f, shop: 1,
                new Color(0.4f, 1f, 0.5f),
                "Attach to a damaged valuable to restore its lost value (up to its original value). Destroyed valuables cannot be repaired.",
                targets: (true, false, false, false));
            if (ModConfig.LinkEnabled.Value)
            {
                Make(baseItem, LinkAName, "Link Drone Set (A + B)", DroneKind.Link, 0, 9500f, 12000f, shop: 1,
                new Color(1f, 0.6f, 0.2f),
                "Two drones (A + B). Attach one to each of two objects to tie them together with a physical cable; grab one to reel the other in.",
                targets: (true, true, false, true));
            Make(baseItem, LinkBName, "Link Drone B", DroneKind.Link, 1, 0f, 0f, shop: 0,
                new Color(0.2f, 0.8f, 1f),
                "Second half of the Link Drone Set.",
                targets: (true, true, false, true));
            }
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogError($"[Drones] Failed to build drone items: {e}");
        }
    }

    // Any vanilla drone Item whose prefab has an ItemDrone. Sorted for a deterministic pick on every client.
    internal static Item FindBaseDrone()
    {
        if (StatsManager.instance.itemDictionary.TryGetValue("Item Drone Indestructible", out var named) &&
            named.prefab != null && named.prefab.Prefab != null && named.prefab.Prefab.GetComponent<ItemDrone>() != null)
            return named;
        var names = new List<string>(StatsManager.instance.itemDictionary.Keys);
        names.Sort(System.StringComparer.Ordinal);
        foreach (var n in names)
        {
            var item = StatsManager.instance.itemDictionary[n];
            var prefab = item != null && item.prefab != null ? item.prefab.Prefab : null;
            if (prefab != null && prefab.GetComponent<ItemDrone>() != null)
                return item;
        }
        return null;
    }

    internal static void Make(Item baseItem, string assetName, string display, DroneKind kind, int slot,
        float priceMin, float priceMax, int shop, Color color, string description,
        (bool valuables, bool nonValuables, bool players, bool enemies) targets)
    {
        var item = Object.Instantiate(baseItem);
        item.name = assetName;
        item.itemName = display;
        item.itemNameLocalized = null;
        item.description = description;
        item.maxAmount = 3;
        item.maxAmountInShop = shop;
        item.maxPurchase = false;
        item.minPlayerCount = 1;
        item.value = Object.Instantiate(baseItem.value);
        item.value.valueMin = priceMin;
        item.value.valueMax = priceMax;
        var preset = ScriptableObject.CreateInstance<ColorPresets>();
        preset.colorMain = color;
        preset.colorLight = Color.Lerp(color, Color.white, 0.5f);
        preset.colorDark = Color.Lerp(color, Color.black, 0.5f);
        item.colorPreset = preset;

        var go = Object.Instantiate(baseItem.prefab.Prefab, _holder.transform);
        go.name = assetName;

        // Strip vanilla drone effects/targeting overrides; keep ItemDrone + battery/toggle plumbing.
        foreach (var mb in go.GetComponents<MonoBehaviour>())
        {
            if (mb == null)
                continue;
            string t = mb.GetType().Name;
            if (t.StartsWith("ItemDrone") && t != "ItemDrone")
                Object.DestroyImmediate(mb);
        }

        var drone = go.GetComponent<ItemDrone>();
        drone.targetValuables = targets.valuables;
        drone.targetNonValuables = targets.nonValuables;
        drone.targetPlayers = targets.players;
        drone.targetEnemies = targets.enemies;

        var ud = go.AddComponent<UtilityDrone>();
        ud.kind = kind;
        ud.slot = slot;
        if (kind == DroneKind.Defence)
            go.AddComponent<Gear.ShieldItem>().mode = Gear.ShieldMode.DefenceDrone;

        var attributes = go.GetComponent<ItemAttributes>();
        attributes.item = item;

        var prefabRef = Items.RegisterItem(attributes);
        if (prefabRef == null)
            Plugin.Log.LogWarning($"[Drones] REPOLib refused to register {assetName}.");
        else
            Plugin.Log.LogInfo($"[Drones] Registered {assetName}.");
    }

    /// <summary>Buying the Link set also buys its second half, so both are delivered by the truck.</summary>
    [HarmonyPatch(typeof(StatsManager), "ItemPurchase")]
    private static class OnPurchase
    {
        [HarmonyPostfix]
        private static void Postfix(StatsManager __instance, string itemName)
        {
            if (itemName == LinkAName && __instance.itemDictionary.ContainsKey(LinkBName))
                __instance.ItemPurchase(LinkBName);
        }
    }
}
