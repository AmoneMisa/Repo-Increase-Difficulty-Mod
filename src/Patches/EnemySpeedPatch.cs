using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Patches;

/// <summary>Scale navigation writes once. Keep defaults raw so overrides using them cannot compound.</summary>
[HarmonyPatch(typeof(EnemyNavMeshAgent), "Awake")]
internal static class EnemySpeedPatch
{
    // These native movement paths bypass NavMeshAgent and use DefaultSpeed directly.
    // Scale those reads only; keep raw defaults for UpdateAgent/OverrideAgent arguments.
    internal static readonly (string Type, string Method)[] DirectPaths = {
        ("EnemyBang","StateMoveUnder"), ("EnemyBang","StateMoveOver"), ("EnemyBang","StateMoveBack"),
        ("EnemyBirthdayBoy","StateAttackUnder"), ("EnemyBirthdayBoy","StateAttackUnderEnd"), ("EnemyBirthdayBoy","StateAttackOver"),
        ("EnemyBombThrower","BackAwayLogic"),
        ("EnemyDuck","StateGoToPlayerUnder"), ("EnemyDuck","StateGoToPlayerOver"), ("EnemyDuck","StateMoveBackToNavMesh"),
        ("EnemyElsa","StateGoToPlayerOverBig"),
        ("EnemyGnome","StateMoveUnder"), ("EnemyGnome","StateMoveOver"), ("EnemyGnome","StateMoveBack"),
        ("EnemyHeadGrabber","GotoOverLogic"),
        ("EnemySlowMouth","StateGoToPlayerOver"), ("EnemySlowMouth","StateGoToPlayerUnder"), ("EnemySlowMouth","StateMoveBackToNavmesh"),
        ("EnemyTick","StateMoveBackToNavMesh"),
    };
    private sealed class State { internal float RawSpeed, RawAcceleration, AppliedSpeed, AppliedAcceleration, DefaultSpeed, DefaultAcceleration; }
    private static readonly ConditionalWeakTable<EnemyNavMeshAgent, State> States = new();

    internal static void Apply(EnemyNavMeshAgent nav, bool rawWrite = false)
    {
        var agent = nav.Agent;
        if (agent == null) return;
        var state = States.GetValue(nav, _ => new State {
            RawSpeed = agent.speed, RawAcceleration = agent.acceleration,
            AppliedSpeed = agent.speed, AppliedAcceleration = agent.acceleration });
        if (rawWrite || !Mathf.Approximately(agent.speed, state.AppliedSpeed)) state.RawSpeed = agent.speed;
        if (rawWrite || !Mathf.Approximately(agent.acceleration, state.AppliedAcceleration)) state.RawAcceleration = agent.acceleration;
        float factor = EnemyBalance.Speed;
        agent.speed = state.AppliedSpeed = state.RawSpeed * factor;
        agent.acceleration = state.AppliedAcceleration = state.RawAcceleration * factor;
        state.DefaultSpeed = nav.DefaultSpeed;
        state.DefaultAcceleration = nav.DefaultAcceleration;
    }
    [HarmonyPostfix]
    private static void Postfix(EnemyNavMeshAgent __instance) => Apply(__instance, true);

    [HarmonyPatch(typeof(EnemyNavMeshAgent), "OverrideAgent")]
    private static class Override
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyNavMeshAgent __instance) => Apply(__instance, true);
    }
    [HarmonyPatch(typeof(EnemyNavMeshAgent), "UpdateAgent")]
    private static class Set
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyNavMeshAgent __instance) => Apply(__instance, true);
    }
    [HarmonyPatch(typeof(EnemyNavMeshAgent), "Update")]
    private static class Live
    {
        [HarmonyPrefix]
        private static void Prefix(EnemyNavMeshAgent __instance)
        {
            if (__instance.Agent == null || !States.TryGetValue(__instance, out var state)) return;
            if (Mathf.Approximately(__instance.DefaultSpeed, state.DefaultSpeed) &&
                Mathf.Approximately(__instance.Agent.speed, state.AppliedSpeed)) __instance.Agent.speed = state.RawSpeed;
            if (Mathf.Approximately(__instance.DefaultAcceleration, state.DefaultAcceleration) &&
                Mathf.Approximately(__instance.Agent.acceleration, state.AppliedAcceleration)) __instance.Agent.acceleration = state.RawAcceleration;
        }
        [HarmonyPostfix]
        private static void Postfix(EnemyNavMeshAgent __instance) => Apply(__instance, true);
    }

    [HarmonyPatch]
    private static class DirectMotion
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            foreach (var path in DirectPaths)
            {
                var method = typeof(Enemy).Assembly.GetType(path.Type)?.GetMethod(path.Method,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null) yield return method;
            }
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var field = typeof(EnemyNavMeshAgent).GetField("DefaultSpeed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var factor = typeof(EnemyBalance).GetProperty(nameof(EnemyBalance.Speed), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetGetMethod(true);
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, field))
                {
                    yield return new CodeInstruction(OpCodes.Call, factor);
                    yield return new CodeInstruction(OpCodes.Mul);
                }
            }
        }
    }
}
