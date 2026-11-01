using System.Runtime.CompilerServices;
using HarmonyLib;
using IncreaseHardMod.Upgrades;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>Keep each station's authored cost and apply the current team upgrade without compounding.</summary>
[HarmonyPatch(typeof(ChargingStation), "Start")]
internal static class ChargeEfficiencyEffect
{
    private static readonly System.Reflection.FieldInfo RateField =
        AccessTools.Field(typeof(ChargingStation), "batteryChargeCost") ??
        AccessTools.Field(typeof(ChargingStation), "chargeRate");
    private sealed class State { internal float BaseRate; }
    private static readonly ConditionalWeakTable<ChargingStation, State> States = new();

    [HarmonyPostfix]
    private static void Postfix(ChargingStation __instance)
    {
        if (RateField == null)
        {
            Plugin.Log.LogWarning("[ChargeEfficiency] No compatible charging cost field found; upgrade not applied.");
            return;
        }
        States.Remove(__instance);
        States.Add(__instance, new State { BaseRate = (float)RateField.GetValue(__instance) });
        Apply(__instance);
    }

    public static void Reapply()
    {
        if (ChargingStation.instance != null) Apply(ChargingStation.instance);
    }

    internal static void Apply(ChargingStation station)
    {
        if (station == null || RateField == null || !States.TryGetValue(station, out var state)) return;
        RateField.SetValue(station, state.BaseRate * TruckUpgrades.ChargeDrainMultiplier);
    }

    [HarmonyPatch(typeof(ChargingStation), "Update")]
    private static class Refresh
    {
        // Before native charging consumes energy, pick up purchases, shared levels and config switches.
        [HarmonyPrefix]
        private static void Prefix(ChargingStation __instance) => Apply(__instance);
    }
}
