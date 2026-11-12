using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Drones;

/// <summary>Saved, host-assigned identities for the two independently deployable parts of a kit.</summary>
internal static class LinkSets
{
    private const string Prefix = "IHM_LinkSet_";
    private const string Next = "IHM_LinkNextSet";
    private static StatsManager _migrationStats;
    private static float _nextMigration;

    internal static int SetOf(ItemAttributes attributes) =>
        attributes == null || string.IsNullOrEmpty(attributes.instanceName) || StatsManager.instance == null ||
        !StatsManager.instance.item.ContainsKey(attributes.instanceName) ? 0 : SetOf(attributes.instanceName);

    private static int SetOf(string instance)
    {
        var stats = StatsManager.instance;
        return stats != null && !string.IsNullOrEmpty(instance) &&
            stats.runStats.TryGetValue(Prefix + instance, out int id) ? id : 0;
    }

    internal sealed class Purchase
    {
        internal HashSet<string> Keys;
        internal int Count;
    }

    internal static Purchase BeforePurchase(StatsManager stats, string itemName)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || itemName != DroneItems.LinkAName) return null;
        EnsureLegacyPairs();
        // Existing unmatched halves must not consume the next kit's delivery reservation.
        foreach (string key in stats.item.Keys)
            if ((IsPart(key, DroneItems.LinkAName) || IsPart(key, DroneItems.LinkBName)) && SetOf(key) == 0)
                Save(Prefix + key, Allocate());
        return new Purchase { Keys = new HashSet<string>(stats.item.Keys, StringComparer.Ordinal),
            Count = stats.itemsPurchased.TryGetValue(itemName, out int count) ? count : 0 };
    }

    internal static void AfterPurchase(StatsManager stats, string itemName, Purchase before)
    {
        if (before == null || !SemiFunc.IsMasterClientOrSingleplayer() || itemName != DroneItems.LinkAName ||
            !stats.itemDictionary.ContainsKey(DroneItems.LinkBName) || !stats.itemDictionary[itemName].physicalItem ||
            !stats.itemsPurchased.TryGetValue(itemName, out int count) || count <= before.Count) return;
        string a = Added(stats, before.Keys, DroneItems.LinkAName);
        var beforePartner = new HashSet<string>(stats.item.Keys, StringComparer.Ordinal);
        stats.ItemPurchase(DroneItems.LinkBName);
        string b = Added(stats, beforePartner, DroneItems.LinkBName);
        int id = Allocate();
        if (id <= 0) return;
        if (a != null) Save(Prefix + a, id); else Save("IHM_LinkPendingA_" + id, 1);
        if (b != null) Save(Prefix + b, id); else Save("IHM_LinkPendingB_" + id, 1);
    }

    private static void AssignPending(string key, bool replaceStale = false)
    {
        if (!replaceStale && SetOf(key) > 0) return;
        string pending = IsPart(key, DroneItems.LinkAName) ? "IHM_LinkPendingA_" :
            IsPart(key, DroneItems.LinkBName) ? "IHM_LinkPendingB_" : null;
        if (pending == null) return;
        int first = int.MaxValue;
        foreach (var entry in StatsManager.instance.runStats)
            if (entry.Value == 1 && entry.Key.StartsWith(pending, StringComparison.Ordinal) &&
                int.TryParse(entry.Key.Substring(pending.Length), out int id) && id > 0) first = Math.Min(first, id);
        if (first == int.MaxValue) return;
        Save(Prefix + key, first);
        Save(pending + first, 0);
    }

    // In a shop, native ItemPurchase updates delivery counts but ItemAdd deliberately does not
    // add an owned item. The real instance key becomes available when the truck delivers it.
    [HarmonyPatch(typeof(StatsManager), nameof(StatsManager.ItemAdd))]
    private static class Delivery
    {
        [HarmonyPrefix]
        private static void Prefix(StatsManager __instance, out HashSet<string> __state) =>
            __state = SemiFunc.IsMasterClientOrSingleplayer() ? new HashSet<string>(__instance.item.Keys) : null;
        [HarmonyPostfix]
        private static void Postfix(StatsManager __instance, HashSet<string> __state)
        {
            if (__state == null) return;
            foreach (string key in __instance.item.Keys)
                if (!__state.Contains(key)) AssignPending(key, replaceStale: true);
        }
    }

    private static string Added(StatsManager stats, HashSet<string> before, string asset)
    {
        foreach (string key in stats.item.Keys)
            if (IsPart(key, asset) && !before.Contains(key)) return key;
        return null;
    }

    private static bool IsPart(string key, string asset) => key.StartsWith(asset + "/", StringComparison.Ordinal);

    private static void Save(string key, int value)
    {
        // Native runStats are saved and sent to late joiners. Do not use scene view IDs as identity.
        if (PunManager.instance != null) PunManager.instance.UpdateStat("runStats", key, value);
        else StatsManager.instance.runStats[key] = value;
    }

    private static int Allocate()
    {
        var stats = StatsManager.instance;
        int id = stats.runStats.TryGetValue(Next, out int last) ? last : 0;
        if (id == int.MaxValue) return 0;
        Save(Next, ++id);
        return id;
    }

    internal static void Bind(string a, string b)
    {
        if (StatsManager.instance == null || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        int id = Allocate();
        if (id <= 0) return;
        Save(Prefix + a, id);
        Save(Prefix + b, id);
    }

    internal static void TickMigration()
    {
        if (_migrationStats == StatsManager.instance && Time.unscaledTime < _nextMigration) return;
        _migrationStats = StatsManager.instance;
        _nextMigration = Time.unscaledTime + 1f;
        EnsureLegacyPairs();
    }

    internal static void EnsureLegacyPairs()
    {
        var stats = StatsManager.instance;
        if (stats == null || !SemiFunc.IsMasterClientOrSingleplayer()) return;
        foreach (string key in stats.item.Keys) AssignPending(key);
        var a = new List<string>();
        var b = new List<string>();
        foreach (string key in stats.item.Keys)
        {
            if (SetOf(key) > 0) continue; // Never steal a surviving half from its missing partner.
            if (IsPart(key, DroneItems.LinkAName)) a.Add(key);
            else if (IsPart(key, DroneItems.LinkBName)) b.Add(key);
        }
        a.Sort(StringComparer.Ordinal);
        b.Sort(StringComparer.Ordinal);
        for (int i = 0; i < Math.Min(a.Count, b.Count); i++) Bind(a[i], b[i]);
    }

    [HarmonyPatch(typeof(ItemAttributes), nameof(ItemAttributes.Update))]
    private static class PartName
    {
        [HarmonyPrefix]
        private static void Prefix(ItemAttributes __instance)
        {
            int set = SetOf(__instance);
            if (set <= 0 || __instance.item == null) return;
            string name = __instance.item.name;
            string setWord = Localization.GameLanguage.Russian ? "Комплект" : "Set";
            string label = name == DroneItems.LinkAName ? $"Link Drone A / {setWord} {set}" :
                name == DroneItems.LinkBName ? $"Link Drone B / {setWord} {set}" : null;
            if (label == null) return;
            __instance.cachedItemName = label; // Current game caches the prompt's localized name.
            __instance.itemName = label;
        }
    }
}
