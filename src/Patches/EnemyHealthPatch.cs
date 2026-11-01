using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Patches;

[HarmonyPatch(typeof(EnemyHealth), "Awake")]
internal static class EnemyHealthPatch
{
    private sealed class State { internal int Base; }
    private static readonly ConditionalWeakTable<EnemyHealth, State> States = new();
    private static readonly HashSet<string> Logged = new();
    [HarmonyPrefix]
    private static void Prefix(EnemyHealth __instance) => Refresh(__instance, false);

    internal static void Refresh(EnemyHealth health, bool preserveCurrent)
    {
        var state = States.GetValue(health, h => new State { Base = h.health });
        var parent = health.GetComponentInParent<EnemyParent>();
        string name = parent != null ? parent.enemyName : health.gameObject.name;
        if (ModConfig.LogEnemyNames.Value && Logged.Add(name))
            Plugin.Log.LogInfo($"[Enemy] Spawned enemy internal name: \"{name}\"");
        int desired = state.Base > 0 ? Mathf.Max(1, Mathf.CeilToInt(state.Base * EnemyBalance.Health(name))) : state.Base;
        if (desired == health.health) return;
        if (preserveCurrent && health.health > 0)
            health.healthCurrent = health.dead || health.healthCurrent <= 0 ? 0 :
                Mathf.Clamp(Mathf.CeilToInt((float)health.healthCurrent / health.health * desired), 1, desired);
        health.health = desired;
    }

    [HarmonyPatch(typeof(EnemyHealth), "OnSpawn")]
    private static class Spawn
    {
        [HarmonyPrefix]
        private static void Prefix(EnemyHealth __instance) => Refresh(__instance, false);
    }
    [HarmonyPatch(typeof(EnemyHealth), "Update")]
    private static class Live
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyHealth __instance) => Refresh(__instance, true);
    }
}
