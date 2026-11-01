using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Patches;

[HarmonyPatch(typeof(EnemyParent), "Awake")]
internal static class EnemyRespawnPatch
{
    private sealed class State { internal float Min, Max, Factor = 1f; }
    private static readonly ConditionalWeakTable<EnemyParent, State> States = new();
    internal static void Refresh(EnemyParent parent, bool timer)
    {
        var state = States.GetValue(parent, p => new State { Min = p.DespawnedTimeMin, Max = p.DespawnedTimeMax });
        float factor = EnemyBalance.Respawn;
        parent.DespawnedTimeMin = state.Min * factor;
        parent.DespawnedTimeMax = state.Max * factor;
        if (timer && !parent.Spawned && parent.DespawnedTimer > 0f && !Mathf.Approximately(state.Factor, factor))
            parent.DespawnedTimer *= factor / state.Factor;
        state.Factor = factor;
    }
    [HarmonyPostfix]
    private static void Postfix(EnemyParent __instance) => Refresh(__instance, false);
    [HarmonyPatch(typeof(EnemyParent), "Despawn")]
    private static class Despawn
    {
        [HarmonyPrefix]
        private static void Prefix(EnemyParent __instance) => Refresh(__instance, false);
    }
    [HarmonyPatch(typeof(EnemyParent), "Update")]
    private static class Live
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyParent __instance) => Refresh(__instance, true);
    }
}
