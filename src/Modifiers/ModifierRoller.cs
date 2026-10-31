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

        // Build the pool of currently-enabled modifiers.
        var pool = new List<ModifierType>();
        foreach (var kv in ModConfig.ModifierEnabled)
        {
            if (kv.Value.Value && !Economy.BlackMarket.IsBanned(Economy.BlackMarket.Kind.Modifier, kv.Key.ToString()))
                pool.Add(kv.Key);
        }
        if (pool.Count == 0)
            return result;

        int min = Mathf.Max(1, ModConfig.ModifiersPerLevelMin.Value);
        int max = Mathf.Max(min, ModConfig.ModifiersPerLevelMax.Value);
        int count = Mathf.Min(Random.Range(min, max + 1), pool.Count);

        // Pick 'count' distinct modifiers by removing from the pool as we go.
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            ModifierType picked = pool[idx];
            pool.RemoveAt(idx);
            result.Active |= picked;

            // Remove anything that conflicts with what we just picked.
            RemoveConflicts(pool, picked);
        }

        RollParameters(result);
        return result;
    }

    /// <summary>Remove any pool entries that can't coexist with 'picked'.</summary>
    private static void RemoveConflicts(List<ModifierType> pool, ModifierType picked)
    {
        foreach (var group in ConflictGroups)
        {
            bool inGroup = System.Array.IndexOf(group, picked) >= 0;
            if (!inGroup)
                continue;

            foreach (var other in group)
            {
                if (other != picked)
                    pool.Remove(other);
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
