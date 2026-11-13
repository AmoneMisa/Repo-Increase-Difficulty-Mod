using System.Collections.Generic;
using IncreaseHardMod.Economy;
using UnityEngine;

namespace IncreaseHardMod.Modifiers;

/// <summary>What a stat modifier turns. Two modifiers that share an axis never roll on the same level.</summary>
public enum StatAxis
{
    EnemyHealth,
    EnemyDamage,
    EnemySpeed,
    EnemyRespawn,   // multiplier on the time before a monster comes back (<1 = faster)
    EnemyCount,
    LootValue,
    Quota,
    PlayerDamage,   // damage players take
    Healing,
    LevelSize,
    Extractions,    // additive, in extraction points
}

public enum StatCategory
{
    Loot,
    Enemy,
    Player,
    Environment,
}

/// <summary>
/// A data-driven level modifier: a name, a tier, a category and one to three multipliers. They sit next to the
/// original flag modifiers (see <see cref="ModifierType"/>) in the same roll, are broadcast with them, and are
/// applied by the same hooks that already carry the depth scaling (enemy health / damage / speed / respawn / count,
/// quota, loot value, level size, healing and damage taken), so they compose with the ramp, the economy events,
/// the truck upgrades and shield items instead of fighting them.
/// </summary>
internal sealed class StatModifier
{
    public int Id;
    public string Name;
    /// <summary>Earlier name, so a renamed modifier keeps its config switch and translation.</summary>
    public string OldName;
    public string Description;
    public EconomyTier Tier;
    public StatCategory Category;
    public readonly Dictionary<StatAxis, float> Effects = new();
}

internal static class StatModifiers
{
    public static readonly List<StatModifier> All = new();

    private static StatModifier Add(string name, EconomyTier tier, StatCategory cat, string desc, params (StatAxis axis, float v)[] fx)
    {
        var m = new StatModifier { Id = All.Count + 1, Name = name, Tier = tier, Category = cat, Description = desc };
        foreach (var (axis, v) in fx)
            m.Effects[axis] = v;
        All.Add(m);
        return m;
    }

