using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Photon.Pun;
using Photon.Voice.PUN;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Guest-side diagnostics for "a late joiner does not see or hear the players already there". For the
/// first 90 s after joining a room as a guest it logs every networked object that arrives from the room
/// cache (player avatars and Voice objects matter here), PUN's scene cleanup of instantiated objects,
/// and, once the level is up, which room members still have no avatar or no voice object on this client.
/// Read-only: it changes no game state.
/// </summary>
internal static class LateJoinNetworkDiagnostics
{
    private static float _joinedAt = -1f, _nextCheck;
    private static string _lastReport;

    private static bool Watching => ModConfig.LateJoinEnabled.Value && _joinedAt >= 0f && PhotonNetwork.InRoom &&
                                    !PhotonNetwork.IsMasterClient && Time.realtimeSinceStartup - _joinedAt < 90f;

    internal static void Joined()
    {
        if (PhotonNetwork.IsMasterClient) { _joinedAt = -1f; return; }
        _joinedAt = Time.realtimeSinceStartup; _nextCheck = 0f; _lastReport = null;
        var room = PhotonNetwork.CurrentRoom;
        Plugin.Log.LogInfo($"[LateJoin] Guest joined room {room?.Name}: players={room?.PlayerCount}, scene={SceneManager.GetActiveScene().name}, queue={PhotonNetwork.IsMessageQueueRunning}, syncScene={PhotonNetwork.AutomaticallySyncScene}.");
    }

    [HarmonyPatch(typeof(NetworkConnect), "OnJoinedRoom")]
    private static class JoinedPatch
    {
        [HarmonyPostfix] private static void Postfix() { try { Joined(); } catch { } }
    }

    [HarmonyPatch(typeof(PhotonNetwork), "NetworkInstantiate", new[] { typeof(ExitGames.Client.Photon.Hashtable), typeof(Photon.Realtime.Player) })]
    private static class CachedSpawnPatch
    {
        [HarmonyPostfix]
        // Positional argument (__1): Harmony binds named parameters by name, and PUN's parameter names are not ours to rely on.
        private static void Postfix(GameObject __result, object[] __args)
        {
            if (!Watching || __result == null) return;
            var creator = __args != null && __args.Length > 1 ? __args[1] as Photon.Realtime.Player : null;
            try
            {
                bool interesting = __result.GetComponent<PlayerAvatar>() != null || __result.GetComponent<PhotonVoiceView>() != null;
                if (!interesting) return;
                var view = __result.GetComponent<PhotonView>();
                Plugin.Log.LogInfo($"[LateJoin] Received spawn {__result.name} from {creator?.NickName ?? "?"} (actor {creator?.ActorNumber}), view {view?.ViewID}, scene={__result.scene.name}, t+{Time.realtimeSinceStartup - _joinedAt:0.0}s.");
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(PhotonNetwork), "LocalCleanupAnythingInstantiated")]
    private static class CleanupPatch
    {
        [HarmonyPrefix]
        private static void Prefix(object[] __args)
        {
            if (!Watching) return;
            string flags = __args != null ? string.Join(",", __args) : "?";
            Plugin.Log.LogInfo($"[LateJoin] PUN local cleanup of instantiated objects ({flags}), t+{Time.realtimeSinceStartup - _joinedAt:0.0}s.");
        }
    }

    /// <summary>Every 5 s while watching: which room members have no avatar / no voice here.</summary>
    internal static void Tick()
    {
        if (!Watching || Time.realtimeSinceStartup < _nextCheck) return;
        _nextCheck = Time.realtimeSinceStartup + 5f;
        try
        {
            var gen = LevelGenerator.Instance;
            var avatars = Object.FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None);
            var voices = Object.FindObjectsByType<PhotonVoiceView>(FindObjectsSortMode.None);
            var withAvatar = new HashSet<int>(avatars.Where(a => a.photonView != null && a.photonView.Owner != null).Select(a => a.photonView.Owner.ActorNumber));
            var withVoice = new HashSet<int>(voices.Select(v => v.GetComponent<PhotonView>()).Where(v => v != null && v.Owner != null).Select(v => v.Owner.ActorNumber));
            var noAvatar = PhotonNetwork.PlayerList.Where(p => !withAvatar.Contains(p.ActorNumber)).Select(p => p.NickName);
            var noVoice = PhotonNetwork.PlayerList.Where(p => !withVoice.Contains(p.ActorNumber)).Select(p => p.NickName);
            string report = $"generated={(gen != null && gen.Generated)}, avatars={avatars.Length} (list {GameDirector.instance?.PlayerList?.Count ?? 0}), voices={voices.Length}, " +
                            $"room={PhotonNetwork.CurrentRoom?.PlayerCount}, missing avatar=[{string.Join(", ", noAvatar)}], missing voice=[{string.Join(", ", noVoice)}], queue={PhotonNetwork.IsMessageQueueRunning}";
            if (report == _lastReport) return;
            _lastReport = report;
            Plugin.Log.LogInfo($"[LateJoin] Guest view t+{Time.realtimeSinceStartup - _joinedAt:0}s: {report}.");
        }
        catch (System.Exception e) { Plugin.Log.LogWarning($"[LateJoin] Guest view check failed harmlessly: {e.Message}"); }
    }
}
