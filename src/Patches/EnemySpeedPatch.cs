using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Patches;

/// <summary>Scale navigation writes once. Keep defaults raw so overrides using them cannot compound.</summary>
[HarmonyPatch(typeof(EnemyNavMeshAgent), nameof(EnemyNavMeshAgent.Awake))]
internal static class EnemySpeedPatch
{
    // These native movement paths bypass NavMeshAgent and use DefaultSpeed directly.
    // Scale those reads only; keep raw defaults for UpdateAgent/OverrideAgent arguments.
    internal static readonly (System.Type Type, string Method)[] DirectPaths = {
        (typeof(EnemyBang), nameof(EnemyBang.StateMoveUnder)), (typeof(EnemyBang), nameof(EnemyBang.StateMoveOver)), (typeof(EnemyBang), nameof(EnemyBang.StateMoveBack)),
        (typeof(EnemyBirthdayBoy), nameof(EnemyBirthdayBoy.StateAttackUnder)), (typeof(EnemyBirthdayBoy), nameof(EnemyBirthdayBoy.StateAttackUnderEnd)), (typeof(EnemyBirthdayBoy), nameof(EnemyBirthdayBoy.StateAttackOver)),
        (typeof(EnemyBombThrower), nameof(EnemyBombThrower.BackAwayLogic)),
        (typeof(EnemyDuck), nameof(EnemyDuck.StateGoToPlayerUnder)), (typeof(EnemyDuck), nameof(EnemyDuck.StateGoToPlayerOver)), (typeof(EnemyDuck), nameof(EnemyDuck.StateMoveBackToNavMesh)),
        (typeof(EnemyElsa), nameof(EnemyElsa.StateGoToPlayerOverBig)),
        (typeof(EnemyGnome), nameof(EnemyGnome.StateMoveUnder)), (typeof(EnemyGnome), nameof(EnemyGnome.StateMoveOver)), (typeof(EnemyGnome), nameof(EnemyGnome.StateMoveBack)),
        (typeof(EnemyHeadGrabber), nameof(EnemyHeadGrabber.GotoOverLogic)),
        (typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateGoToPlayerOver)), (typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateGoToPlayerUnder)), (typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateMoveBackToNavmesh)),
        (typeof(EnemyTick), nameof(EnemyTick.StateMoveBackToNavMesh)),
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

    [HarmonyPatch(typeof(EnemyNavMeshAgent), nameof(EnemyNavMeshAgent.OverrideAgent))]
    private static class Override
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyNavMeshAgent __instance) => Apply(__instance, true);
    }
    [HarmonyPatch(typeof(EnemyNavMeshAgent), nameof(EnemyNavMeshAgent.UpdateAgent))]
    private static class Set
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyNavMeshAgent __instance) => Apply(__instance, true);
    }
    [HarmonyPatch(typeof(EnemyNavMeshAgent), nameof(EnemyNavMeshAgent.Update))]
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
                var method = AccessTools.DeclaredMethod(path.Type, path.Method);
                if (method != null) yield return method;
            }
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var field = AccessTools.Field(typeof(EnemyNavMeshAgent), nameof(EnemyNavMeshAgent.DefaultSpeed));
            var factor = AccessTools.PropertyGetter(typeof(EnemyBalance), nameof(EnemyBalance.Speed));
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
