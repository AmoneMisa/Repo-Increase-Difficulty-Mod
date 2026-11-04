using HarmonyLib;
using IncreaseHardMod.Upgrades;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Heal Power truck upgrade — scales the truck's heal only (level modifiers still scale every heal).
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

    // True only while PlayerAvatar.FinalHealRPC (the truck's heal) runs: Heal Power must not boost heal packs,
    // drones or regeneration, only the truck.
    internal static bool InTruckHeal;

    private static float Modifier => Mathf.Max(0f, Modifiers.StatModifiers.Mult(Modifiers.StatAxis.Healing));
    internal static float TruckMultiplier => Modifier * (ModConfig.TruckUpgradesEnabled.Value ? TruckUpgrades.HealMultiplier : 1f);
    internal static float Multiplier => InTruckHeal ? TruckMultiplier : Modifier;

    internal static void HealRegeneration(PlayerHealth target, int amount)
    {
        // Regen applies the multiplier to its fractional rate, then caps the final whole-HP chunk.
        // Scaling that chunk again would overshoot the cap (or round all weak regen ticks to zero).
        var previous = _regenerationTarget;
        _regenerationTarget = target;
        try { target.Heal(amount, effect: false); }
        finally { _regenerationTarget = previous; }
    }

    /// <summary>Scaled heal rounded up to the next whole HP (the small epsilon stops float noise adding a point).</summary>
    internal static int Scale(int amount, float multiplier) => Mathf.CeilToInt(amount * multiplier - 0.0001f);

    [HarmonyPrefix]
    private static void Prefix(PlayerHealth __instance, ref int healAmount)
    {
        if (healAmount <= 0 || __instance == _regenerationTarget)
            return;

        // Level modifiers (Stimulants / Poor Medicine) times the truck's Heal Power upgrade.
        float m = Multiplier;
        if (Mathf.Approximately(m, 1f))
            return;

        healAmount = Scale(healAmount, m);
    }
}

/// <summary>
/// The Taxman's truck line prints the vanilla heal ("+50"), but the owner's Heal then applies the team
/// multiplier. Rewrite the number host-side, before the line is broadcast, so the chat shows the real heal.
/// </summary>
[HarmonyPatch(typeof(TruckScreenText), nameof(TruckScreenText.MessageSendCustom))]
internal static class TaxmanHealLine
{
    private static readonly System.Text.RegularExpressions.Regex HealNumber =
        new(@"(<color=#00FF00>\+)(\d+)(</color>)", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    internal static string Rewrite(string message, float multiplier)
    {
        if (string.IsNullOrEmpty(message) || !message.Contains("{truck}{check}") || Mathf.Approximately(multiplier, 1f))
            return message;
        return HealNumber.Replace(message, m =>
            m.Groups[1].Value + HealPowerEffect.Scale(int.Parse(m.Groups[2].Value), multiplier) + m.Groups[3].Value,
            1);
    }

    [HarmonyPrefix]
    private static void Prefix(ref string message) => message = Rewrite(message, HealPowerEffect.TruckMultiplier);
}

[HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.FinalHealRPC))]
internal static class TruckHealScope
{
    [HarmonyPrefix] private static void Prefix() => HealPowerEffect.InTruckHeal = true;
    [HarmonyFinalizer] private static void Finalizer() => HealPowerEffect.InTruckHeal = false;
}
