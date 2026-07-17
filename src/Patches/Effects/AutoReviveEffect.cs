using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using IncreaseHardMod.Modifiers;
using IncreaseHardMod.Networking;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// AutoRevive modifier — each player gets ONE free automatic revive per special
/// level. When a player dies, the host brings them back once; further deaths that
/// same level are permanent (until a teammate/truck revives them normally).
///
/// Death funnels through PlayerAvatar.PlayerDeathRPC. We can't revive instantly
/// there: PlayerAvatar.Revive() needs the player's death head to exist (ReviveRPC
/// dereferences playerDeathHead for the respawn position, and logs an error if it's
/// null). The death head spawns as part of the death sequence and assigns itself to
/// playerAvatar.playerDeathHead a moment later. So our Postfix starts a coroutine
/// (hosted on the always-alive ModNetwork object) that waits for that head, then
/// calls Revive().
///
/// Host authority: Revive() issues an RPC to everyone, so exactly one client — the
/// host — must trigger it; we gate on IsMasterClientOrSingleplayer. The one-use-per
/// -level budget is tracked in a set keyed by PlayerAvatar and cleared every time
/// the modifier set changes (ModifierManager.SetCurrent -> ResetForNewLevel), which
/// is precisely once per level transition.
/// </summary>
[HarmonyPatch(typeof(PlayerAvatar), "PlayerDeathRPC")]
internal static class AutoReviveEffect
{
    private static readonly HashSet<PlayerAvatar> UsedThisLevel = new();

    /// <summary>Called from ModifierManager.SetCurrent on every level change.</summary>
    public static void ResetForNewLevel()
    {
        UsedThisLevel.Clear();
    }

    [HarmonyPostfix]
    private static void Postfix(PlayerAvatar __instance)
    {
        if (!ModifierManager.Current.Has(ModifierType.AutoRevive))
            return;

        // Only the host drives revives (Revive() RPCs to everyone).
        if (!SemiFunc.IsMasterClientOrSingleplayer())
            return;

        if (__instance == null || UsedThisLevel.Contains(__instance))
            return;

        UsedThisLevel.Add(__instance);

        var runner = ModNetwork.Instance;
        if (runner == null)
            return;

        runner.StartCoroutine(ReviveWhenReady(__instance));
    }

    /// <summary>
    /// Wait until the player's death head exists (so Revive() has a valid respawn
    /// anchor), then revive. Give up after a few seconds as a safety net so we never
    /// spin forever if the head never spawns.
    /// </summary>
    private static IEnumerator ReviveWhenReady(PlayerAvatar player)
    {
        const float timeout = 5f;
        float waited = 0f;

        while (player != null && player.playerDeathHead == null && waited < timeout)
        {
            waited += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        if (player == null || player.playerDeathHead == null)
            yield break;

        // Still the host and still dead? Then bring them back.
        if (SemiFunc.IsMasterClientOrSingleplayer() && player.deadSet)
            player.Revive();
    }
}
