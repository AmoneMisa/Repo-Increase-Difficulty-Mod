using IncreaseHardMod.Networking;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Host-authoritative state for the cart/truck upgrade shop (Route A: pure code, no
/// custom Unity assets). Three upgrades:
///   • Heal Power        — leveled, scales all incoming healing.
///   • Charge Efficiency — leveled, slows the truck charging station's energy drain.
///   • Loot Cash-In      — one-time unlock: sell leftover valuables once quotas close.
///
/// The HOST owns and mutates this (spending shared run currency); clients only ever
/// receive it. Levels persist for the whole run and reset on RunManager.ResetProgress.
/// State is synced via ModNetwork event code 180. Every apply funnels through
/// <see cref="SetState"/> so derived multipliers and logging happen once, and the
/// charge-drain scale is re-applied to a live ChargingStation if one exists.
/// </summary>
public static class CartUpgradeState
{
    public static int HealLevel { get; private set; }
    public static int ChargeLevel { get; private set; }
    public static bool SellUnlocked { get; private set; }

    /// <summary>All-healing multiplier derived from the Heal Power level (>= 1).</summary>
    public static float HealMultiplier =>
        1f + HealLevel * Mathf.Max(0f, ModConfig.CartHealPercentPerLevel.Value);

    /// <summary>Charging-station drain multiplier derived from Charge Efficiency (0.05..1).</summary>
    public static float ChargeDrainMultiplier =>
        Mathf.Clamp(1f - ChargeLevel * Mathf.Max(0f, ModConfig.CartChargePercentPerLevel.Value), 0.05f, 1f);

    /// <summary>Cost of the NEXT level given how many levels are already owned.</summary>
    public static int NextCost(int baseCost, int levelsOwned)
    {
        float growth = Mathf.Pow(Mathf.Max(1f, ModConfig.CartUpgradeCostGrowth.Value), levelsOwned);
        return Mathf.RoundToInt(baseCost * growth);
    }

    public static int HealNextCost => NextCost(ModConfig.CartHealBaseCost.Value, HealLevel);
    public static int ChargeNextCost => NextCost(ModConfig.CartChargeBaseCost.Value, ChargeLevel);

    public static bool HealMaxed => HealLevel >= ModConfig.CartHealMaxLevel.Value;
    public static bool ChargeMaxed => ChargeLevel >= ModConfig.CartChargeMaxLevel.Value;

    /// <summary>Reset at the start of a new run (levels do not carry between runs).</summary>
    public static void ResetForNewRun()
    {
        if (HealLevel == 0 && ChargeLevel == 0 && !SellUnlocked)
            return;
        SetState(0, 0, false, "new run");
    }

    /// <summary>Applied on clients when the host's broadcast arrives.</summary>
    public static void ApplyNetworkState(int heal, int charge, bool sell)
    {
        SetState(heal, charge, sell, "received from host");
    }

    private static void SetState(int heal, int charge, bool sell, string source)
    {
        HealLevel = Mathf.Max(0, heal);
        ChargeLevel = Mathf.Max(0, charge);
        SellUnlocked = sell;
        // Charge efficiency scales a value computed once in ChargingStation.Start, so
        // re-apply it from the cached base whenever the level changes.
        ChargeEfficiencyEffect.Reapply();
        Plugin.Log.LogInfo($"[CartUpgrades] ({source}) Heal={HealLevel} Charge={ChargeLevel} Sell={SellUnlocked}");
    }

    public static void Broadcast()
    {
        ModNetwork.Instance?.BroadcastCartUpgrades(HealLevel, ChargeLevel, SellUnlocked);
    }

    // --- Host purchase actions (spend shared run currency) ---

    public static bool TryBuyHeal() => TryBuy(HealMaxed, HealNextCost,
        () => SetState(HealLevel + 1, ChargeLevel, SellUnlocked, "host bought Heal Power"));

    public static bool TryBuyCharge() => TryBuy(ChargeMaxed, ChargeNextCost,
        () => SetState(HealLevel, ChargeLevel + 1, SellUnlocked, "host bought Charge Efficiency"));

    public static bool TryBuySell() => TryBuy(SellUnlocked, ModConfig.CartSellCost.Value,
        () => SetState(HealLevel, ChargeLevel, true, "host bought Loot Cash-In"));

    private static bool TryBuy(bool alreadyMaxed, int cost, System.Action apply)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || alreadyMaxed)
            return false;
        if (SemiFunc.StatGetRunCurrency() < cost)
            return false;

        SemiFunc.StatSetRunCurrency(SemiFunc.StatGetRunCurrency() - cost);
        apply();
        Broadcast();
        return true;
    }

    // --- Loot Cash-In (host action) ---

    /// <summary>
    /// True only once the upgrade is owned AND every extraction quota on the level is
    /// closed — that's when leftover loot is otherwise wasted, so cashing it in is safe.
    /// </summary>
    public static bool CanSellNow()
    {
        return SellUnlocked
            && SemiFunc.IsMasterClientOrSingleplayer()
            && RoundDirector.instance != null
            && RoundDirector.instance.allExtractionPointsCompleted;
    }

    /// <summary>
    /// Cash in every remaining valuable for CartSellFraction of its value, zeroing each
    /// so it can't be extracted or sold again. Host-only; shared run currency syncs
    /// through SemiFunc.StatSetRunCurrency. Returns the currency credited.
    /// </summary>
    public static int SellRemainingLoot()
    {
        if (!CanSellNow())
            return 0;

        float fraction = Mathf.Clamp01(ModConfig.CartSellFraction.Value);
        float total = 0f;
        foreach (var v in Object.FindObjectsOfType<ValuableObject>())
        {
            if (v.dollarValueCurrent > 0f)
            {
                total += v.dollarValueCurrent;
                v.dollarValueCurrent = 0f;
            }
        }

        int credit = Mathf.RoundToInt(total * fraction);
        if (credit > 0)
            SemiFunc.StatSetRunCurrency(SemiFunc.StatGetRunCurrency() + credit);
        Plugin.Log.LogInfo($"[CartUpgrades] Loot Cash-In sold leftovers worth {total:0} for {credit} currency.");
        return credit;
    }
}
