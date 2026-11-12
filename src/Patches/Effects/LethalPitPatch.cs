using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace IncreaseHardMod.Patches.Effects;

[HarmonyPatch(typeof(HurtCollider), nameof(HurtCollider.PlayerHurt))]
internal static class LethalPitPatch
{
    internal const int PitDeath = -2; // normalized to -1 by AutoReviveEffect before vanilla sees it

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var hurt = AccessTools.Method(typeof(PlayerHealth), nameof(PlayerHealth.Hurt));
        var resolve = AccessTools.Method(typeof(LethalPitPatch), nameof(ResolveHit));
        foreach (var instruction in instructions)
        {
            if (!instruction.Calls(hurt))
            {
                yield return instruction;
                continue;
            }

            yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
            yield return new CodeInstruction(OpCodes.Call, resolve);
        }
    }

    private static void ResolveHit(PlayerHealth health, int damage, bool savingGrace, int enemyIndex, bool hurtByHeal,
        HurtCollider collider)
    {
        if (!collider.deathPit || !collider.playerKill)
        {
            health.Hurt(damage, savingGrace, enemyIndex, hurtByHeal);
            return;
        }

        // Lethal pits complete the native death sequence without going through damage perks.
        // Keep the game's temporary invulnerability, including its grace period after revival.
        var player = health.playerAvatar;
        if (player == null || player.deadSet || player.isDisabled || health.godMode || health.invincibleTimer > 0f ||
            GameDirector.instance == null || GameDirector.instance.currentState != GameDirector.gameState.Main ||
            (!health.healthSet && LateJoinProgress.Waiting)) return;
        player.PlayerDeath(PitDeath);
    }
}
