using HarmonyLib;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Charge Efficiency cart upgrade — slows the truck charging station's energy drain.
///
/// ChargingStation keeps an internal energy pool (chargeFloat) that ticks down by a
/// private <c>chargeRate</c> each charge step while items charge during a run. The
/// station computes that rate once in Start (chargeRate = 1 / energyPerCrystal /
/// chargeTotal). We postfix Start to cache that freshly-computed BASE rate and then
/// scale the live field by the upgrade's drain multiplier (&lt;1 = slower drain).
///
/// Caching the base — rather than repeatedly multiplying the current value — is what
/// lets <see cref="Reapply"/> re-scale from a stable baseline when the upgrade level
/// changes mid-run (e.g. the host buys a level, or a client receives new state)
/// without compounding.
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
        if (!ModConfig.CartUpgradesEnabled.Value)
        {
            station.chargeRate = _baseRate;
            return;
        }
        station.chargeRate = _baseRate * CartUpgradeState.ChargeDrainMultiplier;
    }
}
