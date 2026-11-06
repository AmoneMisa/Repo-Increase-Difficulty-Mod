using HarmonyLib;
using IncreaseHardMod.Visuals;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>Gives every enemy soul spawned in a level the red soul-style <see cref="HeroAura"/>, shown while pinged.
/// Runs on each client for its own copy, so it is local visuals only.</summary>
[HarmonyPatch(typeof(ValuableObject), "Start")]
internal static class SoulAuraPatch
{
    [HarmonyPostfix]
    private static void Postfix(ValuableObject __instance)
    {
        if (__instance == null || !BiggerSoulDropsEffect.IsEnemyValuable(__instance))
            return;

        HeroAura.Mark(__instance.gameObject, pingOnly: true, soul: true);
    }
}
