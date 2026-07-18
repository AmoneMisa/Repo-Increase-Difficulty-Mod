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
/// Where they come back: ReviveRPC positions the player at their death head. When
/// AutoReviveAtCar is on, we first teleport the death head to a level SpawnPoint (the
/// truck/car drop-off) so the player revives back at the car instead of on the spot,
/// and revive "by truck" so they're fully healed. Otherwise they revive in place.
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
        if (!SemiFunc.IsMasterClientOrSingleplayer() || !player.deadSet)
            yield break;

        if (ModConfig.AutoReviveAtCar.Value && TryMoveHeadToCar(player))
        {
            // Revive "by truck" so TruckHealer fully heals them at the car.
            player.Revive(TruckHealer.instance != null);
        }
        else
        {
            player.Revive();
        }
    }

    /// <summary>
    /// Teleport the player's death head to a level spawn point (the truck/car drop-off).
    /// ReviveRPC reads the head's position for the respawn, so moving it here makes the
    /// player come back at the car. Returns false if no spawn point was found.
    /// </summary>
    private static bool TryMoveHeadToCar(PlayerAvatar player)
    {
        var head = player.playerDeathHead;
        if (head == null || head.physGrabObject == null)
            return false;

        var spawn = FindCarSpawnPoint();
        if (spawn == null)
            return false;

        head.physGrabObject.Teleport(spawn.transform.position + Vector3.up, spawn.transform.rotation);
        return true;
    }

    private static SpawnPoint FindCarSpawnPoint()
    {
        var points = Object.FindObjectsOfType<SpawnPoint>();
        if (points == null || points.Length == 0)
            return null;

        // Prefer a debug/authored spawn (that's what the game uses for the real start
        // position when present), otherwise any spawn point.
        foreach (var p in points)
        {
            if (p != null && p.debug)
                return p;
        }
        return points[0];
    }
}
