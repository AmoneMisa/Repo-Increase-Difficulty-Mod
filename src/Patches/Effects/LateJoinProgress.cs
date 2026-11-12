using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>Fetch the host's saved player stats before a joining client initializes its avatar.</summary>
internal static class LateJoinProgress
{
    internal const string Property = "ihm_join_progress";
    internal const byte RequestCode = 159;
    private const string SnapshotMarker = "ihm_join_snapshot";
    private static Room _pendingRoom;
    private static float _nextRequest;
    private static float _firstRequest;

    private static bool Pending => ModConfig.LateJoinEnabled.Value && _pendingRoom != null &&
        PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom == _pendingRoom && !PhotonNetwork.IsMasterClient;

    internal static bool Waiting => Pending && RunManager.instance != null && !SemiFunc.MenuLevel();

    /// <summary>When this client joined a run already in progress (realtime seconds), or -1.</summary>
    internal static float JoinedLateAt = -1f;

    internal static void JoinedRoom()
    {
        _pendingRoom = null;
        JoinedLateAt = -1f;
        if (!ModConfig.LateJoinEnabled.Value || !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient) return;
        var room = PhotonNetwork.CurrentRoom;
        if (room.CustomProperties[Property] is true)
        {
            _pendingRoom = room;
            JoinedLateAt = Time.realtimeSinceStartup;
            _firstRequest = -1f;
            _nextRequest = 0f;
        }
    }

