using HarmonyLib;
using IncreaseHardMod.Upgrades;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Charge Efficiency truck upgrade — slows the truck charging station's energy drain.
///
/// ChargingStation keeps an internal energy pool (chargeFloat) that ticks down by a
/// private <c>batteryChargeCost</c> each charge step while items charge during a run. The
/// station computes that rate once in Start (batteryChargeCost = 1 / energyPerCrystal / 100). We postfix Start to cache that freshly-computed BASE rate and then
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
    // REPO 0.5 renamed the cost field; retain support for the older game as well.
    private static readonly System.Reflection.FieldInfo RateField =
        AccessTools.Field(typeof(ChargingStation), "batteryChargeCost") ??
        AccessTools.Field(typeof(ChargingStation), "chargeRate");
    private static float _baseRate = -1f;

    [HarmonyPostfix]
    private static void Postfix(ChargingStation __instance)
    {
        if (RateField == null)
        {
            Plugin.Log.LogWarning("[ChargeEfficiency] No compatible charging cost field found; upgrade not applied.");
            return;
        }
        _baseRate = (float)RateField.GetValue(__instance);
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
            RateField.SetValue(station, _baseRate);
            return;
        }
        RateField.SetValue(station, _baseRate * TruckUpgrades.ChargeDrainMultiplier);
    }
}
