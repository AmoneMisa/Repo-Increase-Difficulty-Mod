using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Patches;

/// <summary>
/// Scales enemy max HP based on run depth, with a per-enemy cap.
///
/// Harmony 101: [HarmonyPatch(typeof(X), "Method")] tells Harmony which game
/// method to hook. A [HarmonyPrefix] method runs BEFORE the original. The
/// special parameter name "__instance" is the object the method was called on
/// (here, the EnemyHealth being set up).
///
/// EnemyHealth.Awake() does: healthCurrent = health;
/// By multiplying "health" in a Prefix (before the original runs), the original
/// Awake copies our boosted value into healthCurrent for free. Every client runs
/// this with the same synced levelsCompleted, so HP stays consistent for all.
///
/// The cap is per-enemy: we read the enemy's name from its EnemyParent (found by
/// walking up the transform hierarchy) and look up its configured max multiplier.
/// </summary>
[HarmonyPatch(typeof(EnemyHealth), "Awake")]
internal static class EnemyHealthPatch
{
    // Names we've already logged, so we only print each enemy once per session.
    private static readonly HashSet<string> _loggedNames = new();

    [HarmonyPrefix]
    private static void Prefix(EnemyHealth __instance)
    {
        // enemyName lives on EnemyParent, which sits above EnemyHealth in the
        // prefab hierarchy. Fall back to the GameObject name if it's missing.
        EnemyParent parent = __instance.GetComponentInParent<EnemyParent>();
        string enemyName = parent != null ? parent.enemyName : __instance.gameObject.name;

        if (ModConfig.LogEnemyNames.Value && _loggedNames.Add(enemyName))
            Plugin.Log.LogInfo($"[Enemy] Spawned enemy internal name: \"{enemyName}\"");

        if (!ModConfig.ScaleEnemyHealth.Value)
            return;

        // Per-enemy cap: Elsa 2x, Tick/Loom/Cleanup Crew 1.5x, others global.
        float cap = ModConfig.HealthMaxForEnemy(enemyName);

        // Raw ramp value (1 + perLevel * steps), then clamp to this enemy's cap.
        float raw = 1f + ModConfig.EnemyHealthPerLevel.Value * Difficulty.Steps;
        float mult = Mathf.Clamp(raw, 1f, cap);

        if (mult <= 1f)
            return;

        __instance.health = Mathf.CeilToInt(__instance.health * mult);
    }
}
