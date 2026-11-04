using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using IncreaseHardMod.Upgrades;
using UnityEngine;

namespace IncreaseHardMod.Visuals;

/// <summary>
/// Inventory presentation for the mod's drones, orbs and weapons.
///
/// Icons: vanilla equippables leave <c>ItemAttributes.icon</c> empty and let the prefab's own
/// SemiIconMaker photograph the live model (cached in persistentDataPath/Cache/Icons/Items). Our
/// clones keep that camera, so clearing the icon gives them the exact vanilla framing, lighting and
/// transparent background for our recoloured / replaced models. The cached render is discarded once
/// per launch so art changes in a new mod version are never hidden behind a stale file.
///
/// Shared sprites: ItemAttributes.OnDestroy destroys its icon. An embedded sprite assigned to the
/// prefab is shared by every spawned copy, so the first destroyed copy blanked the icon for all of
/// them. Each instance now receives its own Sprite over the same texture.
///
/// Battery: the inventory slot battery is kept visible, refreshed and drawn above the icon, and
/// held drones/orbs show their battery even while switched off.
/// </summary>
internal static class InventoryItemVisuals
{
    private static readonly HashSet<string> Tracked = new(StringComparer.Ordinal);
    private static readonly HashSet<string> CacheCleared = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Call on a cloned prefab after its art is applied.</summary>
    internal static void Prepare(GameObject prefab)
    {
        if (prefab == null) return;
        Tracked.Add(prefab.name);
        var battery = prefab.GetComponent<ItemBattery>();
        if (battery != null) battery.onlyShowWhenItemToggleIsOn = false;

        var attributes = prefab.GetComponent<ItemAttributes>();
        if (attributes == null || prefab.GetComponentInChildren<SemiIconMaker>(true) == null)
            return; // No native icon camera: keep our authored inventory render as the fallback.
        attributes.icon = null;
        ClearCachedIcon(prefab.name);
    }

    internal static bool IsTracked(GameObject go) =>
        go != null && Tracked.Contains(go.name.Replace("(Clone)", "").Trim());

    private static void ClearCachedIcon(string name)
    {
        if (!CacheCleared.Add(name)) return;
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "Cache", "Icons", "Items", name.ToLowerInvariant() + ".png");
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[Icons] Could not refresh cached icon for {name}: {e.Message}");
        }
    }

    [HarmonyPatch(typeof(ItemAttributes), "Start")]
    private static class PerInstanceSprite
    {
        [HarmonyPrefix]
        private static void Prefix(ItemAttributes __instance)
        {
            var shared = __instance.icon;
            if (shared == null || !UpgradeIcons.IsShared(shared)) return;
            var rect = shared.rect;
            var copy = Sprite.Create(shared.texture, rect,
                new Vector2(shared.pivot.x / rect.width, shared.pivot.y / rect.height), shared.pixelsPerUnit);
            copy.name = shared.name;
            __instance.icon = copy;
        }
    }

    [HarmonyPatch(typeof(InventorySpot), "Update")]
    private static class AlwaysShowBattery
    {
        [HarmonyPostfix]
        private static void Postfix(InventorySpot __instance)
        {
            if (!__instance.IsOccupied()) return;
            var item = __instance.CurrentItem;
            var visual = __instance.batteryVisualLogic;
            if (item == null || visual == null || !IsTracked(item.gameObject)) return;
            var battery = item.GetComponent<ItemBattery>();
            if (battery == null) return;
            if (!visual.gameObject.activeSelf) visual.gameObject.SetActive(true);
            if (visual.targetScale == 0f) visual.ResetOutro();
            if (visual.itemBattery != battery)
            {
                visual.itemBattery = battery;
                visual.BatteryBarsSet();
            }
            // A full-bleed icon must not cover the bar: keep the battery last among its siblings.
            var t = visual.transform;
            if (t.parent != null && t.GetSiblingIndex() != t.parent.childCount - 1) t.SetAsLastSibling();
            if (visual.currentBars != battery.batteryLifeInt && battery.batteryLifeInt >= 0)
                visual.BatteryBarsUpdate(battery.batteryLifeInt, _forceUpdate: true);
        }
    }
}
