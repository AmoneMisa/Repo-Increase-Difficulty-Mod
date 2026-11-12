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
        Plugin.Log.LogInfo($"[Monsters] {brain.Kind} hurt {_damage} by {caller}{HurtSource.Describe()}; held={(pgo != null && pgo.playerGrabbing.Count > 0)}, " +
                           $"stunned={(enemy != null && enemy.IsStunned())}, state={brain.State}" + (_skipped > 0 ? $" (+{_skipped} more since)" : ""));
        _skipped = 0;
    }
}

/// <summary>
/// Diagnostics: remembers which HurtCollider is dealing enemy damage right now, so <see cref="MonsterHurtLog"/> can
/// name it (its object, the monster that owns it, the player behind it) instead of only "HurtCollider".
/// </summary>
[HarmonyPatch(typeof(HurtCollider), "EnemyHurt")]
internal static class HurtSource
{
    private static HurtCollider _current;

    [HarmonyPrefix]
    private static void Prefix(HurtCollider __instance) => _current = __instance;

    [HarmonyPostfix]
    private static void Postfix() => _current = null;

    [HarmonyFinalizer]
    private static System.Exception Finalizer(System.Exception __exception) { _current = null; return __exception; }

    internal static string Describe()
    {
        var hc = _current;
        if (hc == null) return "";
        string owner = hc.enemyHost != null
            ? hc.enemyHost.GetComponentInParent<EnemyParent>()?.enemyName ?? hc.enemyHost.name
            : "no enemy";
        string player = hc.playerCausingHurt != null ? hc.playerCausingHurt.playerName : "-";
        var root = hc.transform.root;
        return $" [collider '{hc.gameObject.name}' on '{(root != null ? root.name : "?")}', owner={owner}, player={player}, " +
               $"enemyDamage={hc.enemyDamage}, at {hc.transform.position.x:0.0},{hc.transform.position.y:0.0},{hc.transform.position.z:0.0}]";
    }
}
