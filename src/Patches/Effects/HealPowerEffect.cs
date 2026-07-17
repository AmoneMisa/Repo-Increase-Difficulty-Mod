using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Heal Power cart upgrade — scales healing effectiveness.
///
/// PlayerHealth.Heal(int, bool) is the single funnel every heal passes through:
/// the truck final-heal, medkits, drones and HealOther/HealOtherRPC all end up
/// calling it on the owning client. Prefixing it and scaling the incoming amount
/// therefore boosts ALL healing by the upgrade multiplier. It runs on the local
/// owner reading the host-synced CartUpgradeState, so every client stays in agreement
/// without any extra networking (the upgrade is a shared cart benefit, applied to
/// everyone).
/// </summary>
[HarmonyPatch(typeof(PlayerHealth), "Heal")]
internal static class HealPowerEffect
{
    [HarmonyPrefix]
    private static void Prefix(ref int healAmount)
    {
        if (!ModConfig.CartUpgradesEnabled.Value)
            return;

        float m = CartUpgradeState.HealMultiplier;
        if (m <= 1f || healAmount <= 0)
            return;

        healAmount = Mathf.RoundToInt(healAmount * m);
    }
}
