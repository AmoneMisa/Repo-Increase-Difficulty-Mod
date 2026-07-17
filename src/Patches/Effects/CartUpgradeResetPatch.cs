using HarmonyLib;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Cart upgrade levels persist for a whole run but must not leak into the next one.
/// RunManager.ResetProgress is exactly where the game zeroes levelsCompleted and clears
/// run stats, so clearing our upgrade state here (host-side, then re-broadcast) keeps
/// them in lockstep with the run lifecycle.
/// </summary>
[HarmonyPatch(typeof(RunManager), "ResetProgress")]
internal static class CartUpgradeResetPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return;

        CartUpgradeState.ResetForNewRun();
        CartUpgradeState.Broadcast();
    }
}
