using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Drones;

/// <summary>Game hooks used by the Ghost and Silencer drones.</summary>
internal static class DronePatches
{
    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.Break))]
    private static class GhostCargoProtection
    {
        [HarmonyPrefix, HarmonyPriority(Priority.High)]
        private static void Prefix(PhysGrabObjectImpactDetector __instance, ref float valueLost)
        {
            if (SemiFunc.IsMasterClientOrSingleplayer() && valueLost > 0f && __instance.isValuable &&
                __instance.physGrabObject != null && UtilityDrone.ProtectsGhost(__instance.physGrabObject))
                valueLost = Gear.UtilityGearRules.GhostLoss(valueLost);
        }
    }
    // ---------------------------------------------------------------- Ghost

    // A ghosted valuable never joins the extraction haul (so it is neither counted nor sucked in).
    [HarmonyPatch(typeof(ValuableObject), nameof(ValuableObject.AddToDollarHaulList))]
    private static class SkipGhostHaul
    {
        [HarmonyPrefix]
        private static bool Prefix(ValuableObject __instance) => !UtilityDrone.IsGhosted(__instance);
    }

    // ------------------------------------------------------------- Silencer
    // Sounds carry no owner, so "belongs to a silenced target" means "is emitted on/next to it".

    [HarmonyPatch(typeof(Sound), nameof(Sound.Play), new[] { typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(float) })]
    private static class QuietPlayAt
    {
        [HarmonyPrefix]
        private static void Prefix(Vector3 position, ref float volumeMultiplier) =>
            volumeMultiplier *= UtilityDrone.NoiseFactorAt(position);
    }

    [HarmonyPatch(typeof(Sound), nameof(Sound.Play), new[] { typeof(Transform), typeof(float), typeof(float), typeof(float), typeof(float) })]
    private static class QuietPlayFollow
    {
        [HarmonyPrefix]
        private static void Prefix(Transform followTarget, ref float volumeMultiplier)
        {
            if (followTarget != null)
                volumeMultiplier *= UtilityDrone.NoiseFactorAt(followTarget.position);
        }
    }

    [HarmonyPatch(typeof(Sound), nameof(Sound.Play), new[] { typeof(Transform), typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(float) })]
    private static class QuietPlayContact
    {
        [HarmonyPrefix]
        private static void Prefix(Vector3 contactPoint, ref float volumeMultiplier) =>
            volumeMultiplier *= UtilityDrone.NoiseFactorAt(contactPoint);
    }

    [HarmonyPatch(typeof(Sound), nameof(Sound.PlayLoop))]
    private static class QuietLoop
    {
        [HarmonyPrefix]
        private static void Prefix(Sound __instance, ref float volumeMultiplier)
        {
            if (__instance.Source != null)
                volumeMultiplier *= UtilityDrone.NoiseFactorAt(__instance.Source.transform.position);
        }
    }

    // Enemies only investigate what is loud enough, so shrink the radius too.
    [HarmonyPatch(typeof(EnemyDirector), nameof(EnemyDirector.SetInvestigate))]
    private static class QuietInvestigate
    {
        [HarmonyPrefix]
        private static void Prefix(Vector3 position, ref float radius) =>
            radius *= UtilityDrone.NoiseFactorAt(position);
    }

    // In-game voice chat / TTS of a silenced player.
    [HarmonyPatch(typeof(PlayerVoiceChat), nameof(PlayerVoiceChat.Update))]
    private static class QuietVoice
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerVoiceChat __instance)
        {
            if (!UtilityDrone.IsPlayerSilenced(__instance.playerAvatar))
                return;
            float m = ModConfig.SilencerNoiseMultiplier.Value;
            if (__instance.lowPassLogic != null)
                __instance.lowPassLogic.Volume *= m;
            if (__instance.lowPassLogicTTS != null)
                __instance.lowPassLogicTTS.Volume *= m;
        }
    }
}
