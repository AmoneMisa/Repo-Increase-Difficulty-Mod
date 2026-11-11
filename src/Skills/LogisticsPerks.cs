using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Skills;

internal static class LogisticsPerks
{
    // Only the cart's own grabbing calculation receives the extra force. Neither
    // the player nor other held objects keep a modified Strength stat afterwards.
    [HarmonyPatch(typeof(PhysGrabObject), "PhysicsGrabbingManipulation")]
    private static class BasketLift
    {
        [HarmonyPrefix]
        private static void Prefix(PhysGrabObject __instance, out List<(PhysGrabber Grabber, float Before)> __state)
        {
            __state = null;
            if (!__instance.isCart || !SemiFunc.IsMasterClientOrSingleplayer()) return;
            foreach (var grabber in __instance.playerGrabbing)
            {
                if (grabber == null) continue;
                int rank = SkillEffects.Level(grabber.playerAvatar, SkillCatalog.LightBasket);
                if (rank <= 0) continue;
                __state ??= new();
                __state.Add((grabber, grabber.overrideGrabStrength));
                float current = grabber.overrideGrabStrength >= 0f ? grabber.overrideGrabStrength : grabber.grabStrength;
                grabber.overrideGrabStrength = current * PerkRules.BasketLift(rank);
            }
        }
        [HarmonyFinalizer]
        private static void Restore(List<(PhysGrabber Grabber, float Before)> __state)
        {
            if (__state == null) return;
            foreach (var state in __state) if (state.Grabber != null) state.Grabber.overrideGrabStrength = state.Before;
        }
    }

    [HarmonyPatch(typeof(HurtCollider), "PlayerHurt")]
    private static class SapperHit
    {
        [HarmonyPrefix]
        private static void Prefix(HurtCollider __instance, PlayerAvatar _player, out int __state)
        {
            __state = __instance.playerDamage;
            var blast = __instance.GetComponentInParent<Gear.GrenadeBlast>();
            if (blast == null || _player == null || blast.Thrower != _player || __state <= 0) return;
            __instance.playerDamage = Mathf.Max(1, Mathf.RoundToInt(__state * PerkRules.SapperFactor(SkillEffects.Level(_player, SkillCatalog.Sapper))));
        }
        [HarmonyFinalizer]
        private static void Restore(HurtCollider __instance, int __state) => __instance.playerDamage = __state;
    }

    // Before death protection (which deliberately leaves 1 HP), after ordinary
    // damage reductions. Native invincibility/invalid-call rejection still applies.
    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Hurt))]
    private static class MonsterDamageFloor
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First + 100)]
        private static void Capture(int damage, out int __state) => __state = damage;
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last - 50)]
        private static void Apply(ref int damage, int enemyIndex, bool hurtByHeal, int __state)
        {
            if (hurtByHeal || __state <= 0) return;
            damage = PerkRules.FinalMobDamage(__state, damage, enemyIndex >= 0, Monsters.MonsterAfflictions.DotDamage);
        }
    }
}
