using System;
using System.Collections.Generic;

namespace IncreaseHardMod.Skills;

/// <summary>What a trade-off skill changes. Factors multiply (1.15 = +15%); <see cref="Fx.MeleeStunSeconds"/>,
/// <see cref="Fx.LuckyLevels"/> and <see cref="Fx.StatusCapSeconds"/> add instead.</summary>
public enum Fx : byte
{
    GunDamage,          // handheld guns, staff and crossbow
    CartDamage,         // cart cannon and cart laser
    MeleeDamage, GrenadeDamage, TumbleDamage,
    DamageDealt,        // everything above
    DamageTaken,
    StatusLength,       // tranquilizer, net, freeze and poison from your own weapons
    StatusCapSeconds,   // raises the 8 s status cap to this (0 = unchanged)
    StunLength,         // melee and tranquilizer stuns
    MeleeStunSeconds,   // extra seconds on melee stuns
    GrabHold,           // how long you can hold a monster before the grab beam overloads
    GrabStrength,
    BatteryItems,       // drain of battery items that are not guns or drones
    BatteryGuns, BatteryDrones,
    WalkSpeed, SprintSpeed, CrouchSpeed,
    SprintStamina,      // stamina spent while sprinting
    MaxStamina,
    RegenRate,          // HP Regeneration speed
    SelfHeal,           // health packs you use, on you
    MedicShare,         // Field Medic's share for teammates
    FlashWidth, FlashRange, FlashBright,
    AutoscanReach,
    Noise,              // how far monsters hear you
    LuckyLevels,        // extra Lucky Finder levels for skill boxes
    CrystalCharge,
    LootBreakLoss,      // value lost when loot you hold breaks
    HoldingLootSpeed,   // movement while holding loot
    StunnedTargetDamage, OtherTargetDamage, // Executioner
}

/// <summary>One trade-off skill: an upside and a downside, both as effects, plus the words for the tree screen.</summary>
public sealed class TradeOff
{
    public string Id;
    public int Tier;
    public (Fx Fx, float Value)[] Effects;
    public string Up, Down;
    /// <summary>Russian lines, shown when the game is set to Russian.</summary>
    public string UpRu, DownRu;
    /// <summary>Behaviour that is not a number (Last Stand, Echo Ping, Oracle, Hunter's Eye).</summary>
    public bool Special;
}

/// <summary>
/// Engine-free rules for the trade-off skills (no Unity), shared by the game and tests/SkillTreeTests.
/// Each page has three tiers; a tier opens once enough points are spent in that page's normal perks, and tiers II
/// and III also need the tier below picked. You hold one skill per tier per page. Tier N costs N points, one level.
/// </summary>
public static class TradeOffRules
{
    public const int Tiers = 3;

    /// <summary>Points spent in a page's normal perks needed for each tier (about 30%, 60% and 85% of the page).</summary>
    internal static int Gate(SkillPage page, int tier)
    {
        if (tier < 1 || tier > Tiers) return int.MaxValue;
        int[] gates = page switch
        {
            SkillPage.Recon => new[] { 8, 16, 24 },
            SkillPage.Combat => new[] { 12, 24, 36 },
            SkillPage.Survival => new[] { 10, 20, 30 },
            SkillPage.Utility => new[] { 9, 18, 27 },
            SkillPage.Arsenal => new[] { 12, 24, 36 },
            _ => null,
        };
        return gates == null ? int.MaxValue : gates[tier - 1];
    }

    internal static string Branch(int tier) => "TradeOffs" + tier;

    private static readonly Dictionary<string, TradeOff> ById = new(StringComparer.Ordinal);
    internal static IEnumerable<TradeOff> All => ById.Values;
    internal static TradeOff Find(string id) => id != null && ById.TryGetValue(id, out var t) ? t : null;

    internal static TradeOff Define(string id, int tier, string up, string down, params (Fx, float)[] effects)
    {
        var t = new TradeOff { Id = id, Tier = tier, Up = up, Down = down, Effects = effects };
        ById[id] = t;
        return t;
    }

    /// <summary>Combined multiplier of <paramref name="fx"/> over the owned trade-offs (1 when none apply).</summary>
    public static float Factor(Func<string, bool> owned, Fx fx)
    {
        float factor = 1f;
        foreach (var t in ById.Values)
        {
            if (!owned(t.Id)) continue;
            foreach (var (kind, value) in t.Effects)
                if (kind == fx) factor *= value;
        }
        return Math.Max(0.05f, factor);
    }

    /// <summary>Sum of an additive effect over the owned trade-offs (0 when none apply).</summary>
    public static float Sum(Func<string, bool> owned, Fx fx)
    {
        float sum = 0f;
        foreach (var t in ById.Values)
        {
            if (!owned(t.Id)) continue;
            foreach (var (kind, value) in t.Effects)
                if (kind == fx) sum += value;
        }
        return sum;
    }

    /// <summary>Status length with the trade-offs applied: longer up to the raised cap (never cutting one that was
    /// already longer), shorter by the factor.</summary>
    public static float StatusSeconds(float seconds, float factor, float cap)
    {
        if (seconds <= 0f || Math.Abs(factor - 1f) < 1e-4f) return seconds;
        if (factor < 1f) return seconds * factor;
        return Math.Max(seconds, Math.Min(seconds * factor, cap));
    }
}
