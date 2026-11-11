using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Economy;

/// <summary>Accelerates audited cooldown clocks, without speeding up attack tells or animation.</summary>
[HarmonyPatch]
internal static class MoonAttackCooldowns
{
    private static readonly Dictionary<MethodBase, FieldInfo[]> Fields = new();
    private static readonly string[] Timers = { "attackCooldown", "headAttackCooldown", "meleeCooldown", "laserCooldown",
        "attackCooldownTimer", "mainAttackCooldownTimer", "tongueAttackRecoveryTimer", "attackPauseTimer",
        "frontPunchCooldownTimer", "backKickCooldownTimer" };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        foreach (string typeName in new[] { "EnemyBombThrower", "EnemyBeamer", "EnemyGnome", "EnemyHeadGrabber",
                     "EnemyOogly", "EnemySnatcher", "EnemyTricycle" })
        {
            var type = typeof(Enemy).Assembly.GetType(typeName);
            if (type == null) continue;
            var update = AccessTools.DeclaredMethod(type, "Update");
            if (update == null) continue;
            var fields = new List<FieldInfo>();
            foreach (string timer in Timers)
            {
                // Missing optional clocks are expected for other enemy types;
                // AccessTools.Field logs a warning for each unsuccessful probe.
                var field = type.GetField(timer, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field?.FieldType == typeof(float)) fields.Add(field);
            }
            if (fields.Count == 0) continue;
            Fields[update] = fields.ToArray();
            yield return update;
        }
        foreach (var (type, method) in new[] { (typeof(EnemyBeamer), "StateAttackEnd"),
                     (typeof(EnemyHunter), "StateShootEnd"), (typeof(EnemyShadow), "StateCoolDown") })
        {
            var target = AccessTools.DeclaredMethod(type, method);
            var field = AccessTools.Field(type, "stateTimer");
            if (target == null || field == null) continue;
            Fields[target] = new[] { field };
            yield return target;
        }
    }

    [HarmonyPrefix]
    private static void Prefix(object __instance, MethodBase __originalMethod, out float[] __state)
    {
        __state = null;
        if (!SemiFunc.IsMasterClientOrSingleplayer() || ExtendedMoons.Current < MoonRules.First) return;
        var fields = Fields[__originalMethod];
        __state = new float[fields.Length];
        for (int i = 0; i < fields.Length; i++) __state[i] = (float)fields[i].GetValue(__instance);
    }

    [HarmonyPostfix]
    private static void Postfix(object __instance, MethodBase __originalMethod, float[] __state)
    {
        if (__state == null) return;
        var fields = Fields[__originalMethod];
        for (int i = 0; i < fields.Length; i++)
        {
            float after = (float)fields[i].GetValue(__instance);
            float before = __state[i];
            // A reset/refresh or an inactive clock is not elapsed attack cooldown.
            if (before > 0f && after > 0f && after < before && before - after <= Time.deltaTime * 4.1f)
                fields[i].SetValue(__instance, MoonRules.CooldownRemaining(before, after, ExtendedMoons.Current));
        }
    }

    [HarmonyPatch(typeof(EnemySnatcher), "PlayerAttackCooldownSet")]
    private static class PlayerClock
    {
        [HarmonyPrefix]
        private static void Prefix(EnemySnatcher __instance, PlayerAvatar _player, out float __state) =>
            __state = _player != null && __instance.playerAttackCooldowns.TryGetValue(_player, out float end) ? end : 0f;
        [HarmonyPostfix]
        private static void Postfix(EnemySnatcher __instance, PlayerAvatar _player, float __state)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || _player == null || ExtendedMoons.Current < MoonRules.First ||
                !__instance.playerAttackCooldowns.TryGetValue(_player, out float end) || end <= __state) return;
            float now = __instance.TongueAttackCooldownTime();
            __instance.playerAttackCooldowns[_player] = Mathf.Max(__state, now + ExtendedMoons.Cooldown(end - now));
        }
    }

    [HarmonyPatch(typeof(EnemySnatcher), "DefensiveAttackPlayerCooldownSet")]
    private static class DefensiveClock
    {
        [HarmonyPrefix]
        private static void Prefix(EnemySnatcher __instance, PlayerAvatar _player, out float __state) =>
            __state = _player != null && __instance.defensiveAttackPlayerCooldowns.TryGetValue(_player, out float end) ? end : 0f;
        [HarmonyPostfix]
        private static void Postfix(EnemySnatcher __instance, PlayerAvatar _player, float __state)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || _player == null || ExtendedMoons.Current < MoonRules.First ||
                !__instance.defensiveAttackPlayerCooldowns.TryGetValue(_player, out float end) || end <= __state) return;
            float now = __instance.TongueAttackCooldownTime();
            __instance.defensiveAttackPlayerCooldowns[_player] = Mathf.Max(__state, now + ExtendedMoons.Cooldown(end - now));
        }
    }
}
