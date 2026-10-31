using System.Collections.Generic;
using HarmonyLib;
using IncreaseHardMod.Networking;
using UnityEngine;

namespace IncreaseHardMod.Economy;

internal enum EconomyTier
{
    VeryGood,
    Good,
    Mixed,
    Bad,
    VeryBad,
}

/// <summary>One level-wide economy event: multipliers on loot value, the quota and the cash an extraction pays.</summary>
internal sealed class EconomyEvent
{
    public int Id;
    public string Name;
    public string Description;
    public EconomyTier Tier;
    public float Loot = 1f;   // value of every valuable on the level
    public float Quota = 1f;  // extraction quota
    public float Pay = 1f;    // currency paid out by a completed extraction
}

/// <summary>
/// Per-level economy events (taxes, bonuses, rich/poor loot). The host rolls at most one event per level,
/// the id is broadcast like the surprise modifiers, and each client applies the same multipliers:
///   * Loot  - <see cref="LevelScalingPatches"/> scales valuables as they are valued (host; synced by the game),
///   * Quota - folded into <see cref="Patches.QuotaPatch"/> (host),
///   * Pay   - applied to the currency an extraction adds (host).
/// Bad events become likelier with depth, good ones rarer, using the same ramp as the difficulty scaling.
/// </summary>
internal static class EconomyEvents
{
    public static readonly List<EconomyEvent> All = new();

    private static void Add(string name, EconomyTier tier, float loot, float quota, float pay, string desc)
    {
        All.Add(new EconomyEvent { Id = All.Count + 1, Name = name, Tier = tier, Loot = loot, Quota = quota, Pay = pay, Description = desc });
    }

    static EconomyEvents()
    {
        // Very good
        Add("Gold Rush", EconomyTier.VeryGood, 1.35f, 1f, 1f, "Loot is worth 35% more.");
        Add("Windfall", EconomyTier.VeryGood, 1f, 1f, 1.30f, "Extractions pay 30% more.");
        Add("Grand Contract", EconomyTier.VeryGood, 1.15f, 0.85f, 1.15f, "More valuable loot, a lighter quota and better pay.");
        // Good
        Add("Rich Vein", EconomyTier.Good, 1.15f, 1f, 1f, "Loot is worth 15% more.");
        Add("Light Quota", EconomyTier.Good, 1f, 0.75f, 1f, "The quota is 25% lower.");
        Add("Modest Quota", EconomyTier.Good, 1f, 0.85f, 1f, "The quota is 15% lower.");
        Add("Generous Client", EconomyTier.Good, 1f, 1f, 1.15f, "Extractions pay 15% more.");
        Add("Tax Break", EconomyTier.Good, 1f, 1f, 1.10f, "A small rebate: extractions pay 10% more.");
        // Mixed
        Add("High Stakes", EconomyTier.Mixed, 1.30f, 1.30f, 1f, "Loot is worth 30% more, but the quota is 30% higher.");
        Add("Fire Sale", EconomyTier.Mixed, 0.80f, 0.80f, 1f, "Loot is worth 20% less, and so is the quota.");
        Add("Hazard Pay", EconomyTier.Mixed, 1f, 1.15f, 1.25f, "Extractions pay 25% more, the quota is 15% higher.");
        Add("Budget Cuts", EconomyTier.Mixed, 1f, 0.85f, 0.80f, "Extractions pay 20% less, the quota is 15% lower.");
        Add("Inflated Loot", EconomyTier.Mixed, 1.20f, 1f, 0.80f, "Loot is worth 20% more, extractions pay 20% less.");
        // Bad
        Add("Tax Season", EconomyTier.Bad, 0.85f, 1f, 1f, "Loot is worth 15% less.");
        Add("Heavy Quota", EconomyTier.Bad, 1f, 1.25f, 1f, "The quota is 25% higher.");
        Add("Tight Budget", EconomyTier.Bad, 1f, 1f, 0.85f, "Extractions pay 15% less.");
        Add("Poor Facility", EconomyTier.Bad, 1f, 1f, 0.70f, "Extractions pay 30% less.");
        // Very bad
        Add("Bankruptcy", EconomyTier.VeryBad, 0.65f, 1f, 1f, "Loot is worth 35% less.");
        Add("Tax Audit", EconomyTier.VeryBad, 1f, 1.10f, 0.75f, "Extractions pay 25% less and the quota is 10% higher.");
        Add("Crushing Quota", EconomyTier.VeryBad, 1f, 1.40f, 1f, "The quota is 40% higher.");
    }

    /// <summary>The event active on the current level, or null.</summary>
    public static EconomyEvent Current { get; private set; }

    public static float LootMult => ModConfig.EconomyEventsEnabled.Value && Current != null ? Current.Loot : 1f;
    public static float QuotaMult => ModConfig.EconomyEventsEnabled.Value && Current != null ? Current.Quota : 1f;
    public static float PayMult => ModConfig.EconomyEventsEnabled.Value && Current != null ? Current.Pay : 1f;

    public static void Set(int id)
    {
        Current = id > 0 && id <= All.Count ? All[id - 1] : null;
        if (Current != null)
            Plugin.Log.LogInfo($"[Economy] {Current.Name}: {Current.Description}");
    }

    /// <summary>Host: roll for the level that is about to start and tell everyone.</summary>
    public static void RollForNextLevel(int levelsCompleted)
    {
        int id = 0;
        if (ModConfig.EconomyEventsEnabled.Value &&
            levelsCompleted >= ModConfig.EconomyStartLevel.Value &&
            Random.Range(0, 100) < ModConfig.EconomyEventChance.Value)
            id = PickWeighted();
        Set(id);
        ModNetwork.Instance?.BroadcastEconomy(id);
    }

    public static void Clear()
    {
        Set(0);
        if (SemiFunc.IsMasterClientOrSingleplayer())
            ModNetwork.Instance?.BroadcastEconomy(0);
    }

    private static int PickWeighted()
    {
        // Depth tilts the odds: more bad events, fewer good ones.
        float bad = Mathf.Clamp(1f + 0.06f * Difficulty.Steps, 1f, 2.5f);
        float good = 1f / Mathf.Sqrt(bad);
        float Weight(EconomyTier t) => t switch
        {
            EconomyTier.VeryGood => 2f * good,
            EconomyTier.Good => 5f * good,
            EconomyTier.Mixed => 4f,
            EconomyTier.Bad => 5f * bad,
            _ => 2f * bad,
        };

        float total = 0f;
        foreach (var e in All)
            total += Weight(e.Tier);
        float roll = Random.value * total;
        foreach (var e in All)
        {
            roll -= Weight(e.Tier);
            if (roll <= 0f)
                return e.Id;
        }
        return All[All.Count - 1].Id;
    }

    // Extraction payouts (the only way currency rises while inside a level): scale the increase.
    [HarmonyPatch(typeof(SemiFunc), "StatSetRunCurrency")]
    private static class PayPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref int value)
        {
            float mult = PayMult;
            if (mult == 1f || !SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel())
                return;
            int current = SemiFunc.StatGetRunCurrency();
            if (value > current)
                value = current + Mathf.Max(0, Mathf.RoundToInt((value - current) * mult));
        }
    }
}
