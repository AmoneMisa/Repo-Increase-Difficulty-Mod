using HarmonyLib;
using IncreaseHardMod.Modifiers;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Wires the game's kill/death/quota events into the LevelTimer countdown. The clock
/// itself and all networking/authority live in LevelTimerController; these patches
/// are just the event taps. All deltas are applied host-side (each hook already runs
/// in a master-only context, and the controller re-checks anyway).
/// </summary>
internal static class LevelTimerEffect
{
    // Countdown begins the moment the host finishes generating a LevelTimer level.
    [HarmonyPatch(typeof(LevelGenerator), nameof(LevelGenerator.GenerateDone))]
    private static class Start
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!ModifierManager.Current.Has(ModifierType.LevelTimer))
                return;
            if (!SemiFunc.IsMasterClientOrSingleplayer())
                return;

            LevelTimerController.StartForLevel();
        }
    }

    // Each enemy death adds time. EnemyHealth.Death only runs on the host.
    [HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.Death))]
    private static class OnKill
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!ModifierManager.Current.Has(ModifierType.LevelTimer))
                return;
            LevelTimerController.AddTime(ModConfig.LevelTimerKillBonus.Value);
        }
    }

    // Each player death removes time.
    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.PlayerDeathRPC))]
    private static class OnPlayerDeath
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!ModifierManager.Current.Has(ModifierType.LevelTimer))
                return;
            if (!SemiFunc.IsMasterClientOrSingleplayer())
                return;
            LevelTimerController.RemoveTime(ModConfig.LevelTimerDeathPenalty.Value);
        }
    }

    // Completing an extraction point's quota adds time.
    [HarmonyPatch(typeof(ExtractionPoint), nameof(ExtractionPoint.StateSet))]
    private static class OnQuota
    {
        [HarmonyPostfix]
        private static void Postfix(ExtractionPoint.State newState)
        {
            if (newState != ExtractionPoint.State.Success)
                return;
            if (!ModifierManager.Current.Has(ModifierType.LevelTimer))
                return;
            if (!SemiFunc.IsMasterClientOrSingleplayer())
                return;
            LevelTimerController.AddTime(ModConfig.LevelTimerQuotaBonus.Value);
        }
    }
}
