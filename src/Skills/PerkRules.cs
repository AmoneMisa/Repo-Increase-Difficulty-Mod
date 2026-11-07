using System;

namespace IncreaseHardMod.Skills;

/// <summary>Pure numbers for the newer tree perks (no Unity), shared by the game and the tests.</summary>
public static class PerkRules
{
    /// <summary>
    /// Desperation: the share of a hit (0..1) taken off while the player's health before the hit is below
    /// <paramref name="threshold"/> of max. Never more than 90%.
    /// </summary>
    public static float DesperationReduction(int health, int maxHealth, int level, float perLevel, float threshold)
    {
        if (level <= 0 || maxHealth <= 0 || health <= 0) return 0f;
        if (!DesperationActive(health, maxHealth, threshold)) return 0f;
        return Math.Min(.9f, Math.Max(0f, perLevel) * level);
    }

    /// <summary>Whether Desperation applies at this health (for the HUD line).</summary>
    public static bool DesperationActive(int health, int maxHealth, float threshold) =>
        maxHealth > 0 && health > 0 && health < Math.Round(maxHealth * (double)Math.Max(0f, threshold), 3); // 0.3f * 100 is 30.000001

    /// <summary>Crouch Speed: crouch-walk speed multiplier for <paramref name="level"/> levels (1 + level x perLevel,
    /// so 3 x 5% = +15% at max). Never slower than normal.</summary>
    public static float CrouchSpeedFactor(int level, float perLevel) => 1f + Math.Max(0, level) * Math.Max(0f, perLevel);

    /// <summary>Damage after a reduction, rounded in the player's favour, never below 1 for a hit that did damage.</summary>
    public static int Reduce(int damage, float fraction)
    {
        if (damage <= 0 || fraction <= 0f) return damage;
        int reduced = (int)Math.Floor(damage * (1f - Math.Min(1f, fraction)));
        return Math.Max(1, reduced);
    }

    /// <summary>Heavy Hitter: a melee stun of <paramref name="baseSeconds"/> with the perk, capped at <paramref name="cap"/>.</summary>
    public static float MeleeStun(float baseSeconds, int level, float perLevel, float cap)
    {
        if (level <= 0 || baseSeconds <= 0f) return baseSeconds;
        float longer = baseSeconds + Math.Max(0f, perLevel) * level;
        return Math.Max(baseSeconds, Math.Min(longer, Math.Max(baseSeconds, cap)));
    }

    /// <summary>Battery Saver: the share of drain that still happens (1 = none saved). Never below 25%.</summary>
    public static float BatteryDrainMultiplier(int level, float perLevel)
    {
        if (level <= 0) return 1f;
        return Math.Max(.25f, 1f - Math.Max(0f, perLevel) * level);
    }

    /// <summary>Field Medic: what the user's own health pack heals.</summary>
    public static int MedicSelfHeal(int baseHeal, int level, float perLevel)
    {
        if (level <= 0 || baseHeal <= 0) return baseHeal;
        return (int)Math.Round(baseHeal * (1f + Math.Max(0f, perLevel) * level), MidpointRounding.AwayFromZero);
    }

    /// <summary>Field Medic: what each teammate near the user gets from the same pack (0 = nothing).</summary>
    public static int MedicShare(int baseHeal, int level, float perLevel)
    {
        if (level <= 0 || baseHeal <= 0) return 0;
        return Math.Max(1, (int)Math.Round(baseHeal * Math.Max(0f, perLevel) * level, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Lucky Finder: skill box weights with <paramref name="shiftPerLevel"/> weight per level moved out of the empty
    /// result into 1, 2 and 3 points (half, three tenths, the rest). The total stays the same.
    /// </summary>
    public static int[] LuckyWeights(int w0, int w1, int w2, int w3, int level, int shiftPerLevel)
    {
        w0 = Math.Max(0, w0); w1 = Math.Max(0, w1); w2 = Math.Max(0, w2); w3 = Math.Max(0, w3);
        if (level <= 0 || shiftPerLevel <= 0) return new[] { w0, w1, w2, w3 };
        int moved = Math.Min(w0, shiftPerLevel * level);
        int one = moved * 5 / 10, two = moved * 3 / 10, three = moved - one - two;
        return new[] { w0 - moved, w1 + one, w2 + two, w3 + three };
    }

    /// <summary>Lucky Finder: battery crystal charge in percent with the perk.</summary>
    public static float LuckyCrystalCharge(float basePercent, int level, float bonusPerLevel) =>
        basePercent + (level > 0 ? Math.Max(0f, bonusPerLevel) * level : 0f);
}
