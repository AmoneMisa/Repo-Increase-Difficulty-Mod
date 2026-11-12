using HarmonyLib;
using UnityEngine;
using C = IncreaseHardMod.Skills.SkillCatalog;

namespace IncreaseHardMod.Skills;

internal static class WeaponAccuracy
{
    [HarmonyPatch(typeof(ItemGun), nameof(ItemGun.ShootRPC))]
    private static class Shot
    {
        [HarmonyPrefix]
        private static void Prefix(ItemGun __instance, out (float Spread, float Recoil) __state)
        {
            __state = (__instance.gunRandomSpread, __instance.gunRecoilForce);
            if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
            var grab = __instance.physGrabObject;
            int rank = SkillEffects.Level(grab != null ? grab.lastPlayerGrabbing : null, C.GunAccuracy);
            if (rank <= 0) return;
            float remaining = PerkRules.GunInaccuracy(rank);
            __instance.gunRandomSpread *= remaining;
            __instance.gunRecoilForce *= remaining;
        }

        [HarmonyFinalizer]
        private static void Restore(ItemGun __instance, (float Spread, float Recoil) __state)
        {
            __instance.gunRandomSpread = __state.Spread;
            __instance.gunRecoilForce = __state.Recoil;
        }
    }

    [HarmonyPatch(typeof(ItemMelee), nameof(ItemMelee.HurtColliderRotationLogic))]
    private static class Swing
    {
        [HarmonyPostfix]
        private static void Postfix(ItemMelee __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
            var grab = __instance.physGrabObject;
            if (grab == null || grab.playerGrabbing.Count == 0 || grab.playerGrabbing[0] == null) return;
            int rank = SkillEffects.Level(grab.playerGrabbing[0].playerAvatar, C.MeleeAccuracy);
            float blend = PerkRules.MeleeAlignmentPerRank * rank;
            if (__instance.TryGetComponent<Gear.Katana>(out _)) blend = Mathf.Lerp(.9f, 1f, blend / .75f);
            if (blend <= 0f || __instance.hurtColliderRotation == null || __instance.swingPoint == null) return;
            var direction = __instance.swingPoint.position - __instance.prevPosition;
            if (direction.sqrMagnitude <= .01f || Vector3.ProjectOnPlane(direction, Vector3.up).sqrMagnitude < .0001f) return;

            // Ease the game's 90-degree snap toward the actual direction of the swing.
            var hit = __instance.hurtColliderRotation;
            var aimed = Quaternion.LookRotation(direction, Vector3.up);
            if (hit.parent != null) aimed = Quaternion.Inverse(hit.parent.rotation) * aimed;
            float yaw = Mathf.LerpAngle(hit.localEulerAngles.y, aimed.eulerAngles.y, blend);
            hit.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }

    [HarmonyPatch(typeof(ItemMeleeWhip), nameof(ItemMeleeWhip.CrackTargetGet))]
    private static class Whip
    {
        [HarmonyPrefix]
        private static void Prefix(ItemMeleeWhip __instance, out float __state)
        {
            __state = __instance.catchRadius;
            if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
            int rank = SkillEffects.Level(__instance.holder, C.MeleeAccuracy);
            __instance.catchRadius *= 1f + PerkRules.WhipRadiusPerRank * rank;
        }

        [HarmonyFinalizer]
        private static void Restore(ItemMeleeWhip __instance, float __state) => __instance.catchRadius = __state;
    }
}
