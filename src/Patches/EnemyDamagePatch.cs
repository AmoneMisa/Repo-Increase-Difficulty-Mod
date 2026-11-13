using HarmonyLib;
using UnityEngine;

namespace IncreaseHardMod.Patches;

/// <summary>
/// Scales damage the local player takes FROM ENEMIES.
///
/// PlayerHealth.Hurt(int damage, bool savingGrace, int enemyIndex, bool hurtByHeal)
/// is where a player actually loses health. It only runs on the machine that
/// owns that player (see the photonView.IsMine check inside), so scaling here is
/// automatically per-player-authoritative.
///
/// "enemyIndex" is -1 when the damage is NOT from an enemy (falls, thrown
/// objects, etc.). We only boost when enemyIndex >= 0 so we don't accidentally
/// buff environmental damage. "ref int damage" lets us rewrite the argument that
/// the original method will then use.
/// </summary>
[HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Hurt))]
internal static class EnemyDamagePatch
{
    // Run FIRST among our Hurt prefixes so the damage is fully scaled before
    // DeathProtection decides whether the (final) blow is lethal and clamps it.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(ref int damage, int enemyIndex, bool hurtByHeal)
    {
        // Not enemy damage, or it's actually a heal routed through Hurt.
        if (enemyIndex < 0 || hurtByHeal || damage <= 0)
            return;

        float mult = EnemyBalance.Damage;
        // Last moon: now and then a monster lands a critical hit for double damage.
        if (Economy.MoonRules.MonsterCrits(Economy.ExtendedMoons.Current, damage, Random.value))
        {
            mult *= Economy.MoonRules.MonsterCritMultiplier;
            Hud.HudNotify.Toast(Localization.GameLanguage.Russian ? "КРИТИЧЕСКИЙ УДАР МОНСТРА" : "MONSTER CRITICAL HIT", Hud.HudStyle.Bad, 1.5f, "monster-crit");
        }
        // A monster shocked by the Wizard Staff hits 25% softer.
        if (Gear.Shock.IsShocked(SemiFunc.EnemyGetFromIndex(enemyIndex))) mult *= Gear.Shock.MonsterDamageFactor;

        if (Mathf.Approximately(mult, 1f))
            return;

        // Round up so a boost always adds at least 1 damage.
        damage = Mathf.Max(1, Mathf.CeilToInt(damage * mult));
    }
}
