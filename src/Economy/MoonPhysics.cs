using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Economy;

internal static class MoonPhysics
{
    [HarmonyPatch(typeof(EnemyRigidbody), "FixedUpdate")]
    private static class Lift
    {
        [HarmonyPrefix]
        private static void Prefix(EnemyRigidbody __instance, out GrabForce __state)
        {
            __state = __instance.grabForceNeeded;
            if (ExtendedMoons.Current < MoonRules.RandomSouls || __state == null || __state.amount >= 100f ||
                __instance.enemy == null || __instance.enemy.EnemyParent == null) return;
            var force = __instance.GetComponent<MoonBodyForce>() ?? __instance.gameObject.AddComponent<MoonBodyForce>();
            if (force.Force == null) force.Force = ScriptableObject.CreateInstance<GrabForce>();
            int tier = (int)__instance.enemy.EnemyParent.difficulty + 1;
            force.Force.amount = MoonRules.GrabForce(tier);
            __instance.grabForceNeeded = force.Force;
        }
        [HarmonyFinalizer]
        private static void Restore(EnemyRigidbody __instance, GrabForce __state) => __instance.grabForceNeeded = __state;
    }

    private sealed class SoulHit { internal float Until; }
    private static readonly ConditionalWeakTable<PhysGrabObjectImpactDetector, SoulHit> SoulHits = new();

    [HarmonyPatch(typeof(HurtCollider), "PhysObjectHurt")]
    private static class SoulImpact
    {
        [HarmonyPostfix]
        private static void Postfix(HurtCollider __instance, PhysGrabObject physGrabObject, bool apply, Enemy enemy, bool __result)
        {
            if (!__result || !apply || enemy != null || ExtendedMoons.Current < MoonRules.First ||
                physGrabObject == null || physGrabObject.isEnemy || physGrabObject.impactDetector == null ||
                __instance.GetComponentInParent<EnemyValuableExplosion>() == null) return;
            // Native impact impulses are consumed on the next physics update.
            SoulHits.GetValue(physGrabObject.impactDetector, _ => new SoulHit()).Until = Time.time + .15f;
        }
    }

    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "Break")]
    private static class SoulValueDamage
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First + 10)]
        private static void Prefix(PhysGrabObjectImpactDetector __instance, ref float valueLost)
        {
            if (!__instance.isValuable || valueLost <= 0f) return;
            if (!SemiFunc.IsMasterClientOrSingleplayer() || !SoulHits.TryGetValue(__instance, out var hit)) return;
            SoulHits.Remove(__instance);
            if (Time.time <= hit.Until && valueLost > 0f && __instance.valuableObject != null)
                valueLost = Mathf.Min(__instance.valuableObject.dollarValueCurrent, valueLost * MoonRules.SoulDanger);
        }
    }

    [HarmonyPatch(typeof(NotValuableObject), nameof(NotValuableObject.Impact))]
    private static class SoulItemDamage
    {
        [HarmonyPrefix]
        private static void Prefix(NotValuableObject __instance, out (int Light, int Medium, int Heavy) __state)
        {
            __state = (__instance.healthLossOnBreakLight, __instance.healthLossOnBreakMedium, __instance.healthLossOnBreakHeavy);
            var detector = __instance.GetComponent<PhysGrabObjectImpactDetector>();
            if (detector == null || !SoulHits.TryGetValue(detector, out var hit)) return;
            SoulHits.Remove(detector);
            if (Time.time > hit.Until) return;
            __instance.healthLossOnBreakLight *= 2;
            __instance.healthLossOnBreakMedium *= 2;
            __instance.healthLossOnBreakHeavy *= 2;
        }
        [HarmonyFinalizer]
        private static void Restore(NotValuableObject __instance, (int Light, int Medium, int Heavy) __state)
        {
            __instance.healthLossOnBreakLight = __state.Light;
            __instance.healthLossOnBreakMedium = __state.Medium;
            __instance.healthLossOnBreakHeavy = __state.Heavy;
        }
    }
}

internal sealed class MoonBodyForce : MonoBehaviour
{
    internal GrabForce Force;
    private void OnDestroy() { if (Force != null) Destroy(Force); }
}
