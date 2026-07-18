using UnityEngine;
using RepoUpgrades = REPOLib.Modules.Upgrades;

namespace IncreaseHardMod.Upgrades;

/// <summary>
/// Truck upgrades — bought in the normal shop like any other upgrade, but their benefit is
/// TEAM-WIDE: once anyone owns a level, the whole squad gets the effect. There is no separate
/// menu; the effects apply themselves (the heal/charge patches read the current team level, and
/// Loot Cash-In auto-triggers), so buying a level in the shop is all that's needed.
///
/// Because REPOLib tracks upgrade levels PER player, "team level" is defined as the highest level
/// owned across every player in the run (<see cref="TeamLevel"/>). That way one buyer lifts the
/// effect for everyone, matching the old shared-cart behaviour without any custom networking —
/// REPOLib already syncs and saves each player's level.
///
///   • Heal Power        — leveled: scales ALL healing (truck final-heal, medkits, drones…).
///   • Charge Efficiency — leveled: slows the truck charging station's energy drain.
///   • Loot Cash-In      — one-time unlock: leftover valuables are auto-cashed (for
///                          <see cref="ModConfig.CartSellFraction"/>) once every quota closes.
/// </summary>
internal static class TruckUpgrades
{
    public const string HealId = "IHM_TruckHeal";
    public const string ChargeId = "IHM_TruckCharge";
    public const string SellId = "IHM_TruckSell";

    public static void Register()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value || !ModConfig.CartUpgradesEnabled.Value)
            return;

        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = HealId,
            DisplayName = "Heal Power",
            Description = "Truck upgrade (whole team): all healing is stronger. Stacks per level.",
            MaxLevel = Mathf.Max(1, ModConfig.CartHealMaxLevel.Value),
            BaseShopPrice = ModConfig.CartHealBaseCost.Value,
            IconFile = "Upgrade_Truck_Heal-Power.png",
        });

        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = ChargeId,
            DisplayName = "Charge Efficiency",
            Description = "Truck upgrade (whole team): the charging station drains its energy slower. Stacks per level.",
            MaxLevel = Mathf.Max(1, ModConfig.CartChargeMaxLevel.Value),
            BaseShopPrice = ModConfig.CartChargeBaseCost.Value,
            IconFile = "Upgrade_Truck_Charge-Power.png",
        });

        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = SellId,
            DisplayName = "Loot Cash-In",
            Description = "Truck upgrade (whole team): once owned, leftover valuables are auto-cashed when every quota is closed. One-time unlock.",
            MaxLevel = 1,
            BaseShopPrice = ModConfig.CartSellCost.Value,
        });
    }

    /// <summary>Highest level of <paramref name="id"/> owned across all players in the run (0 if none).</summary>
    public static int TeamLevel(string id)
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value)
            return 0;
        var up = RepoUpgrades.GetUpgrade(id);
        if (up == null)
            return 0;

        var director = GameDirector.instance;
        if (director == null || director.PlayerList == null)
            return 0;

        int max = 0;
        foreach (var p in director.PlayerList)
        {
            if (p == null || string.IsNullOrEmpty(p.steamID))
                continue;
            int lvl = up.GetLevel(p.steamID);
            if (lvl > max)
                max = lvl;
        }
        return max;
    }

    /// <summary>All-healing multiplier derived from the team's Heal Power level (>= 1).</summary>
    public static float HealMultiplier =>
        1f + TeamLevel(HealId) * Mathf.Max(0f, ModConfig.CartHealPercentPerLevel.Value);

    /// <summary>Charging-station drain multiplier derived from the team's Charge Efficiency (0.05..1).</summary>
    public static float ChargeDrainMultiplier =>
        Mathf.Clamp(1f - TeamLevel(ChargeId) * Mathf.Max(0f, ModConfig.CartChargePercentPerLevel.Value), 0.05f, 1f);

    /// <summary>True when anyone on the team owns the Loot Cash-In unlock.</summary>
    public static bool SellUnlocked => TeamLevel(SellId) > 0;

    /// <summary>
    /// True only once the unlock is owned AND every extraction quota on the level is closed —
    /// that's when leftover loot is otherwise wasted, so cashing it in is safe. Host-only.
    /// </summary>
    public static bool CanSellNow()
    {
        return SellUnlocked
            && SemiFunc.IsMasterClientOrSingleplayer()
            && RoundDirector.instance != null
            && RoundDirector.instance.allExtractionPointsCompleted;
    }

    /// <summary>
    /// Cash in every remaining valuable for CartSellFraction of its value, zeroing each so it
    /// can't be extracted or sold again. Host-only; shared run currency syncs through
    /// SemiFunc.StatSetRunCurrency. Returns the currency credited.
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
        Plugin.Log.LogInfo($"[TruckUpgrades] Loot Cash-In sold leftovers worth {total:0} for {credit} currency.");
        return credit;
    }

    /// <summary>
    /// Drives the automatic Loot Cash-In: once quotas close on a level and someone owns the
    /// unlock, the host cashes leftover loot exactly once per level. Added to the mod runtime
    /// object. The "done" latch resets whenever we're no longer on a run level.
    /// </summary>
    internal sealed class SellController : MonoBehaviour
    {
        private const float CheckInterval = 1f;
        private float _timer;
        private bool _soldThisLevel;

        private void Update()
        {
            if (!ModConfig.CartUpgradesEnabled.Value)
                return;

            // Reset the latch off-level (truck/shop) so the next level can auto-sell again.
            if (!SemiFunc.RunIsLevel())
            {
                _soldThisLevel = false;
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f)
                return;
            _timer = CheckInterval;

            if (_soldThisLevel)
                return;

            if (CanSellNow())
            {
                SellRemainingLoot();
                _soldThisLevel = true;
            }
        }
    }
}