    internal static void Tick()
    {
        if (!Pending) { _pendingRoom = null; return; }
        var pun = PunManager.instance;
        var local = SemiFunc.PlayerAvatarLocal();
        if (!Waiting || pun == null || pun.photonView == null || pun.photonView.ViewID == 0 || pun.statsManager == null ||
            local == null || string.IsNullOrEmpty(local.steamID)) return;

        float now = Time.realtimeSinceStartup;
        if (now < _nextRequest) return;
        if (_firstRequest < 0f) _firstRequest = now;
        if (now - _firstRequest >= 20f)
        {
            _pendingRoom = null;
            Plugin.Log.LogWarning("[LateJoin] Timed out waiting for the host's player stats.");
            return;
        }
        _nextRequest = now + 1f;
        PhotonNetwork.RaiseEvent(RequestCode, null,
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    internal static void OnRequest(int actor)
    {
        if (!ModConfig.LateJoinEnabled.Value || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient ||
            LevelGenerator.Instance == null || !LevelGenerator.Instance.Generated ||
            (!SemiFunc.RunIsLobby() && !SemiFunc.RunIsShop() && !SemiFunc.RunIsLevel())) return;
        var target = PhotonNetwork.CurrentRoom.GetPlayer(actor);
        var pun = PunManager.instance;
        if (target == null || pun == null || pun.photonView == null || pun.statsManager == null || GameDirector.instance == null)
            return;

        var steamIds = new HashSet<string>(StringComparer.Ordinal);
        bool playerReady = false;
        foreach (var player in GameDirector.instance.PlayerList)
        {
            if (player == null || string.IsNullOrEmpty(player.steamID)) continue;
            steamIds.Add(player.steamID);
            if (player.photonView != null && player.photonView.Owner == target) playerReady = true;
        }
        if (!playerReady) return; // the retry will find this actor once their avatar has identified itself

        var snapshot = new Hashtable { [SnapshotMarker] = true };
        foreach (var (key, dictionary) in pun.statsManager.dictionaryOfDictionaries)
        {
            if (key != "playerHealth" && !key.StartsWith("playerUpgrade", StringComparison.Ordinal)) continue;
            var levels = new Hashtable();
            foreach (string steamId in steamIds)
                if (dictionary.TryGetValue(steamId, out int value)) levels.Add(steamId, value);
            snapshot.Add(key, levels);
        }
        pun.photonView.RPC(nameof(PunManager.ReceiveSyncData), target, snapshot, false);
    }

    [HarmonyPatch(typeof(PunManager), nameof(PunManager.ReceiveSyncData))]
    private static class ReceiveProgress
    {
        [HarmonyPrefix]
        private static bool Prefix(Hashtable data, PhotonMessageInfo _info, out bool __state)
        {
            __state = data?[SnapshotMarker] is true;
            return !__state || (Pending && _info.Sender != null && _info.Sender == PhotonNetwork.MasterClient);
        }

        [HarmonyPostfix]
        private static void Postfix(bool __state, bool __runOriginal)
        {
            if (!__state || !__runOriginal) return;
            _pendingRoom = null;
            Plugin.Log.LogInfo("[LateJoin] Applied the host's saved health, upgrades and skill levels.");
        }
    }

    /// <summary>
    /// A late joiner's max health can be computed before the host's upgrade levels arrive (100 + 20 per health
    /// upgrade: 120 instead of 180), and the game never recomputes it during the level. For two minutes after
    /// joining, compare it with the synced upgrades once a second and raise it. Current health is only restored
    /// to the host's saved value when it was merely clamped by the stale maximum; a wounded player is never healed.
    /// </summary>
    internal static void ReconcileHealth()
    {
        if (JoinedLateAt < 0f || Time.realtimeSinceStartup - JoinedLateAt > 120f || Waiting || Time.realtimeSinceStartup < _nextReconcile) return;
        _nextReconcile = Time.realtimeSinceStartup + 1f;
        var avatar = SemiFunc.PlayerAvatarLocal();
        var ph = avatar != null ? avatar.playerHealth : null;
        var stats = StatsManager.instance;
        if (ph == null || !ph.healthSet || stats == null || avatar.isDisabled || avatar.deadSet) return;
        string steamId = SemiFunc.PlayerGetSteamID(avatar);
        if (string.IsNullOrEmpty(steamId)) return;
        int expectedMax = 100 + stats.GetPlayerMaxHealth(steamId);
        if (expectedMax <= ph.maxHealth) return;
        int saved = stats.GetPlayerHealth(steamId);
        int health = LateJoinRules.ReconciledHealth(ph.health, ph.maxHealth, saved, expectedMax);
        Plugin.Log.LogInfo($"[LateJoin] Max health corrected from the host's upgrades: {ph.health}/{ph.maxHealth} -> {health}/{expectedMax}.");
        ph.maxHealth = expectedMax;
        ph.health = health;
        stats.SetPlayerHealth(steamId, health, setInShop: false);
        if (GameManager.Multiplayer() && ph.photonView != null)
            ph.photonView.RPC("UpdateHealthRPC", RpcTarget.Others, ph.health, ph.maxHealth, true, false);
    }

    private static float _nextReconcile;

    [HarmonyPatch]
    private static class ProtectUninitializedHealth
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PlayerHealth), nameof(PlayerHealth.Heal));
            yield return AccessTools.Method(typeof(PlayerHealth), nameof(PlayerHealth.Hurt));
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(PlayerHealth __instance) =>
            __instance.healthSet || !Waiting || __instance.photonView == null || !__instance.photonView.IsMine;
    }

    // These native coroutines read their upgrades once and some write the resulting stats back to the run.
    [HarmonyPatch]
    private static class WaitForProgress
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PlayerHealth), nameof(PlayerHealth.Fetch));
            yield return AccessTools.Method(typeof(PlayerAvatar), nameof(PlayerAvatar.LateStart));
            yield return AccessTools.Method(typeof(PlayerController), nameof(PlayerController.LateStart));
            yield return AccessTools.Method(typeof(PhysGrabber), nameof(PhysGrabber.LateStart));
            yield return AccessTools.Method(typeof(PlayerTumble), nameof(PlayerTumble.TumbleLaunchFetch));
        }

        [HarmonyPostfix]
        private static void Postfix(ref IEnumerator __result)
        {
            if (Waiting) __result = AfterProgress(__result);
        }

        private static IEnumerator AfterProgress(IEnumerator original)
        {
            while (Waiting) yield return null;
            yield return original;
        }
    }
}
