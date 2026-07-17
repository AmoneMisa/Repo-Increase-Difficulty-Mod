using HarmonyLib;

namespace IncreaseHardMod.Patches;

/// <summary>
/// Scales enemy movement speed based on run depth.
///
/// EnemyNavMeshAgent.Awake() captures the enemy's base speed/acceleration:
///     DefaultSpeed = Agent.speed;
///     DefaultAcceleration = Agent.acceleration;
/// Most enemy movement code reads DefaultSpeed (directly or via OverrideAgent),
/// so scaling it here propagates to nearly every enemy's chase/patrol speed.
///
/// This is a [HarmonyPostfix] (runs AFTER Awake) so DefaultSpeed/Acceleration
/// are already populated; we then multiply the captured values and the live
/// agent. The NavMeshAgent only actually runs on the host, and enemy positions
/// are synced to clients over Photon, so movement stays host-authoritative.
/// </summary>
[HarmonyPatch(typeof(EnemyNavMeshAgent), "Awake")]
internal static class EnemySpeedPatch
{
    [HarmonyPostfix]
    private static void Postfix(EnemyNavMeshAgent __instance)
    {
        float mult = Difficulty.Multiplier(
            ModConfig.EnemySpeedPerLevel.Value,
            ModConfig.EnemySpeedMax.Value);

        if (mult <= 1f)
            return;

        __instance.DefaultSpeed *= mult;
        __instance.DefaultAcceleration *= mult;

        if (__instance.Agent != null)
        {
            __instance.Agent.speed *= mult;
            __instance.Agent.acceleration *= mult;
        }
    }
}
