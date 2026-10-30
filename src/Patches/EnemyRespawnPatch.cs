using HarmonyLib;

namespace IncreaseHardMod.Patches;

/// <summary>
/// Shortens how long enemies stay despawned before returning (faster respawns).
///
/// EnemyParent holds DespawnedTimeMin/Max — the range the game randomly picks
/// from to decide how long an enemy waits, gone, before it re-activates. The
/// actual random pick happens later (in EnemyParent.Setup), so if we shrink
/// these two fields in a Postfix on Awake, every subsequent respawn wait is
/// drawn from the reduced range.
///
/// We scale by a factor that shrinks with depth (ReduceMultiplier), floored so
/// enemies never respawn instantly. The despawn/respawn timers are driven by the
/// host, and the values are deterministic from the synced level count, so this
/// stays consistent across the lobby.
/// </summary>
[HarmonyPatch(typeof(EnemyParent), "Awake")]
internal static class EnemyRespawnPatch
{
    [HarmonyPostfix]
    private static void Postfix(EnemyParent __instance)
    {
        if (!ModConfig.ScaleEnemyRespawn.Value)
            return;

        float mult = Difficulty.ReduceMultiplier(
            ModConfig.EnemyRespawnFasterPerLevel.Value,
            ModConfig.EnemyRespawnMinFactor.Value);

        if (mult >= 1f)
            return;

        __instance.DespawnedTimeMin *= mult;
        __instance.DespawnedTimeMax *= mult;
    }
}
