using System;

namespace IncreaseHardMod.Economy;

/// <summary>Additional, cumulative moons after the four native moons. No Unity dependencies.</summary>
internal static class MoonRules
{
    internal const int First = 5, RandomSouls = 6, Resistant = 7;
    // Balance defaults for the probabilities and multiplier not specified in the design.
    internal const float SoulDanger = 2f, SoulChance = .5f, GuardChance = .15f,
        GuardCooldown = 75f, GuardDamage = .67f, StunResistChance = .1f;

    internal static float DamageFactor(int moon) => moon >= Resistant ? .9f : moon >= First ? .95f : 1f;
    internal static float CooldownFactor(int moon) => moon >= RandomSouls ? .8f : moon >= First ? .9f : 1f;
    internal static float StunFactor(int moon) => moon >= Resistant ? .6f : 1f;
    internal static float CooldownRemaining(float before, float after, int moon) =>
        before > 0f && after > 0f && after < before ? Math.Max(0f, before - (before - after) / CooldownFactor(moon)) : after;
    internal static bool SoulDrops(int moon, float roll) => moon < RandomSouls || roll < SoulChance;
    internal static bool GuardProcs(int moon, float now, float ready, float roll) =>
        moon >= Resistant && now >= ready && roll < GuardChance;
    internal static int Damage(int damage, int moon, bool guard) => damage <= 0 ? damage :
        Math.Max(1, (int)Math.Round(damage * DamageFactor(moon) * (guard ? GuardDamage : 1f), MidpointRounding.AwayFromZero));
    /// <summary>Last moon: a monster attack (not a damage-over-time tick) has a 5% chance to deal double damage.</summary>
    internal const float MonsterCritChance = .05f, MonsterCritMultiplier = 2f;
    internal const int MonsterCritMinDamage = 6; // DoT ticks arrive in chunks of at most 4-5 HP
    internal static bool MonsterCrits(int moon, int damage, double roll) =>
        moon >= Resistant && damage >= MonsterCritMinDamage && roll < MonsterCritChance;
    internal static int Strength(int tier) => tier <= 1 ? 3 : tier == 2 ? 5 : 7;
    // Native PhysGrabber: forceMax=4, base grabStrength=1, +.2 per Strength upgrade.
    internal static float GrabForce(int tier) => 4f * (1f + .2f * Strength(tier));
}
