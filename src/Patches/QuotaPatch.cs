using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Patches;

/// <summary>
/// Scales the extraction "quota" (RoundDirector.haulGoal) required each level.
///
/// A [HarmonyPostfix] runs AFTER the original method. RoundDirector.StartRoundLogic
/// sets haulGoal = value. We bump haulGoal afterwards.
///
/// IMPORTANT networking detail: StartRoundLogic is called two ways —
///   1. On the host/singleplayer, with the freshly computed goal.
///   2. On remote clients, via StartRoundRPC, with the value the host already
///      sent them.
/// If we scaled in both places, remote clients would scale a second time and end
/// up higher than the host. So we ONLY scale when we are the host (or in
/// singleplayer). The host's scaled haulGoal is then broadcast to clients as-is.
/// </summary>
[HarmonyPatch(typeof(RoundDirector), nameof(RoundDirector.StartRoundLogic))]
internal static class QuotaPatch
{
    [HarmonyPostfix]
    private static void Postfix(RoundDirector __instance)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return;

        float mult = ModConfig.ScaleQuota.Value
            ? Difficulty.Multiplier(ModConfig.QuotaPerLevel.Value, ModConfig.QuotaMax.Value)
            : 1f;
        mult *= Economy.EconomyEvents.QuotaMult;                        // level economy event (e.g. Heavy Quota)
        mult *= Modifiers.StatModifiers.Mult(Modifiers.StatAxis.Quota); // level modifier (e.g. Heavy Load)

        if (!Mathf.Approximately(mult, 1f))
            __instance.haulGoal = Mathf.CeilToInt(__instance.haulGoal * mult);

        // Safety ceiling: never demand more than a fraction of the loot that
        // actually exists on the level. haulGoalMax is the summed original value
        // of every valuable, fully tallied by the time this runs (host-side).
        // This runs even when the ramp multiplier is 1 so vanilla-but-loot-poor
        // levels also stay beatable.
        if (__instance.haulGoalMax > 0)
        {
            int lootCeiling = Mathf.FloorToInt(
                __instance.haulGoalMax * ModConfig.QuotaMaxLootFraction.Value);
            if (__instance.haulGoal > lootCeiling)
                __instance.haulGoal = lootCeiling;
        }
    }
}
