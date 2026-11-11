using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using IncreaseHardMod.Upgrades;
using UnityEngine;
using C = IncreaseHardMod.Skills.SkillCatalog;
using Object = UnityEngine.Object;

namespace IncreaseHardMod.Skills;

internal static class GrenadePerks
{
    internal static int BlastRank(ItemGrenade grenade, PlayerAvatar thrower)
    {
        if (grenade.GetComponent<ItemGrenadeExplosive>() != null) return SkillEffects.Level(thrower, C.ExplosiveGrenade);
        // The three child grenades inherit the activator and the parent's specialisation.
        if (grenade.GetComponent<ItemGrenadeDuctTaped>() != null || grenade.GetComponent<ItemGrenadeHuman>() != null)
            return SkillEffects.Level(thrower, C.ClusterGrenade);
        return 0;
    }

    internal static int DamageWithGeneralPerks(int damage, PlayerAvatar thrower) =>
        damage > 0 ? Mathf.Max(1, Mathf.RoundToInt(damage *
            CombatDamageUpgrades.Multiplier(CombatDamageUpgrades.GrenadeId, thrower) *
            TradeOffs.DamageFactor(thrower, TradeOffs.Damage.Grenade))) : damage;

    internal static void BoostStun(HurtCollider hurt, int rank)
    {
        if (hurt == null || rank <= 0) return;
        if (!hurt.enemyLogic)
        {
            hurt.enemyLogic = true;
            hurt.enemyKill = false;
        }
        float original = hurt.enemyStun ? Mathf.Max(0f, hurt.enemyStunTime) : 0f;
        hurt.enemyStun = true;
        hurt.enemyStunTime = Mathf.Min(original + PerkRules.GrenadeStunPerRank * rank, Mathf.Max(original, C.StatusCapSeconds));
        hurt.enemyDamageCooldown = Mathf.Max(.25f, hurt.enemyDamageCooldown);
    }

    private static void BoostUtility(HurtCollider hurt, PlayerAvatar thrower, int rank)
    {
        if (hurt == null || rank <= 0) return;
        BoostStun(hurt, rank);
        hurt.enemyDamage = DamageWithGeneralPerks(PerkRules.GrenadeDamage(hurt.enemyDamage, rank), thrower);
    }

    [HarmonyPatch]
    private static class UtilityExplosion
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ItemGrenadeStun), nameof(ItemGrenadeStun.Explosion));
            yield return AccessTools.Method(typeof(ItemGrenadeShockwave), nameof(ItemGrenadeShockwave.Explosion));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var replace = AccessTools.Method(typeof(UtilityExplosion), nameof(Spawn));
            foreach (var code in instructions)
            {
                if (code.opcode != OpCodes.Call || code.operand is not MethodInfo method || method.DeclaringType != typeof(Object) ||
                    method.Name != nameof(Object.Instantiate) || method.ReturnType != typeof(GameObject) ||
                    method.GetParameters().Length != 3)
                {
                    yield return code;
                    continue;
                }
                yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(code).MoveBlocksFrom(code);
                yield return new CodeInstruction(OpCodes.Call, replace);
            }
        }

        private static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Component grenade)
        {
            var blast = Object.Instantiate(prefab, position, rotation);
            var source = grenade.GetComponent<ItemGrenade>();
            var thrower = source != null ? source.activatorPlayerAvatar : null;
            foreach (var collider in blast.GetComponentsInChildren<HurtCollider>(true))
            {
                collider.gameObject.AddComponent<Gear.GrenadeBlast>().Thrower = thrower;
                collider.playerCausingHurtOverride = thrower;
            }
            bool stun = grenade is ItemGrenadeStun;
            int rank = SkillEffects.Level(thrower, stun ? C.StunGrenade : C.ShockwaveGrenade);
            if (rank > 0)
            {
                var hurt = blast.GetComponentInChildren<HurtCollider>(true);
                var range = stun && hurt != null ? hurt.transform : blast.transform;
                range.localScale *= PerkRules.GrenadeRadius(rank);
                // Capture the activator here, before the grenade is consumed and the blast's Start runs.
                if (hurt != null) hurt.playerCausingHurtOverride = thrower;
                BoostUtility(hurt, thrower, rank);
            }
            return blast;
        }
    }
}
