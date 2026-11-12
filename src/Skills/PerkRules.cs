using System;

namespace IncreaseHardMod.Skills;

/// <summary>Pure numbers for the newer tree perks (no Unity), shared by the game and the tests.</summary>
public static class PerkRules
{
    internal const float FlameDamageCap = .30f, LaserDamageCap = .35f;
    internal static float CappedWeaponBonus(float configured, float cap) => Math.Min(Math.Max(0f, configured), cap);
    /// <summary>Wide View: degrees added to the camera's base field of view, +5 per rank, never more than +15.</summary>
    internal const float FovPerRank = 5f, FovCap = 15f;
    internal static float FovBonus(int rank) => Math.Min(FovCap, FovPerRank * Math.Clamp(rank, 0, 3));
    internal static float FireRate(int rank) => 1f + .25f * Math.Clamp(rank, 0, 3) / 3f;
    internal static float DurationResistance(int rank) => rank <= 0 ? 0f : rank == 1 ? .2f : rank == 2 ? .35f : .5f;
    internal static float SapperFactor(int rank) => rank <= 0 ? 1f : rank == 1 ? .85f : rank == 2 ? .75f : .65f;
    internal static float LinkSpeed(int rank) => 1f + .1f * Math.Clamp(rank, 0, 3);
    internal static float BasketLift(int rank) => 1f + .1f * Math.Clamp(rank, 0, 5);
    internal static float MedicRate(float configured) => Math.Clamp(configured, 0f, .12f);
    internal static int FinalMobDamage(int original, int reduced, bool monster, int dot)
        => original <= 0 ? reduced : dot > 0 ? dot : monster ? Math.Max(1, reduced) : reduced;
    internal static int ActualHitDamage(int before, int after) => Math.Max(0, Math.Max(0, before) - Math.Max(0, after));
    internal static float FreezeSeconds(float baseSeconds, int rank, bool root, float rootSeconds)
    {
        float cap = Math.Max(baseSeconds, 6f);
        return Math.Min(cap, baseSeconds + (cap - baseSeconds) * Math.Clamp(rank, 0, 3) / 3f +
            (root ? Math.Min(.25f, Math.Max(0, rootSeconds)) : 0f));
    }
    internal const float GunAccuracyPerRank = .12f, MeleeAlignmentPerRank = .25f, WhipRadiusPerRank = .1f;
    internal const float GrenadeRadiusPerRank = .1f, GrenadeDamagePerRank = .1f, GrenadeStunPerRank = .5f;
    internal const int GrenadeUtilityDamagePerRank = 5;

    internal static float GunInaccuracy(int rank) => 1f - GunAccuracyPerRank * rank;
    internal static float GrenadeRadius(int rank) => 1f + GrenadeRadiusPerRank * rank;
    internal static int GrenadeDamage(int damage, int rank) => damage > 0
        ? (int)Math.Round(damage * (1f + GrenadeDamagePerRank * rank), MidpointRounding.AwayFromZero)
        : GrenadeUtilityDamagePerRank * rank;

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

    /// <summary>Instinct buffs: 3, 4 and 5 seconds at levels 1-3.</summary>
    public static float BuffSeconds(int level) => 2f + Math.Max(1, Math.Min(3, level));

    /// <summary>Instinct cooldowns: 60, 45 and 30 seconds at levels 1-3 (from the moment the buff starts).</summary>
    public static float BuffCooldown(int level) => 75f - 15f * Math.Max(1, Math.Min(3, level));

    /// <summary>Mending Surge heals 5%, 10% or 15% of max health over 5 seconds.</summary>
    public static float SurgeShare(int level) => .05f * Math.Max(1, Math.Min(3, level));

    /// <summary>Vampirism: share of melee damage dealt returned as health, rising evenly to <paramref name="cap"/> at the
    /// last level (29% at 7 of 7 by default).</summary>
    public static float VampireShare(int level, int maxLevel, float cap) =>
        level <= 0 || maxLevel <= 0 ? 0f : Math.Max(0f, Math.Min(1f, cap)) * Math.Min(level, maxLevel) / maxLevel;

    /// <summary>Whole HP to heal now from <paramref name="damage"/> dealt; the fraction carries over in <paramref name="carry"/>.</summary>
    public static int VampireHeal(int damage, float share, ref float carry)
    {
        if (damage <= 0 || share <= 0f) return 0;
        carry += damage * share;
        int heal = (int)Math.Floor(carry + 1e-4f); // 10 x 2.9 must add up to 29, not 28.9999
        carry = Math.Max(0f, carry - heal);
        return heal;
    }

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