    static StatModifiers()
    {
        // ---- loot ----
        Add("Gilded Loot", EconomyTier.Good, StatCategory.Loot, "Valuables are worth 25% more.", (StatAxis.LootValue, 1.25f));
        Add("Rich Pickings", EconomyTier.VeryGood, StatCategory.Loot, "Valuables are worth 50% more, the quota 25% higher.", (StatAxis.LootValue, 1.5f), (StatAxis.Quota, 1.25f));
        Add("Cheap Trinkets", EconomyTier.Bad, StatCategory.Loot, "Valuables are worth 20% less.", (StatAxis.LootValue, 0.8f));
        Add("Light Load", EconomyTier.Good, StatCategory.Loot, "The quota is 20% lower.", (StatAxis.Quota, 0.8f));
        Add("Heavy Load", EconomyTier.Bad, StatCategory.Loot, "The quota is 25% higher.", (StatAxis.Quota, 1.25f));
        // ---- enemies ----
        Add("Frail Monsters", EconomyTier.Good, StatCategory.Enemy, "Monsters have 25% less health.", (StatAxis.EnemyHealth, 0.75f));
        Add("Docile Monsters", EconomyTier.Good, StatCategory.Enemy, "Monsters hit 25% softer.", (StatAxis.EnemyDamage, 0.75f));
        Add("Sluggish Monsters", EconomyTier.Good, StatCategory.Enemy, "Monsters move 20% slower.", (StatAxis.EnemySpeed, 0.8f));
        Add("Slow Respawns", EconomyTier.Good, StatCategory.Enemy, "Monsters take 50% longer to come back.", (StatAxis.EnemyRespawn, 1.5f));
        Add("Thin Patrols", EconomyTier.Good, StatCategory.Enemy, "25% fewer monsters.", (StatAxis.EnemyCount, 0.75f));
        Add("Tough Monsters", EconomyTier.Bad, StatCategory.Enemy, "Monsters have 40% more health.", (StatAxis.EnemyHealth, 1.4f));
        Add("Brutal Monsters", EconomyTier.Bad, StatCategory.Enemy, "Monsters hit 35% harder.", (StatAxis.EnemyDamage, 1.35f));
        Add("Hasty Monsters", EconomyTier.Bad, StatCategory.Enemy, "Monsters move 25% faster.", (StatAxis.EnemySpeed, 1.25f));
        Add("Relentless Respawns", EconomyTier.Bad, StatCategory.Enemy, "Monsters come back 40% sooner.", (StatAxis.EnemyRespawn, 0.6f));
        Add("Infestation", EconomyTier.Bad, StatCategory.Enemy, "35% more monsters.", (StatAxis.EnemyCount, 1.35f));
        Add("Apex Hunters", EconomyTier.VeryBad, StatCategory.Enemy, "Monsters are tougher, harder-hitting and faster.", (StatAxis.EnemyHealth, 1.3f), (StatAxis.EnemyDamage, 1.3f), (StatAxis.EnemySpeed, 1.15f));
        Add("Swarm", EconomyTier.VeryBad, StatCategory.Enemy, "60% more monsters, but each with 15% less health.", (StatAxis.EnemyCount, 1.6f), (StatAxis.EnemyHealth, 0.85f));
        // ---- players ----
        Add("Padded Suits", EconomyTier.Good, StatCategory.Player, "Players take 20% less damage.", (StatAxis.PlayerDamage, 0.8f));
        Add("Stimulants", EconomyTier.Good, StatCategory.Player, "All healing is 40% stronger.", (StatAxis.Healing, 1.4f));
        Add("Glass Cannons", EconomyTier.Mixed, StatCategory.Player, "Players take 30% more damage, monsters have 30% less health.", (StatAxis.PlayerDamage, 1.3f), (StatAxis.EnemyHealth, 0.7f));
        Add("Thin Skin", EconomyTier.Bad, StatCategory.Player, "Players take 30% more damage.", (StatAxis.PlayerDamage, 1.3f));
        Add("Poor Medicine", EconomyTier.Bad, StatCategory.Player, "All healing is 40% weaker.", (StatAxis.Healing, 0.6f));
        Add("Fragile Everything", EconomyTier.VeryBad, StatCategory.Player, "Players take 50% more damage and heal 30% less.", (StatAxis.PlayerDamage, 1.5f), (StatAxis.Healing, 0.7f));
        // ---- environment ----
        Add("Sprawling Facility", EconomyTier.Mixed, StatCategory.Environment, "A 25% bigger level with 15% more valuable loot.", (StatAxis.LevelSize, 1.25f), (StatAxis.LootValue, 1.15f));
        Add("Compact Facility", EconomyTier.Mixed, StatCategory.Environment, "A 20% smaller level with 15% fewer monsters.", (StatAxis.LevelSize, 0.8f), (StatAxis.EnemyCount, 0.85f));
        Add("Extra Dock", EconomyTier.Good, StatCategory.Environment, "One more extraction point.", (StatAxis.Extractions, 1f));
        Add("Cramped Dock", EconomyTier.Bad, StatCategory.Environment, "One fewer extraction point.", (StatAxis.Extractions, -1f));
        // ---- batch 2 (appended: ids are positions, so existing ones keep theirs) ----
        Add("Gold Rush", EconomyTier.Mixed, StatCategory.Loot, "Valuables are worth 40% more, and 25% more monsters guard them.", (StatAxis.LootValue, 1.4f), (StatAxis.EnemyCount, 1.25f));
        Add("Bargain Bin", EconomyTier.Mixed, StatCategory.Loot, "Valuables are worth 30% less, and the quota is 30% lower.", (StatAxis.LootValue, .7f), (StatAxis.Quota, .7f));
        Add("Skeleton Crew", EconomyTier.Mixed, StatCategory.Enemy, "40% fewer monsters, but the quota is 15% higher.", (StatAxis.EnemyCount, .6f), (StatAxis.Quota, 1.15f));
        Add("Glass Jaw Monsters", EconomyTier.Mixed, StatCategory.Enemy, "Monsters have 40% less health but hit 30% harder.", (StatAxis.EnemyHealth, .6f), (StatAxis.EnemyDamage, 1.3f));
        Add("Blitz", EconomyTier.Bad, StatCategory.Enemy, "Monsters are 30% faster and come back 30% sooner, with 20% less health.", (StatAxis.EnemySpeed, 1.3f), (StatAxis.EnemyRespawn, .7f), (StatAxis.EnemyHealth, .8f));
        Add("Marathon", EconomyTier.Mixed, StatCategory.Environment, "A 40% bigger level with one more extraction point.", (StatAxis.LevelSize, 1.4f), (StatAxis.Extractions, 1f));
        Add("Field Hospital", EconomyTier.Good, StatCategory.Player, "Healing is 60% stronger, but players take 10% more damage.", (StatAxis.Healing, 1.6f), (StatAxis.PlayerDamage, 1.1f));
        Add("Kevlar Crew", EconomyTier.Good, StatCategory.Player, "Players take 30% less damage, but the quota is 20% higher.", (StatAxis.PlayerDamage, .7f), (StatAxis.Quota, 1.2f)).OldName = "Iron Lungs";
    }

    public static StatModifier Get(int id) => id >= 1 && id <= All.Count ? All[id - 1] : null;

    // ---- the combined effect of whatever is active on this level ------------------------------------------

    private static readonly Dictionary<StatAxis, float> Cache = new();

    public static void Recalculate(int[] active)
    {
        Cache.Clear();
        if (active == null)
            return;
        foreach (int id in active)
        {
            var m = Get(id);
            if (m == null)
                continue;
            foreach (var kv in m.Effects)
            {
                if (kv.Key == StatAxis.Extractions)
                    Cache[kv.Key] = (Cache.TryGetValue(kv.Key, out var e) ? e : 0f) + kv.Value;
                else
                    Cache[kv.Key] = (Cache.TryGetValue(kv.Key, out var p) ? p : 1f) * kv.Value;
            }
        }
    }

    /// <summary>Product of every active multiplier on <paramref name="axis"/> (1 = unchanged).</summary>
    public static float Mult(StatAxis axis) => Cache.TryGetValue(axis, out var v) ? v : 1f;

    /// <summary>Sum of the active additive effects (extraction points).</summary>
    public static int Add(StatAxis axis) => Cache.TryGetValue(axis, out var v) ? Mathf.RoundToInt(v) : 0;
}
