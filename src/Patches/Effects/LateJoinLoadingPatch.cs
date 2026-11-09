using HarmonyLib;
using Photon.Pun;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// A late joiner's loading screen never closed. LoadingUI waits until every avatar in the player list has
/// levelAnimationCompleted, and that flag only arrives through PlayerAvatar.LoadingLevelAnimationCompletedRPC,
/// which the game sends to RpcTarget.All, not buffered. Players already in the level sent it before the guest
/// joined, so the guest never received it and waited forever on "loading never ends?".
/// Guest only, once per join: after this client's own level intro has finished, mark the avatars that were
/// already playing as finished too. Normal level starts are untouched (no join happened).
/// </summary>
[HarmonyPatch(typeof(LoadingUI), "Update")]
internal static class LateJoinLoadingPatch
{
    private static bool _pending;

    [HarmonyPatch(typeof(NetworkConnect), "OnJoinedRoom")]
    private static class JoinedPatch
    {
        [HarmonyPostfix] private static void Postfix() => _pending = PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient;
    }

    [HarmonyPrefix]
    private static void Prefix()
    {
        if (!_pending) return;
        try
        {
            if (!ModConfig.LateJoinEnabled.Value || !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient) { _pending = false; return; }
            var gen = LevelGenerator.Instance;
            var local = PlayerController.instance != null ? PlayerController.instance.playerAvatarScript : null;
            if (gen == null || !gen.Generated || GameDirector.instance == null || local == null || !local.levelAnimationCompleted) return;
            int marked = 0;
            foreach (var player in GameDirector.instance.PlayerList)
                if (player != null && !player.levelAnimationCompleted) { player.levelAnimationCompleted = true; marked++; }
            _pending = false;
            Plugin.Log.LogInfo($"[LateJoin] Guest intro done: marked {marked} player(s) already in the level as loaded (their signal predates this join).");
        }
        catch (System.Exception e) { _pending = false; Plugin.Log.LogWarning($"[LateJoin] Loading-screen release failed harmlessly: {e.Message}"); }
    }
}
