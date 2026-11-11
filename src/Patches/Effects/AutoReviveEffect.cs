using IncreaseHardMod.Quests;
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
/// ReviveRPC uses the death head's position. Recover heads from lethal pits at the truck
/// even without the modifier, so they are available for a manual revive. AutoReviveAtCar
/// also moves heads there on ordinary deaths and enables the truck's heal.
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

    [HarmonyPrefix]
    private static void Prefix(ref int enemyIndex, out bool __state)
    {
        __state = enemyIndex == LethalPitPatch.PitDeath;
        if (__state) enemyIndex = -1;
    }

    [HarmonyPostfix]
    private static void Postfix(PlayerAvatar __instance, bool __state)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || __instance == null || !__instance.deadSet) return;
        var runner = ModNetwork.Instance;
        if (runner == null) return;
        bool autoRevive = ModifierManager.Current.Has(ModifierType.AutoRevive) && UsedThisLevel.Add(__instance);
        if (__state || autoRevive) runner.StartCoroutine(ResolveDeathWhenReady(__instance, __state, autoRevive));
    }

    /// <summary>
    /// Wait until the death sequence has FULLY settled, then revive.
    ///
    /// It is NOT enough to wait for the death head: that head is pre-spawned at level start
    /// (LevelGenerator gives every avatar one before any death), so it's already non-null the
    /// instant PlayerDeathRPC fires. Reviving then runs mid-death-animation, and the still-pending
    /// PlayerDeathDone() — which sets isDisabled=true and disables the avatar GameObject — fires a
    /// moment later and RE-KILLS the freshly revived player, dropping them into the spectating
    /// death head ("flying at 0 HP"). Wait for isDisabled and the end of the head's placement timer.
    /// </summary>
    private static IEnumerator ResolveDeathWhenReady(PlayerAvatar player, bool recoverHead, bool autoRevive)
    {
        const float timeout = 10f;
        float waited = 0f;

        while (player != null && player.deadSet && waited < timeout && !DeathReady(player))
        {
            waited += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        if (!SemiFunc.IsMasterClientOrSingleplayer() || !DeathReady(player))
            yield break;

        bool atCar = (recoverHead || ModConfig.AutoReviveAtCar.Value) && TryMoveHeadToSafety(player.playerDeathHead);
        if (!autoRevive) yield break;
        if (atCar)
        {
            // Revive reads the grab object's cached center, refreshed after teleport.
            yield return new WaitForSeconds(0.15f);
            if (!SemiFunc.IsMasterClientOrSingleplayer() || !DeathReady(player)) yield break;
        }
        QuestManager.MarkAutoRevive(player); // the mod's free revive is not a teammate rescue
        player.Revive(atCar && ModConfig.AutoReviveAtCar.Value && TruckHealer.instance != null);
    }

    private static bool DeathReady(PlayerAvatar player) =>
        player != null && player.deadSet && player.isDisabled && player.playerDeathHead != null &&
        player.playerDeathHead.triggered && player.playerDeathHead.triggeredTimer <= 0f;

    /// <summary>
    /// Teleport the player's death head to a level spawn point (the truck/car drop-off).
    /// ReviveRPC reads the head's position for the respawn, so moving it here makes the
    /// player come back at the car. Returns false if no spawn point was found.
    /// </summary>
    internal static bool TryMoveHeadToSafety(PlayerDeathHead head, Transform preferredPoint = null)
    {
        if (head == null || head.physGrabObject == null)
            return false;

        var safety = TruckSafetySpawnPoint.instance;
        var fallback = preferredPoint == null && safety == null ? FindCarSpawnPoint() : null;
        var spawn = preferredPoint != null ? preferredPoint :
            safety != null ? safety.transform : fallback != null ? fallback.transform : null;
        if (spawn == null)
            return false;

        var grab = head.physGrabObject;
        for (int i = grab.playerGrabbing.Count - 1; i >= 0; i--)
            if (grab.playerGrabbing[i] != null)
                grab.playerGrabbing[i].OverrideGrabRelease(grab.photonView != null ? grab.photonView.ViewID : -1);
        // Use the exact position of authored safety points, as the game does.
        var position = fallback != null ? spawn.position + Vector3.up : spawn.position;
        grab.Teleport(position, spawn.rotation);
        if (grab.rb != null)
        {
            grab.rb.velocity = Vector3.zero;
            grab.rb.angularVelocity = Vector3.zero;
        }
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
