using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// Diagnostics: one log line per hit on one of our monsters, with what caused it (the calling method) and whether
/// it was held or stunned - to track down damage nobody dealt (a playtest saw the Naga Queen hurt "by herself").
/// Host only, at most a few lines a second.
/// </summary>
[HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.Hurt))]
internal static class MonsterHurtLog
{
    private static float _next;
    private static int _skipped;

    [HarmonyPrefix]
    private static void Prefix(EnemyHealth __instance, int _damage)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance == null || __instance.dead) return;
        var brain = __instance.GetComponentInParent<EnemyParent>()?.GetComponentInChildren<MonsterBrain>();
        if (brain == null) return;
        if (Time.time < _next) { _skipped++; return; }
        _next = Time.time + .3f;
        var enemy = __instance.enemy;
        var pgo = enemy != null && enemy.HasRigidbody ? enemy.Rigidbody.physGrabObject : null;
        var frames = new StackTrace(2, false).GetFrames();
        var cause = frames != null && frames.Length > 0 ? frames[0].GetMethod() : null;
        var caller = cause != null ? $"{cause.DeclaringType?.Name}.{cause.Name}" : "?";
        Plugin.Log.LogInfo($"[Monsters] {brain.Kind} hurt {_damage} by {caller}; held={(pgo != null && pgo.playerGrabbing.Count > 0)}, " +
                           $"stunned={(enemy != null && enemy.IsStunned())}, state={brain.State}" + (_skipped > 0 ? $" (+{_skipped} more since)" : ""));
        _skipped = 0;
    }
}
