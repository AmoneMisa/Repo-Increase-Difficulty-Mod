using HarmonyLib;
using IncreaseHardMod.Modifiers;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// MonstersPitTanky modifier — monsters take less damage from death pits, so
/// shoving an enemy into a pit is a weaker tactic.
///
/// Death-pit damage is not a special code path in EnemyHealth; it flows through
/// the same EnemyHealth.Hurt(int, Vector3) that every hit uses. The pit-specific
/// context lives in HurtCollider.EnemyHurt: only when a collider is a deathPit AND
/// the moon is level 2+ AND the enemy still has its health cooldown ready does the
/// game deal CHIP damage (120/80/40 by moon) instead of an instant kill. On moons
/// below 2 a pit is an instant kill (enemyKill = true) and we deliberately leave
/// that alone — surviving an intended instakill would be buggy.
///
/// So we can't just reduce all EnemyHurt damage. Instead we bracket that one chip
/// branch: a Prefix on EnemyHurt raises a static flag ONLY when the exact chip
/// condition holds, a Prefix on EnemyHealth.Hurt scales the damage while the flag
/// is up, and a Finalizer restores the context even after an exception. Unity runs all this on the
/// single main thread, so the static flag is safe. HurtCollider.EnemyHurt itself
/// only ever runs host-side (its callers gate on IsMasterClientOrSingleplayer),
/// and health is host-authoritative, so the reduction applies once and syncs.
/// </summary>
internal static class MonsterPitTankyEffect
{
    // True only for the duration of a single pit chip-damage Hurt call.
    private static bool _inPitChip;

    [HarmonyPatch(typeof(HurtCollider), nameof(HurtCollider.EnemyHurt))]
    private static class Bracket
    {
        [HarmonyPrefix]
        private static void Prefix(HurtCollider __instance, Enemy _enemy, out bool __state)
        {
            __state = _inPitChip;
            _inPitChip = false;

            if (!EnemyBalance.Surprises || !ModifierManager.Current.Has(ModifierType.MonstersPitTanky) ||
                !SemiFunc.IsMasterClientOrSingleplayer() || !__instance.enemyLogic || _enemy == __instance.enemyHost)
                return;

            // Reproduce the game's chip-damage condition exactly (see EnemyHurt):
            // pit + moon 2+ + enemy has health + its pit cooldown is ready.
            if (!__instance.deathPit)
                return;
            if (SemiFunc.MoonLevel() < 2)
                return;
            if (_enemy == null || !_enemy.HasHealth)
                return;
            if (_enemy.Health.deathPitCooldown > 0f)
                return;

            _inPitChip = true;
        }

        [HarmonyFinalizer]
        private static void Finalizer(bool __state)
        {
            _inPitChip = __state;
        }
    }

    [HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.Hurt))]
    private static class Reduce
    {
        [HarmonyPrefix]
        private static void Prefix(ref int _damage)
        {
            if (!_inPitChip || _damage <= 0)
                return;

            float factor = Mathf.Clamp(EnemyBalance.PitDamage, 0f, 5f);
            _damage = Mathf.Max(0, Mathf.RoundToInt(_damage * factor));
        }
    }
}
