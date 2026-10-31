using System.Collections.Generic;
using UnityEngine;

namespace IncreaseHardMod.Modifiers;

/// <summary>
/// Host-only logic that decides which surprises a level gets. Runs ONLY on the
/// host/singleplayer; the result is then broadcast so everyone shares it.
///
/// Rolling steps:
///   1. Is the surprise system on and is this level eligible (deep enough)?
///   2. Roll the "special level" chance. If it fails, return an empty set.
///   3. Pick N distinct modifiers (N between the configured min/max) from the
///      pool of modifiers the player left enabled.
///   4. Roll any numeric parameters the chosen modifiers need.
/// </summary>
public static class ModifierRoller
{
    /// <summary>
    /// Sets of mutually-exclusive modifiers. If one from a group is already
    /// picked, the others in that group are removed from the pool so we never
    /// roll a contradiction (e.g. loot that's both heavier and lighter).
    /// </summary>
    private static readonly ModifierType[][] ConflictGroups =
    {
        new[] { ModifierType.HeavyLoot, ModifierType.LightLoot },
        new[] { ModifierType.FragileLoot, ModifierType.ProtectedLoot },
        new[] { ModifierType.LightMonsters, ModifierType.HeavyMonsters },
    };


    public static LevelModifiers Roll(int levelsCompleted)
    {
        var result = LevelModifiers.Empty;

        if (!ModConfig.SurprisesEnabled.Value)
            return result;

        // Not deep enough yet for special levels.
        if (levelsCompleted < ModConfig.SurprisesStartLevel.Value)
            return result;

        // Chance for this level to be special at all.
        if (Random.Range(0, 100) >= ModConfig.SpecialLevelChancePercent.Value)
            return result;

        // Build the pool of currently-enabled modifiers: the original flag modifiers plus the stat modifiers,
        // minus anything blocked at the Black Market.
        var pool = new List<Entry>();
        foreach (var kv in ModConfig.ModifierEnabled)
        {
            if (kv.Value.Value && !Economy.BlackMarket.IsBanned(Economy.BlackMarket.Kind.Modifier, kv.Key.ToString()))
                pool.Add(new Entry { Flag = kv.Key, Tier = Economy.BlackMarket.TierOfModifier(kv.Key) });
        }
        foreach (var s in StatModifiers.All)
        {
            if (ModConfig.StatModifierEnabled.TryGetValue(s.Id, out var on) && on.Value &&
                !Economy.BlackMarket.IsBanned(Economy.BlackMarket.Kind.Modifier, "stat:" + s.Id))
                pool.Add(new Entry { Stat = s, Tier = s.Tier });
        }
        if (pool.Count == 0)
            return result;
        var stats = new List<StatModifier>();

        int min = Mathf.Max(1, ModConfig.ModifiersPerLevelMin.Value);
        int max = Mathf.Max(min, ModConfig.ModifiersPerLevelMax.Value);
        int count = Mathf.Min(Random.Range(min, max + 1), pool.Count);

        // Pick 'count' distinct modifiers, weighted by tier and run depth, removing conflicts as we go.
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            var picked = PickWeighted(pool);
            pool.Remove(picked);
            if (picked.Stat != null)
                stats.Add(picked.Stat);
            else
            {
                result.Active |= picked.Flag;
                RemoveConflicts(pool, picked.Flag);
            }
            pool.RemoveAll(e => Clashes(e, result, stats));
        }

        result.Stats = stats.ConvertAll(s => s.Id).ToArray();
        RollParameters(result);
        return result;
    }

    private sealed class Entry
    {
        public ModifierType Flag;
        public StatModifier Stat;
        public Economy.EconomyTier Tier;
    }

    // Deeper runs lean toward the nasty tiers and away from the generous ones (same tilt as the economy events).
    private static Entry PickWeighted(List<Entry> pool)
    {
        float bad = Mathf.Clamp(1f + 0.06f * Difficulty.Steps, 1f, 2.5f);
        float good = 1f / Mathf.Sqrt(bad);
        float Weight(Economy.EconomyTier t) => t switch
        {
            Economy.EconomyTier.VeryGood => 2f * good,
            Economy.EconomyTier.Good => 5f * good,
            Economy.EconomyTier.Mixed => 4f,
            Economy.EconomyTier.Bad => 5f * bad,
            _ => 2f * bad,
        };

        float total = 0f;
        foreach (var e in pool)
            total += Weight(e.Tier);
        float roll = Random.value * total;
        foreach (var e in pool)
        {
            roll -= Weight(e.Tier);
            if (roll <= 0f)
                return e;
        }
        return pool[pool.Count - 1];
    }

    private static bool Pushes(StatModifier s, StatAxis axis, bool up) =>
        s.Effects.TryGetValue(axis, out var v) && (up ? v > 1.2f : v < 0.9f);

    // A candidate is out if it turns an axis a picked stat modifier already turns, or if it piles a second
    // punishment onto the lever one of the original flag modifiers already hits.
    private static bool Clashes(Entry e, LevelModifiers picked, List<StatModifier> stats)
    {
        if (e.Stat != null)
        {
            foreach (var s in stats)
                foreach (var axis in e.Stat.Effects.Keys)
                    if (s.Effects.ContainsKey(axis))
                        return true;
            if (picked.Has(ModifierType.LevelTimer) && Pushes(e.Stat, StatAxis.Quota, true)) return true;
            if (picked.Has(ModifierType.ReducedPlayerHp) && Pushes(e.Stat, StatAxis.PlayerDamage, true)) return true;
            if (picked.Has(ModifierType.FragileLoot) && Pushes(e.Stat, StatAxis.LootValue, false)) return true;
            return false;
        }

        foreach (var s in stats)
        {
            if (e.Flag == ModifierType.LevelTimer && Pushes(s, StatAxis.Quota, true)) return true;
            if (e.Flag == ModifierType.ReducedPlayerHp && Pushes(s, StatAxis.PlayerDamage, true)) return true;
            if (e.Flag == ModifierType.FragileLoot && Pushes(s, StatAxis.LootValue, false)) return true;
        }
        return false;
    }

    /// <summary>Remove any pool entries that can't coexist with 'picked'.</summary>
    private static void RemoveConflicts(List<Entry> pool, ModifierType picked)
    {
        foreach (var group in ConflictGroups)
        {
            bool inGroup = System.Array.IndexOf(group, picked) >= 0;
            if (!inGroup)
                continue;

            foreach (var other in group)
            {
                if (other != picked)
                    pool.RemoveAll(e => e.Stat == null && e.Flag == other);
            }
        }
    }

    /// <summary>Fill in numeric parameters for whichever modifiers were chosen.</summary>
    private static void RollParameters(LevelModifiers m)
    {
        if (m.Has(ModifierType.ExtraSouls))
            m.ExtraSouls = Random.Range(4, 11); // 4-10 inclusive

        if (m.Has(ModifierType.ReducedPlayerHp))
        {
            // 10-90% in 10% steps.
            m.PlayerHpPercent = Random.Range(1, 10) * 10;
        }

        if (m.Has(ModifierType.MoneyLossOnDeath))
            m.MoneyLossPercent = Random.Range(5, 26); // lose 5-25% per death
    }
}
