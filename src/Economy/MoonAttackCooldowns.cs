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

    // Each enemy's attack cooldown clocks, ticked down in the method listed with them.
    private static readonly (Type Type, string Method, string[] Timers)[] Clocks =
    {
        (typeof(EnemyBombThrower), nameof(EnemyBombThrower.Update),
            new[] { nameof(EnemyBombThrower.headAttackCooldown), nameof(EnemyBombThrower.meleeCooldown) }),
        (typeof(EnemyBeamer), nameof(EnemyBeamer.Update), new[] { nameof(EnemyBeamer.laserCooldown) }),
        (typeof(EnemyGnome), nameof(EnemyGnome.Update), new[] { nameof(EnemyGnome.attackCooldown) }),
        (typeof(EnemyHeadGrabber), nameof(EnemyHeadGrabber.Update), new[] { nameof(EnemyHeadGrabber.attackCooldown) }),
        (typeof(EnemyOogly), nameof(EnemyOogly.Update), new[] { nameof(EnemyOogly.attackCooldownTimer) }),
        (typeof(EnemySnatcher), nameof(EnemySnatcher.Update), new[] {
            nameof(EnemySnatcher.mainAttackCooldownTimer), nameof(EnemySnatcher.tongueAttackRecoveryTimer),
            nameof(EnemySnatcher.attackPauseTimer), nameof(EnemySnatcher.frontPunchCooldownTimer),
            nameof(EnemySnatcher.backKickCooldownTimer) }),
        (typeof(EnemyTricycle), nameof(EnemyTricycle.Update), new[] { nameof(EnemyTricycle.attackCooldown) }),
        (typeof(EnemyBeamer), nameof(EnemyBeamer.StateAttackEnd), new[] { nameof(EnemyBeamer.stateTimer) }),
        (typeof(EnemyHunter), nameof(EnemyHunter.StateShootEnd), new[] { nameof(EnemyHunter.stateTimer) }),
        (typeof(EnemyShadow), nameof(EnemyShadow.StateCoolDown), new[] { nameof(EnemyShadow.stateTimer) }),
    };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        foreach (var (type, method, timers) in Clocks)
        {
            var target = AccessTools.DeclaredMethod(type, method);
            if (target == null) continue;
            var fields = new List<FieldInfo>();
            foreach (string timer in timers)
            {
                var field = AccessTools.Field(type, timer);
                if (field?.FieldType == typeof(float)) fields.Add(field);
            }
            if (fields.Count == 0) continue;
            Fields[target] = fields.ToArray();
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

    [HarmonyPatch(typeof(EnemySnatcher), nameof(EnemySnatcher.PlayerAttackCooldownSet))]
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

    [HarmonyPatch(typeof(EnemySnatcher), nameof(EnemySnatcher.DefensiveAttackPlayerCooldownSet))]
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
