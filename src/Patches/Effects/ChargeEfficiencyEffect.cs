using HarmonyLib;
using IncreaseHardMod.Upgrades;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Charge Efficiency truck upgrade — slows the truck charging station's energy drain.
///
/// ChargingStation keeps an internal energy pool (chargeFloat) that ticks down by a
/// private <c>chargeRate</c> each charge step while items charge during a run. The
/// station computes that rate once in Start (chargeRate = 1 / energyPerCrystal /
/// chargeTotal). We postfix Start to cache that freshly-computed BASE rate and then
/// scale the live field by the upgrade's drain multiplier (&lt;1 = slower drain).
///
/// The multiplier comes from the team's highest Charge Efficiency level
/// (<see cref="TruckUpgrades.ChargeDrainMultiplier"/>). Because the station recomputes
/// its rate once in Start, applying there covers each fresh level; the multiplier reads
/// the current team level so it reflects whatever anyone has bought by then.
/// </summary>
[HarmonyPatch(typeof(ChargingStation), "Start")]
internal static class ChargeEfficiencyEffect
{
    private static float _baseRate = -1f;

    [HarmonyPostfix]
    private static void Postfix(ChargingStation __instance)
    {
        _baseRate = __instance.chargeRate;
        Apply(__instance);
    }

    /// <summary>Re-scale the live charging station (if any) from the cached base rate.</summary>
    public static void Reapply()
    {
        if (_baseRate < 0f || ChargingStation.instance == null)
            return;
        Apply(ChargingStation.instance);
    }

    private static void Apply(ChargingStation station)
    {
        if (!ModConfig.TruckUpgradesEnabled.Value)
        {
            station.chargeRate = _baseRate;
            return;
        }
        station.chargeRate = _baseRate * TruckUpgrades.ChargeDrainMultiplier;
    }
}
