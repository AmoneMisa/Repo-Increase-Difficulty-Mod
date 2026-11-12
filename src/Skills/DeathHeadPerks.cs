using HarmonyLib;
using UnityEngine;
using C = IncreaseHardMod.Skills.SkillCatalog;

namespace IncreaseHardMod.Skills;

/// <summary>
/// Utility perks for your death head (both under Death Head Battery):
///   * Head Recharge: while you watch a teammate, the head's battery refills faster (+50% per level, the game refills
///     1% per second). Runs on your own client, where the game keeps your head battery.
///   * Head Leap: a charged head jump carries further, +20% distance per level, twice as far at level 5. The host
///     applies the jump impulse, so the host scales it by the head owner's level.
/// </summary>
internal static class DeathHeadPerks
{
    internal const float RechargePerLevel = .5f, LeapDistancePerLevel = .2f;

    /// <summary>Jump distance multiplier for a Head Leap level (1 = the game's own jump, 2 at level 5).</summary>
    internal static float LeapDistance(int level) => 1f + LeapDistancePerLevel * Mathf.Max(0, level);

    [HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.HeadEnergyLogic))]
    private static class Recharge
    {
        [HarmonyPostfix]
        private static void Postfix(SpectateCamera __instance)
        {
            int level = SkillEffects.LocalLevel(C.HeadRecharge);
            if (level <= 0 || __instance.headEnergyPauseTimer > 0f || !__instance.CheckState(SpectateCamera.State.Normal)) return;
            // The game adds deltaTime / 100 in this state; add the bonus share of that on top.
            __instance.headEnergy = Mathf.Clamp01(__instance.headEnergy + Time.deltaTime / 100f * RechargePerLevel * level);
            __instance.headEnergyEnough = __instance.headEnergy >= .25f;
        }
    }

    [HarmonyPatch(typeof(PlayerDeathHead), nameof(PlayerDeathHead.FixedUpdate))]
    private static class Leap
    {
        [HarmonyPrefix]
        private static void Prefix(PlayerDeathHead __instance)
        {
            if (__instance == null || __instance.spectatedJumpForce <= 0f || __instance.overrideSpectated || !SemiFunc.IsMasterClientOrSingleplayer()) return;
            int level = SkillStore.Level(__instance.playerAvatar, C.HeadLeap);
            if (level <= 0) return;
            // The game pushes the head forward with 4 x charge. A thrown body travels about as far as the square of its
            // launch speed, so the square root of the distance multiplier goes on the push.
            __instance.spectatedJumpForce *= Mathf.Sqrt(LeapDistance(level));
        }
    }
}
