using HarmonyLib;
using UnityEngine;
using C = IncreaseHardMod.Skills.SkillCatalog;

namespace IncreaseHardMod.Skills;

/// <summary>
/// Survival perks about monster souls (EnemyValuable orbs):
///  - Soul Ward: an exploding soul (moon 4+) hurts you less. The soul's explosion object is marked when it goes off
///    (EnemyValuableExplosion.Start, the same soul-only hook Souls Explode Harder uses); its hurt collider then deals
///    less to a player with the perk. Each client hurts only its own player, so this runs where the damage lands.
///  - Soul Keeper: a soul you are holding loses less value when it is hit. Loot damage is decided by the host
///    (PhysGrabObjectImpactDetector.Break), reading the synced perk levels of whoever holds the soul.
/// </summary>
internal static class SoulPerks
{
    internal const float WardPerLevel = .2f, KeeperPerLevel = .15f;

    /// <summary>Marks a soul's explosion so its hurt colliders know where they came from.</summary>
    internal sealed class SoulBlast : MonoBehaviour { }

    private static bool FromSoul(Component c) => c != null && c.GetComponentInParent<SoulBlast>() != null;

    [HarmonyPatch(typeof(EnemyValuableExplosion), "Start")]
    private static class MarkBlast
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyValuableExplosion __instance)
        {
            var root = __instance.transform.root.gameObject;
            if (root.GetComponent<SoulBlast>() == null) root.AddComponent<SoulBlast>();
        }
    }

    // A soul explosion that spawns its blast as a separate object passes the mark on to it.
    [HarmonyPatch(typeof(ParticleScriptExplosion), nameof(ParticleScriptExplosion.Spawn))]
    private static class MarkSpawnedBlast
    {
        [HarmonyPostfix]
        private static void Postfix(ParticleScriptExplosion __instance, ParticlePrefabExplosion __result)
        {
            if (__result == null || !FromSoul(__instance)) return;
            var root = __result.transform.root.gameObject;
            if (root.GetComponent<SoulBlast>() == null) root.AddComponent<SoulBlast>();
        }
    }

    [HarmonyPatch(typeof(HurtCollider), "PlayerHurt")]
    private static class WardBlast
    {
        [HarmonyPrefix]
        private static void Prefix(HurtCollider __instance, PlayerAvatar _player, out int __state)
        {
            __state = -1;
            if (_player == null || __instance.playerDamage <= 0 || !FromSoul(__instance)) return;
            int level = SkillEffects.Level(_player, C.SoulWard);
            if (level <= 0) return;
            __state = __instance.playerDamage;
            float factor = Mathf.Clamp01(1f - WardPerLevel * level);
            __instance.playerDamage = Mathf.Max(1, Mathf.RoundToInt(__instance.playerDamage * factor));
        }

        [HarmonyPostfix]
        private static void Postfix(HurtCollider __instance, int __state)
        {
            if (__state >= 0) __instance.playerDamage = __state;
        }
    }

    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.Break))]
    private static class KeepSoul
    {
        [HarmonyPrefix]
        private static void Prefix(PhysGrabObjectImpactDetector __instance, ref float valueLost)
        {
            if (valueLost <= 0f || !__instance.isValuable || !SemiFunc.IsMasterClientOrSingleplayer()) return;
            var grab = __instance.physGrabObject;
            if (grab == null || grab.playerGrabbing.Count == 0 || grab.GetComponent<EnemyValuable>() == null) return;
            int level = 0;
            foreach (var grabber in grab.playerGrabbing)
                if (grabber != null) level = Mathf.Max(level, SkillEffects.Level(grabber.playerAvatar, C.SoulKeeper));
            if (level > 0) valueLost *= Mathf.Clamp01(1f - KeeperPerLevel * level);
        }
    }
}
