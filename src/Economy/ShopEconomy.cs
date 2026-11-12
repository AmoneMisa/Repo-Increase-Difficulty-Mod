using System.Collections.Generic;
using HarmonyLib;
using IncreaseHardMod.Networking;
using UnityEngine;

namespace IncreaseHardMod.Economy;

/// <summary>A shop-wide price event: one multiplier per price channel of the game's shop.</summary>
internal sealed class ShopEconomyEvent
{
    public int Id;
    public string Name;
    public string Description;
    public EconomyTier Tier;
    public float Item = 1f;      // weapons, drones, orbs, carts, grenades, mines, trackers, this mod's gear
    public float Upgrade = 1f;   // player upgrades, including this mod's personal and truck upgrades
    public float Health = 1f;    // health packs
    public float Crystal = 1f;   // power crystals (truck charge)
}

/// <summary>
/// Shop price events. When the run enters a shop the host rolls one (or none) and broadcasts its id; the price
/// channels are then scaled where the game computes them: upgrades / health packs / crystals through the
/// ShopManager value functions, everything else through ItemAttributes.GetValue (shared with the player-count
/// price scaling in <see cref="Patches.Effects.ShopPriceScaleEffect"/>). Because this mod's upgrades and
/// gear are ordinary shop items, they follow the same channels with no special handling.
/// </summary>
internal static class ShopEconomy
{
    public static readonly List<ShopEconomyEvent> All = new();

    private static void Add(string name, EconomyTier tier, float item, float upgrade, float health, float crystal, string desc)
    {
        All.Add(new ShopEconomyEvent
        {
            Id = All.Count + 1, Name = name, Tier = tier, Item = item, Upgrade = upgrade, Health = health, Crystal = crystal, Description = desc,
        });
    }

    static ShopEconomy()
    {
        Add("Black Friday", EconomyTier.VeryGood, .55f, .55f, .55f, .55f, "Everything is 45% off.");
        Add("Grand Opening", EconomyTier.VeryGood, .60f, .60f, .70f, .70f, "Opening-week prices: 30-40% off.");
        Add("Weekend Sale", EconomyTier.Good, .80f, 1f, 1f, 1f, "Items are 20% off.");
        Add("Upgrade Discount", EconomyTier.Good, 1f, .60f, 1f, 1f, "Upgrades are 40% off.");
        Add("Medic Sale", EconomyTier.Good, 1f, 1f, .50f, 1f, "Health packs are half price.");
        Add("Crystal Clearance", EconomyTier.Good, 1f, 1f, 1f, .60f, "Power crystals are 40% off.");
        Add("Loyalty Program", EconomyTier.Good, .85f, .85f, .85f, .85f, "Everything is 15% off.");
        Add("Boutique Day", EconomyTier.Mixed, 1.30f, .70f, 1f, 1f, "Items cost 30% more, upgrades are 30% off.");
        Add("Field Hospital", EconomyTier.Mixed, 1f, 1f, .60f, 1.40f, "Health packs are 40% off, crystals cost 40% more.");
        Add("Arms Race", EconomyTier.Mixed, .75f, 1.30f, 1f, 1f, "Items are 25% off, upgrades cost 30% more.");
        Add("Supply Run", EconomyTier.Mixed, 1.20f, 1f, 1f, .70f, "Crystals are 30% off, items cost 20% more.");
        Add("Inflation", EconomyTier.Bad, 1.30f, 1.30f, 1.30f, 1.30f, "Everything costs 30% more.");
        Add("Import Tax", EconomyTier.Bad, 1.25f, 1f, 1f, 1f, "Items cost 25% more.");
        Add("Luxury Tax", EconomyTier.Bad, 1.40f, 1f, 1f, 1f, "Items cost 40% more.");
        Add("Upgrade Surcharge", EconomyTier.Bad, 1f, 1.40f, 1f, 1f, "Upgrades cost 40% more.");
        Add("Medical Shortage", EconomyTier.Bad, 1f, 1f, 1.50f, 1f, "Health packs cost 50% more.");
        Add("Crystal Shortage", EconomyTier.Bad, 1f, 1f, 1f, 1.50f, "Power crystals cost 50% more.");
        Add("Market Manipulation", EconomyTier.VeryBad, 1.60f, 1.60f, 1.60f, 1.60f, "Everything costs 60% more.");
        Add("Hyper Inflation", EconomyTier.VeryBad, 1.90f, 1.70f, 1.70f, 1.70f, "Prices are 70-90% higher.");
        Add("Security Tax", EconomyTier.VeryBad, 1.50f, 1.50f, 1f, 1f, "Items and upgrades cost 50% more.");
    }

    public static ShopEconomyEvent Current { get; private set; }

    private static bool On => ModConfig.ShopEconomyEnabled.Value && Current != null;
    public static float ItemMult => On ? Current.Item : 1f;
    public static float UpgradeMult => On ? Current.Upgrade : 1f;
    public static float HealthMult => On ? Current.Health : 1f;
    public static float CrystalMult => On ? Current.Crystal : 1f;

    public static void Set(int id)
    {
        Current = id > 0 && id <= All.Count ? All[id - 1] : null;
        if (Current != null)
            Plugin.Log.LogInfo($"[ShopEconomy] {Current.Name}: {Current.Description}");
    }

    public static void RollForShop()
    {
        int id = 0;
        if (ModConfig.ShopEconomyEnabled.Value && Random.Range(0, 100) < ModConfig.ShopEconomyChance.Value)
            id = Pick();
        Set(id);
        ModNetwork.Instance?.BroadcastShopEconomy(id);
    }

    public static void Clear()
    {
        Set(0);
        if (SemiFunc.IsMasterClientOrSingleplayer())
            ModNetwork.Instance?.BroadcastShopEconomy(0);
    }

    private static int Pick()
    {
        // Same depth tilt as the level economy: deeper runs see more bad markets.
        float bad = Mathf.Clamp(1f + 0.06f * Difficulty.Steps, 1f, 2.5f);
        float good = 1f / Mathf.Sqrt(bad);
        float Weight(EconomyTier t) => t switch
        {
            EconomyTier.VeryGood => 1.5f * good,
            EconomyTier.Good => 5f * good,
            EconomyTier.Mixed => 3f,
            EconomyTier.Bad => 5f * bad,
            _ => 1.5f * bad,
        };

        float total = 0f;
        foreach (var e in All)
            if (!BlackMarket.IsBanned(BlackMarket.Kind.ShopEconomy, e.Id.ToString()))
                total += Weight(e.Tier);
        if (total <= 0f)
            return 0;
        float roll = Random.value * total;
        foreach (var e in All)
        {
            if (BlackMarket.IsBanned(BlackMarket.Kind.ShopEconomy, e.Id.ToString()))
                continue;
            roll -= Weight(e.Tier);
            if (roll <= 0f)
                return e.Id;
        }
        return 0;
    }

    // ---- price channels the game computes through ShopManager -----------------------------------

    [HarmonyPatch(typeof(ShopManager), nameof(ShopManager.UpgradeValueGet))]
    private static class UpgradePrice
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result) => __result = Mathf.Ceil(__result * UpgradeMult);
    }

    [HarmonyPatch(typeof(ShopManager), nameof(ShopManager.HealthPackValueGet))]
    private static class HealthPrice
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result) => __result = Mathf.Ceil(__result * HealthMult);
    }

    [HarmonyPatch(typeof(ShopManager), nameof(ShopManager.CrystalValueGet))]
    private static class CrystalPrice
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result) => __result = Mathf.Ceil(__result * CrystalMult);
    }
}
