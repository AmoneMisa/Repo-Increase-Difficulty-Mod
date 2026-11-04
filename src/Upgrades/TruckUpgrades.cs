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
/// effect for everyone. A run-wide record and REPOLib levels are synchronized by the host,
/// including late joiners and sessions where the original buyer has left.
///
///   • Heal Power        — leveled: scales ALL healing (truck final-heal, medkits, drones…).
///   • Charge Efficiency — leveled: slows the truck charging station's energy drain.
///   • Loot Cash-In      — one-time unlock: leftover valuables are auto-cashed (for
///                          <see cref="ModConfig.TruckSellFraction"/>) once every quota closes.
/// </summary>
internal static class TruckUpgrades
{
    public const string HealId = "IHM_TruckHeal";
    public const string ChargeId = "IHM_TruckCharge";
    public const string SellId = "IHM_TruckSell";
    internal static bool IsShared(string id) => id == HealId || id == ChargeId || id == SellId;
    internal static void StoreShared(string id, int level)
    {
        if (!IsShared(id) || !SemiFunc.IsMasterClientOrSingleplayer() || StatsManager.instance == null) return;
        string key = "IHM_Shared_" + id;
        StatsManager.instance.runStats[key] = level;
        if (PunManager.instance != null) PunManager.instance.UpdateStat("runStats", key, level);
        ShareLevels();
    }

    internal static void ShareLevels()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || GameDirector.instance == null || GameDirector.instance.PlayerList == null) return;
        foreach (string id in new[] { HealId, ChargeId, SellId })
        {
            var upgrade = RepoUpgrades.GetUpgrade(id);
            if (upgrade == null) continue;
            int level = TeamLevel(id);
            var definition = PlayerUpgradeRegistry.FindDefinition(id);
            if (definition != null) level = Mathf.Min(level, definition.MaxLevel);
            foreach (var avatar in GameDirector.instance.PlayerList)
                if (avatar != null && !string.IsNullOrEmpty(avatar.steamID) && upgrade.GetLevel(avatar) < level)
                    upgrade.SetLevel(avatar, level);
        }
    }

    public static void Register()
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value || !ModConfig.TruckUpgradesEnabled.Value)
            return;

        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = HealId,
            DisplayName = "Heal Power",
            Description = $"Truck upgrade (whole team): all healing heals {ModConfig.TruckHealPercentPerLevel.Value * 100f:0.#}% more per level, compounding (each level multiplies the previous heal).",
            MaxLevel = Mathf.Max(1, ModConfig.TruckHealMaxLevel.Value),
            BaseShopPrice = ModConfig.TruckHealBaseCost.Value,
            IconFile = "Upgrade_Truck_Heal-Power.png",
        });

        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = ChargeId,
            DisplayName = "Charge Efficiency",
            Description = "Truck upgrade (whole team): each level multiplies the charging station's remaining energy drain; at least 5% drain remains.",
            MaxLevel = Mathf.Max(1, ModConfig.TruckChargeMaxLevel.Value),
            BaseShopPrice = ModConfig.TruckChargeBaseCost.Value,
            IconFile = "Upgrade_Truck_Charge-Power.png",
        });

        PlayerUpgradeRegistry.Define(new PlayerUpgradeRegistry.Definition
        {
            Id = SellId,
            DisplayName = "Loot Cash-In",
            Description = "Truck upgrade (whole team): once owned, leftover valuables are auto-cashed when every quota is closed. One-time unlock.",
            MaxLevel = 1,
            BaseShopPrice = ModConfig.TruckSellCost.Value,
            IconFile = "Upgrade_Truck_Cache.png",
        });
    }

    /// <summary>Highest level of <paramref name="id"/> owned across all players in the run (0 if none).</summary>
    public static int TeamLevel(string id)
    {
        if (!ModConfig.PlayerUpgradesEnabled.Value || !ModConfig.TruckUpgradesEnabled.Value)
            return 0;
        var up = RepoUpgrades.GetUpgrade(id);
        if (up == null)
            return 0;

        int max = 0;
        if (StatsManager.instance != null && StatsManager.instance.runStats.TryGetValue("IHM_Shared_" + id, out var shared)) max = shared;
        // Includes departed players; joining/leaving must never remove a purchased team effect.
        foreach (int level in up.PlayerDictionary.Values) max = Mathf.Max(max, level);
        var definition = PlayerUpgradeRegistry.FindDefinition(id);
        return definition != null ? Mathf.Clamp(max, 0, definition.MaxLevel) : Mathf.Max(0, max);
    }

    /// <summary>Truck-heal multiplier: each level multiplies the heal by (1 + p), default 1.05, compounding.</summary>
    public static float HealMultiplier => Mathf.Pow(1f + Mathf.Clamp(ModConfig.TruckHealPercentPerLevel.Value, 0f, 1f), TeamLevel(HealId));

    /// <summary>Each level multiplies the remaining station drain, with a 5% minimum drain.</summary>
    public static float ChargeDrainMultiplier =>
        Mathf.Max(.05f, Mathf.Pow(1f - Mathf.Clamp(ModConfig.TruckChargePercentPerLevel.Value, 0f, .95f), TeamLevel(ChargeId)));

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
    /// Cash in every remaining valuable for TruckSellFraction of its value, zeroing each so it
    /// can't be extracted or sold again. Host-only; shared run currency syncs through
    /// SemiFunc.StatSetRunCurrency. Returns the currency credited.
    /// </summary>
    public static int SellRemainingLoot()
    {
        if (!CanSellNow())
            return 0;

        float fraction = Mathf.Clamp01(ModConfig.TruckSellFraction.Value);
        float total = 0f;
        foreach (var v in Object.FindObjectsOfType<ValuableObject>())
        {
            if (v.dollarValueCurrent > 0f)
            {
                total += v.dollarValueCurrent;
                v.dollarValueCurrent = 0f;
            }
        }

        int credit = Mathf.CeilToInt(total * fraction / 1000f); // Native extraction rounds up whole K.
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
        private float _shareTimer;

        private void Update()
        {
            if (!ModConfig.TruckUpgradesEnabled.Value)
                return;
            _shareTimer -= Time.unscaledDeltaTime;
            if (_shareTimer <= 0f) { _shareTimer = 1f; ShareLevels(); }
            if (RunManager.instance == null || RunManager.instance.levelCurrent == null) return;

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
