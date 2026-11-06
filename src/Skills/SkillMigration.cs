using System;
using System.Collections.Generic;

namespace IncreaseHardMod.Skills;

/// <summary>
/// Converts a player's old shop-bought custom upgrade levels into tree nodes, once per save.
/// Nothing a player paid for is lost:
///  - same-length ladders map 1:1 (Stamina 3/5 -> Stamina Efficiency 3/5);
///  - compressed ladders keep the effect (Melee 3/10 -> 1/5) and the remainder is refunded as points, rounded up;
///  - Map Suite level N unlocks the nodes of the features it had;
///  - a converted node's missing prerequisites (branch roots) are granted for free;
///  - vanilla Tumble Wings / Battery Head levels become free levels (they keep working and cost no points).
/// Earned points are set so that everything granted is paid for, plus the refund to spend freely.
/// Death Protection and the truck upgrades are not part of the tree and are left alone.
/// </summary>
internal static class SkillMigration
{
    internal sealed class Result
    {
        public SkillLoadout Loadout;
        public int Refund;
        public readonly List<string> Lines = new();
    }

    // Old upgrade id -> tree node. The old caps come from the player's config at migration time.
    internal static readonly (string OldId, string Node)[] Ladders =
    {
        ("IHM_Stamina", SkillCatalog.StaminaEfficiency),
        ("IHM_HpRegen", SkillCatalog.HpRegen),
        ("IHM_GunBatteryBullet", SkillCatalog.BulletShots),
        ("IHM_GunBatteryLaser", SkillCatalog.LaserShots),
        ("IHM_GunBatteryTranq", SkillCatalog.TranqShots),
        ("IHM_DroneBattery", SkillCatalog.DroneBattery),
        ("IHM_MeleeDamage", SkillCatalog.MeleeDamage),
        ("IHM_GrenadeDamage", SkillCatalog.GrenadeDamage),
        ("IHM_TumbleDamage", SkillCatalog.TumbleDamage),
        ("IHM_Armor", SkillCatalog.Armor),
        ("IHM_Overcharge", SkillCatalog.Overcharge),
        ("IHM_Autoscan", SkillCatalog.Autoscan),
        ("IHM_InventorySlots", SkillCatalog.InventorySlot),
        ("IHM_PingAnywhere", SkillCatalog.Ping),
        ("IHM_PingInfo", SkillCatalog.TargetInfo),
    };

    internal const string OldMapId = "IHM_Map";

    /// <summary>True for an old shop upgrade whose job the tree took over (it is no longer sold).</summary>
    internal static bool IsTreeCovered(string oldId)
    {
        if (oldId == OldMapId) return true;
        foreach (var (id, _) in Ladders) if (id == oldId) return true;
        return false;
    }

    // Map Suite's old ladder: level N added feature N (see MapUpgrade). Level 5 held both quota heading and tracker.
    internal static readonly string[][] MapLadder =
    {
        new[] { SkillCatalog.MapUplink },
        new[] { SkillCatalog.DeadPlayers },
        new[] { SkillCatalog.ActiveMonsters },
        new[] { SkillCatalog.RespawnTimers },
        new[] { SkillCatalog.QuotaHeading, SkillCatalog.ValuableTracker },
        new[] { SkillCatalog.MapPings },
        new[] { SkillCatalog.ZoomOne },
        new[] { SkillCatalog.ZoomTwo },
        new[] { SkillCatalog.CosmeticTracker },
        new[] { SkillCatalog.MonsterDots },
    };

    /// <param name="oldLevels">Old upgrade id -> owned level.</param>
    /// <param name="oldMax">Old upgrade id -> its cap when it was bought (config); missing ids use the node's own cap.</param>
    internal static Result Convert(IReadOnlyDictionary<string, int> oldLevels, IReadOnlyDictionary<string, int> oldMax,
        int headBattery = 0, int tumbleWings = 0)
    {
        var result = new Result { Loadout = new SkillLoadout() };
        var loadout = result.Loadout;

        foreach (var (oldId, nodeId) in Ladders)
        {
            int owned = Get(oldLevels, oldId);
            if (owned <= 0) continue;
            var node = SkillCatalog.Find(nodeId);
            int max = Math.Max(1, oldMax != null && oldMax.TryGetValue(oldId, out var m) && m > 0 ? m : node.MaxLevel);
            double scaled = owned * (double)node.MaxLevel / max;
            int level = Math.Min(node.MaxLevel, (int)Math.Floor(scaled + 1e-9));
            double leftover = scaled - level;
            int refund = leftover > 1e-9 ? (int)Math.Ceiling(leftover * node.Cost - 1e-9) : 0;
            if (level > 0) Grant(loadout, nodeId, level);
            result.Refund += refund;
            result.Lines.Add($"{oldId} {owned}/{max} -> {nodeId} {level}/{node.MaxLevel}" + (refund > 0 ? $" +{refund} pt" : ""));
        }

        int map = Math.Min(Get(oldLevels, OldMapId), MapLadder.Length);
        for (int i = 0; i < map; i++)
            foreach (var nodeId in MapLadder[i])
                Grant(loadout, nodeId, 1);
        if (map > 0) result.Lines.Add($"{OldMapId} {map}/{MapLadder.Length} -> {map} map feature level(s)");

        // Roots the converted nodes need, granted free of charge (they are paid for by the earned points below).
        bool added = true;
        while (added)
        {
            added = false;
            foreach (var node in SkillCatalog.All)
            {
                if (loadout.BoughtLevel(node.Id) <= 0) continue;
                foreach (var need in node.Requires)
                    if (loadout.Level(need) < 1) { Grant(loadout, need, 1); result.Lines.Add($"{need} granted (needed by {node.Id})"); added = true; }
            }
        }

        if (headBattery > 0) loadout.Free[SkillCatalog.HeadBattery] = headBattery;
        if (tumbleWings > 0) loadout.Free[SkillCatalog.TumbleWings] = tumbleWings;

        loadout.Earned = loadout.Spent + result.Refund;
        return result;
    }

    private static int Get(IReadOnlyDictionary<string, int> levels, string id) =>
        levels != null && levels.TryGetValue(id, out var level) ? level : 0;

    private static void Grant(SkillLoadout loadout, string id, int level)
    {
        var node = SkillCatalog.Find(id);
        int next = Math.Min(node.MaxLevel, Math.Max(loadout.BoughtLevel(id), level));
        loadout.Bought[id] = next;
    }
}
