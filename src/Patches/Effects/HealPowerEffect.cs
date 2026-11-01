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
    private static PlayerHealth _regenerationTarget;

    internal static float Multiplier => Mathf.Max(0f, Modifiers.StatModifiers.Mult(Modifiers.StatAxis.Healing) *
        (ModConfig.TruckUpgradesEnabled.Value ? TruckUpgrades.HealMultiplier : 1f));

    internal static void HealRegeneration(PlayerHealth target, int amount)
    {
        // Regen applies the multiplier to its fractional rate, then caps the final whole-HP chunk.
        // Scaling that chunk again would overshoot the cap (or round all weak regen ticks to zero).
        var previous = _regenerationTarget;
        _regenerationTarget = target;
        try { target.Heal(amount, effect: false); }
        finally { _regenerationTarget = previous; }
    }

    [HarmonyPrefix]
    private static void Prefix(PlayerHealth __instance, ref int healAmount)
    {
        if (healAmount <= 0 || __instance == _regenerationTarget)
            return;

        // Level modifiers (Stimulants / Poor Medicine) times the truck's Heal Power upgrade.
        float m = Multiplier;
        if (Mathf.Approximately(m, 1f))
            return;

        healAmount = Mathf.RoundToInt(healAmount * m);
    }
}
