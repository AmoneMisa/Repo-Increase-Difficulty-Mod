using System;

namespace IncreaseHardMod.Monsters;

public enum EliteTrait : byte { None, Armored, Regenerating, Thorned }

/// <summary>
/// Engine-free rules for elite monsters (shared with tests/MonsterTests). Once the depth ramp has maxed monster HP,
/// plain stat growth stops being interesting, so deep levels give some monsters one elite trait instead.
/// The roll is a pure function of the monster's network view id and the run depth, so every client works out the
/// same trait with no extra messages; only the host applies the effects (it runs monster damage).
/// </summary>
public static class EliteRules
{
    /// <summary>Completed levels before elites appear: the default depth ramp maxes monster HP at 18.</summary>
    public const int StartLevel = 16;
    public const float BaseChance = .10f, ChancePerLevel = .03f, MaxChance = .40f;

    public const float ArmoredDamageFactor = .7f;      // until its first real stun
    public const float RegenDelaySeconds = 5f, RegenShareCap = 1f;
    public const float RegenPerSecond = .015f;         // share of max HP per second
    public const float ThornShare = .10f;              // melee damage reflected

    public static float Chance(int levelsCompleted) =>
        levelsCompleted < StartLevel ? 0f : Math.Min(MaxChance, BaseChance + ChancePerLevel * (levelsCompleted - StartLevel));

    public static EliteTrait Roll(int viewId, int levelsCompleted)
    {
        float chance = Chance(levelsCompleted);
        if (chance <= 0f || viewId <= 0) return EliteTrait.None;
        uint h = Hash((uint)viewId, (uint)levelsCompleted);
        float roll = (h & 0xFFFF) / 65536f;
        if (roll >= chance) return EliteTrait.None;
        return (EliteTrait)(1 + (int)((h >> 16) % 3));
    }

    /// <summary>Whole HP healed this frame, carrying the fraction so slow regeneration still adds up.</summary>
    public static int Regen(int maxHealth, float dt, float sinceHurt, ref float carry)
    {
        if (maxHealth <= 0 || dt <= 0f || sinceHurt < RegenDelaySeconds) return 0;
        carry += maxHealth * RegenPerSecond * dt;
        int heal = (int)Math.Floor(carry);
        carry -= heal;
        return heal;
    }

    public static int ArmoredDamage(int damage, bool broken) =>
        broken || damage <= 0 ? damage : Math.Max(1, (int)Math.Round(damage * ArmoredDamageFactor));

    public static string Name(EliteTrait trait, bool russian) => trait switch
    {
        EliteTrait.Armored => russian ? "Элита: броня" : "Elite: Armored",
        EliteTrait.Regenerating => russian ? "Элита: регенерация" : "Elite: Regenerating",
        EliteTrait.Thorned => russian ? "Элита: шипы" : "Elite: Thorned",
        _ => "",
    };

    // Small integer mix (no System.Random: its sequence is not guaranteed across runtimes).
    private static uint Hash(uint a, uint b)
    {
        uint h = a * 0x9E3779B1u ^ (b + 0x7F4A7C15u) * 0x85EBCA77u;
        h ^= h >> 15; h *= 0x2C1B3C6Du;
        h ^= h >> 12; h *= 0x297A2D39u;
        h ^= h >> 15;
        return h;
    }
}
