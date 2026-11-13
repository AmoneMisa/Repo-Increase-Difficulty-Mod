using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Monsters;

/// <summary>
/// Elite monsters on deep levels (see <see cref="EliteRules"/>): Armored takes 30% less damage until it is stunned
/// for real, Regenerating heals after five quiet seconds, Thorned reflects 10% of melee damage. Every client rolls
/// the same trait from the view id, so pings can name it; only the host changes HP.
/// </summary>
internal static class EliteMonsters
{
    private sealed class State
    {
        internal EliteTrait Trait;
        internal int Level = -1;
        internal bool ArmorBroken;
        internal float LastHurt;
        internal float Carry;
    }

    private static readonly ConditionalWeakTable<Enemy, State> States = new();

    private static bool Enabled => ModConfig.EliteMonstersEnabled.Value;

    private static State For(Enemy enemy)
    {
        if (enemy == null || enemy.PhotonView == null) return null;
        var state = States.GetOrCreateValue(enemy);
        int level = Difficulty.LevelsCompleted;
        if (state.Level != level)
        {
            // A pooled monster can come back on a later level: roll again for the new depth.
            state.Level = level;
            state.Trait = EliteRules.Roll(enemy.PhotonView.ViewID, level);
            state.ArmorBroken = false;
            state.Carry = 0f;
            state.LastHurt = Time.time;
        }
        return state;
    }

    internal static EliteTrait Trait(Enemy enemy) => Enabled ? For(enemy)?.Trait ?? EliteTrait.None : EliteTrait.None;

    /// <summary>Thorned elites reflect this share of melee damage (0 for every other monster).</summary>
    internal static float ThornShare(Enemy enemy) => Trait(enemy) == EliteTrait.Thorned ? EliteRules.ThornShare : 0f;

    internal static string PingLabel(Enemy enemy)
    {
        var trait = Trait(enemy);
        return trait == EliteTrait.None ? null : EliteRules.Name(trait, Localization.GameLanguage.Russian);
    }

    [HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.Hurt))]
    private static class Hurt
    {
        [HarmonyPrefix, HarmonyPriority(Priority.Low)]
        private static void Prefix(EnemyHealth __instance, ref int _damage)
        {
            if (!Enabled || !SemiFunc.IsMasterClientOrSingleplayer() || _damage <= 0 || __instance.dead) return;
            var state = For(__instance.enemy);
            if (state == null || state.Trait == EliteTrait.None) return;
            state.LastHurt = Time.time;
            if (state.Trait == EliteTrait.Armored) _damage = EliteRules.ArmoredDamage(_damage, state.ArmorBroken);
        }
    }

    [HarmonyPatch(typeof(EnemyStateStunned), nameof(EnemyStateStunned.Set))]
    private static class Stunned
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyStateStunned __instance, float time)
        {
            // Grab upkeep calls Set(.1) every frame; only a real stun cracks the armour.
            if (!Enabled || time <= .1f || !SemiFunc.IsMasterClientOrSingleplayer()) return;
            var state = For(__instance.enemy);
            if (state != null && state.Trait == EliteTrait.Armored) state.ArmorBroken = true;
        }
    }

    [HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.Update))]
    private static class Regenerate
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyHealth __instance)
        {
            if (!Enabled || !SemiFunc.IsMasterClientOrSingleplayer() || __instance.dead || __instance.healthCurrent <= 0 ||
                __instance.healthCurrent >= __instance.health) return;
            var state = For(__instance.enemy);
            if (state == null || state.Trait != EliteTrait.Regenerating) return;
            int heal = EliteRules.Regen(__instance.health, Time.deltaTime, Time.time - state.LastHurt, ref state.Carry);
            if (heal > 0) __instance.healthCurrent = Mathf.Min(__instance.health, __instance.healthCurrent + heal);
        }
    }
}
