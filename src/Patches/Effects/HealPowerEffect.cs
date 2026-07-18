using HarmonyLib;
using IncreaseHardMod.Upgrades;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Heal Power truck upgrade — scales healing effectiveness.
///
/// PlayerHealth.Heal(int, bool) is the single funnel every heal passes through:
/// the truck final-heal, medkits, drones and HealOther/HealOtherRPC all end up
/// calling it on the owning client. Prefixing it and scaling the incoming amount
/// therefore boosts ALL healing by the upgrade multiplier. The multiplier is derived
/// from the team's highest Heal Power level (<see cref="TruckUpgrades.HealMultiplier"/>),
/// so one buyer lifts healing for everyone; REPOLib already syncs each player's level.
/// </summary>
[HarmonyPatch(typeof(PlayerHealth), "Heal")]
internal static class HealPowerEffect
{
    [HarmonyPrefix]
    private static void Prefix(ref int healAmount)
    {
        if (!ModConfig.CartUpgradesEnabled.Value)
            return;

        float m = TruckUpgrades.HealMultiplier;
        if (m <= 1f || healAmount <= 0)
            return;

        healAmount = Mathf.RoundToInt(healAmount * m);
    }
}
